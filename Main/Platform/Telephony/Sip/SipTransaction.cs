//-----------------------------------------------------------------------------
// FILE:        SipTransaction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The base class for all SIP transactions.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// The base class for all SIP transactions.
    /// </summary>
    public abstract class SipTransaction
    {
        private string                  id;         // The globally unique transaction ID
        private SipTransactionState     state;      // The current transaction state
        private ISipTransport           transport;  // Transport used for this transaction
        private ISipAgent               agent;      // The agent that owns this transaction
        private SipBaseTimers           baseTimers; // The transport's base timers.
        private object                  agentState; // Agent defined transaction state

        // Transaction timers: See page 265 in RFC 3261 for more information.

        /// <summary>
        /// INVITE request retransmit timer (UDP only).
        /// </summary>
        protected PolledTimer TimerA = new PolledTimer();

        /// <summary>
        /// INVITE transaction timeout timer.
        /// </summary>
        protected PolledTimer TimerB = new PolledTimer();

        /// <summary>
        /// Proxy INVITE transaction timeout timer.
        /// </summary>
        protected PolledTimer TimerC = new PolledTimer();

        /// <summary>
        /// Wait timer for response retransmits.
        /// </summary>
        protected PolledTimer TimerD = new PolledTimer();

        /// <summary>
        /// Non-INVITE request retransmit timer (UDP only).
        /// </summary>
        protected PolledTimer TimerE = new PolledTimer();

        /// <summary>
        /// Non-INVITE transaction timeout timer.
        /// </summary>
        protected PolledTimer TimerF = new PolledTimer();

        /// <summary>
        /// INVITE response retransmit timer.
        /// </summary>
        protected PolledTimer TimerG = new PolledTimer();

        /// <summary>
        /// Wait timer for ACK receipt.
        /// </summary>
        protected PolledTimer TimerH = new PolledTimer();

        /// <summary>
        /// Wait timer for ACK retransmits.
        /// </summary>
        protected PolledTimer TimerI = new PolledTimer();

        /// <summary>
        /// Wait timer for non-INVITE request retransmits.
        /// </summary>
        protected PolledTimer TimerJ = new PolledTimer();

        /// <summary>
        /// Wait timer for response retransmits.
        /// </summary>
        protected PolledTimer TimerK = new PolledTimer();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="agent">The <see cref="ISipAgent" /> that owns this transaction.</param>
        /// <param name="id">The globally unique transaction ID.</param>
        /// <param name="transport">The <see cref="ISipTransport" /> to be used for this transaction.</param>
        /// <remarks>
        /// The timers <see cref="TimerA" /> through <see cref="TimerK" /> are initialized
        /// with the correct <see cref="PolledTimer.Interval" /> values for the given transport.
        /// These timers will then need to be <see cref="PolledTimer.Reset()" /> before they
        /// are actually used so they will be scheduled to fire at the correct time.
        /// </remarks>
        protected SipTransaction(ISipAgent agent, string id, ISipTransport transport)
        {
            this.agent      = agent;
            this.id         = id;
            this.state      = SipTransactionState.Unknown;
            this.transport  = transport;
            this.baseTimers = transport.Settings.BaseTimers;
            this.agentState = null;
        }

        /// <summary>
        /// Returns the transport's <see cref="SipBaseTimers" />.
        /// </summary>
        public SipBaseTimers BaseTimers
        {
            get { return baseTimers; }
        }

        /// <summary>
        /// Returns the <see cref="ISipAgent" /> managing this transaction.
        /// </summary>
        public ISipAgent Agent
        {
            get { return agent; }
        }

        /// <summary>
        /// Returns the transaction ID.
        /// </summary>
        /// <remarks>
        /// The transaction ID appears in SIP messages as the <b>branch</b> parameter
        /// of the <b>Via</b> header for the source SIP element.
        /// </remarks>
        public string ID
        {
            get { return id; }
        }

        /// <summary>
        /// The current transaction state.  See <see cref="SipTransactionState" />.
        /// </summary>
        public SipTransactionState State
        {
            get { return state; }
            set { state = value; }
        }

        /// <summary>
        /// Returns the <see cref="ISipTransport" /> associated with this transaction.
        /// </summary>
        public ISipTransport Transport
        {
            get { return transport; }
        }

        /// <summary>
        /// Available for storing agent defined state.
        /// </summary>
        public object AgentState
        {
            get { return agentState; }
            set { agentState = value; }
        }

        /// <summary>
        /// This method will be called periodically on a background thread to handle
        /// message resending as well as timeout related state transitions.
        /// </summary>
        public abstract void OnBkTask();
    }
}
