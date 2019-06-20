//-----------------------------------------------------------------------------
// FILE:        SipClientAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP client as described in RFC 3261 as
//              a User Agent Client (UAC).

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill): Implement some kind of cancel behavior.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP client as described in RFC 3261 as a User Agent Client (UAC).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SIP agents are responsible for managing the state of the client or server
    /// side of a transaction.  SIP defines two basic transaction patterns, the
    /// request/response pattern for non-INVITE transactions and the INVITE/response/ACK
    /// pattern for INVITE transactions.  SIP agents implement a state machine
    /// that handles message retransmissions, etc. for unreliable transports
    /// such as UDP.  The LillTek SIP stack includes two agents: <see cref="SipClientAgent" /> 
    /// and <see cref="SipServerAgent" />.  Each of these implement the corresponding
    /// side of a SIP transaction.
    /// </para>
    /// <para>
    /// SIP agents are typically instantiated within the context of a <see cref="SipCore" />
    /// implementation.  This core generally instantiates the <see cref="ISipTransport" />
    /// instances used to communicate with outside world as well as the <see cref="ISipMessageRouter" />
    /// which decides how to route messages between transports and agents.
    /// </para>
    /// <para>
    /// Use the <see cref="SipClientAgent" /> constructor to create an instance.  Then call
    /// <see cref="Request" /> to submit a generalized request to an remote SIP endpoint synchronously,
    /// or you can use the asynchronous methods: <see cref="BeginRequest(SipRequest,SipDialog,AsyncCallback,object)" />
    /// and <see cref="EndRequest" />.
    /// </para>
    /// <note>
    /// The <see cref="SipClientAgent" /> class makes no attempt to respond to authentication
    /// challenges for submitted requests.  The <see cref="SipStatus.Unauthorized" /> and
    /// <see cref="SipStatus.ProxyAuthenticationRequired" /> responses will be returned
    /// by the request methods, just like any other response.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class SipClientAgent : ISipAgent
    {
        private SipCore             core;               // The SIP core that owns this agent
        private ISipMessageRouter   router;             // The message router

        // The active transactions keyed by transaction ID.

        private Dictionary<string, SipClientTransaction> transactions = new Dictionary<string, SipClientTransaction>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="core">The SIP core that owns this agent.</param>
        /// <param name="router">The <see cref="ISipMessageRouter" />.</param>
        public SipClientAgent(SipCore core, ISipMessageRouter router)
        {
            this.core   = core;
            this.router = router;
        }

        /// <summary>
        /// Returns the <see cref="SipCore" /> that owns this agent.
        /// </summary>
        public SipCore Core
        {
            get { return core; }
        }

        /// <summary>
        /// Stops the agent, terminating any outstanding transactions.
        /// </summary>
        public void Stop()
        {
            // Terminate the transactions 

            foreach (SipClientTransaction transaction in transactions.Values)
                transaction.Terminate();

            transactions.Clear();
        }

        //---------------------------------------------------------------------
        // Request submission methods.

        /// <summary>
        /// Used internal by the client's async request methods to hold operation state.
        /// </summary>
        private sealed class ClientAsyncResult : AsyncResult
        {
            public SipRequest   Request;            // The request being submitted
            public SipDialog    Dialog;             // The dialog for INVITEs (or null)
            public SipResult    SipResult;          // The ultimate request result

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="request">The request being submitted.</param>
            /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
            /// <param name="callback">The application callback (or <c>null</c>).</param>
            /// <param name="state">The application state (or <c>null</c>).</param>
            public ClientAsyncResult(SipRequest request, SipDialog dialog, AsyncCallback callback, object state)
                : base(null, callback, state)
            {
                this.Request   = request;
                this.Dialog    = dialog;
                this.SipResult = null;
            }
        }

        /// <summary>
        /// Initiates an asynchronous SIP request transaction.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <para>
        /// All requests to <see cref="BeginRequest(SipRequest,SipDialog,AsyncCallback,object)" /> must be matched with a 
        /// call to <see cref="EndRequest" />.
        /// </para>
        /// <note>
        /// This method adds reasonable <b>Call-ID</b> and <b>CSeq</b> headers to the request if these
        /// headers are not already present.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginRequest(SipRequest request, SipDialog dialog, AsyncCallback callback, object state)
        {
            ClientAsyncResult       arClient = new ClientAsyncResult(request, dialog, callback, state);
            SipValue                viaValue;
            SipCSeqValue            vCSeq;
            string                  transactionID;
            SipClientTransaction    transaction;
            ISipTransport           transport;
            NetworkBinding          remoteEP;

            if (dialog != null && request.Method != SipMethod.Invite)
                throw new InvalidOperationException("Dialogs may be created only for INVITE requests.");

            arClient.Dialog = dialog;

            transport = router.SelectTransport(this, request, out remoteEP);
            if (transport == null)
                throw new SipException("No approriate transport is available.");

            // Initialize the request's Via header and transaction ID as necessary.

            transactionID      = SipHelper.GenerateBranchID();
            viaValue           = new SipValue(string.Format("SIP/2.0/{0} {1}", transport.Name, transport.Settings.ExternalBinding.Address));
            viaValue["branch"] = transactionID;
            viaValue["rport"]  = string.Empty;

            request.PrependHeader(SipHeader.Via, viaValue);

            // Initialize common headers as necessary

            if (!request.ContainsHeader(SipHeader.CallID))
                request.AddHeader(SipHeader.CallID, SipHelper.GenerateCallID());

            vCSeq = request.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            if (vCSeq == null)
            {
                vCSeq = new SipCSeqValue(SipHelper.GenCSeq(), request.MethodText);
                request.AddHeader(SipHeader.CSeq, vCSeq);
            }

            // Initialize the transaction

            transaction = new SipClientTransaction(this, request, transactionID, transport, remoteEP);
            transaction.AgentState = arClient;

            // Handle initial dialog INVITE specific initialization

            if (dialog != null && request.Method == SipMethod.Invite && dialog.State == SipDialogState.Waiting)
            {
                // Client-side dialogs need to know the transaction so 
                // they'll be able to send the confirming ACK.

                dialog.InitiatingTransaction = transaction;

                // Dialogs need to know about the sequence number used in INVITE requests so
                // that the ACK can be generated with the same sequence number.

                dialog.AckCSeq = vCSeq.Number;

                // The dialog has been intialized enough to be added to the core's
                // early dialog table.

                core.AddEarlyDialog(dialog);
            }

            // Start the transaction

            using (TimedLock.Lock(this))
            {
                transactions.Add(transactionID, transaction);
            }

            transaction.Start();
            arClient.Started();

            return arClient;
        }

        /// <summary>
        /// Completes the asynchronous request transaction begun by a call to <see cref="BeginRequest(SipRequest,SipDialog,AsyncCallback,object)" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> returned by <see cref="BeginRequest(SipRequest,SipDialog,AsyncCallback,object)" />.</param>
        /// <returns>The <see cref="SipResult" /> detailing the final disposition of the operation.</returns>
        public SipResult EndRequest(IAsyncResult ar)
        {
            var arClient = (ClientAsyncResult)ar;

            arClient.Wait();
            try
            {
                if (arClient.Exception != null)
                    throw arClient.Exception;

                return arClient.SipResult;
            }
            finally
            {
                arClient.Dispose();
            }
        }

        /// <summary>
        /// Executes a synchronous SIP request transaction.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
        /// <returns>The <see cref="SipResult" /> detailing the result of the operation.</returns>
        /// <remarks>
        /// <note>
        /// This method adds reasonable <b>Call-ID</b> and <b>CSeq</b> headers to the request if these
        /// headers are not already present.
        /// </note>
        /// </remarks>
        public SipResult Request(SipRequest request, SipDialog dialog)
        {
            var ar = BeginRequest(request, dialog, null, null);
            
            return EndRequest(ar);
        }

        //---------------------------------------------------------------------
        // Event handling methods called by SipClientTransaction.

        /// <summary>
        /// Handles messages received by a transport to be processed by this agent.
        /// </summary>
        /// <param name="transport">The source transport.</param>
        /// <param name="message">The received message.</param>
        public void OnReceive(ISipTransport transport, SipMessage message)
        {
            SipResponse             response = (SipResponse)message;
            SipClientTransaction    transaction;
            string                  transactionID;

            if (response == null)
                return;     // Ignore any requests

            // Route the message to the correct transaction.

            if (!response.TryGetTransactionID(out transactionID))
                return;

            using (TimedLock.Lock(this))
            {
                if (!transactions.TryGetValue(transactionID, out transaction))
                {
                    // The response doesn't map to an existing transaction.
                    // We're going to pass this to the core's OnUncorrelatedResponse()
                    // method.

                    core.OnUncorrelatedResponse(this, response);
                    return;
                }
            }

            transaction.OnResponse(transport, response);
        }

        /// <summary>
        /// Called when a transaction receives a 1xx response.
        /// </summary>
        /// <param name="transaction">The source <see cref="SipClientTransaction" />.</param>
        /// <param name="response">The <see cref="SipResponse" /> received.</param>
        internal void OnProceeding(SipClientTransaction transaction, SipResponse response)
        {
            var arClient = (ClientAsyncResult)transaction.AgentState;

            core.OnResponseReceived(new SipResponseEventArgs(response.Status, response, transaction, arClient.Dialog, this, this.core));
        }

        /// <summary>
        /// Called when a non-INVITE transaction completes.
        /// </summary>
        /// <param name="transaction">The source <see cref="SipClientTransaction" />.</param>
        /// <param name="status">The completion status.</param>
        /// <param name="response">The final response (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="response"/> parameter will be passed as <c>null</c> if
        /// the transaction was completed without receiving a final message (such
        /// as a timeout).  In this case, the agent should look to the <paramref name="status"/>
        /// property for the final disposition of the transaction.
        /// </para>
        /// <para>
        /// This method also handles the resubmission of the request with additional
        /// authentication information if necessary.
        /// </para>
        /// </remarks>
        internal void OnComplete(SipClientTransaction transaction, SipStatus status, SipResponse response)
        {
            var arClient = (ClientAsyncResult)transaction.AgentState;

            if (response == null)
            {
                // The operation has completed without receiving a final response (probably
                // due to a time out or some kind of transport related problem).

                arClient.SipResult = new SipResult(arClient.Request, arClient.Dialog, this, status);
                arClient.Notify();

                core.OnResponseReceived(new SipResponseEventArgs(status, response, transaction, arClient.Dialog, this, this.core));
                return;
            }

            // We have the final response.

            arClient.SipResult = new SipResult(arClient.Request, arClient.Dialog, this, response);
            arClient.Notify();

            core.OnResponseReceived(new SipResponseEventArgs(response.Status, response, transaction, arClient.Dialog, this, this.core));
        }

        /// <summary>
        /// Called when an INVITE transaction completes.
        /// </summary>
        /// <param name="transaction">The source <see cref="SipClientTransaction" />.</param>
        /// <param name="status">The completion status.</param>
        /// <param name="response">The final response (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="response"/> parameter will be passed as <c>null</c> if
        /// the transaction was completed without receiving a final message (such
        /// as a timeout).  In this case, the agent should look to the <paramref name="status"/>
        /// property for the final disposition of the transaction.
        /// </para>
        /// <para>
        /// This method also handles the resubmission of the request with additional
        /// authentication information if necessary.
        /// </para>
        /// <para>
        /// The method may create a custom ACK <see cref="SipResponse" /> to be delivered
        /// back to the server by saving the response in the <paramref name="response"/> parameter.
        /// Otherwise, if this value is left as <c>null</c>, the client transaction will
        /// generate a default ACK response and send it.
        /// </para>
        /// </remarks>
        internal void OnInviteComplete(SipClientTransaction transaction, SipStatus status, SipResponse response)
        {
            var arClient = (ClientAsyncResult)transaction.AgentState;
            var args     = new SipResponseEventArgs(status, response, transaction, arClient.Dialog, this, this.core);

            if (response == null)
            {
                // The operation has completed without receiving a final response (probably
                // due to a time out or some kind of transport related problem).

                arClient.SipResult = new SipResult(arClient.Request, arClient.Dialog, this, status);
                arClient.Notify();

                core.OnResponseReceived(args);
                core.OnInviteFailed(args, status);
                return;
            }

            // We have the final response.  Compute the dialog ID from the response's
            // Call-ID, and To/From tags and assign it to the dialog.

            arClient.Dialog.ID = SipDialog.GetDialogID(response);

            // Call the core's OnInviteConfirmed() method so it can perform
            // any dialog specific activities.

            if (response.IsSuccess)
                core.OnInviteConfirmed(args);
            else
                core.OnInviteFailed(args, status);

            // Signal completion of the async operation.

            arClient.SipResult = new SipResult(arClient.Request, arClient.Dialog, this, response);
            arClient.Notify();
        }

        /// <summary>
        /// Called periodically on a background thread to handle transaction
        /// related activities.
        /// </summary>
        public void OnBkTask()
        {
            List<string>                delList = new List<string>();
            List<SipClientTransaction>  transList;

            // Create a list of all outstanding transactions

            using (TimedLock.Lock(this))
            {
                transList = new List<SipClientTransaction>(transactions.Count);
                foreach (var transaction in transactions.Values)
                    transList.Add(transaction);
            }

            // Call each transaction's OnBkTask() method outside of the lock.

            foreach (SipClientTransaction transaction in transList)
                transaction.OnBkTask();

            // Delete all terminated transactions.

            using (TimedLock.Lock(this))
            {
                foreach (string id in transactions.Keys)
                {
                    var transaction = transactions[id];

                    if (transaction.State == SipTransactionState.Terminated)
                        delList.Add(id);
                }

                foreach (string id in delList)
                    transactions.Remove(id);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better locking diagnostics.
        /// </summary>
        /// <returns>A lock key to be used to identify this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
