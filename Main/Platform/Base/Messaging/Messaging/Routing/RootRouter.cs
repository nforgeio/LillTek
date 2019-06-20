//-----------------------------------------------------------------------------
// FILE:        RootRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the root router class.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;

// $todo(jeff.lill): 
//
// For now a RootRouter is just a minor extension of
// a hub router.  At some point we're going to want to implement 
// scalability features such as multiple routers, route
// replication, etc.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a root message router.
    /// </summary>
    public class RootRouter : HubRouter
    {
        /// <summary>
        /// This constructor initializes the router.
        /// </summary>
        public RootRouter()
            : base()
        {
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
        ///     for presence packets on the specified <see cref="RouterSettings.CloudAdapter"/> for 
        ///     the <see cref="RouterSettings.CloudEP" /> multicast endpoint.
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
        /// <tr valign="top"><td>CloudAdapter</td><td>ANY</td><td>The discovery multicast network adapter address</td></tr>
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
        /// <tr valign="top"><td>TcpEP</td><td>ANY:0</td><td>TCP listening network endpoint</td></tr>
        /// <tr valign="top"><td>TcpMsgQueueCountMax</td><td>1000</td><td>Max queued outbound TCP normal priority messages.</td></tr>
        /// <tr valign="top"><td>TcpMsgQueueSizeMax</td><td>10MB</td><td>Max bytes of serialized queued outbound TCP normal priority messages.</td></tr>
        /// <tr valign="top"><td>TcpBacklog</td><td>100</td><td>Max pending connecting TCP sockets</td></tr>
        /// <tr valign="top"><td>TcpDelay</td><td>off</td><td>Enables Nagle on TCP channels</td></tr>
        /// <tr valign="top"><td>BkInterval</td><td>1s</td><td>Background task interval</td></tr>
        /// <tr valign="top"><td>MaxIdle</td><td>5m</td><td>Maximum time a TCP socket should idle before being closed automatically</td></tr>
        /// <tr valign="top"><td>EnableP2P</td><td>true</td><td>Enables peer-to-peer routing between routers on the same subnet</td></tr>
        /// <tr valign="top"><td>AdvertiseTime</td><td>1m</td><td>RouterAdvertiseMsg multicast interval</td></tr>
        /// <tr valign="top"><td>KeepAliveTime</td><td>1m</td><td>Root uplink keep-alive time</td></tr>
        /// <tr valign="top"><td>PhysicalRouteTTL</td><td>3m</td><td>Maximum time a physical route will be maintained without being refreshed with a RouterAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DefMsgTTL</td><td>5</td><td>Default message time-to-live (max hops)</td></tr>
        /// <tr valign="top"><td>SharedKey</td><td>PLAINTEXT</td><td>The shared encryption key used to encrypt all message traffic.</td></tr>
        /// <tr valign="top"><td>SessionRetries</td><td>3</td><td>Maximum session initiation retries.</td></tr>
        /// <tr valign="top"><td>SessionTimeout</td><td>10s</td><td>Default session timeout</td></tr>
        /// <tr valign="top"><td>MaxLogicalAdvertiseEPs</td><td>256</td><td>Maximum number of logical endpoints to be included in a single LogicalAdvertiseMsg</td></tr>
        /// <tr valign="top"><td>DeadRouterTTL</td><td>0s</td><td>Maximum time to wait for a <see cref="ReceiptMsg" /> before declaring a dead router.  Use 0 to disable dead router detection.</td></tr>
        /// <tr valign="top"><td>UplinkEP[#]</td><td>(none)</td><td>Zero or more static logical endpoints to be advertised to the hub routers.</td></tr>
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
        public new void Start()
        {
            base.StartAsRoot();
        }

        /// <summary>
        /// Starts the router using the configuration settings passed.
        /// </summary>
        /// <param name="settings">The configuration settings.</param>
        public new void Start(RouterSettings settings)
        {
            base.StartAsRoot(settings);
        }
    }
}
