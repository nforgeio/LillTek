//-----------------------------------------------------------------------------
// FILE:        NsgQueueHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core functionality provided by the Message Queue service.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs.AppStore;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;
using LillTek.Transactions;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements the core functionality provided by the Message Queue service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is a thin wrapper over the <see cref="MsgQueueEngine" /> class.
    /// See the class documentation for more details.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// The default configuration key prefix is <b>LillTek.Datacenter.MessageQueue</b>.
    /// Most of the application settings are described in <see cref="MsgQueueEngineSettings" />.
    /// The additional settings are:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PersistTo</td>
    ///     <td>DISK</td>
    ///     <td>
    ///     <para>
    ///     Indicates how the messages are to be persisted.  The possible
    ///     values are <b>DISK</b> and <b>MEMORY</b>.  <b>DISK</b> specifies
    ///     that the message will be persisted as files in the file system
    ///     directory indicated by the <b>Folder</b> setting and that messages will
    ///     be durable across system restarts.
    ///     </para>
    ///     <para>
    ///     <b>MEMORY</b> specifies that messages will be stored in memory
    ///     which will result in a very high performance system but has
    ///     the disadvantage of no durability across system restarts and
    ///     the limitations of available memory.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Folder</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     The fully qualified path to the folder where messages are to be
    ///     persisted when persisting message to disk.  This defaults to 
    ///     <b>$(AppPath)\Messages</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>LogTo</td>
    ///     <td>DISK</td>
    ///     <td>
    ///     <para>
    ///     Indicates how transactions against the message queue are
    ///     to be persisted.  The possible values are <b>DISK</b> or <b>MEMORY</b>.
    ///     <b>DISK</b> specifies that the logs will be persisted as files in a 
    ///     file system folder and that transactions will be durable across system 
    ///     restarts.
    ///     </para>
    ///     <para>
    ///     <b>MEMORY</b> specifies that transactions will be stored in memory
    ///     which will result in a very high performance system but has
    ///     the disadvantage of no durability across system restarts and
    ///     the limitations of available memory.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>LogFolder</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     The fully qualified path to the folder where transactions are to be
    ///     persisted when persisting message to disk.  This defaults to 
    ///     <b>$(AppPath)\Messages\Log</b>.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// <para><b><u>Performance Counters</u></b></para>
    /// <para>
    /// The class can be configured to expose performance counters.  Call the
    /// static <see cref="InstallPerfCounters" /> method to add the class performance
    /// counters to a <see cref="PerfCounterSet" /> during application installation
    /// and then pass a set instance to the <see cref="Start" /> method.
    /// </para>
    /// <para>
    /// The table below describes the performance counters exposed
    /// by a Message Queue Service.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>        
    /// <th width="1">Type</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Runtime</td>
    ///     <td>Count</td>
    ///     <td>Elapsed service runtime in minutes.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Session Rate</td>
    ///     <td>Rate</td>
    ///     <td>Connections established per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Enqueued Messages</td>
    ///     <td>Rate</td>
    ///     <td>Message enqueued to the service per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Dequeued Messages</td>
    ///     <td>Rate</td>
    ///     <td>Messages dequeued from the service per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Peeked Messages</td>
    ///     <td>Rate</td>
    ///     <td>Messages peeked from the service per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Expired Messages</td>
    ///     <td>Rate</td>
    ///     <td>Messages expired and discarded per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Commits</td>
    ///     <td>Rate</td>
    ///     <td>Transaction commits per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Rollbacks</td>
    ///     <td>Rate</td>
    ///     <td>Transaction rollbacks per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Session Count</td>
    ///     <td>Count</td>
    ///     <td>Number of connected client sessions.</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class MsgQueueHandler : ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string Enqueued_Name     = "Enqueued messages/sec";
            const string Dequeued_Name     = "Dequeued messages/sec";
            const string Peeked_Name       = "Peeked messages/sec";
            const string Expired_Name      = "Expired messages/sec";
            const string Commits_Name      = "Commits/sec";
            const string Rollbacks_Name    = "Rollbacks/sec";
            const string SessionRate_Name  = "Sessions/sec";
            const string SessionCount_Name = "Session Count";
            const string Runtime_Name      = "Runtime (min)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Enqueued_Name, "Messages enqueued to the service/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Dequeued_Name, "Messages dequeued from the service/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Peeked_Name, "Messages peeked from the service/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Expired_Name, "Messages expired and discarded/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Commits_Name, "Transaction commits/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Rollbacks_Name, "Transaction rollbacks/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + SessionRate_Name, "Session connections/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + SessionCount_Name, "Number of connected sessions", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Enqueued;           // # enqueued messages/sec
            public PerfCounter Dequeued;           // # dequeued messages/sec
            public PerfCounter Peeked;             // # peeked messages/sec
            public PerfCounter Expired;            // # expired messages/sec
            public PerfCounter Commits;            // # transaction commits/sec
            public PerfCounter Rollbacks;          // # transaction rollbacks/sec
            public PerfCounter SessionRate;        // # of sessions established/sec
            public PerfCounter SessionCount;       // # of sessions
            public PerfCounter Runtime;            // Service runtime in minutes

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
                    Enqueued     = perfCounters[perfPrefix + Enqueued_Name];
                    Dequeued     = perfCounters[perfPrefix + Dequeued_Name];
                    Peeked       = perfCounters[perfPrefix + Peeked_Name];
                    Expired      = perfCounters[perfPrefix + Expired_Name];
                    Commits      = perfCounters[perfPrefix + Commits_Name];
                    Rollbacks    = perfCounters[perfPrefix + Rollbacks_Name];
                    SessionRate  = perfCounters[perfPrefix + SessionRate_Name];
                    SessionCount = perfCounters[perfPrefix + SessionCount_Name];
                    Runtime      = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {
                    Enqueued     =
                    Dequeued     =
                    Peeked       =
                    Expired      =
                    Commits      =
                    Rollbacks    =
                    SessionRate  =
                    SessionCount =
                    Runtime      = PerfCounter.Stub;
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
        public const string ConfigPrefix = "LillTek.Datacenter.MessageQueue";

        private MsgRouter       router;                 // The associated router (or null if the handler is stopped).
        private MsgQueueEngine  engine;                 // The message queue engine
        private Perf            perf;                   // Performance counters
        private DateTime        startTime;              // Time the service was started (UTC)

        /// <summary>
        /// Constructs a message queue service handler instance.
        /// </summary>
        public MsgQueueHandler()
        {
            this.router = null;
            this.engine = null;
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.MessageQueue</b>).</param>
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
            Config              config;
            IMsgQueueStore      store;
            ITransactionLog     log;

            keyPrefix = keyPrefix != null ? keyPrefix : ConfigPrefix;
            config    = new Config(keyPrefix);

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Verify the router parameter

            if (router == null)
                throw new ArgumentNullException("router", "Router cannot be null.");

            if (this.router != null)
                throw new InvalidOperationException("This handler has already been started.");

            // Initialize the performance counters

            startTime = DateTime.UtcNow;
            perf      = new Perf(perfCounters, perfPrefix);

            try
            {
                // Initialize the router

                this.router = router;

                // Load the handler configurtation settings

                switch (config.Get("PersistTo", "disk").ToLowerInvariant())
                {
                    case "memory":

                        store = new MsgQueueMemoryStore();
                        break;

                    default:
                    case "disk":

                        store = new MsgQueueFileStore(config.Get("Folder", EnvironmentVars.Expand("$(AppPath)\\Messages")));
                        break;
                }

                switch (config.Get("LogTo", "disk").ToLowerInvariant())
                {
                    case "memory":

                        log = new MemoryTransactionLog();
                        break;

                    default:
                    case "disk":

                        log = new FileTransactionLog(config.Get("LogFolder", EnvironmentVars.Expand("$(AppPath)\\Messages\\Log")));
                        break;
                }

                // Initialize and start the queue engine.

                engine                = new MsgQueueEngine(store, log);
                engine.ConnectEvent  += new MsgQueueEngineDelegate(OnConnect);
                engine.EnqueueEvent  += new MsgQueueEngineDelegate(OnEnqueue);
                engine.DequeueEvent  += new MsgQueueEngineDelegate(OnDequeue);
                engine.PeekEvent     += new MsgQueueEngineDelegate(OnPeek);
                engine.ExpireEvent   += new MsgQueueEngineDelegate(OnExpire);
                engine.CommitEvent   += new MsgQueueEngineDelegate(OnCommit);
                engine.RollbackEvent += new MsgQueueEngineDelegate(OnRollback);
                engine.BkTaskEvent   += new MsgQueueEngineDelegate(OnBkTask);

                engine.Start(router, MsgQueueEngineSettings.LoadConfig(keyPrefix));
            }
            catch
            {
                if (engine != null)
                {
                    engine.Stop();
                    engine = null;
                }

                throw;
            }
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
            if (router == null)
                return;

            using (TimedLock.Lock(this))
            {
                if (engine != null)
                {
                    engine.Stop();
                    engine = null;
                }

                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }
            }
        }

        /// <summary>
        /// Called when a connection is established.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnConnect(object sender)
        {
            perf.SessionRate.Increment();
        }

        /// <summary>
        /// Called when a message is enqueued.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnEnqueue(object sender)
        {
            perf.Enqueued.Increment();
        }

        /// <summary>
        /// Called when a message is dequeued.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnDequeue(object sender)
        {
            perf.Dequeued.Increment();
        }

        /// <summary>
        /// Called when a message is peeked.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnPeek(object sender)
        {
            perf.Peeked.Increment();
        }

        /// <summary>
        /// Called when a message expires and is discarded.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnExpire(object sender)
        {
            perf.Expired.Increment();
        }

        /// <summary>
        /// Called when a transaction is committed.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnCommit(object sender)
        {
            perf.Commits.Increment();
        }

        /// <summary>
        /// Called when a transaction is rolled back.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnRollback(object sender)
        {
            perf.Rollbacks.Increment();
        }

        /// <summary>
        /// Implements background task processing.
        /// </summary>
        /// <param name="sender">The <see cref="MsgQueueEngine" />.</param>
        private void OnBkTask(object sender)
        {
            perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;
            perf.SessionCount.RawValue = engine.SessionCount;
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
