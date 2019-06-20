//-----------------------------------------------------------------------------
// FILE:        RouterSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used for explicitly specifying message router settings.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Used for explicitly specifying message router settings.
    /// </summary>
    /// <remarks>
    /// These settings are initialized to reasonable default values
    /// by the class constructor.
    /// </remarks>
    public sealed class RouterSettings
    {
        //---------------------------------------------------------------------
        // Fields used by all router classes.

        /// <summary>
        /// Name of the application hosting the router.
        /// </summary>
        public string AppName = Helper.EntryAssemblyFile;

        /// <summary>
        /// Brief description of the application hosting the router.
        /// </summary>
        public string AppDescription = string.Empty;

        /// <summary>
        /// The globally unique physical route for this router instance (default: <b>physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)</b>).
        /// </summary>
        public MsgEP RouterEP = null;

        /// <summary>
        /// Specifies how the router will go about discovering other routers on the
        /// network.  The possible values are <b>MULTICAST</b> (the default) and
        /// <b>UDPBROADCAST</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <b>MULTICAST</b> is specified then the router will broadcast and listen
        /// for presence packets on the specified <see cref="CloudAdapter"/> for the <see cref="CloudEP" />
        /// multicast endpoint.
        /// </para>
        /// <para>
        /// If <b>UDPBROADCAST</b> is specified then the router will use the LillTek
        /// Broadcast Server to handle the transmission and reception presence packets
        /// on networks that don't support multicast.  The <see cref="BroadcastSettings" />
        /// property can be used to configure the internal <see cref="UdpBroadcastClient" />
        /// used to manage these broadcasts.
        /// </para>
        /// </remarks>
        public DiscoveryMode DiscoveryMode = DiscoveryMode.Multicast;

        /// <summary>
        /// Multicast group endpoint used for router discovery (default: <see cref="Const.DCCloudEP" />).
        /// </summary>
        public IPEndPoint CloudEP = Const.DCCloudEP;

        /// <summary>
        /// Specifies the IP address of the network adapter to be used for transmitting
        /// and receiving multicast discovery packets. (default: ANY).
        /// </summary>
        public IPAddress CloudAdapter = IPAddress.Any;

        /// <summary>
        /// Settings for the <see cref="UdpBroadcastClient" /> used to manage the precence
        /// packets used for router discovery when operating in <see cref="LillTek.Messaging.DiscoveryMode.UdpBroadcast "/>
        /// discovery mode.  This is initialized with reasonable default values.
        /// </summary>
        public UdpBroadcastClientSettings BroadcastSettings = new UdpBroadcastClientSettings();

        /// <summary>
        /// Multicast socket send buffer size (default: 64K).
        /// </summary>
        public int MulticastSendBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// Multicast socket receive buffer size (default: 64K).
        /// </summary>
        public int MulticastReceiveBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// UDP network endpoint (default: ANY:0).
        /// </summary>
        public NetworkBinding UdpEP = new NetworkBinding(IPAddress.Any, 0);

        /// <summary>
        /// UDP send buffer size (default: 64K).
        /// </summary>
        public int UdpSendBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// UDP receive buffer size (default: 64K).
        /// </summary>
        public int UdpReceiveBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// UDP network endpoint (default: ANY:0).
        /// </summary>
        public NetworkBinding TcpEP = new NetworkBinding(IPAddress.Any, 0);

        /// <summary>
        /// Maximum pending inbound socket connections (Default: 100).
        /// </summary>
        public int TcpBacklog = 100;

        /// <summary>
        /// Enables Nagle on the TCP connections (default: false).
        /// </summary>
        public bool TcpDelay = false;

        /// <summary>
        /// UDP send buffer size (default: 64K).
        /// </summary>
        public int TcpSendBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// UDP receive buffer size (default: 64K).
        /// </summary>
        public int TcpReceiveBufferSize = MsgRouter.DefSockBufSize;

        /// <summary>
        /// The master background task interval (default: 1s).
        /// </summary>
        public TimeSpan BkInterval = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Maximum time a TCP socket can remain idle before being closed automatically (default: 5m).
        /// </summary>
        public TimeSpan MaxIdle = TimeSpan.FromMinutes(5.0);

        /// <summary>
        /// Enables peer-to-peer routing between this router and other P2P enabled
        /// routers on the current subnet (default: true).
        /// </summary>
        public bool EnableP2P = true;

        /// <summary>
        /// Interval at which the router will advertise its presence via a multicast transmission. (default: 1m).
        /// </summary>
        public TimeSpan AdvertiseTime = TimeSpan.FromMinutes(1.0);

        /// <summary>
        /// Maximum time a physical route will be maintained without being refreshed with a 
        /// RouterAdvertiseMsg (default: 3m).
        /// </summary>
        public TimeSpan PhysicalRouteTTL = TimeSpan.FromMinutes(3.0);

        /// <summary>
        /// Default message time-to-live, or maximum router hops (default: 5).
        /// </summary>
        public int DefMsgTTL = 5;

        /// <summary>
        /// Shared encryption key used for encrypting message traffic. (default: PLAINTEXT).
        /// </summary>
        public SymmetricKey SharedKey = new SymmetricKey("PLAINTEXT");

        /// <summary>
        /// Default time the router's session manager will cache idempotent replies (default: 2m).
        /// </summary>
        public TimeSpan SessionCacheTime = TimeSpan.FromMinutes(2.0);

        /// <summary>
        /// Default maximum session retry count (default: 3).
        /// </summary>
        public int SessionRetries = 3;

        /// <summary>
        /// Default session timeout (default: 10s).
        /// </summary>
        public TimeSpan SessionTimeout = TimeSpan.FromSeconds(10.0);

        /// <summary>
        /// Maximum number of logical endpoints to be included in a single logical route advertise message.
        /// (default: 256).
        /// </summary>
        public int MaxLogicalAdvertiseEPs = 256;

        /// <summary>
        /// Maximum time to wait for a <see cref="ReceiptMsg" /> before declaring a dead router.
        /// Use 0 to disable dead router detection. (default: 0s).
        /// </summary>
        public TimeSpan DeadRouterTTL = TimeSpan.Zero;

        /// <summary>
        /// Maximum number of normal priority outbound messages to queue in a TCP channel before beginning to
        /// discard messages.
        /// </summary>
        public int TcpMsgQueueCountMax = 1000;

        /// <summary>
        /// Maximum bytes of serialized normal priority outbound messages to queue in a TCP channel
        /// before beginning to discard messages.
        /// </summary>
        public int TcpMsgQueueSizeMax = 10 * 1024 * 1024;

        /// <summary>
        /// Maximum number of normal priority outbound messages to queue in a UDP channel before beginning to
        /// discard messages or 0 to disable the check.
        /// </summary>
        public int UdpMsgQueueCountMax = 1000;

        /// <summary>
        /// Maximum bytes of serialized normal priority outbound messages to queue in a UDP channel
        /// before beginning to discard messages.
        /// </summary>
        public int UdpMsgQueueSizeMax = 10 * 1024 * 1024;

        /// <summary>
        /// Specifies the set of logical endpoints where the router is to optimize routing for
        /// locality, favoring routes to phyiscal endpoints that are closer to the router.
        /// </summary>
        public LocalEPMap LocalityMap = new LocalEPMap();

        //---------------------------------------------------------------------
        // Fields used by Hub Routers

        /// <summary>
        /// Host/IP and port of the root router's listening TCP socket (a static route),
        /// formatted as IP:port or host:port or the string "DETACHED" if the hub router is
        /// not to be connected to a root (used by <see cref="HubRouter" /> default: none).
        /// </summary>
        public string ParentEP = null;

        /// <summary>
        /// Zero or more static logical endpoints to be advertised to the root router 
        /// (default: none).
        /// </summary>
        public MsgEP[] DownlinkEP = new MsgEP[0];

        /// <summary>
        /// Interval between <see cref="HubKeepAliveMsg" /> transmissions on the uplink
        /// connection to the root router (default: 1m).
        /// </summary>
        public TimeSpan HubKeepAliveTime = TimeSpan.FromMinutes(1.0);

        //---------------------------------------------------------------------
        // Fields used by Root Routers

        /// <summary>
        /// Zero or more static logical endpoints to be advertised to hub routers
        /// (default: none).
        /// </summary>
        public MsgEP[] UplinkEP = new MsgEP[0];

        /// <summary>
        /// Constructs the default router settings.
        /// </summary>
        public RouterSettings()
        {
            this.RouterEP = EnvironmentVars.Expand("physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)");
        }

        /// <summary>
        /// Constructs the default router settings using a specific router endpoint.
        /// </summary>
        /// <param name="routerEP">Physical endpoint of the router being started.</param>
        public RouterSettings(MsgEP routerEP)
        {
            Assertion.Test(routerEP.IsPhysical);

            this.RouterEP = routerEP;
        }
    }
}
