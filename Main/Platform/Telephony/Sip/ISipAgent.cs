//-----------------------------------------------------------------------------
// FILE:        ISipAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the common behavior of a SIP agent.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines the common behavior of a SIP agent.  The
    /// <see cref="SipClientAgent" /> and <see cref="SipServerAgent" />
    /// classes implement this interface.
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
    /// </remarks>
    /// <threadsafety instance="true" />
    public interface ISipAgent : ILockable
    {
        /// <summary>
        /// Returns the <see cref="SipCore" /> that owns this agent.
        /// </summary>
        SipCore Core { get; }

        /// <summary>
        /// Stops the agent, terminating any outstanding transactions.
        /// </summary>
        void Stop();

        /// <summary>
        /// Handles messages received by a transport to be processed by this agent.
        /// </summary>
        /// <param name="transport">The source transport.</param>
        /// <param name="message">The received message.</param>
        void OnReceive(ISipTransport transport, SipMessage message);

        /// <summary>
        /// Called periodically on a background thread to handle transaction
        /// related activities.
        /// </summary>
        void OnBkTask();
    }
}
