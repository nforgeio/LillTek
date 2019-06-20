//-----------------------------------------------------------------------------
// FILE:        ISipMessageRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Responsible for routing SIP messages received by transports
//              to the proper SipAgent.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{

    /// <summary>
    /// Responsible for routing SIP messages received by a <see cref="ISipTransport" /> to
    /// the appropriate <see cref="ISipAgent" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="ISipMessageRouter" /> is required when starting a <see cref="ISipTransport" />
    /// via its <see cref="ISipTransport.Start(SipTransportSettings,ISipMessageRouter)" /> method.  The 
    /// transport will then pass any <see cref="SipMessage" />s it receives to the router 
    /// by calling <see cref="Route" />.  The router will then pass the message on to 
    /// the <see cref="ISipAgent" /> best able to process it.
    /// </para>
    /// <para>
    /// Most SIP applications will have more than one <see cref="ISipAgent" /> processing
    /// messages received by a transport.  Client applicatons, for example, will have 
    /// a <see cref="SipClientAgent" /> for submitting requests to a registrar service
    /// as well as to peers and will also need a <see cref="SipServerAgent" /> to process
    /// requests submitted to the application by peers.  So, client applications will
    /// need to implement a simple <see cref="ISipMessageRouter" /> that routes <see cref="SipResponse" />
    /// messages to the <see cref="SipClientAgent" /> and <see cref="SipRequest" />
    /// messages to the <see cref="SipServerAgent" />.
    /// </para>
    /// <para>
    /// SIP proxies present a more complex scenario.  In this situation its possible to
    /// have multiple <see cref="SipClientAgent" /> and <see cref="SipServerAgent" />s
    /// expecting messages from a transport.  In this case, the <see cref="ISipMessageRouter" />
    /// will need to make routing decisions based on both the message type as well as
    /// where other message properties (e.g. from an internal or external network).
    /// </para>
    /// <para>
    /// Agents that need to send <see cref="SipRequest" />s, will use <see cref="SelectTransport" /> so
    /// that the router implementation can choose the appropriate transport as well as the
    /// destination server's <see cref="NetworkBinding" />.
    /// </para>
    /// </remarks>
    public interface ISipMessageRouter
    {

        /// <summary>
        /// Routes a <see cref="SipMessage" /> received by an <see cref="ISipTransport" /> to the <see cref="ISipAgent" />
        /// instance that needs to handle it.
        /// </summary>
        /// <param name="transport">The <see cref="ISipTransport" /> that received the message.</param>
        /// <param name="message">The <see cref="SipMessage" /> received by the transport.</param>
        void Route(ISipTransport transport, SipMessage message);

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
        ISipTransport SelectTransport(ISipAgent agent, SipRequest request, out NetworkBinding remoteEP);
    }
}
