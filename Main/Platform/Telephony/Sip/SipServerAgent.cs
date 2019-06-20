//-----------------------------------------------------------------------------
// FILE:        SipServerAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP server as described in RFC 3261 as
//              a User Agent Server (UAS).

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP server as described in RFC 3261 as a User Agent Server (UAS).
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
    /// SIP agents are instantiated within the context of a <see cref="SipCore" />
    /// implementation.  This core generally instantiates the <see cref="ISipTransport" />
    /// instances used to communicate with outside world as well as the <see cref="ISipMessageRouter" />
    /// which decides how to route messages between transports and agents.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class SipServerAgent : ISipAgent
    {
        private SipCore             core;               // The SIP core that owns this agent
        private ISipMessageRouter   router;             // The message router

        // The active transactions keyed by transaction ID.

        private Dictionary<string, SipServerTransaction> transactions = new Dictionary<string, SipServerTransaction>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="core">The SIP core that owns this agent.</param>
        /// <param name="router">The <see cref="ISipMessageRouter" />.</param>
        public SipServerAgent(SipCore core, ISipMessageRouter router)
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

            foreach (var transaction in transactions.Values)
                transaction.Terminate();

            transactions.Clear();
        }

        /// <summary>
        /// Handles messages received by a transport to be processed by this agent.
        /// </summary>
        /// <param name="transport">The source transport.</param>
        /// <param name="message">The received message.</param>
        public void OnReceive(ISipTransport transport, SipMessage message)
        {
            SipRequest              request       = message as SipRequest;
            SipServerTransaction    transaction   = null;
            bool                    confirmingAck = false;
            string                  transactionID;

            // Route the message to the correct transaction.

            if (!request.TryGetTransactionID(out transactionID))
                return;

            try
            {
                using (TimedLock.Lock(this))
                {
                    if (!transactions.TryGetValue(transactionID, out transaction) || transaction.State == SipTransactionState.Terminated)
                    {
                        if (request.Method == SipMethod.Ack)
                        {
                            // Must be a confirmation ACK from a client.

                            confirmingAck = true;
                            return;
                        }

                        transaction = new SipServerTransaction(this, transactionID, transport, SysTime.Now + core.Settings.ServerTransactionTTL);
                        transactions.Add(transactionID, transaction);
                    }
                }
            }
            finally
            {
                if (confirmingAck)
                    core.OnConfirmingAck(this, request);
                else
                    transaction.OnRequest(request);
            }
        }

        /// <summary>
        /// Handles non-INVITE requests received on a transaction.
        /// </summary>
        /// <param name="transaction">The <see cref="SipServerTransaction" />.</param>
        /// <param name="request">The received <see cref="SipRequest" />.</param>
        internal void OnRequest(SipServerTransaction transaction, SipRequest request)
        {
            core.OnRequestReceived(new SipRequestEventArgs(request, transaction, null, this, this.core, null));
        }

        /// <summary>
        /// Called when an INVITE transaction is initiated.
        /// </summary>
        /// <param name="transaction">The <see cref="SipServerTransaction" />.</param>
        /// <param name="inviteRequest">The received INVITE <see cref="SipRequest" />.</param>
        internal void OnInviteBegin(SipServerTransaction transaction, SipRequest inviteRequest)
        {
            core.OnInviteReceived(new SipRequestEventArgs(inviteRequest, transaction, null, this, this.core, null));
        }

        /// <summary>
        /// Called when an INVITE transaction completes.
        /// </summary>
        /// <param name="transaction">The <see cref="SipServerTransaction" />.</param>
        /// <param name="inviteRequest">The received INVITE <see cref="SipRequest" />.</param>
        /// <param name="inviteResponse">The INVITE <see cref="SipResponse" /> sent back to the client (or <c>null</c>).</param>
        /// <param name="ackRequest">
        /// The received ACK <see cref="SipRequest" /> or <c>null</c> if the transaction 
        /// timed-out without receiving an ACK.
        /// </param>
        internal void OnInviteComplete(SipServerTransaction transaction, SipRequest inviteRequest, SipResponse inviteResponse, SipRequest ackRequest)
        {
            if (ackRequest == null || inviteResponse == null || !inviteResponse.IsSuccess)
                core.OnInviteFailed(new SipRequestEventArgs(null, transaction, null, this, this.core, inviteRequest), SipStatus.Stack_Timeout);
            else
                core.OnInviteConfirmed(new SipRequestEventArgs(ackRequest, transaction, null, this, this.core, inviteRequest));
        }

        /// <summary>
        /// Called periodically on a background thread to handle transaction
        /// related activities.
        /// </summary>
        public void OnBkTask()
        {
            List<string>                delList = new List<string>();
            List<SipServerTransaction>  transList;

            // Create a list of all outstanding transactions

            using (TimedLock.Lock(this))
            {
                transList = new List<SipServerTransaction>(transactions.Count);
                foreach (SipServerTransaction transaction in transactions.Values)
                    transList.Add(transaction);
            }

            // Call each transaction's OnBkTask() method outside of the lock.

            foreach (SipServerTransaction transaction in transList)
                transaction.OnBkTask();

            // Delete all terminated transactions.

            using (TimedLock.Lock(this))
            {
                foreach (string id in transactions.Keys)
                {
                    SipServerTransaction transaction = transactions[id];

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
