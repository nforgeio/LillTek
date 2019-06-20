//-----------------------------------------------------------------------------
// FILE:        Sentinel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side interface to a SentinelServiceHandler.

using System;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.ServiceProcess;

using LillTek.Common;
using LillTek.Datacenter.Msgs.SentinelService;
using LillTek.Messaging;
using LillTek.Windows;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Implements the client side interface to a <c>SentinelServiceHandler</c>. />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Start off by constructing an instance, passing the service's endpoint (typically
    /// <see cref="AbstractEP" />).  You can then start communicating to the service by
    /// calling the various methods defined by this class.  By default, these calls will
    /// be load balanced across the service instances.
    /// </para>
    /// <para>
    /// It is useful to be able to establish communication with a particular service instance
    /// (this is important because each instances view of the data center state might be
    /// slightly different due to timing issues etc).  Use the <see cref="Connect" /> method
    /// to establish a persistent connection with a service instance.  The instance will be
    /// chosen at random for load balancing.  Once an connection is established, all subsequent
    /// traffic will be routed to that instance.
    /// </para>
    /// <para>
    /// Use <see cref="Disconnect" /> to break the connection after which, messages will
    /// again be load balanced across the service instances.
    /// </para>
    /// </remarks>
    public sealed class Sentinel
    {
        /// <summary>
        /// The abstract endpoint of the LillTek.Datacenter.Server.SentinelServiceHandler class.
        /// </summary>
        public const string AbstractEP = "abstract://LillTek/DataCenter/Sentinel";

        /// <summary>
        /// The abstract instance endpoint of the LillTek.Datacenter.Server.SentinelServiceHandler class.
        /// </summary>
        public const string InstanceEP = "abstract://LillTek/DataCenter/Sentinel/Instances/Instance";

        private MsgRouter   router;         // The associated message router
        private MsgEP       serviceEP;      // The generic sentinel service endpoint.
        private MsgEP       instanceEP;     // Endpoint of the connected sentinel instance.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="router">The application message router to be used for communications.</param>
        /// <param name="serviceEP">The sentinel service endpoint.</param>
        public Sentinel(MsgRouter router, MsgEP serviceEP)
        {
            Global.RegisterMsgTypes();

            this.router     = router;
            this.serviceEP  = serviceEP;
            this.instanceEP = null;
        }

        /// <summary>
        /// Performs a query to the currently configured sentinel service endpoint.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The response.</returns>
        private Msg Query(Msg query)
        {
            return router.Query(instanceEP != null ? instanceEP : serviceEP, query);
        }

        /// <summary>
        /// Returns <c>true</c> if connected to a specific service instance.
        /// </summary>
        public bool IsConnected
        {
            get { return instanceEP != null; }
        }

        /// <summary>
        /// Establishes a connection to a randomly choosen Sentinel Service instance.
        /// </summary>
        public void Connect()
        {
            instanceEP = ((ConnectAck)router.Query(serviceEP, new ConnectMsg())).InstanceEP;
        }

        /// <summary>
        /// Breaks the connection to a Sentinel Service instance.
        /// </summary>
        public void Disconnect()
        {
            instanceEP = null;
        }

        /// <summary>
        /// Transmits a log entry to the sentinel service for archiving.
        /// </summary>
        /// <param name="logName">The event log name.</param>
        /// <param name="logEntry">The entry to be logged.</param>
        public void LogEvent(string logName, EventLogEntry logEntry)
        {
            Query(new LogEventMsg(logName, logEntry));
        }
    }
}
