//-----------------------------------------------------------------------------
// FILE:        GeoTrackerNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core functionality of the GeoTracker Service.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.GeoTracker.Msgs;
using LillTek.Messaging;

// $todo(jeff.lill):
//
// Implement reverse geocoding.

// $todo(jeff.lill):
//
// Make GeoTrackerNode a bit smarter at dealing with changes to the number of servers
// within a dynamic cluster.  Rather than always performing a parallel query to
// retrieve the state of a specific entity, the class could track the time since
// the dynamnic cluster changed and only use parallel queries when this time is
// less than the interval at which we hold old location fixes.  We'll need some
// changes to ITopologyProvider to signal changes to the cluster.

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Implements the core functionality of the GeoTracker Service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="GeoTrackerNode" /> class implements the core server side functionality
    /// for a GeoTracker server node.  See <see cref="OverviewDoc" /> for more information
    /// on what this means.
    /// </para>
    /// <note>
    /// This product includes GeoLite data created by MaxMind, available from http://www.maxmind.com/.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class GeoTrackerNode
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {

            // Performance counter names

            const string IPGeocode_Name     = "IP Geocode Queries/sec";
            const string FixesReceived_Name = "Fixes Received/sec";
            const string FixesArchived_Name = "Fixes Archived/sec";
            const string Runtime_Name       = "Runtime (min)";
            const string Entities_Name      = "Entities Tracked";
            const string Groups_Name        = "Groups Tracked";

            /// <summary>
            /// Installs the service's performance counters by adding them to the
            /// performance counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public static void Install(PerfCounterSet perfCounters, string perfPrefix)
            {
                if (perfCounters == null)
                    return;

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                perfCounters.Add(new PerfCounter(perfPrefix + IPGeocode_Name, "IP Geocode queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + FixesReceived_Name, "Location fixes received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + FixesArchived_Name, "Location fixes received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + Entities_Name, "Number of entities tracked", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + Groups_Name, "Number of groups tracked", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter IPGeocode;           // # IP geocoding operations/sec
            public PerfCounter FixesReceived;       // # Location fixes received/sec
            public PerfCounter FixesArchived;       // # Location fixes archived/sec
            public PerfCounter Runtime;             // Service runtime in minutes
            public PerfCounter Entities;            // # of entities tracked
            public PerfCounter Groups;              // # of entities tracked

            /// <summary>
            /// Initializes the service's performance counters from the performance
            /// counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public Perf(PerfCounterSet perfCounters, string perfPrefix)
            {
                Install(perfCounters, perfPrefix);

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                if (perfCounters != null)
                {
                    IPGeocode     = perfCounters[perfPrefix + IPGeocode_Name];
                    FixesReceived = perfCounters[perfPrefix + FixesReceived_Name];
                    FixesArchived = perfCounters[perfPrefix + FixesArchived_Name];
                    Runtime       = perfCounters[perfPrefix + Runtime_Name];
                    Entities      = perfCounters[perfPrefix + Entities_Name];
                    Groups        = perfCounters[perfPrefix + Groups_Name];
                }
                else
                {

                    IPGeocode     =
                    FixesReceived =
                    FixesArchived =
                    Runtime       =
                    Entities      =
                    Groups        = PerfCounter.Stub;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the performance counters managed by the class to the performance counter
        /// set passed (if not null).  This will be called during the application installation
        /// process when performance counters are being installed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        public static void InstallPerfCounters(PerfCounterSet perfCounters, string perfPrefix)
        {
            Perf.Install(perfCounters, perfPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The service's default configuration key prefix.
        /// </summary>
        public const string ConfigPrefix = "LillTek.GeoTracker.Server";

        /// <summary>
        /// <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.GeoTracker";

        private object                      syncLock = new object();    // Instance used for thread synchronization
        private MsgRouter                   router;                     // The associated router (or null)
        private bool                        isRunning;                  // True if the handler is running
        private Perf                        perf;                       // Performance counters
        private DateTime                    startTime;                  // Time the service was started (UTC)
        private GeoTrackerServerSettings    settings;                   // The service settings
        private GatedTimer                  bkTimer;                    // Background task timer
        private ITopologyProvider           clusterClient;              // Manages the client side of cluster communication
        private ITopologyProvider           clusterServer;              // Manages the server side of cluster communication
        private IPGeocoder                  ipGeocoder;                 // Handles mapping of IP address to GeoFix.
        private GeoFixCache                 fixCache;                   // In-memory cache of recent entity and group info
        private IGeoFixArchiver             archiver;                   // The fix archiver

        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoTrackerNode()
        {
            // Register the GeoTracker messages types with LillTek Messaging.

            LillTek.GeoTracker.Global.RegisterMsgTypes();
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="settings">The configuration settings.</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, GeoTrackerServerSettings settings, PerfCounterSet perfCounters, string perfPrefix)
        {
            if (this.isRunning)
                throw new InvalidOperationException("This node has already been started.");

            if (router == null)
                throw new ArgumentNullException("router");

            // Initialize the performance counters

            this.startTime = DateTime.UtcNow;
            this.perf      = new Perf(perfCounters, perfPrefix);

            // General initialization

            this.settings      = settings;
            this.bkTimer       = new GatedTimer(new TimerCallback(OnBkTimer), null, settings.BkInterval);
            this.ipGeocoder    = new IPGeocoder(this);
            this.clusterClient = Helper.CreateInstance<ITopologyProvider>(settings.ClusterTopology);
            this.clusterServer = Helper.CreateInstance<ITopologyProvider>(settings.ClusterTopology);
            this.fixCache      = new GeoFixCache(settings);
            this.archiver      = Helper.CreateInstance<IGeoFixArchiver>(settings.GeoFixArchiver);

            EntityState.MaxEntityFixes = settings.MaxEntityGeoFixes;

            try
            {
                // Initialize the router

                this.router = router;
                this.router.Dispatcher.AddTarget(this, "GeoTrackerServerEP", new SimpleEPMunger(settings.ServerEP), null);

                // Initialize the cluster

                this.clusterClient.OpenClient(router, settings.ClusterEP, settings.ClusterArgs);
                this.clusterServer.OpenServer(router, "GeoTrackerClusterEP", settings.ClusterEP, this, settings.ClusterArgs);

                // Start the archiver.

                archiver.Start(this, settings.GeoFixArchiverArgs);

                this.isRunning = true;
            }
            catch
            {
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.GeoTracker.Server</b>).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix)
        {
            Start(router, GeoTrackerServerSettings.LoadConfig(keyPrefix ?? ConfigPrefix), perfCounters, perfPrefix);
        }

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        public void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            lock (syncLock)
            {
                router.Dispatcher.RemoveTarget(this);

                if (clusterClient != null)
                    clusterClient.Close();

                if (clusterServer != null)
                    clusterServer.Close();

                if (fixCache != null)
                    fixCache.Stop();

                if (ipGeocoder != null)
                    ipGeocoder.Stop();

                if (archiver != null)
                {
                    archiver.Stop();
                    archiver = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                isRunning = false;
            }
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        /// <summary>
        /// Returns the server settings.
        /// </summary>
        public GeoTrackerServerSettings Settings
        {
            get { return settings; }
        }

        /// <summary>
        /// Called by <see cref="IGeoFixArchiver" /> implementations when <see cref="GeoFix" />es are actually
        /// archived.  This increments the underlying performance counter.
        /// </summary>
        /// <param name="count">The number of archived fixes.</param>
        public void IncrementFixesReceivedBy(int count)
        {
            perf.FixesArchived.IncrementBy(count);
        }

        /// <summary>
        /// Called periodically on a worker thread when the background timer is raised.
        /// </summary>
        /// <param name="state"></param>
        private void OnBkTimer(object state)
        {
            try
            {
                lock (syncLock)
                {
                    if (!isRunning)
                        return;

                    // Update the perf counters

                    perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;
                    perf.Entities.RawValue = fixCache.EntityCount;
                    perf.Groups.RawValue = fixCache.GroupCount;
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // Unit test related extensions.

        /// <summary>
        /// <b>Used for unit testing only:</b> Returns the node's <see cref="IPGeocoder" /> instance.
        /// </summary>
        internal IPGeocoder IPGeocoder
        {
            get { return ipGeocoder; }
        }

        /// <summary>
        /// <b>Used for unit testing only:</b> Returns the node's <see cref="GeoFixCache" /> instance.
        /// </summary>
        internal GeoFixCache FixCache
        {
            get { return fixCache; }
        }

        //---------------------------------------------------------------------
        // External cluster message handlers.

        /// <summary>
        /// Handles IP address to <see cref="GeoFix" /> queries.
        /// </summary>
        /// <param name="msg"></param>
        [MsgHandler(LogicalEP = "logical://LillTek/GeoTracker/Server", DynamicScope = "GeoTrackerServerEP")]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
        public void OnExternalMsg(IPToGeoFixMsg msg)
        {
            try
            {
                perf.IPGeocode.Increment();

                if (!settings.IPGeocodeEnabled)
                    throw new NotAvailableException("GeoTracker: IP Geocoding is disabled.");

                if (msg.Address.AddressFamily != AddressFamily.InterNetwork)
                    throw new NotSupportedException(string.Format("GeoTracker: [{0}] network addresses cannot be geocoded. Only IPv4 addresses are supported.", msg.Address.AddressFamily));

                var fix = ipGeocoder.MapIPAddress(msg.Address);

                router.ReplyTo(msg, new IPToGeoFixAck(fix));
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                throw;
            }
        }

        /// <summary>
        /// Handles external <see cref="GeoFix"/> submissions by passing the request onto the 
        /// correct server in the cluster.
        /// </summary>
        /// <param name="msg"></param>
        [MsgHandler(LogicalEP = "logical://LillTek/GeoTracker/Server", DynamicScope = "GeoTrackerServerEP")]
        [MsgSession(Type = SessionTypeID.Query, IsAsync = false, Idempotent = false)]
        public void OnExternalMsg(GeoFixMsg msg)
        {
            try
            {
                clusterClient.BeginQuery(msg.EntityID, msg.Clone(),
                    ar =>
                    {
                        clusterClient.EndQuery(ar);
                        router.ReplyTo(msg, new Ack());

                        msg._Session.OnAsyncFinished();
                    },
                    null);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                throw;
            }
        }

        //---------------------------------------------------------------------
        // Internal cluster message handlers.

        /// <summary>
        /// Handles internal <see cref="GeoFix"/> submissions by adding them to the cache.
        /// </summary>
        /// <param name="msg"></param>
        [MsgHandler(LogicalEP = "logical://LillTek/GeoTracker/Cluster", DynamicScope = "GeoTrackerClusterEP")]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
        public void OnInternalMsg(GeoFixMsg msg)
        {
            try
            {
                if (archiver != null)
                {
                    // Archive the fixes.

                    foreach (var fix in msg.Fixes)
                        archiver.Archive(msg.EntityID, msg.GroupID, fix);
                }

                perf.FixesReceived.IncrementBy(msg.Fixes.Length);
                fixCache.AddEntityFixes(msg.EntityID, msg.GroupID, msg.Fixes);
                router.ReplyTo(msg, new Ack());
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                throw;
            }
        }
    }
}
