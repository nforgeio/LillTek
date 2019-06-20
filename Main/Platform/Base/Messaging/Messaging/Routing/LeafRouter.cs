//-----------------------------------------------------------------------------
// FILE:        LeafRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a MsgRouter is capable of routing messages between
//              itself and a hub router on the subnet.

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
    /// Implements a MsgRouter is capable of routing messages between itself 
    /// and a hub router on the subnet.
    /// </summary>
    public class LeafRouter : MsgRouter
    {

        private MsgEP           hubEP;              // The hub's physical endpoint (or null)
        private ChannelEP       hubChannelEP;       // The hub's TCP channel endpoint (or null)
        private IPAddress       hubIPAddress;       // The hub's IP address (or null)
        private int             hubUdpPort;         // The hub's UDP port (or 0)
        private int             hubTcpPort;         // The hub's listening TCP port (or 0)
        private bool            isRunning;          // True if the router is running
        private bool            dupLeafDetected;    // True if a duplicate leaf EP has been detected.

        private TimeSpan        advertiseTime;      // RouterAdvertiseMsg multicast interval
        private DateTime        lastAdvertise;      // Time (SYS) of the last RouterAdvertiseMsg multicast

        /// <summary>
        /// This constructor initializes the router.
        /// </summary>
        public LeafRouter()
            : base()
        {
            this.hubEP           = null;
            this.hubIPAddress    = null;
            this.hubUdpPort      = 0;
            this.hubTcpPort      = 0;
            this.isRunning       = false;
            this.dupLeafDetected = false;
        }

        /// <summary>
        /// Starts the router, reading configuration parameters from the 
        /// application's configuration settings.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method will gather the instance settings from the application's
        /// configuration settings, as described below.  Multiple router instances
        /// can be specified in a single configuration file.  The router name
        /// parameter is used to distinguish between the settings for each router
        /// instance.  The method will look for configuration keys prefixed by
        /// </para>
        /// <code language="none">
        /// "MsgRouter." + name + "."
        /// </code>            
        /// <para>
        /// So, when loading the TcpEP setting, Start("Foo") will query for 
        /// "Router.Foo.TcpEP".
        /// </para>
        /// <para>
        /// Here are the configuration settings LeafRouter expects: (note
        /// that all settings are prefixed by "MsgRouter." as in "MsgRouter.RouterEP".
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top"><td>AppName</td><td>EXE file name</td><td>Name of the application hosting the router</td></tr>
        /// <tr valign="top"><td>AppDescription</td><td>(none)</td><td>Description of the application hosting thr router</td></tr>
        /// <tr valign="top">
        ///     <td>RouterEP</td>
        ///     <td>physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)</td>
        ///     <td>
        ///         Physical MsgEP for this instance.  The endpoint should be three levels deep
        ///         such as physical://root.com:40/hub/leaf and the leaf name should be unique across
        ///         all leaf routers beneath the hub.  One way to guarantee uniquness is to use the $(guid)
        ///         environment variable in the endpoint, as in physical://root.com:40/hub/$(MachineName)-$(Guid).
        ///         This will be replaced with a newly generated GUID when the configuration variable
        ///         is processed.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DiscoveryMode</td>
        ///     <td>MULTICAST</td>
        ///     <td>
        ///     <para>
        ///     Specifies how the router will go about discovering other routers on the
        ///     network.  The possible values are <b>MULTICAST</b> (the default) and
        ///     <b>UDPBROADCAST</b>.
        ///     </para>
        ///     <para>
        ///     If <b>MULTICAST</b> is specified then the router will broadcast and listen
        ///     for presence packets on the specified <see cref="RouterSettings.CloudAdapter"/> for the 
        ///     <see cref="RouterSettings.CloudEP" /> multicast endpoint.
        ///     </para>
        ///     <para>
        ///     If <b>UDPBROADCAST</b> is specified then the router will use the LillTek
        ///     Broadcast Server to handle the transmission and reception presence packets
        ///     on networks that don't support multicast.  The <see cref="RouterSettings.BroadcastSettings" />
        ///     property can be used to configure the internal <see cref="UdpBroadcastClient" />
        ///     used to manage these broadcasts.
        ///     </para>
        ///     </td>
        /// </tr>
        /// <tr valign="top"><td>CloudEP</td><td><see cref="Const.DCCloudEP" /></td><td>The discovery UDP multicast network group and port</td></tr>
        /// <tr valign="top"><td>CloudAdapter</td><td>ANY</td><td>The discovery UDP multicast network adapter address</td></tr>
        /// <tr valign="top"><td>MulticastSendBufferSize</td><td>64K</td><td>UDP multicast socket send buffer size</td></tr>
        /// <tr valign="top"><td>MulticastReceiveBufferSize</td><td>64K</td><td>Multicast socket receive buffer size</td></tr>
        /// <tr valign="top">
        ///     <td>BroadcastSettings</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     Settings for the <see cref="UdpBroadcastClient" /> used to manage the precence
        ///     packets used for router discovery when operating in <see cref="DiscoveryMode.UdpBroadcast "/>
        ///     discovery mode.  This is initialized with reasonable default values.
        ///     </td>
        /// </tr>
        /// <tr valign="top"><td>UdpEP</td><td>ANY:0</td><td>UDP network endpoint</td></tr>
        /// <tr valign="top"><td>UdpMsgQueueCountMax</td><td>1000</td><td>Max queued outbound UDP normal priority messages.</td></tr>
        /// <tr valign="top"><td>UdpMsgQueueSizeMax</td><td>10MB</td><td>Max bytes of serialized queued outbound UDP normal priority messages.</td></tr>
        /// <tr valign="top"><td>UdpSendBufferSize</td><td>64K</td><td>UDP unicast socket send buffer size</td></tr>
        /// <tr valign="top"><td>UdpReceiveBufferSize</td><td>64K</td><td>UDP unicast socket receive buffer size</td></tr>
        /// <tr valign="top"><td>TcpEP</td><td>ANY:0</td><td>TCP listening network endpoint</td></tr>
        /// <tr valign="top"><td>TcpMsgQueueCountMax</td><td>1000</td><td>Max queued outbound TCP normal priority messages.</td></tr>
        /// <tr valign="top"><td>TcpMsgQueueSizeMax</td><td>10MB</td><td>Max bytes of serialized queued outbound TCP normal priority messages.</td></tr>
        /// <tr valign="top"><td>TcpBacklog</td><td>100</td><td>Max pending connecting TCP sockets</td></tr>
        /// <tr valign="top"><td>TcpDelay</td><td>off</td><td>Enables Nagle on TCP channels</td></tr>
        /// <tr valign="top"><td>TcpSendBufferSize</td><td>64K</td><td>TCP socket send buffer size</td></tr>
        /// <tr valign="top"><td>TcpReceiveBufferSize</td><td>64K</td><td>TCP socket receive buffer size</td></tr>
        /// <tr valign="top"><td>BkInterval</td><td>1s</td><td>Background task interval</td></tr>
        /// <tr valign="top"><td>MaxIdle</td><td>5m</td><td>Maximum time a TCP socket should idle before being closed automatically</td></tr>
        /// <tr valign="top"><td>EnableP2P</td><td>true</td><td>Enables peer-to-peer routing between routers on the same subnet</td></tr>
        /// <tr valign="top"><td>AdvertiseTime</td><td>1m</td><td>RouterAdvertiseMsg multicast interval</td></tr>
        /// <tr valign="top"><td>PhysicalRouteTTL</td><td>3m</td><td>Maximum time a physical route will be maintained without being refreshed with a RouterAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DefMsgTTL</td><td>5</td><td>Default message time-to-live (max hops)</td></tr>
        /// <tr valign="top"><td>SharedKey</td><td>PLAINTEXT</td><td>The shared encryption key used to encrypt all message traffic.</td></tr>
        /// <tr valign="top"><td>SessionCacheTime</td><td>2m</td><td>Default time the router's session manager will cache idempotent replies.</td></tr>
        /// <tr valign="top"><td>SessionRetries</td><td>3</td><td>Maximum session initiation retries.</td></tr>
        /// <tr valign="top"><td>SessionTimeout</td><td>10s</td><td>Default session timeout</td></tr>
        /// <tr valign="top"><td>MaxLogicalAdvertiseEPs</td><td>256</td><td>Maximum number of logical endpoints to be included in a single LogicalAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DeadRouterTTL</td><td>0s</td><td>Maximum time to wait for a <see cref="ReceiptMsg" /> before declaring a dead router.  Use 0 to disable dead router detection.</td></tr>
        /// <tr valign="top">
        ///     <td>RouteLocal</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     <para>
        ///     An array of zero or more logical routes for which messages should favor 
        ///     local destinations.  These routes may include wildcards.  Here's an example 
        ///     configuration fragment:
        ///     </para>
        ///     <code lang="none">
        ///     #section MsgRouter
        /// 
        ///         RouteLocal[0] = abstract://Test/Local
        ///         RouteLocal[1] = abstract://MyApps/*
        /// 
        ///     #endsection
        ///     </code>
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public void Start()
        {
            RouterSettings  settings;
            MsgEP           routerEP;
            Config          config;
            string          v;

            // Load the configuration settings

            config = new Config(MsgHelper.ConfigPrefix);

            v = config.Get("RouterEP", EnvironmentVars.Expand("physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)"));

            try
            {
                routerEP = MsgEP.Parse(v);
            }
            catch
            {
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is invalid.");
            }

            if (routerEP.Segments.Length != 2)
                throw new MsgException("[MsgRouter.RouterEP] must specify a valid two segment leaf endpoint (eg: physical://root/hub/leaf).");

            settings                            = new RouterSettings(routerEP);
            settings.AppName                    = config.Get("AppName", settings.AppName);
            settings.AppDescription             = config.Get("AppDescription", settings.AppDescription);
            settings.DiscoveryMode              = config.Get<DiscoveryMode>("DiscoveryMode", settings.DiscoveryMode);
            settings.CloudAdapter               = config.Get("CloudAdapter", settings.CloudAdapter);
            settings.CloudEP                    = config.Get("CloudEP", settings.CloudEP);
            settings.BroadcastSettings          = new UdpBroadcastClientSettings(Config.CombineKeys(MsgHelper.ConfigPrefix, "BroadcastSettings"));
            settings.UdpEP                      = config.Get("UdpEP", settings.UdpEP);
            settings.UdpMsgQueueCountMax        = config.Get("UdpMsgQueueCountMax", settings.UdpMsgQueueCountMax);
            settings.UdpMsgQueueSizeMax         = config.Get("UdpMsgQueueSizeMax", settings.UdpMsgQueueSizeMax);
            settings.TcpEP                      = config.Get("TcpEP", settings.TcpEP);
            settings.TcpMsgQueueCountMax        = config.Get("TcpMsgQueueCountMax", settings.TcpMsgQueueCountMax);
            settings.TcpMsgQueueSizeMax         = config.Get("TcpMsgQueueSizeMax", settings.TcpMsgQueueSizeMax);
            settings.TcpBacklog                 = config.Get("TcpBacklog", settings.TcpBacklog);
            settings.MaxIdle                    = config.Get("MaxIdle", settings.MaxIdle);
            settings.EnableP2P                  = config.Get("EnableP2P", settings.EnableP2P);
            settings.SessionCacheTime           = config.Get("SessionCacheTime", settings.SessionCacheTime);
            settings.SessionRetries             = config.Get("SessionRetries", settings.SessionRetries);
            settings.SessionTimeout             = config.Get("SessionTimeout", settings.SessionTimeout);
            settings.TcpDelay                   = config.Get("TcpDelay", settings.TcpDelay);
            settings.BkInterval                 = config.Get("BkInterval", settings.BkInterval);
            settings.DefMsgTTL                  = config.Get("DefMsgTTL", settings.DefMsgTTL);
            settings.PhysicalRouteTTL           = config.Get("PhysicalRouteTTL", settings.PhysicalRouteTTL);
            settings.MaxLogicalAdvertiseEPs     = config.Get("MaxLogicalAdvertiseEPs", settings.MaxLogicalAdvertiseEPs);
            settings.DeadRouterTTL              = config.Get("DeadRouterTTL", settings.DeadRouterTTL);
            settings.MulticastSendBufferSize    = config.Get("MulticastSendBufferSize", settings.MulticastSendBufferSize);
            settings.MulticastReceiveBufferSize = config.Get("MulticastReceiveBufferSize", settings.MulticastReceiveBufferSize);
            settings.UdpSendBufferSize          = config.Get("UdpSendBufferSize", settings.UdpSendBufferSize);
            settings.UdpReceiveBufferSize       = config.Get("UdpReceiveBufferSize", settings.UdpReceiveBufferSize);
            settings.TcpSendBufferSize          = config.Get("TcpSendBufferSize", settings.TcpSendBufferSize);
            settings.TcpReceiveBufferSize       = config.Get("TcpReceiveBufferSize", settings.TcpReceiveBufferSize);
            settings.AdvertiseTime              = config.Get("AdvertiseTime", settings.AdvertiseTime);

            v = config.Get("SharedKey");
            if (!string.IsNullOrWhiteSpace(v))
            {
                try
                {
                    settings.SharedKey = new SymmetricKey(v);
                }
                catch
                {
                    // Ignoring
                }
            }

            // Get the local route endpoints

            foreach (string ep in config.GetArray("RouteLocal"))
                settings.LocalityMap.Add(ep);

            Start(settings);
        }

        /// <summary>
        /// Starts the router using the configuration settings passed.
        /// </summary>
        /// <param name="settings">The configuration settings.</param>
        public void Start(RouterSettings settings)
        {
            IPAddress       cloudAdapter;
            IPEndPoint      cloudEP;
            IPEndPoint      udpEP;
            IPEndPoint      tcpEP;
            int             tcpBacklog;
            TimeSpan        maxIdle;

            this.RouterEP = settings.RouterEP;
            if (this.RouterEP.Segments.Length != 2)
                throw new MsgException("RouterEP must specify a valid two segment leaf endpoint (physical://root/hub/leaf).");

            Trace(0, "Router Start: " + this.AppName, this.RouterEP.ToString(), string.Empty);

            cloudAdapter = settings.CloudAdapter;
            cloudEP      = settings.CloudEP;
            udpEP        = settings.UdpEP;
            tcpEP        = settings.TcpEP;
            tcpBacklog   = settings.TcpBacklog;
            maxIdle      = settings.MaxIdle;

            base.AppName                = settings.AppName;
            base.AppDescription         = settings.AppDescription;
            base.DiscoveryMode          = settings.DiscoveryMode;
            base.BroadcastSettings      = settings.BroadcastSettings;
            base.EnableP2P              = settings.EnableP2P;
            base.SessionCacheTime       = settings.SessionCacheTime;
            base.SessionRetries         = settings.SessionRetries;
            base.SessionTimeout         = settings.SessionTimeout;
            base.TcpMsgQueueCountMax    = settings.TcpMsgQueueCountMax;
            base.TcpMsgQueueSizeMax     = settings.TcpMsgQueueSizeMax;
            base.TcpDelay               = settings.TcpDelay;
            base.BkInterval             = settings.BkInterval;
            base.DefMsgTTL              = settings.DefMsgTTL;
            base.PhysRouteTTL           = settings.PhysicalRouteTTL;
            base.MaxLogicalAdvertiseEPs = settings.MaxLogicalAdvertiseEPs;
            base.DeadRouterTTL          = settings.DeadRouterTTL;
            base.UdpMsgQueueCountMax    = settings.UdpMsgQueueCountMax;
            base.UdpMsgQueueSizeMax     = settings.UdpMsgQueueSizeMax;
            base.UdpMulticastSockConfig = new SocketConfig(settings.MulticastSendBufferSize, settings.MulticastReceiveBufferSize);
            base.UdpUnicastSockConfig   = new SocketConfig(settings.UdpSendBufferSize, settings.MulticastReceiveBufferSize);
            base.TcpSockConfig          = new SocketConfig(settings.TcpSendBufferSize, settings.TcpReceiveBufferSize);

            this.advertiseTime          = settings.AdvertiseTime;

            // Crank this sucker up

            using (TimedLock.Lock(this.SyncRoot))
            {
                base.PhysicalRoutes = new PhysicalRouteTable(this, base.PhysRouteTTL);
                base.LogicalRoutes  = new LogicalRouteTable(this);

                base.SetDispatcher(new MsgDispatcher(this));
                base.Dispatcher.AddTarget(this);
                base.EnableEncryption(settings.SharedKey);
                base.Start(cloudAdapter, cloudEP, udpEP, tcpEP, tcpBacklog, maxIdle);

                lastAdvertise  = SysTime.Now + advertiseTime;
                this.isRunning = true;

                Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                 this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, true, this.EnableP2P));
            }

            // Pause a second to give the router a chance to discover the local hub.

            Thread.Sleep(1000);
        }

        /// <summary>
        /// Stops the router.
        /// </summary>
        /// <remarks>
        /// It is not an error to stop a router that's not running.
        /// </remarks>
        public new void Stop()
        {
            if (!isRunning)
                return;

            Trace(0, "Router Stop", this.RouterEP != null ? this.RouterEP.ToString() : string.Empty, string.Empty);

            // Multicast a RouterStopMsg and then wait a moment
            // to give the message a chance to be transmitted before
            // stopping the router.

            using (TimedLock.Lock(this.SyncRoot))
            {
                Multicast(new RouterStopMsg(this.RouterEP));
                this.isRunning = false;
            }

            Thread.Sleep(250);  // Wait a moment to allow the multicast to proceed
            // before closing the router's sockets
            base.Stop();

            using (TimedLock.Lock(this.SyncRoot))
            {
                base.PhysicalRoutes = null;
                base.LogicalRoutes  = null;
            }
        }

        /// <summary>
        /// Handles all background tasks.
        /// </summary>
        protected override void OnBkTimer()
        {
            base.OnBkTimer();

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (SysTime.Now >= lastAdvertise + advertiseTime && RouterEP != null)
                {
                    Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                     base.UdpEP.Port, base.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, this.EnableP2P, false));
                    lastAdvertise = SysTime.Now;
                }
            }
        }

        /// <summary>
        /// Called by the message dispatcher if the set of logical endpoints
        /// implemented by the router changes.
        /// </summary>
        /// <param name="logicalEndpointSetID">The new logical endpoint set ID.</param>
        /// <remarks>
        /// This implementation multicasts a RouterAdvertiseMsg with the
        /// new logical endpoint set ID.
        /// </remarks>
        public override void OnLogicalEndpointSetChange(Guid logicalEndpointSetID)
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning)
                    return;

                Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                 this.UdpEP.Port, this.TcpEP.Port, logicalEndpointSetID, false, false));
            }
        }

        /// <summary>
        /// Handles LeafSettingMsgs received from the local hub router.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(LeafSettingsMsg msg)
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                if (advertiseTime != msg.AdvertiseTime)
                {
                    advertiseTime = msg.AdvertiseTime;
                    lastAdvertise = SysTime.Now;
                }

                if (msg.HubIPAddress != null && msg.HubTcpPort != 0)
                {
                    if (hubIPAddress != msg.HubIPAddress || hubTcpPort != msg.HubTcpPort)
                        hubChannelEP = new ChannelEP(Transport.Tcp, new IPEndPoint(msg.HubIPAddress, msg.HubTcpPort));
                }
                else
                    hubChannelEP = null;

                hubEP = msg.HubEP;
                hubIPAddress = msg.HubIPAddress;
                hubUdpPort = msg.HubUdpPort;
                hubTcpPort = msg.HubTcpPort;

                if (msg.DiscoverLogical)
                    SendLogicalAdvertiseMsgs(msg.HubEP);
            }
        }

        /// <summary>
        /// Generates LogicalAdvertiseMsgs for the logical endpoints handled
        /// by this router and sends them to the router endpoint passed.
        /// </summary>
        /// <param name="routerEP">The endpoint.</param>
        private void SendLogicalAdvertiseMsgs(MsgEP routerEP)
        {
            LogicalAdvertiseMsg[] adMsgs;

            using (TimedLock.Lock(this.SyncRoot))
            {
                adMsgs = GenLogicalAdvertiseMsgs(null, true);
                if (adMsgs.Length == 0)
                    return;     // There is nothing to advertise

                for (int i = 0; i < adMsgs.Length; i++)
                    SendTo(routerEP, adMsgs[i]);
            }
        }

        /// <summary>
        /// Handles received LogicalAdvertiseMsgs by adding the logical
        /// endpoints to the routing table.
        /// </summary>
        /// <param name="msg">The received message.</param>
        [MsgHandler]
        public void OnMsg(LogicalAdvertiseMsg msg)
        {
            PhysicalRoute   physRoute;
            MsgEP[]         logicalEPs;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!base.EnableP2P)
                    return;     // Don't maintain physical routes if the
                                // leaf router is not enabled for P2P.

                physRoute = this.PhysicalRoutes[msg.RouterEP];
                if (physRoute == null)
                {
                    // No physical route exists yet so add one

                    AddPhysicalRoute(msg.RouterEP, msg.AppName, msg.AppDescription, msg.RouterInfo, msg.LogicalEndpointSetID,
                                     new IPEndPoint(msg.IPAddress, msg.UdpPort), new IPEndPoint(msg.IPAddress, msg.TcpPort));
                    physRoute = this.PhysicalRoutes[msg.RouterEP];
                }

                logicalEPs = msg.LogicalEPs;
                for (int i = 0; i < logicalEPs.Length; i++)
                    base.LogicalRoutes.Add(new LogicalRoute(logicalEPs[i], physRoute));
            }
        }

        /// <summary>
        /// Unit test only property that returns <c>true</c> if a duplicate leaf endpoint
        /// is detected in a received RouterAdvertiseMsg.
        /// </summary>
        internal bool DuplicateLeafDetected 
        {
            get {return dupLeafDetected;}
        }

        /// <summary>
        /// If the router is enabled for peer-to-peer routing then we'll want to
        /// collect any information about routes to peer routers.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(RouterAdvertiseMsg msg)
        {
            PhysicalRoute   physRoute;
            bool            discoverLogical;

            if (this.RouterEP == null)
                return;     // Router is not fully initialized

            if (this.RouterEP.IsPhysicalMatch(msg.RouterEP))
            {
                // I've noticed that on multi-homed machines, we can see source IP addresses for 
                // multicast messages from ourself that differ from what we think our IP address
                // is.  I'm going to check the source IP address against all of the local IP
                // addresses to avoid issuing invalid warnings.

                var isLocalIPAddress = NetHelper.IsLocalAddress(msg.IPAddress);

                if (msg.TcpPort != this.TcpEP.Port || !isLocalIPAddress)
                {
                    this.dupLeafDetected = true;
                    SysLog.LogWarning("Duplicate router [{0}] appears to be advertising on TCP[{1}:{2}].  Local endpoint is TCP[{3}:{4}].",
                                      msg.RouterEP.ToString(-1, false),
                                      msg.IPAddress, msg.TcpPort,
                                      this.TcpEP.Address, this.TcpEP.Port);
                }

                if (msg.UdpPort != this.UdpEP.Port || !isLocalIPAddress)
                {
                    this.dupLeafDetected = true;
                    SysLog.LogWarning("Duplicate router [{0}] appears to be advertising on UDP[{1}:{2}].  Local endpoint is UDP[{3}:{4}].",
                                      msg.RouterEP.ToString(-1, false),
                                      msg.IPAddress, msg.UdpPort,
                                      this.UdpEP.Address, this.UdpEP.Port);
                }

                return;     // Don't add routes to self to the routing table
            }

            // If the source router is this router's hub then send it a RouterAdvertiseMsg
            // so the hub can continue the route discovery process.

            if (msg.ReplyAdvertise && this.RouterEP.GetPhysicalParent().Equals(msg.RouterEP))
            {
                // Set up a temporary route to the hub so the RouterAdvertiseMsg can be delivered.
                // These values will be finalized when the LeafSettingsMsg is received.

                this.hubEP        = msg.RouterEP;
                this.hubChannelEP = new ChannelEP(Transport.Tcp, new IPEndPoint(msg.IPAddress, msg.TcpPort));

                // Send the RouterAdvertiseMsg to the hub.

                SendTo(msg.RouterEP, new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                           this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, false, false));
                return;
            }

            // Ignore the message if either this router or the advertised router is not
            // peer-to-peer enabled.  In these cases, messages will be forwarded to
            // the hub for delivery.

            if (!base.EnableP2P || !msg.RouterInfo.IsP2P)
                return;

            // Add/update the physical route to the advertised router.

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning)
                    return;

                // Handle route table management for peer routers.

                if (!this.RouterEP.IsPhysicalPeer(msg.RouterEP))
                {
                    const string format =
@"RouterEP: {0}
Route:    {1}";
                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Ignore route", this.GetType().Name + ": " + msg.RouterEP.ToString(), string.Format(null, format, this.RouterEP.ToString(), msg.RouterEP.ToString()));
                    return;
                }

                // If this is the first time we've seen this router or if the router's set
                // of handled logical endpoints has changed then we'll need to set the 
                // DiscoverLogical=true on the RouterAdvertiseMsg so the other will send
                // us its logical endpoints.

                physRoute       = base.PhysicalRoutes[msg.RouterEP];
                discoverLogical = physRoute == null || physRoute.LogicalEndpointSetID != msg.LogicalEndpointSetID;

                // Send a RouterAdvertiseMsg for this router back to the source router if that
                // router is P2P enabled and a reply is requested.  This will give that router 
                // a chance to learn about this router.

                if (msg.RouterInfo.IsP2P && (msg.ReplyAdvertise || discoverLogical))
                    SendTo(msg.RouterEP, new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                               this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, false, discoverLogical));

                AddPhysicalRoute(msg.RouterEP, msg.AppName, msg.AppDescription, msg.RouterInfo, msg.LogicalEndpointSetID,
                                 new IPEndPoint(msg.IPAddress, msg.UdpPort), new IPEndPoint(msg.IPAddress, msg.TcpPort));

                // Send LogicalAdvertiseMsgs back to the sender if requested.

                if (msg.DiscoverLogical)
                    SendLogicalAdvertiseMsgs(msg.RouterEP);
            }
        }

        /// <summary>
        /// Stub to prevent this message from bleeding into an application default
        /// message handler.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(HubKeepAliveMsg msg)
        {
        }

        /// <summary>
        /// Handles RouterStopMsgs received from the local routers.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(RouterStopMsg msg)
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning)
                    return;

                base.PhysicalRoutes.Remove(msg.RouterEP);
                base.LogicalRoutes.Flush();
            }
        }

        /// <summary>
        /// Called when the router detects that it's been assigned a new network address.
        /// </summary>
        protected override void OnNewEP()
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                 base.UdpEP.Port, base.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, false, false));
                lastAdvertise = SysTime.Now;
            }
        }

        /// <summary>
        /// Handles the physical routing of the message passed.
        /// </summary>
        /// <param name="physicalEP">The physical target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <para>
        /// If peer-to-peer routing is disabled (EnableP2P=false), then all messages
        /// will be routed to the parent hub router if one is present.
        /// </para>
        /// <para>
        /// If peer-to-peer routing is enabled then messages with physical endpoints 
        /// below this router's hub in the hierarchy will be routed directly to the 
        /// target router if a physical route to the target is known.  All other
        /// messages will be routed to the parent hub if one is present.
        /// </para>
        /// </remarks>
        protected override void RoutePhysical(MsgEP physicalEP, Msg msg)
        {
            Assertion.Test(physicalEP.IsPhysical, "Physical endpoint expected");
            Assertion.Test(msg._ToEP.ChannelEP == null, "ChannelEP must be null for routed messages.");

            if (msg._TTL == 0)
            {
                OnDiscardMsg(msg, "TTL=0");
                return;
            }

            msg._TTL--;

            using (TimedLock.Lock(this.SyncRoot))
            {
                // Extended behavior

                if (base.EnableP2P && this.RouterEP.IsPhysicalPeer(physicalEP))
                {
                    // P2P is enabled and the message destination is under the same hub
                    // as this router, so route the message directly to the target router
                    // if a physical route exists to the router.  Otherwise, drop through
                    // and route the message to the hub.

                    PhysicalRoute route;

                    route = base.PhysicalRoutes[physicalEP];
                    if (route != null)
                    {
                        TransmitTcp(route, msg);
                        return;
                    }
                }

                if (hubChannelEP == null)
                {
                    OnDiscardMsg(msg, "No route or hub");
                    return;
                }

                Transmit(hubChannelEP, msg);
            }
        }

        /// <summary>
        /// Handles the logical routing of the message passed.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <para>
        /// This method has to deal with two basic cases, broadcast
        /// and non-broadcast messages.
        /// </para>
        /// <para>
        /// For non-broadcast messages, the method first attempts to
        /// dispatch the message to a local application handler.  If this
        /// succeeds we're done.  If this fails and peer-to-peer routing
        /// is enabled, then the router looks at its logical routing table
        /// for a matching route and sends the message there, randomly selecting
        /// one if there are multiple matches.  If no matching route is found or 
        /// if peer-to-peer routing is disabled, then the message is forwarded
        /// up to the hub router (if one is available).
        /// </para>
        /// <para><b><u>Broadcasting</u></b></para>
        /// <para>
        /// Broadcast messages are handled a bit differently.  First, the
        /// message will be dispatched to every matching application handler
        /// known to this router.  What happens next depends on whether the
        /// router is enabled for peer-to-peer and whether the message originated
        /// at this router.
        /// </para>
        /// <para>
        /// If the message did not originate at this router, then we're done.
        /// </para>
        /// <para>
        /// Otherwise the message will be forwarded to the hub router (if present)
        /// as well as to any peer routers advertising matching endpoints if
        /// peer-to-peer routing is enabled.
        /// </para>
        /// </remarks>
        protected override void RouteLogical(Msg msg)
        {
            Assertion.Test(msg._ToEP.IsLogical);

            if (msg._TTL == 0)
            {
                OnDiscardMsg(msg, "TTL=0");
                return;
            }

            msg._TTL--;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if ((msg._Flags & MsgFlag.Broadcast) == 0)
                {
                    // Not a broadcast message

                    if (this.Dispatcher.Dispatch(msg))
                    {
                        SendMsgReceipt(msg);
                        return;
                    }

                    if (base.EnableP2P)
                    {
                        List<LogicalRoute>  routes;
                        LogicalRoute        route;

                        if ((msg._Flags & MsgFlag.ClosestRoute) != 0)
                            routes = base.LogicalRoutes.GetClosestRoutes(msg._ToEP);
                        else
                            routes = base.LogicalRoutes.GetRoutes(msg._ToEP);

                        if (routes.Count > 0)
                        {

                            if (routes.Count == 1)
                                route = routes[0];
                            else
                                route = routes[Helper.RandIndex(routes.Count)];

                            TransmitTcp(route.PhysicalRoute, msg);
                            return;
                        }
                    }

                    // P2P is disabled or we didn't find a matching logical route.

                    if (hubChannelEP == null)
                    {
                        OnDiscardMsg(msg, "No route or hub");
                        return;
                    }

                    Transmit(hubChannelEP, msg);
                }
                else
                {
                    // This is a broadcast message.  First, dispatch to any local 
                    // application handlers.

                    this.Dispatcher.Dispatch(msg);

                    if (msg._ReceiveChannel != null)
                        return;     // If the message didn't originate here then we're done

                    // If this router is P2P enabled then forward the message
                    // to any peer routers advertising this endpoint.

                    if (base.EnableP2P)
                    {
                        List<LogicalRoute> routes;

                        if ((msg._Flags & MsgFlag.ClosestRoute) != 0)
                            routes = base.LogicalRoutes.GetClosestRoutes(msg._ToEP);
                        else
                            routes = base.LogicalRoutes.GetRoutes(msg._ToEP);

                        for (int i = 0; i < routes.Count; i++)
                            TransmitTcp(routes[i].PhysicalRoute, msg);
                    }

                    // Forward to the hub is one is present.

                    if (hubChannelEP != null)
                        Transmit(hubChannelEP, msg);
                }
            }
        }

        /// <summary>
        /// Used by unit tests to modify the time interval for
        /// RouterAdvertiseMsg broadcasts.
        /// </summary>
        internal TimeSpan AdvertiseTime
        {
            get {return advertiseTime;}
            set {advertiseTime = value;}
        }
    }
}
