//-----------------------------------------------------------------------------
// FILE:        SipServerTransaction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements SIP server side transactions.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements SIP server side transactions.
    /// </summary>
    /// <threadsafety instance="true" />
    public sealed class SipServerTransaction : SipTransaction
    {
        private DateTime        ttd;        // Time-to-die (SYS)
        private SipServerAgent  agent;
        private ISipTransport   transport;
        private bool            isUdp;
        private SipRequest      request;
        private NetworkBinding  remoteEP;
        private SipResponse     provisionalResponse;
        private SipResponse     finalResponse;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="agent">The <see cref="ISipAgent" /> that owns this transaction.</param>
        /// <param name="id">The globally unique transaction ID.</param>
        /// <param name="transport">The <see cref="ISipTransport" /> to be used for this transaction.</param>
        /// <param name="ttd">(Time-to-die) The time (SYS) where the transaction should terminate itself regardless of its current state.</param>
        public SipServerTransaction(SipServerAgent agent, string id, ISipTransport transport, DateTime ttd)
            : base(agent, id, transport)
        {
            this.ttd                 = ttd;
            this.agent               = (SipServerAgent)agent;
            this.transport           = transport;
            this.isUdp               = !transport.IsStreaming;

            this.request             = null;
            this.remoteEP            = null;
            this.provisionalResponse = null;
            this.finalResponse       = null;
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

                    break;

                case SipTransactionState.InviteProceeding:

                    break;

                case SipTransactionState.InviteCompleted:

                    base.TimerG.Interval = base.BaseTimers.T1;
                    base.TimerH.Interval = Helper.Multiply(base.BaseTimers.T1, 64);
                    break;

                case SipTransactionState.InviteConfirmed:

                    base.TimerI.Interval = isUdp ? base.BaseTimers.T4 : TimeSpan.Zero;
                    break;

                case SipTransactionState.Trying:

                    break;

                case SipTransactionState.Proceeding:

                    break;

                case SipTransactionState.Completed:

                    base.TimerJ.Interval = isUdp ? Helper.Multiply(base.BaseTimers.T1, 64) : TimeSpan.Zero;
                    break;

                case SipTransactionState.Terminated:

                    break;
            }
        }

        /// <summary>
        /// The managing <see cref="ISipAgent" /> is responsible for calling this
        /// method whenever it receives requests correlated to this transaction.
        /// </summary>
        /// <param name="request">The received <see cref="SipRequest" />.</param>
        public void OnRequest(SipRequest request)
        {
            SipRequest  callbackMsg          = null;
            bool        callOnRequest        = false;
            bool        callOnInviteBegin    = false;
            bool        callOnInviteComplete = false;

            try
            {
                request.SourceTransaction = this;

                using (TimedLock.Lock(agent))
                {
                    if (this.request == null)
                    {
                        SipViaValue         viaValue;
                        SipContactValue     toValue;
                        NetworkBinding      sentBy;
                        IPAddress           address;

                        // This is the initial transaction request.

                        this.request = request;

                        // Handle the Via "received" and "rport" header parameters (mostly) as described on page
                        // RFC 3261 (page 145) and RFC 3581 (page 4).

                        viaValue = request.GetHeader<SipViaValue>(SipHeader.Via);
                        if (viaValue == null)
                        {
                            // Illegal request

                            SetState(SipTransactionState.Terminated);
                            return;
                        }

                        sentBy = viaValue.SentByBinding;
                        if (sentBy == null || sentBy.IsHost || sentBy.Address != request.RemoteEndpoint.Address)
                            viaValue.Received = request.RemoteEndpoint.Address.ToString();

                        if (viaValue.RPort != null)
                            viaValue.RPort = request.RemoteEndpoint.Port.ToString();

                        // Determine the destination network endpoint based on the
                        // rules described on RFC 3261 (page 146).

                        if (request.SourceTransport.IsStreaming)
                        {
                            // $todo(jeff.lill): 
                            //
                            // This implementation is incomplete.  To be fully
                            // compliant with the RFC, I'd have to check to
                            // see if the connection is still present in the
                            // transport and if not, use the received and
                            // rport values as described.

                            remoteEP = request.RemoteEndpoint;
                        }
                        else
                        {
                            if (viaValue.MAddr != null)
                            {
                                if (!IPAddress.TryParse(viaValue.MAddr, out address))
                                {
                                    SipException e;

                                    // Illegal request

                                    SetState(SipTransactionState.Terminated);

                                    e = new SipException("Illegal request: Invalid [Via: maddr].");
                                    e.Transport = transport.Name;
                                    e.SourceEndpoint = request.RemoteEndpoint;
                                    e.BadMessage = request;

                                    throw e;
                                }

                                remoteEP = new NetworkBinding(address, viaValue.SentByBinding.Port);
                            }
                            else
                            {
                                remoteEP = request.RemoteEndpoint;
                            }
                        }

                        // INVITE and non-INVITE requests have different state machines.

                        if (request.Method == SipMethod.Invite)
                        {
                            // Start an INVITE transaction

                            SetState(SipTransactionState.InviteProceeding);

                            // If the request has a "To" header without a "tag" parameter then 
                            // generate a tag.  Note that this code will cause provisional INVITE
                            // responses to include a generated tag which the RFC indicates
                            // SHOULD NOT be done.  But, it's much safer to do this once here
                            // for all transaction types, avoiding special cases, and besides,
                            // I've noticed that Asterisk includes a tag in its provisional
                            // INVITE responses.

                            toValue = request.GetHeader<SipContactValue>(SipHeader.To);
                            if (toValue != null)
                            {
                                if (toValue["tag"] == null)
                                    toValue["tag"] = SipHelper.GenerateTagID();

                                request.SetHeader(SipHeader.To, toValue);
                            }

                            // Always send an initial provisional trying response.

                            provisionalResponse = request.CreateResponse(SipStatus.Trying, null);
                            SendResponse(provisionalResponse);

                            // Setup to call the agent's OnInviteBegin() method.

                            callOnInviteBegin = true;
                            callbackMsg = request;
                        }
                        else if (request.Method == SipMethod.Ack)
                        {
                            // Allow an ACK request to drop through to the state machine.
                        }
                        else
                        {
                            // Start a non-INVITE transaction

                            SetState(SipTransactionState.Trying);

                            // Setup to call the agent's OnRequest() method.

                            callOnRequest = true;
                            callbackMsg = request;
                        }

                        return;
                    }

                    // Handle state specific processing

                    switch (base.State)
                    {
                        default:
                        case SipTransactionState.Unknown:

                            SysLog.LogError("Unexpected SIP transaction state.");
                            SetState(SipTransactionState.Terminated);
                            return;

                        case SipTransactionState.InviteCalling:

                            break;

                        case SipTransactionState.InviteProceeding:

                            if (provisionalResponse != null)
                                transport.Send(remoteEP, provisionalResponse);

                            break;

                        case SipTransactionState.InviteCompleted:

                            if (request.Method == SipMethod.Ack)
                            {
                                SetState(SipTransactionState.InviteConfirmed);

                                // Setup to call OnInviteComplete(ack);

                                callOnInviteComplete = true;
                                callbackMsg = request;
                                return;
                            }

                            Assertion.Test(finalResponse != null);
                            transport.Send(remoteEP, finalResponse);
                            break;

                        case SipTransactionState.InviteConfirmed:

                            break;

                        case SipTransactionState.Trying:

                            break;

                        case SipTransactionState.Proceeding:

                            Assertion.Test(provisionalResponse != null);
                            transport.Send(remoteEP, provisionalResponse);
                            break;

                        case SipTransactionState.Completed:

                            Assertion.Test(finalResponse != null);
                            transport.Send(remoteEP, finalResponse);
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

                if (callOnRequest)
                    agent.OnRequest(this, callbackMsg);

                if (callOnInviteBegin)
                    agent.OnInviteBegin(this, request);

                if (callOnInviteComplete)
                    agent.OnInviteComplete(this, this.request, finalResponse, callbackMsg);
            }
        }

        /// <summary>
        /// Aborts the transaction without sending a response.
        /// </summary>
        public void Abort()
        {
            SendResponse(null);
        }

        /// <summary>
        /// The managing <see cref="ISipAgent" /> is responsible for calling this
        /// method whenever it needs to send a response for the transaction.
        /// </summary>
        /// <param name="response">The <see cref="SipResponse" /> (or <c>null</c> to abort).</param>
        /// <remarks>
        /// You may pass <paramref name="response"/> as <c>null</c> to abort the transaction
        /// without sending a response.  This is equivalent to calling <see cref="Abort" />.
        /// </remarks>
        public void SendResponse(SipResponse response)
        {
            try
            {
                using (TimedLock.Lock(agent))
                {
                    if (response == null)
                    {
                        // Handle aborting by transitioning to the completed state so
                        // request retransmits will continue to be absorbed by the
                        // transaction.

                        SetState(SipTransactionState.Completed);
                        return;
                    }

                    // Handle state specific processing

                    switch (base.State)
                    {
                        default:
                        case SipTransactionState.Unknown:

                            SysLog.LogError("Unexpected SIP transaction state.");
                            SetState(SipTransactionState.Terminated);
                            return;

                        case SipTransactionState.InviteCalling:

                            break;

                        case SipTransactionState.InviteProceeding:

                            if (response.IsProvisional)
                            {
                                // Provisional

                                provisionalResponse = response;
                                transport.Send(remoteEP, provisionalResponse);
                                return;
                            }

                            if (response.IsSuccess)
                            {
                                // Final response (success)

                                finalResponse = response;
                                transport.Send(remoteEP, finalResponse);
                                SetState(SipTransactionState.Terminated);
                                return;
                            }

                            // Final response (error)

                            finalResponse = response;
                            transport.Send(remoteEP, finalResponse);
                            SetState(SipTransactionState.InviteCompleted);
                            break;

                        case SipTransactionState.InviteCompleted:

                            break;

                        case SipTransactionState.InviteConfirmed:

                            break;

                        case SipTransactionState.Trying:

                            if (response.IsProvisional)
                            {
                                // Provisional

                                provisionalResponse = response;
                                transport.Send(remoteEP, provisionalResponse);
                                SetState(SipTransactionState.Proceeding);
                                return;
                            }

                            // Final response

                            finalResponse = response;
                            transport.Send(remoteEP, finalResponse);
                            SetState(SipTransactionState.Completed);

                            break;

                        case SipTransactionState.Proceeding:

                            if (response.IsProvisional)
                            {
                                // Provisional

                                provisionalResponse = response;
                                transport.Send(remoteEP, provisionalResponse);
                                return;
                            }

                            // Final response

                            finalResponse = response;
                            transport.Send(remoteEP, finalResponse);
                            SetState(SipTransactionState.Completed);

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
            }
        }

        /// <summary>
        /// This method will be called periodically on a background thread to handle
        /// message resending as well as timeout related state transitions.
        /// </summary>
        public override void OnBkTask()
        {
            bool        callOnInviteComplete = false;
            SipRequest  callbackMsg          = null;

            try
            {
                using (TimedLock.Lock(agent))
                {
                    if (SysTime.Now >= ttd)
                    {
                        SetState(SipTransactionState.Terminated);
                        return;
                    }

                    switch (base.State)
                    {
                        default:
                        case SipTransactionState.Unknown:

                            break;

                        case SipTransactionState.InviteCalling:

                            break;

                        case SipTransactionState.InviteProceeding:

                            break;

                        case SipTransactionState.InviteCompleted:

                            if (base.TimerH.HasFired)
                            {
                                SetState(SipTransactionState.Terminated);

                                // Setup to call OnInviteComplete(ackRequest=null) 
                                // indicating that the dialog was not established.

                                callOnInviteComplete = true;
                                return;
                            }

                            if (base.TimerG.HasFired)
                            {
                                transport.Send(remoteEP, finalResponse);
                                base.TimerG.Interval = Helper.Min(base.BaseTimers.T2, Helper.Multiply(base.TimerG.Interval, 2));
                            }
                            break;

                        case SipTransactionState.InviteConfirmed:

                            if (base.TimerI.HasFired)
                                SetState(SipTransactionState.Terminated);

                            break;

                        case SipTransactionState.Trying:

                            break;

                        case SipTransactionState.Proceeding:

                            break;

                        case SipTransactionState.Completed:

                            if (base.TimerJ.HasFired)
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

                if (callOnInviteComplete)
                    agent.OnInviteComplete(this, request, finalResponse, callbackMsg);
            }
        }
    }
}
