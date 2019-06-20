//-----------------------------------------------------------------------------
// FILE:        SipClientTransaction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements SIP client side transactions.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements SIP client side transactions.
    /// </summary>
    /// <threadsafety instance="true" />
    public sealed class SipClientTransaction : SipTransaction
    {
        private SipClientAgent  agent;
        private ISipTransport   transport;
        private bool            isUdp;
        private SipRequest      request;
        private SipRequest      ackRequest;
        private NetworkBinding  remoteEP;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="agent">The <see cref="ISipAgent" /> that owns this transaction.</param>
        /// <param name="request">The <see cref="SipRequest" /> initiating the transaction.</param>
        /// <param name="id">The globally unique transaction ID.</param>
        /// <param name="transport">The <see cref="ISipTransport" /> to be used for this tranaaction.</param>
        /// <param name="remoteEP">The server side's <see cref="NetworkBinding" />.</param>
        public SipClientTransaction(SipClientAgent agent, SipRequest request, string id, ISipTransport transport, NetworkBinding remoteEP)
            : base(agent, id, transport)
        {
            if (request.Method == SipMethod.Ack)
                throw new SipException("Client transactions cannot be initiated with an ACK request.");

            this.agent      = (SipClientAgent)agent;
            this.transport  = transport;
            this.isUdp      = !transport.IsStreaming;
            this.request    = request;
            this.ackRequest = null;
            this.remoteEP   = remoteEP;
        }

        /// <summary>
        /// Starts the client transaction.
        /// </summary>
        public void Start()
        {
            SipCSeqValue vCSeq;

            // Add a CSeq header to the request if necessary and intialize the
            // transaction's local sequence number.

            vCSeq = request.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            if (vCSeq == null)
                request.AddHeader(SipHeader.CSeq, new SipCSeqValue(SipHelper.GenCSeq(), request.MethodText));

            // Add Max-Forwards if necessary.

            if (request.GetHeaderText(SipHeader.MaxForwards) == null)
                request.AddHeader(SipHeader.MaxForwards, SipHelper.MaxForwards);

            // Start the transaction

            using (TimedLock.Lock(agent))
            {
                SetState(request.Method == SipMethod.Invite ? SipTransactionState.InviteCalling : SipTransactionState.Trying);
                transport.Send(remoteEP, request);
            }
        }

        /// <summary>
        /// Terminate's the transaction immediately.
        /// </summary>
        public void Terminate()
        {
            SetState(SipTransactionState.Terminated);
        }

        /// <summary>
        /// Sets a new transaction state.
        /// </summary>
        /// <param name="newState">The new state.</param>
        private void SetState(SipTransactionState newState)
        {
            SipTransactionState oldState;

            if (newState == base.State)
                return;

            oldState   = base.State;
            base.State = newState;

            switch (newState)
            {
                case SipTransactionState.InviteCalling:

                    if (isUdp)
                        base.TimerA.Interval = base.BaseTimers.T1;

                    base.TimerB.Interval = Helper.Multiply(base.BaseTimers.T1, 64);
                    break;

                case SipTransactionState.InviteProceeding:

                    break;

                case SipTransactionState.InviteCompleted:

                    if (isUdp)
                        base.TimerD.Interval = Helper.Min(Helper.Multiply(base.BaseTimers.T1, 64), TimeSpan.FromSeconds(32));
                    else
                        base.TimerD.Interval = TimeSpan.Zero;

                    break;

                case SipTransactionState.Trying:

                    base.TimerF.Interval = Helper.Multiply(base.BaseTimers.T1, 64);

                    if (isUdp)
                        base.TimerE.Interval = base.BaseTimers.T1;

                    break;

                case SipTransactionState.Proceeding:

                    break;

                case SipTransactionState.Completed:

                    if (isUdp)
                        base.TimerK.Interval = base.BaseTimers.T4;
                    else
                        base.State = SipTransactionState.Terminated;

                    break;

                case SipTransactionState.Terminated:

                    break;
            }
        }

        /// <summary>
        /// The managing <see cref="ISipAgent" /> is responsible for calling this
        /// method whenever it receives responses correlated to this transaction.
        /// </summary>
        /// <param name="transport">The source <see cref="ISipTransport" />.</param>
        /// <param name="response">The received <see cref="SipResponse" />.</param>
        public void OnResponse(ISipTransport transport, SipResponse response)
        {
            bool            callOnComplete       = false;
            bool            callOnProceeding     = false;
            bool            callOnInviteComplete = false;
            SipResponse     callbackMsg          = null;
            SipStatus       status               = SipStatus.OK;
            SipCSeqValue    vCSeq;

            this.transport = transport;

            try
            {
                response.SourceTransaction = this;

                using (TimedLock.Lock(agent))
                {
                    // Ignore messages without a sequence number
                    //
                    // $todo(jeff.lill): Probably should check the method too

                    vCSeq = response.GetHeader<SipCSeqValue>(SipHeader.CSeq);
                    if (vCSeq == null)
                        return;

                    // Handle state specific processing

                    switch (base.State)
                    {
                        default:
                        case SipTransactionState.Unknown:

                            SysLog.LogError("Unexpected SIP transaction state.");
                            SetState(SipTransactionState.Terminated);

                            // Setup to call the agent's completion method

                            callOnComplete = true;
                            callbackMsg    = null;
                            status         = SipStatus.Stack_ProtocolError;
                            return;

                        case SipTransactionState.InviteCalling:

                            if (!request.MatchCSeq(response))
                                return;     // Ignore responses whose CSeq header doesn't match the request

                            if (response.IsProvisional)
                            {
                                // Provisional response.

                                SetState(SipTransactionState.InviteProceeding);

                                // Setup to call the agent's proceeding method

                                callOnProceeding = true;
                                callbackMsg      = response;
                                status           = response.Status;
                                return;
                            }

                            if (response.IsNonSuccessFinal)
                            {
                                // Final response non-2xx response.  Generate and 
                                // send the ACK request to the server to squelch
                                // any further responses and then enter the
                                // InviteCompleted state to absorb any responses
                                // that do make it through.

                                ackRequest = CreateAckRequest(request, response);
                                transport.Send(remoteEP, ackRequest);

                                SetState(SipTransactionState.InviteCompleted);

                                // Setup to call the agent's invite completed method

                                callOnInviteComplete = true;
                                callbackMsg          = response;
                                status               = response.Status;
                                return;
                            }

                            // Must be a 2xx response.  Setup to call the agent's
                            // completed method and enter the terminated state
                            // without sending an ACK request.
                            //
                            // Note that the agent is required to do this as
                            // described in RFC 3261 on pages 128-129.

                            SetState(SipTransactionState.Terminated);

                            callOnInviteComplete = true;
                            callbackMsg          = response;
                            status               = response.Status;
                            break;

                        case SipTransactionState.InviteProceeding:

                            if (!request.MatchCSeq(response))
                                return;     // Ignore responses whose CSeq header doesn't match the request

                            if (response.IsProvisional)
                            {
                                // Setup to call the agent's proceeding method

                                callOnProceeding = true;
                                callbackMsg      = response;
                                status           = response.Status;
                                return;
                            }

                            if (response.IsNonSuccessFinal)
                            {
                                // Final response non-2xx response.  Generate and 
                                // send the ACK request to the server to squelch
                                // any further responses and then enter the
                                // InviteCompleted state to absorb any responses
                                // that do make it through.

                                // $todo(jeff.lill): 
                                //
                                // I need to figure out a way to
                                // map to the dialog so that it
                                // can generate the ACK rather than
                                // doing this locally.

                                ackRequest = CreateAckRequest(request, response);
                                transport.Send(remoteEP, ackRequest);

                                SetState(SipTransactionState.InviteCompleted);

                                // Setup to call the agent's invite completed method

                                callOnInviteComplete = true;
                                callbackMsg          = response;
                                status               = response.Status;
                                return;
                            }

                            // Must be a 2xx response.  Setup to call the agent's
                            // completed method and enter the terminated state
                            // without sending an ACK request.
                            //
                            // Note that the agent is required to do this as
                            // described in RFC 3261 on pages 128-129.

                            SetState(SipTransactionState.Terminated);

                            callOnInviteComplete = true;
                            callbackMsg          = response;
                            status               = response.Status;
                            break;

                        case SipTransactionState.InviteCompleted:

                            // Retransmit the ACK if we get another final response

                            if (response.IsFinal)
                                transport.Send(remoteEP, ackRequest);

                            break;

                        case SipTransactionState.Trying:

                            if (!request.MatchCSeq(response))
                                return;     // Ignore responses whose CSeq header doesn't match the request

                            if (response.IsProvisional)
                            {
                                // Provisional response.

                                SetState(SipTransactionState.Proceeding);

                                // Setup to call the agent's proceeding method

                                callOnProceeding = true;
                                callbackMsg = response;
                                status = response.Status;
                                return;
                            }
                            else
                            {
                                // Final response

                                SetState(SipTransactionState.Completed);

                                // Setup to call the agent's completion method

                                callOnComplete = true;
                                callbackMsg    = response;
                                status         = response.Status;
                                return;
                            }

                        case SipTransactionState.Proceeding:

                            if (!request.MatchCSeq(response))
                                return;     // Ignore responses whose CSeq header doesn't match the request

                            if (response.IsProvisional)
                            {
                                // Setup to call the agent's proceeding method

                                callOnProceeding = true;
                                callbackMsg      = response;
                                status           = response.Status;
                                return;
                            }

                            // Final response.

                            SetState(SipTransactionState.Completed);

                            // Setup to call the agent's completion method

                            callOnComplete = true;
                            callbackMsg    = response;
                            status         = response.Status;
                            return;

                        case SipTransactionState.Completed:

                            break;

                        case SipTransactionState.Terminated:

                            break;
                    }
                }
            }
            finally
            {
                // Handle the agent callbacks outside of the lock to avoid
                // deadlock issues.

                if (callOnProceeding)
                    agent.OnProceeding(this, callbackMsg);

                if (callOnComplete)
                    agent.OnComplete(this, status, callbackMsg);

                if (callOnInviteComplete)
                    agent.OnInviteComplete(this, status, response);
            }
        }

        /// <summary>
        /// Transmits an ACK request to the server side of the transaction.
        /// </summary>
        /// <param name="ackRequest">The ACK <see cref="SipRequest" />.</param>
        /// <remarks>
        /// <para>
        /// This method must be called for client-side dialogs after the server has confirmed
        /// the dialog.
        /// </para>
        /// <note>
        /// This works only for the dialogs initiated the dialog, whose <see cref="SipDialog.IsInitiating" />
        /// property returns <c>true</c>.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if request is not an ACK request.</exception>
        public void SendAckRequest(SipRequest ackRequest)
        {            if (ackRequest.Method != SipMethod.Ack)
                throw new ArgumentException("SendAckRequest() can only send ACK requests.");

            transport.Send(remoteEP, ackRequest);
        }

        /// <summary>
        /// This method will be called periodically on a background thread to handle
        /// message resending as well as timeout related state transitions.
        /// </summary>
        public override void OnBkTask()
        {

            bool            callOnComplete   = false;
            bool            callOnProceeding = false;
            SipResponse     callbackMsg      = null;
            SipStatus       status           = SipStatus.OK;

            try
            {
                using (TimedLock.Lock(agent))
                {
                    switch (base.State)
                    {
                        default:
                        case SipTransactionState.Unknown:

                            break;

                        case SipTransactionState.InviteCalling:

                            if (base.TimerB.HasFired)
                            {
                                // Request has timed out.

                                SetState(SipTransactionState.Terminated);

                                // Setup to call the agent's completion method

                                callOnComplete = true;
                                callbackMsg    = null;
                                status         = SipStatus.RequestTimeout;
                                return;
                            }

                            if (base.TimerA.HasFired)
                            {
                                // Retransmit the request and reset the
                                // timer for exponential backoff

                                transport.Send(remoteEP, request);
                                base.TimerA.Interval = new TimeSpan(base.TimerA.Interval.Ticks * 2);
                            }
                            break;

                        case SipTransactionState.InviteProceeding:

                            if (base.TimerB.HasFired)
                            {
                                // Request has timed out.

                                SetState(SipTransactionState.Terminated);

                                // Setup to call the agent's completion method

                                callOnComplete = true;
                                callbackMsg    = null;
                                status         = SipStatus.RequestTimeout;
                                return;
                            }
                            break;

                        case SipTransactionState.InviteCompleted:

                            if (base.TimerD.HasFired)
                                SetState(SipTransactionState.Terminated);

                            break;

                        case SipTransactionState.Trying:

                            if (base.TimerF.HasFired)
                            {
                                // Request has timed out.

                                SetState(SipTransactionState.Terminated);

                                // Setup to call the agent's completion method

                                callOnComplete = true;
                                callbackMsg    = null;
                                status         = SipStatus.RequestTimeout;
                                return;
                            }

                            if (isUdp && base.TimerE.HasFired)
                            {
                                // Retransmit for UDP

                                transport.Send(remoteEP, request);
                                base.TimerE.Interval = Helper.Min(base.BaseTimers.T2, Helper.Multiply(base.TimerE.Interval, 2));
                            }
                            break;

                        case SipTransactionState.Proceeding:

                            if (base.TimerF.HasFired)
                            {
                                // Request has timed out.

                                SetState(SipTransactionState.Terminated);

                                // Setup to call the agent's completion method

                                callOnComplete = true;
                                callbackMsg    = null;
                                status         = SipStatus.RequestTimeout;
                                return;
                            }

                            if (base.TimerE.HasFired)
                            {
                                // Retransmit for all transports.

                                transport.Send(remoteEP, request);
                                base.TimerE.Interval = base.BaseTimers.T2;
                            }
                            break;

                        case SipTransactionState.Completed:

                            if (base.TimerK.HasFired)
                                SetState(SipTransactionState.Terminated);

                            break;

                        case SipTransactionState.Terminated:

                            break;
                    }
                }
            }
            finally
            {
                // Handle the agent callbacks outside of the lock to avoid
                // deadlock issues.

                if (callOnComplete)
                    agent.OnComplete(this, status, callbackMsg);

                if (callOnProceeding)
                    agent.OnProceeding(this, (SipResponse)callbackMsg);
            }
        }

        /// <summary>
        /// Creates the proper ACK request based on the original INVITE request sent
        /// to the server and the 2xx response received.
        /// </summary>
        /// <param name="inviteRequest">The INVITE <see cref="SipRequest" /> sent to the server.</param>
        /// <param name="response">The 2xx <see cref="SipResponse" /> received from the server.</param>
        /// <returns>The created ACK <see cref="SipRequest" />.</returns>
        private SipRequest CreateAckRequest(SipRequest inviteRequest, SipResponse response)
        {
            SipRequest      ackRequest;
            SipHeader       callID;
            SipHeader       to;
            SipHeader       from;
            SipHeader       via;
            SipHeader       contact;
            SipHeader       route;
            SipCSeqValue    vCSeq;

            if (inviteRequest.Method != SipMethod.Invite)
                throw new ArgumentException("INVITE request expected.", "inviteRequest");

            if (response.IsProvisional)
                throw new ArgumentException("Non-provisional response expected.", "response");

            ackRequest = new SipRequest(SipMethod.Ack, inviteRequest.Uri, null);

            callID  = inviteRequest[SipHeader.CallID];
            to      = response[SipHeader.To];
            from    = inviteRequest[SipHeader.From];
            via     = inviteRequest[SipHeader.Via];
            contact = inviteRequest[SipHeader.Contact];
            route   = inviteRequest[SipHeader.Route];

            if (callID == null)
                throw new SipException("INVITE request is missing header: [Call-ID]");

            if (to == null)
                throw new SipException("INVITE response is missing header: [To]");

            if (from == null)
                throw new SipException("INVITE request is missing header: [From]");

            if (contact == null)
                throw new SipException("INVITE request is missing header: [Contact]");

            if (via == null)
                throw new SipException("INVITE request is missing header: [Via]");

            vCSeq = inviteRequest.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            if (vCSeq == null)
                throw new SipException("INVITE request is missing header: [CSeq]");

            ackRequest.AddHeader(SipHeader.Via, via.Text);
            ackRequest.AddHeader(SipHeader.To, to.Text);
            ackRequest.AddHeader(SipHeader.From, from.Text);
            ackRequest.AddHeader(SipHeader.Contact, contact.Text);
            ackRequest.AddHeader(SipHeader.CallID, callID.Text);
            ackRequest.AddHeader(SipHeader.CSeq, new SipCSeqValue(vCSeq.Number, "ACK"));
            ackRequest.AddHeader(SipHeader.MaxForwards, SipHelper.MaxForwards);
            ackRequest.AddHeader(SipHeader.UserAgent, agent.Core.Settings.UserAgent);

            if (route != null)
                ackRequest.Headers.Add(SipHeader.Route, route.Clone());

            return ackRequest;
        }
    }
}
