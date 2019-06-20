//-----------------------------------------------------------------------------
// FILE:        SipBasicCore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a basic SIP client core binding a SipClientAgent
//              and a SipServerAgent to one or more SipTransports.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// I'm bacically just hacking this for now.  I need to come back
// and really do a clean implementation.

// $todo(jeff.lill): 
//
// I don't handle the case where the ACK confirming an INVITE
// has an SDP payload.  This code will continue deliverying
// its media using the SDP sent in the original 2xx response
// to the INVITE.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a basic SIP core binding a <see cref="SipClientAgent" /> and a 
    /// <see cref="SipServerAgent" /> to one or more <see cref="ISipTransport" />s.
    /// See <see cref="SipCore" /> for information on the basic operation of a core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once a <see cref="SipBasicCore" /> has been bound to one or more <see cref="ISipTransport" />s
    /// and started using the base class <see cref="SipCore.Start" /> method, this core will
    /// begin routing messages received by the transports to the two agents as well as
    /// messages from the agent to the appropriate outbound transports.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SipBasicCore : SipCore, ISipMessageRouter
    {

        private SipClientAgent clientAgent;        // Used for sending requests
        private SipServerAgent serverAgent;        // Used for receiving requests

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="SipCoreSettings" />.</param>
        public SipBasicCore(SipCoreSettings settings)
            : base(settings)
        {
            this.clientAgent = new SipClientAgent(this, this);
            this.serverAgent = new SipServerAgent(this, this);

            base.SetRouter(this);
            base.Agents.Add(clientAgent);
            base.Agents.Add(serverAgent);
        }

        /// <summary>
        /// Stops the core, unregistering the current entity if necessary.
        /// </summary>
        public override void Stop()
        {
            base.Stop();
        }

        /// <summary>
        /// Handles background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        public override void OnBkTask(object state)
        {
            base.OnBkTask(state);
        }

        //---------------------------------------------------------------------
        // ISipMessageRouter implementation

        /// <summary>
        /// Routes a <see cref="SipMessage" /> received by an <see cref="ISipTransport" /> to the <see cref="ISipAgent" />
        /// instance that needs to handle it.
        /// </summary>
        /// <param name="transport">The <see cref="ISipTransport" /> that received the message.</param>
        /// <param name="message">The <see cref="SipMessage" /> received by the transport.</param>
        public void Route(ISipTransport transport, SipMessage message)
        {
            // Routing is easy: 
            //
            //      Servers get the requests
            //      Clients get the responses.

            if (message is SipRequest)
                serverAgent.OnReceive(transport, message);
            else
                clientAgent.OnReceive(transport, message);
        }

        /// <summary>
        /// Returns the <see cref="ISipTransport" /> that will be used to
        /// deliver a <see cref="SipMessage" /> from a source <see cref="ISipAgent" />.
        /// </summary>
        /// <param name="agent">The source agent.</param>
        /// <param name="request">The <see cref="SipRequest" /> to be delivered.</param>
        /// <param name="remoteEP">Returns as the destination server's <see cref="NetworkBinding" />.</param>
        /// <returns>The <see cref="ISipTransport" /> that will be used for delivery (or <c>null</c>).</returns>
        /// <remarks>
        /// <note>
        /// <c>null</c> is a valid return value.  This indicates that there are
        /// no appropriate transports available to deliver this message.
        /// </note>
        /// </remarks>
        public ISipTransport SelectTransport(ISipAgent agent, SipRequest request, out NetworkBinding remoteEP)
        {
            SipTransportType    transportType;
            SipUri              proxyUri;

            proxyUri = base.OutboundProxyUri;
            if (proxyUri != null)
            {
                // Select a transport to route the message to the outbound proxy.

                if (!SipHelper.TryGetRemoteBinding("<" + proxyUri + ">", out remoteEP, out transportType))
                    return null;
            }
            else if (!SipHelper.TryGetRemoteBinding("<" + request.Uri + ">", out remoteEP, out transportType))
                return null;

            // Select the first transport that looks decent.  If the desired transport
            // is not specified, then favor UDP since most of the world is compatible
            // with that.

            if (transportType == SipTransportType.UDP || transportType == SipTransportType.Unspecified)
            {
                foreach (ISipTransport transport in base.Transports)
                    if (transport.TransportType == SipTransportType.UDP)
                        return transport;

                return null;
            }

            // Otherwise match the transport.

            foreach (ISipTransport transport in base.Transports)
                if (transport.TransportType == transportType)
                    return transport;

            return null;
        }
    }
}
