//-----------------------------------------------------------------------------
// FILE:        Const.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines global LillTek constants

using System;
using System.Net;

namespace LillTek
{
    /// <summary>
    /// Defines global LillTek constants.
    /// </summary>
    public static class Const
    {
        //---------------------------------------------------------------------
        // LillTek Service related names

        /// <summary>
        /// Service name of the LillTek.Datacenter.AuthService application.
        /// </summary>
        public const string AuthServiceName = "LillTek Authentication Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.AuthService application.
        /// </summary>
        public const string AuthServicePerf = "LillTek.Authentication Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.ConfigService application.
        /// </summary>
        public const string ConfigServiceName = "LillTek Configuration Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.ConfigService application.
        /// </summary>
        public const string ConfigServicePerf = "LillTek.Configuration Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.RouterService application.
        /// </summary>
        public const string RouterServiceName = "LillTek Router Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.RouterService application.
        /// </summary>
        public const string RouterServicePerf = "LillTek.Router Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.ServerManager application.
        /// </summary>
        public const string ServerManagerName = "LillTek Server Manager";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.ServerManager application.
        /// </summary>
        public const string ServerManagerPerf = "LillTek.Server Manager";

        /// <summary>
        /// Service name of the LillTek.Datacenter.SentinelService application.
        /// </summary>
        public const string SentinelName = "LillTek Sentinel Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.SentinelService application.
        /// </summary>
        public const string SentinelPerf = "LillTek.Sentinel Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.AppStore application.
        /// </summary>
        public const string AppStoreName = "LillTek AppStore Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.AppStore application.
        /// </summary>
        public const string AppStorePerf = "LillTek.AppStore Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.DynDnsService application.
        /// </summary>
        public const string DynDnsName = "LillTek DynDNS Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.DynDnsService application.
        /// </summary>
        public const string DynDnsPerf = "LillTek.DynDNS Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.DynDnsClientService application.
        /// </summary>
        public const string DynDnsClientName = "LillTek DynDNS Client Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.DynDnsClientService application.
        /// </summary>
        public const string DynDnsClientPerf = "LillTek.DynDNS Client Service";

        /// <summary>
        /// Service name of the LillTek.Datacenter.BroadcastServer application.
        /// </summary>
        public const string BroadcastServerName = "LillTek Broadcast Server";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.BroadcastServer application.
        /// </summary>
        public const string BroadcastServerPerf = "LillTek.Broadcast Server";

        /// <summary>
        /// Service name of the LillTek.Datacenter.MessageQueue application.
        /// </summary>
        public const string MessageQueueName = "LillTek Message Queue";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.MessageQueue application.
        /// </summary>
        public const string MessageQueuePerf = "LillTek.Message Queue";

        /// <summary>
        /// Service name of the LillTek.Datacenter.MssGateway application.
        /// </summary>
        public const string MssGatewayName = "LillTek MSS Gateway";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.MssGateway application.
        /// </summary>
        public const string MssGatewayPerf = "LillTek.MSS Gateway";

        /// <summary>
        /// Service name of the LillTek.Datacenter.HeartbeatService application.
        /// </summary>
        public const string HeartbeatName = "LillTek Heartbeat Service";

        /// <summary>
        /// Performance counter set name for the LillTek.Datacenter.HeartbeatService application.
        /// </summary>
        public const string HeartbeatPerf = "LillTek.Heartbeat Service";

        /// <summary>
        /// Service name for the LillTek Communication Gateway service.
        /// </summary>
        public const string LillcomGatewayName = "LillTek Communication Gateway Service";

        /// <summary>
        /// Performance counter set name for the LillTek Communication Gateway application.
        /// </summary>
        public const string LillcomGatewayPerf = "LillTek.Communication Gateway Service";

        /// <summary>
        /// Service name for the LillTek.GeoTracker.Service service.
        /// </summary>
        public const string GeoTrackerName = "LillTek GeoTracker Service";

        /// <summary>
        /// Performance counter set name for the LillTek.GeoTracker.Service application.
        /// </summary>
        public const string GeoTrackerPerf = "LillTek.GeoTracker Service";

        //---------------------------------------------------------------------
        // LillTek Data Center constants

        /// <summary>
        /// The default multicast group used by LillTek DataCenter services for router discovery.
        /// </summary>
        public static readonly IPAddress DCCloudGroup;

        /// <summary>
        /// The default multicast port used by LillTek DataCenter services for router discovery.
        /// </summary>
        public const int DCCloudPort = 30;

        /// <summary>
        /// The default multicast endpoint used by LillTek DataCenter services for router discovery.
        /// </summary>
        public static readonly IPEndPoint DCCloudEP;

        /// <summary>
        /// The default root router port number used by LillTek DataCenter services.
        /// </summary>
        public const int DCRootPort = 32;

        /// <summary>
        /// The default messaging hub name to use in data center application configuration settings.
        /// </summary>
        public const string DCDefHubName = "Hub";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Const()
        {
            DCCloudGroup = IPAddress.Parse("231.223.0.2");
            DCCloudEP    = new IPEndPoint(DCCloudGroup, DCCloudPort);
        }
    }
}