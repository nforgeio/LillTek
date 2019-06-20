//-----------------------------------------------------------------------------
// FILE:        RadiusAttributeType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: RADIUS protocol packet codes.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Defines thr possible RADIUS attribute types.
    /// </summary>
    public enum RadiusAttributeType
    {
        /// <summary>
        /// This attribute indicates the name of the user to be authenticated.
        /// It MUST be sent in Access-Request packets if available.
        /// </summary>
        UserName = 1,

        /// <summary>
        /// This attribute indicates the password of the user to be
        /// authenticated, or the user's input following an Access-Challenge.
        /// It is only used in Access-Request packets.
        /// </summary>
        UserPassword = 2,

        /// <summary>
        /// This attribute indicates the response value provided by a PPP
        /// Challenge-Handshake Authentication Protocol (CHAP) user in
        /// response to the challenge.  It is only used in Access-Request
        /// packets.
        /// </summary>
        ChapPassword = 3,

        /// <summary>
        /// This attribute indicates the identifying IP Address of the NAS
        /// which is requesting authentication of the user, and SHOULD be
        /// unique to the NAS within the scope of the RADIUS server. NAS-IP-
        /// Address is only used in Access-Request packets.  Either NAS-IP-
        /// Address or NAS-Identifier MUST be present in an Access-Request
        /// packet.
        /// </summary>
        NasIpAddress = 4,

        /// <summary>
        /// This attribute indicates the physical port number of the NAS which
        /// is authenticating the user.  It is only used in Access-Request
        /// packets.  Note that this is using "port" in its sense of a
        /// physical connection on the NAS, not in the sense of a TCP or UDP
        /// port number.  Either NAS-Port or NAS-Port-Type (61) or both SHOULD
        /// be present in an Access-Request packet, if the NAS differentiates
        /// among its ports.
        /// </summary>
        NasPort = 5,

        /// <summary>
        /// This attribute indicates the type of service the user has
        /// requested, or the type of service to be provided.  It MAY be used
        /// in both Access-Request and Access-Accept packets.  A NAS is not
        /// required to implement all of these service types, and MUST treat
        /// unknown or unsupported Service-Types as though an Access-Reject
        /// had been received instead.
        /// </summary>
        ServiceType = 6,

        /// <summary>
        /// This attribute indicates the framing to be used for framed access.
        /// It MAY be used in both Access-Request and Access-Accept packets.
        /// </summary>
        FramedProtocol = 7,

        /// <summary>
        /// This attribute indicates the address to be configured for the
        /// user.  It MAY be used in Access-Accept packets.  It MAY be used in
        /// an Access-Request packet as a hint by the NAS to the server that
        /// it would prefer that address, but the server is not required to
        /// honor the hint.
        /// </summary>
        FramedIPAddress = 8,

        /// <summary>
        /// This attribute indicates the IP netmask to be configured for the
        /// user when the user is a router to a network.  It MAY be used in
        /// Access-Accept packets.  It MAY be used in an Access-Request packet
        /// as a hint by the NAS to the server that it would prefer that
        /// netmask, but the server is not required to honor the hint.
        /// </summary>
        FramedIPNetmask = 9,

        /// <summary>
        /// This attribute indicates the routing method for the user, when the
        /// user is a router to a network.  It is only used in Access-Accept
        /// packets.
        /// </summary>
        FramedRouting = 10,

        /// <summary>
        /// This attribute indicates the name of the filter list for this
        /// user.  Zero or more Filter-Id attributes MAY be sent in an
        /// Access-Accept packet.
        /// </summary>
        FilterId = 11,

        /// <summary>
        /// This attribute indicates the Maximum Transmission Unit to be
        /// configured for the user, when it is not negotiated by some other
        /// means (such as PPP).  It MAY be used in Access-Accept packets.  It
        /// MAY be used in an Access-Request packet as a hint by the NAS to
        /// the server that it would prefer that value, but the server is not
        /// required to honor the hint.
        /// </summary>
        FramedMtu = 12,

        /// <summary>
        /// This attribute indicates a compression protocol to be used for the
        /// link.  It MAY be used in Access-Accept packets.  It MAY be used in
        /// an Access-Request packet as a hint to the server that the NAS
        /// would prefer to use that compression, but the server is not
        /// required to honor the hint.
        /// </summary>
        FramedCompression = 13,

        /// <summary>
        /// This attribute indicates the system with which to connect the user,
        /// when the Login-Service Attribute is included.  It MAY be used in
        /// Access-Accept packets.  It MAY be used in an Access-Request packet as
        /// a hint to the server that the NAS would prefer to use that host, but
        /// the server is not required to honor the hint.
        /// </summary>
        LoginIpHost = 14,

        /// <summary>
        /// This attribute indicates the service to use to connect the user to
        /// the login host.  It is only used in Access-Accept packets.
        /// </summary>
        LoginService = 15,

        /// <summary>
        /// This attribute indicates the TCP port with which the user is to be
        /// connected, when the Login-Service Attribute is also present.  It
        /// is only used in Access-Accept packets.
        /// </summary>
        LoginTcpPort = 16,

        // 17 (unassigned) **********************

        /// <summary>
        /// This attribute indicates text which MAY be displayed to the user.
        /// </summary>
        ReplyMessage = 17,

        /// <summary>
        /// This attribute indicates a dialing string to be used for callback.
        /// It MAY be used in Access-Accept packets.  It MAY be used in an
        /// Access-Request packet as a hint to the server that a Callback
        /// service is desired, but the server is not required to honor the
        /// hint.
        /// </summary>
        CallbackNumber = 19,

        /// <summary>
        /// This attribute indicates the name of a place to be called, to be
        /// interpreted by the NAS.  It MAY be used in Access-Accept packets.
        /// </summary>
        CallbackId = 20,

        // 21 (unassigned) **********************

        /// <summary>
        /// This attribute provides routing information to be configured for
        /// the user on the NAS.  It is used in the Access-Accept packet and
        /// can appear multiple times.
        /// </summary>
        FramedRoute = 22,

        /// <summary>
        /// This attribute indicates the IPX Network number to be configured
        /// for the user.  It is used in Access-Accept packets.
        /// </summary>
        FramedIpxNetwork = 23,

        /// <summary>
        /// This attribute is available to be sent by the server to the client
        /// in an Access-Challenge and MUST be sent unmodified from the client
        /// to the server in the new Access-Request reply to that challenge,
        /// if any.
        /// </summary>
        State = 24,

        /// <summary>
        /// This attribute is available to be sent by the server to the client
        /// in an Access-Accept and SHOULD be sent unmodified by the client to
        /// the accounting server as part of the Accounting-Request packet if
        /// accounting is supported.  The client MUST NOT interpret the
        /// attribute locally.
        /// </summary>
        Class = 25,

        /// <summary>
        /// This attribute is available to allow vendors to support their own
        /// extended Attributes not suitable for general usage.  It MUST not
        /// affect the operation of the RADIUS protocol.
        /// </summary>
        VendorSpecific = 26,

        /// <summary>
        /// This attribute sets the maximum number of seconds of service to be
        /// provided to the user before termination of the session or prompt.
        /// This attribute is available to be sent by the server to the client
        /// in an Access-Accept or Access-Challenge.
        /// </summary>
        SessionTimeout = 27,

        /// <summary>
        /// This attribute sets the maximum number of consecutive seconds of
        /// idle connection allowed to the user before termination of the
        /// session or prompt.  This attribute is available to be sent by the
        /// server to the client in an Access-Accept or Access-Challenge.
        /// </summary>
        IdleTimeout = 28,

        /// <summary>
        /// This attribute indicates what action the NAS should take when the
        /// specified service is completed.  It is only used in Access-Accept
        /// packets.
        /// </summary>
        TerminationAction = 29,

        /// <summary>
        /// This attribute allows the NAS to send in the Access-Request packet
        /// the phone number that the user called, using Dialed Number
        /// Identification (DNIS) or similar technology.  Note that this may
        /// be different from the phone number the call comes in on.  It is
        /// only used in Access-Request packets.
        /// </summary>
        CalledStationId = 30,

        /// <summary>
        /// This attribute allows the NAS to send in the Access-Request packet
        /// the phone number that the call came from, using Automatic Number
        /// Identification (ANI) or similar technology.  It is only used in
        /// Access-Request packets.
        /// </summary>
        CallingStationId = 31,

        /// <summary>
        /// This attribute contains a string identifying the NAS originating
        /// the Access-Request.  It is only used in Access-Request packets.
        /// Either NAS-IP-Address or NAS-Identifier MUST be present in an
        /// Access-Request packet.
        /// </summary>
        NasIdentifier = 32,

        /// <summary>
        /// This attribute is available to be sent by a proxy server to
        /// another server when forwarding an Access-Request and MUST be
        /// returned unmodified in the Access-Accept, Access-Reject or
        /// Access-Challenge.  When the proxy server receives the response to
        /// its request, it MUST remove its own Proxy-State (the last Proxy-
        /// State in the packet) before forwarding the response to the NAS.
        /// </summary>
        ProxyState = 33,

        /// <summary>
        /// This attribute indicates the system with which the user is to be
        /// connected by LAT.  It MAY be used in Access-Accept packets, but
        /// only when LAT is specified as the Login-Service.  It MAY be used
        /// in an Access-Request packet as a hint to the server, but the
        /// server is not required to honor the hint.
        /// </summary>
        LoginLatService = 34,

        /// <summary>
        /// This attribute indicates the Node with which the user is to be
        /// automatically connected by LAT.  It MAY be used in Access-Accept
        /// packets, but only when LAT is specified as the Login-Service.  It
        /// MAY be used in an Access-Request packet as a hint to the server,
        /// but the server is not required to honor the hint.
        /// </summary>
        LoginLatNode = 35,

        /// <summary>
        /// This attribute contains a string identifying the LAT group codes
        /// which this user is authorized to use.  It MAY be used in Access-
        /// Accept packets, but only when LAT is specified as the Login-
        /// Service.  It MAY be used in an Access-Request packet as a hint to
        /// the server, but the server is not required to honor the hint.
        /// </summary>
        LoginLatGroup = 36,

        /// <summary>
        /// This attribute indicates the AppleTalk network number which should
        /// be used for the serial link to the user, which is another
        /// AppleTalk router.  It is only used in Access-Accept packets.  It
        /// is never used when the user is not another router.
        /// </summary>
        FramedAppleTalkLink = 37,

        /// <summary>
        /// This attribute indicates the AppleTalk Network number which the
        /// NAS should probe to allocate an AppleTalk node for the user.  It
        /// is only used in Access-Accept packets.  It is never used when the
        /// user is another router.  Multiple instances of This attribute
        /// indicate that the NAS may probe using any of the network numbers
        /// specified.
        /// </summary>
        FramedAppleTalkNetwork = 38,

        /// <summary>
        /// This attribute indicates the AppleTalk Default Zone to be used for
        /// this user.  It is only used in Access-Accept packets.  Multiple
        /// instances of This attribute in the same packet are not allowed.
        /// </summary>
        FramedAppleTalkZone = 39,

        // 4059 (reserved for accounting) *******

        /// <summary>
        /// This attribute contains the CHAP Challenge sent by the NAS to a
        /// PPP Challenge-Handshake Authentication Protocol (CHAP) user.  It
        /// is only used in Access-Request packets.
        /// </summary>
        ChapChallenge = 60,

        /// <summary>
        /// This attribute indicates the type of the physical port of the NAS
        /// which is authenticating the user.  It can be used instead of or in
        /// addition to the NAS-Port (5) attribute.  It is only used in
        /// Access-Request packets.  Either NAS-Port (5) or NAS-Port-Type or
        /// both SHOULD be present in an Access-Request packet, if the NAS
        /// differentiates among its ports.
        /// </summary>
        NasPortType = 61,

        /// <summary>
        /// This attribute sets the maximum number of ports to be provided to
        /// the user by the NAS.  This attribute MAY be sent by the server to
        /// the client in an Access-Accept packet.  It is intended for use in
        /// conjunction with Multilink PPP [12] or similar uses.  It MAY also
        /// be sent by the NAS to the server as a hint that that many ports
        /// are desired for use, but the server is not required to honor the
        /// hint.
        /// </summary>
        PortLimit = 62,

        /// <summary>
        /// This attribute indicates the Port with which the user is to be
        /// connected by LAT.  It MAY be used in Access-Accept packets, but
        /// only when LAT is specified as the Login-Service.  It MAY be used
        /// in an Access-Request packet as a hint to the server, but the
        /// server is not required to honor the hint.
        /// </summary>
        LoginLatPort = 63,

        /// <summary>
        /// The first experimental attribute type.
        /// </summary>
        ExperimentalFirst = 192,

        /// <summary>
        /// The last experimental attribute type.
        /// </summary>
        ExperimentalLast = 223,

        /// <summary>
        /// The first implementation specific attribute type.
        /// </summary>
        ImplementationFirst = 224,

        /// <summary>
        /// The last implementation specific attribute type.
        /// </summary>
        ImplementationLast = 240
    }
}
