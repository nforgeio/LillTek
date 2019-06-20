//-----------------------------------------------------------------------------
// FILE:        HubRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a LeafRouter that routes messages between itself,
//              router above and below it in the hierarchy.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;

// $todo(jeff.lill): 
//
// I need to add functionality that allows a hub router
// to advertise routes on behalf of a leaf router.  Without
// this, we're going to have a scalability problem with
// large number of service instances when one or more
// of them expose transient endpoints.  In the current
// implementation, every time a transient endpoint is
// exposed, all servers in the data center will query
// that server for its routing table, resulting in high
// load on that server and also with a lot of socket
// connections with the buffer overhead, etc.
//
// Here's an alternative way of doing this:
//
//  1. If there's no hub router present, the current protocol is used.
//
//  2. If there is a hub router, the leaf router sends its new route
//     to the hub and requests that it advertise it.
//
//  3. The hub replicates the new route to its peers (if any) and
//     then broadcasts the advertise message.  Leaf routers on the
//     subnet then contact the hubs to obtain the new route, probably
//     using already existing socket connections.
//
//  4. As a further optimization, we could avoid having the hub
//     broadcast the advertisement to all servers, and instead 
//     have hub routers perform the routing for the first few
//     messages from leaf routers.  Then, if it looks like there's
//     going to be a lot of traffic, the hub could forward the
//     route to the leaf router so it could route messages directly
//     to the destination.  This is basically a route referral,
//     which would be done only for P2P enabled routers.

// $todo(jeff.lill): 
//
// Come back and implement the ability of multiple hub
// routers to coexist on the same subnet to provide
// scalability and failover.

// $todo(jeff.lill): 
//
// The logical uplink/downlink implementation is a bit of
// a hack.  It should work reasonably well for now, but
// I'd like to come back and completely redo it sometime.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a HubRouter that routes messages between itself and routers
    /// above and below it in the hierarchy.
    /// </summary>
    public class HubRouter : MsgRouter
    {
        private bool                isRoot;             // True if this is a RootRouter.
        private bool                isRunning;          // True if the router is running
        private string              parentHost;         // Parent router's host name (or dotted quad)
        private int                 parentPort;         // Parent router's port
        private bool                isDetached;         // True if not connected to a root rooter
        private MsgEP               parentEP;           // Parent endpoint (or null if this is the root)
        private TimeSpan            advertiseTime;      // RouterAdvertiseMsg multicast interval
        private DateTime            lastAdvertise;      // Time (SYS) of the last RouterAdvertiseMsg multicast
        private TimeSpan            hubKeepAliveTime;   // Interval between KeepAliveMsgs on the uplink channel
        private DateTime            lastKeepAlive;      // Last time an uplink KeepAliveMsg was sent (SYS)
        private TcpChannel          uplinkChannel;      // TCP uplink channel to the root
        private MsgEP[]             uplinkEPs;          // Root router endpoints to be advertised to child hubs
        private MsgEP[]             downlinkEPs;        // Hub router endpoints to be advertised to the root.
        private LogicalRouteTable   uplinkRoutes;       // Set up uplink routes received from the root by a hub

        /// <summary>
        /// This constructor initializes the router.
        /// </summary>
        public HubRouter()
            : base()
        {
            this.isRoot        = false;
            this.isRunning     = false;
            this.isDetached    = false;
            this.parentHost    = null;
            this.parentPort    = 0;
            this.parentEP      = null;
            this.uplinkChannel = null;
            this.uplinkEPs     = new MsgEP[0];
            this.downlinkEPs   = new MsgEP[0];
            this.uplinkRoutes  = null;
        }

        /// <summary>
        /// Returns the parent router's endpoint or <c>null</c> if this
        /// is a root router or the hub router is detached.
        /// </summary>
        public MsgEP ParentEP
        {
            get { return parentEP; }
        }

        /// <summary>
        /// Returns <c>true</c> if the router is not attached to a parent root router.
        /// </summary>
        public bool IsDetached
        {
            get { return isDetached; }
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
        /// So, when loading the TcpEP setting, Start("Foo") will query for "Router.Foo.TcpEP".
        /// </para>
        /// <para>
        /// Here are the configuration settings HubRouter expects: (note
        /// that all settings are prefixed by "MsgRouter." as in MsgRouter.RouterEP".
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
        ///     <td>(required)</td>
        ///     <td>
        ///         Physical MsgEP for this instance.  The endpoint should be two levels deep
        ///         such as physical://root.com:40/hub and the hub name should be unique across
        ///         all hub routers.  One way to guarantee uniquness is to use the $(Guid)
        ///         environment variable in the endpoint, as in physical://root.com:40/%(Guid).
        ///         This will be replaced with a newly generated GUID when the configuration variable
        ///         is processed.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>ParentEP</td>
        ///     <td>(none)</td>
        ///     <td>
        ///         Host/IP and port of the root router's 
        ///         listening TCP socket (a static route),
        ///         formatted as IP:port or host:port or the
        ///         string "DETACHED" if the hub router is
        ///         not to be connected to a root.
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
        /// <tr valign="top"><td>CloudAdapter</td><td>ANY</td><td>The discover UDP multicast network adapter address</td></tr>
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
        /// <tr valign="top"><td>AdvertiseTime</td><td>1m</td><td>RouterAdvertiseMsg multicast interval</td></tr>
        /// <tr valign="top"><td>HubKeepAliveTime</td><td>1m</td><td>Interval at which the hub router will send <see cref="HubKeepAlive" /> messages to the root router on the uplink.</td></tr>
        /// <tr valign="top"><td>PhysicalRouteTTL</td><td>3m</td><td>Maximum time a physical route will be maintained without being refreshed with a RouterAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DefMsgTTL</td><td>5</td><td>Default message time-to-live (max hops)</td></tr>
        /// <tr valign="top"><td>SharedKey</td><td>PLAINTEXT</td><td>The shared encryption key used to encrypt all message traffic.</td></tr>
        /// <tr valign="top"><td>SessionCacheTime</td><td>2m</td><td>Default time the router's session manager will cache idempotent replies.</td></tr>
        /// <tr valign="top"><td>SessionRetries</td><td>3</td><td>Maximum session initiation retries.</td></tr>
        /// <tr valign="top"><td>SessionTimeout</td><td>10s</td><td>Default session timeout</td></tr>
        /// <tr valign="top"><td>MaxLogicalAdvertiseEPs</td><td>256</td><td>Maximum number of logical endpoints to be included in a single LogicalAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DeadRouterTTL</td><td>0s</td><td>Maximum time to wait for a <see cref="ReceiptMsg" /> before declaring a dead router.  Use 0 to disable dead router detection.</td></tr>
        /// <tr valign="top"><td>DownlinkEP[#]</td><td>(none)</td><td>Zero or more static logical endpoints to be advertised to the root router (encoded as a zero-based array).</td></tr>
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
            Config          config;
            MsgEP           routerEP;
            RouterSettings  settings;
            string[]        eps;
            string          v;

            // Load the configuration settings

            config = new Config(MsgHelper.ConfigPrefix);

            v = config.Get("RouterEP");
            if (v == null)
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is required.");

            try
            {
                routerEP = MsgEP.Parse(v);
            }
            catch
            {
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is invalid.");
            }

            if (routerEP.Segments.Length < 1)
                throw new MsgException("[MsgRouter.RouterEP] must specify a valid one segment hub endpoint (physical://root/hub).");

            routerEP = MsgEP.CopyMaxSegments(routerEP, 1);

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
            settings.HubKeepAliveTime           = config.Get("HubKeepAliveTime", settings.HubKeepAliveTime);

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

            // Load the downlink logical endpoints

            eps                 = config.GetArray("DownlinkEP");
            settings.DownlinkEP = new MsgEP[eps.Length];

            for (int i = 0; i < eps.Length; i++)
            {
                try
                {
                    MsgEP ep = eps[i];

                    if (!ep.IsLogical)
                        throw new MsgException(string.Empty);

                    settings.DownlinkEP[i] = ep;
                }
                catch
                {
                    throw new MsgException("Configuration setting [DownlinkEP[{0}]={1}] is not a logical endpoint.", i, eps[i]);
                }
            }

            Start(settings);
        }

        /// <summary>
        /// Starts the router using the configuration settings passed.
        /// </summary>
        /// <param name="settings">The configuration settings.</param>
        public void Start(RouterSettings settings)
        {
            string          parentNetEP;
            int             pos;
            IPAddress       cloudAdapter;
            IPEndPoint      cloudEP;
            IPEndPoint      udpEP;
            IPEndPoint      tcpEP;
            int             tcpBacklog;
            TimeSpan        maxIdle;

            if (settings.RouterEP.Segments.Length < 1)
                throw new MsgException("RouterEP must specify a valid one segment hub endpoint (physical://root/hub).");

            this.RouterEP = MsgEP.CopyMaxSegments(settings.RouterEP, 1);

            Trace(0, "Router Start: " + this.AppName, this.RouterEP.ToString(), string.Empty);

            parentNetEP  = settings.ParentEP;
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
            this.hubKeepAliveTime       = settings.HubKeepAliveTime;

            // Load the downlink logical endpoints

            this.downlinkEPs = new MsgEP[settings.DownlinkEP.Length];

            for (int i = 0; i < settings.DownlinkEP.Length; i++)
                this.downlinkEPs[i] = settings.DownlinkEP[i].Clone();

            // Determine the parent router's host and port.  If parentNetEP is valid then
            // use these values or if the parent router is the root, then use its host
            // and port.  If neither of these conditions are true then we'll treat this
            // as a root.

            MsgEP routerEP = this.RouterEP;

            parentEP = routerEP.GetPhysicalParent();
            if (routerEP.Segments.Length > 0)
            {   
                // This router is not the root
                parentHost = null;
                parentPort = -1;

                if (String.Compare(parentNetEP, "DETACHED", true) == 0)
                {
                    isDetached = true;
                    parentEP   = null;
                }
                else
                {
                    pos = -1;
                    if (parentNetEP != null)
                        pos = parentNetEP.IndexOf(':');

                    if (pos != -1)
                    {
                        parentHost = parentNetEP.Substring(0, pos);

                        try
                        {
                            parentPort = int.Parse(parentNetEP.Substring(pos + 1));
                        }
                        catch
                        {
                        }
                    }

                    if (parentHost == null || parentPort == -1)
                    {
                        // The parentEP wasn't present or is invalid.

                        if (parentNetEP != null && parentNetEP != string.Empty)
                            throw new MsgException("Invalid ParentEP setting.");

                        if (routerEP.IsDetachedRoot)
                        {
                            isDetached = true;
                            parentEP = null;
                        }
                        else
                        {
                            parentHost = routerEP.RootHost;
                            parentPort = routerEP.RootPort;
                        }
                    }
                }
            }

            // Crank this sucker up

            using (TimedLock.Lock(this.SyncRoot))
            {
                base.PhysicalRoutes = new PhysicalRouteTable(this, base.PhysRouteTTL);
                base.LogicalRoutes  = new LogicalRouteTable(this);
                this.uplinkRoutes   = new LogicalRouteTable(this);

                base.SetDispatcher(new MsgDispatcher(this));
                base.Dispatcher.AddTarget(this);
                base.EnableEncryption(settings.SharedKey);
                base.Start(cloudAdapter, cloudEP, udpEP, tcpEP, tcpBacklog, maxIdle);

                this.lastKeepAlive = DateTime.MinValue;     // Schedule an immediate keepalive to
                                                            // establish the uplink channel

                lastAdvertise   = SysTime.Now + advertiseTime;
                this.isRunning = true;

                if (cloudEP == null)
                    SysLog.LogWarning("Hub router started with no CloudEP");
                else
                    Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                     this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, true, true));
            }

            // Pause a second to give the leaf routers a chance to register themselves.

            Thread.Sleep(1000);
        }

        /// <summary>
        /// Starts the router in "root" mode, reading configuration parameters from the 
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
        /// So, when loading the TcpEP setting, Start("Foo") will query for "Router.Foo.TcpEP".
        /// </para>
        /// <para>
        /// Here are the configuration settings LeafRouter expects: (note
        /// that all settings are prefixed by "MsgRouter." as in MsgRouter.RouterEP".
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
        ///     <td>(required)</td>
        ///     <td>
        ///         Physical MsgEP for this instance.  The endpoint should be two levels deep
        ///         such as physical://root.com:40/hub and the hub name should be unique across
        ///         all hub routers.  One way to guarantee uniquness is to use the $(Guid)
        ///         environment variable in the endpoint, as in physical://root.com:40/%(Guid).
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
        /// <tr valign="top"><td>UdpSendBufferSize</td><td>64K</td><td>UDP unicast socket send buffer size</td></tr>
        /// <tr valign="top"><td>UdpReceiveBufferSize</td><td>64K</td><td>UDP unicast socket receive buffer size</td></tr>
        /// <tr valign="top"><td>TcpEP</td><td>ANY:0</td><td>TCP listening network endpoint</td></tr>
        /// <tr valign="top"><td>TcpBacklog</td><td>100</td><td>Max pending connecting TCP sockets</td></tr>
        /// <tr valign="top"><td>TcpDelay</td><td>off</td><td>Enables Nagle on TCP channels</td></tr>
        /// <tr valign="top"><td>TcpSendBufferSize</td><td>64K</td><td>TCP socket send buffer size</td></tr>
        /// <tr valign="top"><td>TcpReceiveBufferSize</td><td>64K</td><td>TCP socket receive buffer size</td></tr>
        /// <tr valign="top"><td>BkInterval</td><td>1s</td><td>Background task interval</td></tr>
        /// <tr valign="top"><td>MaxIdle</td><td>5m</td><td>Maximum time a TCP socket should idle before being closed automatically</td></tr>
        /// <tr valign="top"><td>AdvertiseTime</td><td>1m</td><td>RouterAdvertiseMsg multicast interval</td></tr>
        /// <tr valign="top"><td>KeepAliveTime</td><td>1m</td><td>Root uplink keep-alive time</td></tr>
        /// <tr valign="top"><td>PhysicalRouteTTL</td><td>3m</td><td>Maximum time a physical route will be maintained without being refreshed with a RouterAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DefMsgTTL</td><td>5</td><td>Default message time-to-live (max hops)</td></tr>
        /// <tr valign="top"><td>SharedKey</td><td>PLAINTEXT</td><td>The shared encryption key used to encrypt all message traffic.</td></tr>
        /// <tr valign="top"><td>SessionCacheTime</td><td>2m</td><td>Default time the router's session manager will cache idempotent replies.</td></tr>
        /// <tr valign="top"><td>SessionRetries</td><td>3</td><td>Maximum session initiation retries.</td></tr>
        /// <tr valign="top"><td>SessionTimeout</td><td>10s</td><td>Default session timeout</td></tr>
        /// <tr valign="top"><td>MaxLogicalAdvertiseEPs</td><td>256</td><td>Maximum number of logical endpoints to be included in a single LogicalAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DeadRouterTTL</td><td>0s</td><td>Maximum time to wait for a <see cref="ReceiptMsg" /> before declaring a dead router.  Use 0 to disable dead router detection.</td></tr>
        /// <tr valign="top"><td>UplinkEP[#]</td><td>(none)</td><td>Zero or more static logical endpoints to be advertised to the hub routers (encoded as a zero-based array).</td></tr>
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
        protected void StartAsRoot()
        {
            Config          config;
            MsgEP           routerEP;
            RouterSettings  settings;
            string[]        eps;
            string          v;

            // Load the configuration settings

            config = new Config(MsgHelper.ConfigPrefix);

            v = config.Get("RouterEP");
            if (v == null)
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is required.");

            try
            {
                routerEP = MsgEP.Parse(v);
            }
            catch
            {
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is invalid.");
            }

            routerEP = MsgEP.CopyMaxSegments(routerEP, 0);

            settings                            = new RouterSettings(routerEP);
            settings.AppName                    = config.Get("AppName", settings.AppName);
            settings.AppDescription             = config.Get("AppDescription", settings.AppDescription);
            settings.DiscoveryMode              = config.Get<DiscoveryMode>("DiscoveryMode", settings.DiscoveryMode);
            settings.CloudAdapter               = config.Get("CloudAdapter", settings.CloudAdapter);
            settings.CloudEP                    = config.Get("CloudEP", settings.CloudEP);
            settings.BroadcastSettings          = new UdpBroadcastClientSettings(Config.CombineKeys(MsgHelper.ConfigPrefix, "BroadcastSettings"));
            settings.CloudAdapter               = config.Get("CloudAdapter", settings.CloudAdapter);
            settings.CloudEP                    = config.Get("CloudEP", settings.CloudEP);
            settings.UdpEP                      = config.Get("UdpEP", settings.UdpEP);
            settings.TcpEP                      = config.Get("TcpEP", settings.TcpEP);
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
            settings.MulticastSendBufferSize    = config.Get("MulticastSendBufferSize", MsgRouter.DefSockBufSize);
            settings.MulticastReceiveBufferSize = config.Get("MulticastReceiveBufferSize", MsgRouter.DefSockBufSize);
            settings.UdpSendBufferSize          = config.Get("UdpSendBufferSize", MsgRouter.DefSockBufSize);
            settings.UdpReceiveBufferSize       = config.Get("UdpReceiveBufferSize", MsgRouter.DefSockBufSize);
            settings.TcpSendBufferSize          = config.Get("TcpSendBufferSize", MsgRouter.DefSockBufSize);
            settings.TcpReceiveBufferSize       = config.Get("TcpReceiveBufferSize", MsgRouter.DefSockBufSize);
            settings.AdvertiseTime              = config.Get("AdvertiseTime", settings.AdvertiseTime);
            settings.HubKeepAliveTime           = config.Get("HubKeepAliveTime", settings.HubKeepAliveTime);

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

            // Load the uplink logical endpoints

            eps               = config.GetArray("UplinkEP");
            settings.UplinkEP = new MsgEP[eps.Length];

            for (int i = 0; i < eps.Length; i++)
            {
                try
                {
                    MsgEP ep = eps[i];

                    if (!ep.IsLogical)
                        throw new MsgException(string.Empty);

                    settings.UplinkEP[i] = ep;
                }
                catch
                {
                    throw new MsgException("Configuration setting [UplinkEP[{0}]={1}] is not a logical endpoint.", i, eps[i]);
                }
            }

            // Get the local route endpoints

            foreach (string ep in config.GetArray("RouteLocal"))
                settings.LocalityMap.Add(ep);

            StartAsRoot(settings);
        }


        /// <summary>
        /// Starts the router in "root" mode, using the configuration settings
        /// passed.
        /// </summary>
        /// <param name="settings">The configuration settings.</param>
        protected void StartAsRoot(RouterSettings settings)
        {
            Config          config;
            string          v;
            IPAddress       cloudAdapter;
            IPEndPoint      cloudEP;
            IPEndPoint      udpEP;
            IPEndPoint      tcpEP;
            int             tcpBacklog;
            TimeSpan        maxIdle;

            // Load the configuration settings

            config = new Config(MsgHelper.ConfigPrefix);

            v = config.Get("RouterEP");
            if (v == null)
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is required.");

            try
            {
                this.RouterEP = MsgEP.Parse(v);
            }
            catch
            {
                throw new MsgException("[MsgRouter.RouterEP] configuration setting is invalid.");
            }

            if (this.RouterEP.Segments.Length > 0)
                this.RouterEP = MsgEP.CopyMaxSegments(this.RouterEP, 0);

            Trace(0, "Router Start: " + this.AppName, this.RouterEP.ToString(), string.Empty);

            cloudAdapter = settings.CloudAdapter;
            cloudEP      = settings.CloudEP;
            udpEP        = settings.UdpEP;
            tcpEP        = settings.TcpEP;
            tcpEP.Port   = RouterEP.RootPort;
            tcpBacklog   = settings.TcpBacklog;
            maxIdle       = settings.MaxIdle;

            base.AppName                = settings.AppName;
            base.AppDescription         = settings.AppDescription;
            base.DiscoveryMode          = settings.DiscoveryMode;
            base.BroadcastSettings      = settings.BroadcastSettings;
            base.EnableP2P              = settings.EnableP2P;
            base.SessionCacheTime       = settings.SessionCacheTime;
            base.SessionRetries         = settings.SessionRetries;
            base.SessionTimeout         = settings.SessionTimeout;
            base.TcpDelay               = settings.TcpDelay;
            base.BkInterval             = settings.BkInterval;
            base.DefMsgTTL              = settings.DefMsgTTL;
            base.PhysRouteTTL           = settings.PhysicalRouteTTL;
            base.MaxLogicalAdvertiseEPs = settings.MaxLogicalAdvertiseEPs;
            base.DeadRouterTTL          = settings.DeadRouterTTL;
            base.UdpMulticastSockConfig = new SocketConfig(settings.MulticastSendBufferSize, settings.MulticastReceiveBufferSize);
            base.UdpUnicastSockConfig   = new SocketConfig(settings.UdpSendBufferSize, settings.MulticastReceiveBufferSize);
            base.TcpSockConfig          = new SocketConfig(settings.TcpSendBufferSize, settings.TcpReceiveBufferSize);

            this.advertiseTime          = settings.AdvertiseTime;
            this.hubKeepAliveTime =      settings.HubKeepAliveTime;

            this.isRoot     = true;
            this.parentHost = null;
            this.parentPort = -1;
            this.parentEP   = null;

            // Load the uplink logical endpoints

            this.uplinkEPs = new MsgEP[settings.UplinkEP.Length];
            for (int i = 0; i < settings.UplinkEP.Length; i++)
                this.uplinkEPs[i] = settings.UplinkEP[i].Clone();

            // Get the local route endpoints

            foreach (string ep in config.GetArray("RouteLocal"))
                settings.LocalityMap.Add(ep);

            // Crank this sucker up

            using (TimedLock.Lock(this.SyncRoot))
            {
                base.PhysicalRoutes = new PhysicalRouteTable(this, base.PhysRouteTTL);
                base.LogicalRoutes  = new LogicalRouteTable(this);

                base.SetDispatcher(new MsgDispatcher(this));
                base.Dispatcher.AddTarget(this);
                base.EnableEncryption(settings.SharedKey);

                base.Start(cloudAdapter, cloudEP, udpEP, tcpEP, tcpBacklog, maxIdle);

                this.lastKeepAlive = DateTime.MinValue;     // Schedule an immediate keepalive to
                                                            // establish the uplink channel

                lastAdvertise  = SysTime.Now + advertiseTime;
                this.isRunning = true;

                if (cloudEP != null)
                    Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                     this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, true, true));
            }

            // Pause a second to give the leaf routers a chance to register themselves.

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
                if (base.MulticastEnabled)
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
                this.uplinkRoutes   = null;
            }
        }

        /// <summary>
        /// Returns the uplink logical route table.
        /// </summary>
        public LogicalRouteTable UplinkRoutes
        {
            get { return uplinkRoutes; }
        }

        /// <summary>
        /// This method attempts to establish an uplink TCP connection to the
        /// parent router, if one is not already established and then initiates
        /// the transmission of the message passed to the parent.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void Uplink(Msg msg)
        {
            if (isDetached)     // Don't establish an uplink if the hub router is detacted
                return;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (parentEP == null)
                    return; // This is a root router

                if (uplinkChannel == null)
                {
                    uplinkChannel          = new TcpChannel(this);
                    uplinkChannel.IsUplink = true;
                    uplinkChannel.Connect(parentHost, parentPort, msg);
                }
                else
                    uplinkChannel.Transmit(new ChannelEP(Transport.Tcp, uplinkChannel.RemoteEP), msg);
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
                if (!isRunning)
                    return;

                if (parentEP != null && SysTime.Now >= lastKeepAlive + hubKeepAliveTime)
                {
                    Uplink(new HubKeepAliveMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo, this.Dispatcher.LogicalEndpointSetID));
                    lastKeepAlive = SysTime.Now;
                }

                if (SysTime.Now >= lastAdvertise + advertiseTime)
                {
                    if (this.CloudEP != null)
                        Multicast(new RouterAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                         base.UdpEP.Port, base.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID, true, false));

                    lastAdvertise = SysTime.Now;
                }
            }
        }

        /// <summary>
        /// Handles RouterAdvertiseMsgs received from the local routers.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(RouterAdvertiseMsg msg)
        {
            PhysicalRoute   physRoute;
            bool            discoverLogical;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning)
                    return;

                if (!this.RouterEP.IsPhysicalDescendant(msg.RouterEP))
                {
                    const string format =
@"RouterEP: {0}
Route:    {1}";
                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Ignore route", this.GetType().Name + ": " + msg.RouterEP.ToString(), string.Format(null, format, this.RouterEP.ToString(), msg.RouterEP.ToString()));
                    return;
                }

                physRoute       = base.PhysicalRoutes[msg.RouterEP];
                discoverLogical = physRoute == null || physRoute.LogicalEndpointSetID != msg.LogicalEndpointSetID;

                AddPhysicalRoute(msg.RouterEP, msg.AppName, msg.AppDescription, msg.RouterInfo, msg.LogicalEndpointSetID,
                                 new IPEndPoint(msg.IPAddress, msg.UdpPort), new IPEndPoint(msg.IPAddress, msg.TcpPort));

                // Send a LeafSettingsMsg back to the leaf router specifying the settings it should use

                SendTo(msg.RouterEP, new LeafSettingsMsg(this.RouterEP, this.UdpEP.Port, this.TcpEP.Port, advertiseTime, discoverLogical));
            }
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
            }
        }

        /// <summary>
        /// Generates the set of LogicalAdvertiseMsgs for the current router and the
        /// set of logical endpoints passed.
        /// </summary>
        /// <param name="logicalEPs">The logical endpoints.</param>
        /// <returns>An array of zero or more LogicalAdvertiseMsgs.</returns>
        internal LogicalAdvertiseMsg[] GenLogicalAdvertiseMsgs(MsgEP[] logicalEPs)
        {
            LogicalAdvertiseMsg[]   msgs;
            int                     cMsgs;
            int                     cRemain;
            int                     cEPs;
            int                     msgPos;
            int                     epPos;
#if DEBUG
            foreach (MsgEP ep in logicalEPs)
                Assertion.Test(ep.IsLogical);
#endif
            cMsgs = logicalEPs.Length / base.MaxLogicalAdvertiseEPs;
            if (logicalEPs.Length % base.MaxLogicalAdvertiseEPs > 0)
                cMsgs++;

            msgs    = new LogicalAdvertiseMsg[cMsgs];
            msgPos  = 0;
            epPos   = 0;
            cRemain = logicalEPs.Length;

            while (cRemain > 0)
            {
                MsgEP[] eps;

                cEPs = cRemain;
                if (cEPs > base.MaxLogicalAdvertiseEPs)
                    cEPs = base.MaxLogicalAdvertiseEPs;

                eps = new MsgEP[cEPs];
                for (int i = 0; i < cEPs; i++)
                    eps[i] = logicalEPs[epPos++];

                msgs[msgPos++] = new LogicalAdvertiseMsg(eps, this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo,
                                                         this.UdpEP.Port, this.TcpEP.Port, this.Dispatcher.LogicalEndpointSetID);
                cRemain -= cEPs;
            }

            return msgs;
        }

        /// <summary>
        /// Handles HubAdvertiseMsgs sent by hub routers to root routers.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(HubAdvertiseMsg msg)
        {
            LogicalAdvertiseMsg[] msgs;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning || !isRoot)
                    return;

                if (!this.RouterEP.IsPhysicalDescendant(msg.HubEP))
                {
                    const string format =
@"RouterEP: {0}
Route:    {1}";
                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Ignore route", this.GetType().Name + ": " + msg.HubEP.ToString(), string.Format(null, format, this.RouterEP.ToString(), msg.HubEP.ToString()));
                    return;
                }

                AddPhysicalRoute(msg.HubEP, msg.AppName, msg.AppDescription, msg.RouterInfo, msg.LogicalEndpointSetID, null,
                                 new IPEndPoint(msg.IPAddress, msg.TcpPort));

                // Send a HubSettingsMsg back to the hub router specifying the settings it should use

                SendTo(msg.HubEP, new HubSettingsMsg(this.Dispatcher.LogicalEndpointSetID, hubKeepAliveTime));

                // Send the uplink logical endpoints to the hub

                msgs = GenLogicalAdvertiseMsgs(uplinkEPs);
                for (int i = 0; i < msgs.Length; i++)
                    SendTo(msg.HubEP, msgs[i]);
            }
        }

        /// <summary>
        /// Handles the HubSettingsMsg received from the root router.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(HubSettingsMsg msg)
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning || isRoot)
                    return;

                this.hubKeepAliveTime = msg.KeepAliveTime;
                this.lastKeepAlive    = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Root routers will receive this periodically from hubs and then
        /// make sure that the routes are refreshed.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(HubKeepAliveMsg msg)
        {
            if (isRoot)
                AddPhysicalRoute(msg.ChildEP, msg.AppName, msg.AppDescription, msg.RouterInfo, msg.LogicalEndpointSetID, null,
                                 new IPEndPoint(msg.IPAddress, msg.TcpPort));
        }

        /// <summary>
        /// Stub to prevent this message from bleeding into an application default
        /// message handler.
        /// </summary>
        /// <param name="msg">The message.</param>
        [MsgHandler]
        public void OnMsg(LeafSettingsMsg msg)
        {
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
                if (!isRoot && msg.RouterEP.IsPhysicalRoot)
                {
                    // Add logical endpoints received from the root to
                    // the uplinkRoutes table.

                    logicalEPs = msg.LogicalEPs;
                    for (int i = 0; i < logicalEPs.Length; i++)
                        uplinkRoutes.Add(new LogicalRoute(logicalEPs[i], new PhysicalRoute(msg.RouterEP, msg.AppName, msg.AppDescription, msg.RouterInfo,
                                                          Guid.Empty, null, null, DateTime.MaxValue)));

                    return;
                }

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
        /// Called when the router detects that it's been assigned a new network address.
        /// </summary>
        protected override void OnNewEP()
        {
            using (TimedLock.Lock(this.SyncRoot))
            {
                if (!isRunning)
                    return;

                // Multicast a LeafSettingsMsg to all leaf routers letting them know
                // about the new hub endpoint.

                if (base.MulticastEnabled)
                    Multicast(new LeafSettingsMsg(base.RouterEP, base.UdpEP.Port, base.TcpEP.Port, advertiseTime, false));
            }
        }

        /// <summary>
        /// Adds the channel to the main hash table
        /// </summary>
        /// <param name="channel">The newly initialized channel.</param>
        /// <remarks>
        /// This will be called by a Tcp channel when it receives the intialization
        /// message from the other endpoint.  At this point, the channel's RemoteEP
        /// property will be fully initialized.
        /// </remarks>
        internal override void OnTcpInit(TcpChannel channel)
        {
            HubAdvertiseMsg         msg;
            LogicalAdvertiseMsg[]   msgs;

            base.OnTcpInit(channel);

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (channel == uplinkChannel)
                {
                    // Send a HubAdvertiseMsg to the root...

                    msg      = new HubAdvertiseMsg(this.RouterEP, this.AppName, this.AppDescription, this.RouterInfo, this.Dispatcher.LogicalEndpointSetID);
                    msg._TTL = 1;

                    SendTo(parentEP, msg);

                    // ...followed by the downlink LogicalAdvertiseMsgs

                    msgs = GenLogicalAdvertiseMsgs(downlinkEPs);
                    for (int i = 0; i < msgs.Length; i++)
                        SendTo(parentEP, msgs[i]);
                }
            }
        }

        /// <summary>
        /// Called when a TCP channel detects that the remote endpoint closes
        /// its connection.
        /// </summary>
        /// <param name="channel">The channel.</param>
        internal override void OnTcpClose(TcpChannel channel)
        {
            base.OnTcpClose(channel);

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (channel == uplinkChannel)
                    uplinkChannel = null;
                else if (channel.IsDownlink)
                    base.PhysicalRoutes.Remove(new ChannelEP(Transport.Tcp, channel.RemoteEP));
            }
        }

        /// <summary>
        /// Handles the physical routing of the message passed.
        /// </summary>
        /// <param name="physicalEP">The physical target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// This override extends the behavior of the base implementation
        /// by routing messages with physical endpoints below that of the
        /// hub to the appropriate leaf router.  Messages with endpoints
        /// outside of the hub's domain will be routed up to the root
        /// router (if there is one).
        /// </remarks>
        protected override void RoutePhysical(MsgEP physicalEP, Msg msg)
        {
            PhysicalRoute route;

            Assertion.Test(physicalEP.IsPhysical, "Physical endpoint expected");
            Assertion.Test(msg._ToEP.ChannelEP == null, "ChannelEP should be null for routed messages.");

            using (TimedLock.Lock(this.SyncRoot))
            {
                // Extended behavior

                if (msg._TTL == 0)
                {
                    OnDiscardMsg(msg, "TTL=0");
                    return;
                }

                route = base.PhysicalRoutes[physicalEP];
                if (route != null)
                {
                    TransmitTcp(route, msg);
                    return;
                }

                if (isRoot)
                {
                    // Try routing the message to one of the hubs

                    route = base.PhysicalRoutes[physicalEP.ToString(1, false)];
                    if (route != null)
                    {
                        TransmitTcp(route, msg);
                        return;
                    }

                    OnDiscardMsg(msg, "Hub route not found.");
                    return;
                }
                else if (parentEP == null)
                {
                    OnDiscardMsg(msg, "No root router.");
                    return;
                }

                msg._TTL--;
                Uplink(msg);
                return;
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
        /// succeeds we're done.  If this fails then the router looks
        /// in its logical routing table for any child routers advertising
        /// a matching endpoint.  If any matches are found then one
        /// will be randomly selected and the message will be forwarded
        /// there.
        /// </para>
        /// <para>
        /// If no matches are found and this router is acting as a hub,
        /// then the uplink logical routing table will be searched for
        /// matching endpoints.  If any are found then the message will
        /// be forwarded to the root.
        /// </para>
        /// <para><b><u>Broadcasting</u></b></para>
        /// <para>
        /// Broadcast messages are handled a bit differently.  First, the
        /// message will be dispatched to every matching application handler
        /// known to this router. 
        /// </para>
        /// <para>
        /// Then, if the message was received on a UDP channel then no
        /// further routing will be done.  The thought here is that UDP
        /// multicasting can be done in this situation so that forwarding
        /// over TCP won't be necessary.  This is a bit of a hack but it
        /// simplified the implementation a bit.
        /// </para>
        /// <para>
        /// Next, the logical route table will be searched for routers that
        /// advertise endpoints that match the message target endpoint.
        /// If the message originated at this router then it will be forwarded
        /// to all of these routers.  If the message originated at another
        /// router then what happens next depends on whether that router
        /// was a peer-to-peer enabled leaf or not.
        /// </para>
        /// <para>
        /// If the originating router was a peer-to-peer leaf then this router
        /// will assume that the originating router already handled the broadcasting 
        /// to all of the other peer-to-peer routers on the subnet.  So, this
        /// router will forward the message only to the non-peer-to-peer leaf
        /// routers advertising a matching endpoint.
        /// </para>
        /// <para>
        /// If the originating router was not peer-to-peer enabled then the
        /// message will be forwarded to all leaf routers advartising a matching
        /// endpoint.
        /// </para>
        /// <para>
        /// Finally, if the router is a hub and the message wasn't forwarded
        /// here from the root and if the uplink logical route table has
        /// matching routes, then the message will be forwarded to the
        /// root.
        /// </para>
        /// </remarks>
        protected override void RouteLogical(Msg msg)
        {
            List<LogicalRoute>  routes;
            LogicalRoute        route;
            TcpChannel          tcpChannel;
            MsgEP               sourceEP;
            bool                fromP2PRouter;

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
                    // Not a broadcast message.

                    if (this.Dispatcher.Dispatch(msg))
                    {
                        SendMsgReceipt(msg);
                        return;
                    }

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

                    if (!isRoot && !isDetached)
                    {
                        routes = uplinkRoutes.GetRoutes(msg._ToEP);
                        if (routes.Count > 0)
                        {
                            Uplink(msg);
                            return;
                        }

                        SendMsgReceipt(msg);
                        OnDiscardMsg(msg, "No known endpoints.");
                    }
                }
                else
                {
                    // This is a broadcast message.  First, dispatch to any local 
                    // application handlers.

                    this.Dispatcher.Dispatch(msg);

                    // If the message was originally received on a UdpChannel
                    // then we're finished.

                    if (msg._ReceiveChannel != null)
                    {
                        if (msg._ReceiveChannel is UdpChannel)
                            return;

                        tcpChannel = (TcpChannel)msg._ReceiveChannel;
                        sourceEP = tcpChannel.RouterEP;
                        fromP2PRouter = tcpChannel.IsP2P && !(tcpChannel.IsUplink || tcpChannel.IsDownlink);
                    }
                    else
                    {
                        sourceEP = null;
                        fromP2PRouter = false;
                    }

                    if (fromP2PRouter)
                    {
                        // The message came from a peer-to-peer enabled leaf
                        // router so we're going to assume that the originating
                        // router has already forwarded the message to the other
                        // P2P enabled leaf routers on the subnet.
                        //
                        // So all we have to do is to forward the message to the
                        // non-P2P leaf routers advertising matching endpoints.

                        routes = base.LogicalRoutes.GetRoutes(msg._ToEP);
                        for (int i = 0; i < routes.Count; i++)
                        {
                            route = routes[i];
                            if ((sourceEP == null || !sourceEP.Equals(route.PhysicalRoute.RouterEP)) && !route.PhysicalRoute.RouterInfo.IsP2P)
                                TransmitTcp(route.PhysicalRoute, msg);
                        }
                    }
                    else
                    {
                        // The message came from a non-peer-to-peer router
                        // then forward the message to all child routers advertising
                        // matching endpoints (except the router that forwarded
                        // the message here).

                        routes = base.LogicalRoutes.GetRoutes(msg._ToEP);
                        for (int i = 0; i < routes.Count; i++)
                        {
                            route = routes[i];
                            if (sourceEP == null || !sourceEP.Equals(route.PhysicalRoute.RouterEP))
                                TransmitTcp(route.PhysicalRoute, msg);
                        }
                    }

                    // If this is not the root and root didn't forward this message
                    // and if the root advertised a matching uplink endpoint, then
                    // forward the message up to the root.

                    if (!isRoot && (sourceEP == null || !sourceEP.Equals(parentEP)) && uplinkRoutes.GetRoutes(msg._ToEP).Count > 0)
                        Uplink(msg);
                }
            }
        }

        /// <summary>
        /// Used by Unit tests to modify the time interval for
        /// RouterAdvertiseMsg broadcasts.
        /// </summary>
        internal TimeSpan AdvertiseTime
        {
            get {return advertiseTime;}
            set {advertiseTime = value;}
        }
    }
}
