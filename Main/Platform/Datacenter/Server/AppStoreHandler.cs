//-----------------------------------------------------------------------------
// FILE:        AppStoreHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core functionality provided by the Application Store service.

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

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements the core functionality provided by the Application Store service
    /// which is responsible for facilitating the deployment of application code
    /// across one or more data centers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The application store service is responsible for making application code available
    /// to servers that need to download and run it.  Application code consists of managed
    /// assembly files, traditional DLL or EXE files, as well as data files bundled into
    /// a standard format ZIP archive called an <b>Application Package</b>.  Application
    /// packages are typically managed via the <see cref="AppPackage" /> class.
    /// </para>
    /// <para>
    /// Individual application packages are identified via a specialized URI called
    /// an <b>application reference</b>application reference, or <b>appref</b>.
    /// URIs use the <b>appref://</b> scheme.  The host and URI segments form the
    /// unique application name, and the query parameters specify the package's
    /// version.  Note that an application is entirely abstract.  The host name
    /// does not refer to a particular DNS host and the port is ignored.  See
    /// <see cref="AppRef" /> for more information.
    /// </para>
    /// <example>
    /// <para>Here's a typical appref URI:</para>
    /// <blockquote>
    /// appref://MyApps/ServerSide/MyServerApp.zip?version=1.2.3.4
    /// </blockquote>
    /// </example>
    /// <para>
    /// The <see cref="AppStoreClient" /> class provides client side access to an
    /// application store by providing methods to upload and download packages
    /// from the store as well as the capability of caching packages on the
    /// local computer.
    /// </para>
    /// <para>
    /// Most applications will not use the <see cref="AppStoreHandler" /> and
    /// <see cref="AppStoreClient" /> classes directly but will instead rely on a
    /// local <b>LillTek Service Manager</b> to download, extract, and then run 
    /// application code on a command from a global <b>LillTek Director Service</b>.
    /// Here's an outline of how this works:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     The Director determines that some work needs to be performed.
    ///     </item>
    ///     <item>
    ///     The application code necessary to perform this work is identified
    ///     and a server is selected to perform the operation.
    ///     </item>
    ///     <item>
    ///     The Director commands the Service Manager on the selected server
    ///     to perform the work by sending the application code's appref
    ///     and any work specific parameters.
    ///     </item>
    ///     <item>
    ///     The Service Manager uses <see cref="AppStoreClient" /> to download
    ///     the application package from an application store, if the package
    ///     is not already cached locally.
    ///     </item>
    ///     <item>
    ///     The Service Manager extracts the files from the package to a 
    ///     new folder on the local machine and then runs the application.
    ///     </item>
    /// </list>
    /// <para>
    /// Application packages are loaded onto an application store either by manually
    /// copying the package ZIP file to the repository folder on the server or via
    /// a tool such as <b>Vegomatic</b>.  Application package file names in this
    /// repository must conform the value returned by the <see cref="AppRef" />
    /// class' <see cref="AppRef.FileName" /> property.  Here's an example of an appref URI 
    /// and the corresponding file name:
    /// </para>
    /// <example>
    /// <c>URI:  appref://MyApps/Server/MyApp.zip?version=1.2.3.4</c><br/>
    /// <c>File: myapps.server.myapp-0001.0002.0003.0004.zip</c>
    /// </example>
    /// <para><b><u>Application Store Endpoint</u></b></para>
    /// <para>
    /// Application store instances listen for requests on globally unique
    /// cluster member endpoints under <see cref="AppStoreClient.AbstractBaseEP" />
    /// which defaults to:
    /// </para>
    /// <blockquote><c>abstract://LillTek/DataCenter/AppStore</c></blockquote>
    /// <para>
    /// This can be modified by adding an entry to the <b>MsgRouter.AbstractMap</b>
    /// array in the application configuration file.
    /// </para>
    /// <para>
    /// Note that this endpoint is also used as the base endpoint for
    /// the application store cluster.
    /// </para>
    /// <para><b><u>Application Store Clusters</u></b></para>
    /// <para>
    /// A key goal of the application store cluster architecture is to provide
    /// an easy-to-manage and fault tolerant way to deploy application code
    /// across servers and data centers.
    /// </para>
    /// <para>
    /// Multiple application store instances can be organized into a cluster
    /// for scalability and failover.  This is implemented internally using the
    /// <see cref="ClusterMember" /> class.  One application store instance will
    /// be designated as the <b>Primary</b> store and the others will be
    /// designated as a <b>Cache</b>.  The primary store holds the definitive
    /// set of application packages.  New packages are published to the
    /// primary store.  The caching stores periodically synchronize the
    /// set of packages they each maintain to match the primary set.
    /// </para>
    /// <para>
    /// Here's a summary of how <see cref="AppStoreClient" /> and the application
    /// store cluster interact: 
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     <see cref="AppStoreClient" />Requests an application package by sending
    ///     a message with the appref to the application store endpoint.
    ///     </item>
    ///     <item>
    ///     If the primary store receives the request, it will initiate 
    ///     delivery of the package to the <see cref="AppStoreClient" />.
    ///     </item>
    ///     <item>
    ///     If a caching store receives the request and has the package in
    ///     its cache then it will initiate delivery of the package.  If it
    ///     does not have the package, it will first download the package
    ///     from the primary server before initiating delivery to the
    ///     <see cref="AppStoreClient" />.
    ///     </item>
    /// </list>
    /// <para>
    /// The idea here is to have multiple application store instances available
    /// within each data center that almost always have the up-to-date set of 
    /// application packages.  This architecture does a pretty respectable
    /// job of achieving this ideal.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, application store service settings are prefixed by 
    /// <b>LillTek.Datacenter.AppStore</b> (a custom prefix can be
    /// passed to <see cref="Start" /> if desired).  The available settings
    /// and their default values are described in the table below:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Mode</td>
    ///     <td>Primary</td>
    ///     <td>
    ///     Indicates whether the instance should operate as a primary
    ///     or caching store.  The possible values are <b>Primary</b>
    ///     and <b>Cache</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PackageFolder</td>
    ///     <td>Packages</td>
    ///     <td>
    ///     The path to the folder where the application packages are to be
    ///     stored.  This can be a path relative to the application's
    ///     installation folder or an absolute file system path.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PackageScanInterval</td>
    ///     <td>5m</td>
    ///     <td>
    ///     Specifies the frequency that the application store scans its
    ///     package folder for changes to the set of packages.  This defaults
    ///     to a relatively long time since the application also enlists in
    ///     file system events so it will pick up most changes to the
    ///     set of packages very quickly.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PrimaryPollInterval</td>
    ///     <td>15m</td>
    ///     <td>
    ///     This controls how often caching stores poll the primary for 
    ///     the current set of application packages.  Use <b>0</b> to
    ///     disable polling.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PrimaryBroadcast</td>
    ///     <td>yes</td>
    ///     <td>
    ///     Indicates whether the primary store should broadcast a change
    ///     notification to the cluster whenever a change is made to the
    ///     set of application packages.  Caching stores use this message
    ///     as a signal to synchronize their caches.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BkTaskInterval</td>
    ///     <td>1s</td>
    ///     <td>
    ///     The interval at which engine background tasks are scheduled.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Cluster</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     <b>Cluster</b> is a subsection in the configuration that
    ///     that specifies the settings required to establish a cooperative
    ///     cluster of application store instances on the network.  The application
    ///     store service uses the <see cref="ClusterMember" /> class to perform
    ///     the work necessary to join the cluster.  The <b>ClusterBaseEP</b>
    ///     setting is required.
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
    /// by application stores.
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
    ///     <td>Package Uploads</td>
    ///     <td>Rate</td>
    ///     <td>Application package uploads per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Package Deliveries</td>
    ///     <td>Rate</td>
    ///     <td>Application package deliveries per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Package Cache Loads</td>
    ///     <td>Rate</td>
    ///     <td>Application package downloads from the primary application store.</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class AppStoreHandler : ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string Uploads_Name    = "Package Uploads/sec";
            const string Deliveries_Name = "Package Deliveries/sec";
            const string CacheLoads_Name = "Package Cache Loads/sec";
            const string Runtime_Name    = "Runtime (min)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Uploads_Name, "Application executable packages received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Deliveries_Name, "Application executable packages deliveries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + CacheLoads_Name, "Application executable package cache loads/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Uploads;            // # package uploads/sec
            public PerfCounter Deliveries;         // # package deliveries/sec
            public PerfCounter CacheLoads;         // # package cache updates/sec
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
                    Uploads    = perfCounters[perfPrefix + Uploads_Name];
                    Deliveries = perfCounters[perfPrefix + Deliveries_Name];
                    CacheLoads = perfCounters[perfPrefix + CacheLoads_Name];
                    Runtime    = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {
                    Uploads    =
                    Deliveries =
                    CacheLoads =
                    Runtime    = PerfCounter.Stub;
                }
            }
        }

        /// <summary>
        /// Used to track a pending download so we can avoid downloading the same
        /// application package in parallel transfer sessions.
        /// </summary>
        private sealed class PendingDownload
        {
            public AppRef           AppRef;
            private OneTimeEvent    waitEvent;
            private Exception       exception;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="appRef"><see cref="AppRef" /> of the package being downloaded.</param>
            public PendingDownload(AppRef appRef)
            {
                this.AppRef    = appRef;
                this.waitEvent = new OneTimeEvent();
            }

            /// <summary>
            /// Wait for the package to be downloaded.
            /// </summary>
            public void Wait()
            {
                waitEvent.Wait();

                if (exception != null)
                    throw new Exception(exception.Message, exception);
            }

            /// <summary>
            /// Signals to waiting threads that the download has completed.
            /// </summary>
            /// <param name="e">non-<c>null</c> if there was an error.</param>
            public void Done(Exception e)
            {
                exception = e;
                waitEvent.Set();
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
        public const string ConfigPrefix = "LillTek.Datacenter.AppStore";

        private const string DynamicScope = "AppStoreHandler";

        private MsgRouter           router;                 // The associated router (or null if the handler is stopped).
        private object              syncLock;               // Instance used for thread synchronization
        private ClusterMember       cluster;                // AppStore cluster state
        private GatedTimer          bkTimer;                // The background task timer
        private Perf                perf;                   // Performance counters
        private DateTime            startTime;              // Time the service was started (UTC)
        private AppStoreMode        mode;                   // Operation mode (PRIMARY or CACHE)
        private TimeSpan            primaryPollInterval;    // Interval at which cache mode stores poll
                                                            // the primary server to synchronize package sets
        private DateTime            primaryPollTime;        // Next scheduled time to poll the primary (SYS)
        private bool                primaryBroadcast;       // Indicates whether the primary server should
                                                            // broadcast change notifications to the cluster
                                                            // for file system changes
        private TimeSpan            packageScanInterval;    // Interval between scanning the package folder for changes
        private DateTime            packageScanTime;        // Next scheduled time to scan for package changes (SYS)
        private AppPackageFolder    packageFolder;          // Maintains state about the package folder
        private AsyncCallback       onTransfer;             // Delegate called when package transfers complete
        private bool                forceSync;              // True if OnBkTimer() is to perform a one-time sync
                                                            // with the primary even if primaryPollTime=0
        private int                 cDownloads;             // # of successful downloads from this instance
        private bool                netFail;                // True to simulate a network failure

        // This table is used to track pending package downloads
        // so that we can avoid downloading the same package in
        // multiple simultaneous sessions.

        private Dictionary<AppRef, PendingDownload> downloads;

        //---------------------------------------
        // Clustering Implementation Note:
        //
        // Note that the one of the application store instances in the cluster needs
        // to be configured as the primary application store.  This is done by setting
        // the following configuration setting:
        //
        //      LillTek.Datacenter.AppStore = Primary
        //
        // The primary application store holds the definitive set of application
        // packages against which the non-primary store instances will synchronize
        // themselves.  Note that the primary application store is not necessarily
        // the cluster master.  These two concepts are independent.  The cluster master
        // will be elected normally from the pool of application store instances and
        // will be responsible for replicating cluster state global and instance state.
        //
        // At this point there is no global cluster state.  Each instance does expose
        // the following properties to the cluster:
        // 
        //      Mode        This is set to "Primary" or "Cache" and indicates which
        //                  application store instances are configured to be the 
        //                  primary instance (For this implementation, only one
        //                  instance should be configured as the primary).

        /// <summary>
        /// Constructs a application store service handler instance.
        /// </summary>
        public AppStoreHandler()
        {
            this.router   = null;
            this.syncLock = null;
            this.bkTimer  = null;
            this.cluster  = null;
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.AppStore</b>).</param>
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
            var config = new Config(keyPrefix != null ? keyPrefix : ConfigPrefix);

            // Make sure the syncLock is set early.

            this.syncLock = router.SyncRoot;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Verify the router parameter

            if (router == null)
                throw new ArgumentNullException("router", "Router cannot be null.");

            if (this.router != null)
                throw new InvalidOperationException("This handler has already been started.");

            // General initialization

            mode                = config.Get<AppStoreMode>("Mode", AppStoreMode.Primary);
            primaryBroadcast    = config.Get("PrimaryBroadcast", true);
            packageScanInterval = config.Get("PackageScanInterval", TimeSpan.FromMinutes(5));
            primaryPollInterval = config.Get("PrimaryPollInterval", TimeSpan.FromMinutes(15));
            primaryPollTime     = SysTime.Now;
            onTransfer          = new AsyncCallback(OnTransfer);
            downloads           = new Dictionary<AppRef, PendingDownload>();
            forceSync           = false;
            cDownloads          = 0;
            netFail             = false;

            // Initialize the package folder

            packageFolder              = new AppPackageFolder(syncLock, config.Get("PackageFolder", "Packages"));
            packageFolder.ChangeEvent += new MethodArg1Invoker(OnPackageFolderChange);
            packageScanTime            = SysTime.Now;

            // Initialize the performance counters

            startTime = DateTime.UtcNow;
            perf      = new Perf(perfCounters, perfPrefix);

            // Crank up the background task timer.

            bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, config.Get("BkTaskInterval", TimeSpan.FromSeconds(1)));

            try
            {
                // Initialize the router

                this.router = router;

                // Join the cluster, initializing this instance's state.

                cluster         = new ClusterMember(router, ClusterMemberSettings.LoadConfig(config.KeyPrefix + "Cluster"));
                cluster["Mode"] = this.mode.ToString();

                cluster.ClusterStatusUpdate += new ClusterMemberEventHandler(OnClusterStatusUpdate);
                cluster.Start();

                // Rather than calling cluster.JoinWait() which could take a really long
                // time, I'm going to sleep for two seconds.  There are three scenarios:
                //
                //      1. This is the first Application Store instance.
                //
                //      2. Other instances are running but they haven't
                //         organized into a cluster.
                //
                //      3. A cluster is already running.
                //
                // If #1 is the current situation, then it will take a very long time
                // for JoinWait() to return because we have to go through the entire
                // missed master broadcast and election periods.  Since we're the only
                // instance, we could have started serving content well before this.
                //
                // #2 won't be very common but if it is the case, the worst thing
                // that will happen is that it will take a while to discover the
                // primary store.
                //
                // If #3 is the case, then two seconds should be long enough for the
                // master to send the instance a cluster update.

                Thread.Sleep(2000);

                // Register the message handlers via the cluster member 
                // so that the endpoint used will be the member's instanceEP.

                cluster.AddTarget(this, AppStoreHandler.DynamicScope);
            }
            catch
            {
                if (packageFolder != null)
                {
                    packageFolder.Dispose();
                    packageFolder = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                router.Dispatcher.RemoveTarget(this);

                if (cluster != null)
                {
                    cluster.Stop();
                    cluster = null;
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

            using (TimedLock.Lock(syncLock))
            {
                if (packageFolder != null)
                {
                    packageFolder.Dispose();
                    packageFolder = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (cluster != null)
                {
                    cluster.Stop();
                    cluster = null;
                }

                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }
            }
        }

        /// <summary>
        /// Called when the set of packages in the package folder changes.
        /// </summary>
        /// <param name="sender">The package folder.</param>
        private void OnPackageFolderChange(object sender)
        {
            if (mode == AppStoreMode.Primary && primaryBroadcast && router != null)
                router.BroadcastTo(AppStoreClient.AbstractClusterEP, new AppStoreMsg(AppStoreMsg.SyncCmd));
        }

        /// <summary>
        /// Returns the <see cref="ClusterMember" /> class used by this instance
        /// to implement clustering support.
        /// </summary>
        public ClusterMember Cluster
        {
            get { return cluster; }
        }

        /// <summary>
        /// Scans the package folder for changes.
        /// </summary>
        public void Scan()
        {
            using (TimedLock.Lock(syncLock))
                packageFolder.Scan();

        }

        /// <summary>
        /// Purges abandoned files from the package folder.
        /// </summary>
        public void Purge()
        {
            using (TimedLock.Lock(syncLock))
                packageFolder.Purge();
        }

        /// <summary>
        /// Synchronizes the package folder with the primary
        /// application store.
        /// </summary>
        public void Sync()
        {
            SyncWithPrimary();
        }

        /// <summary>
        /// Available by unit test to manually enable or disable
        /// primary application store polling.
        /// </summary>
        internal bool PrimaryBroadcast
        {
            get { return primaryBroadcast; }
            set { primaryBroadcast = value; }
        }

        /// <summary>
        /// Available by unit tests to manually set the primary polling
        /// interval.  Use <see cref="TimeSpan.Zero" /> to disable polling.
        /// </summary>
        internal TimeSpan PrimaryPollInterval
        {
            get { return primaryPollInterval; }

            set
            {
                primaryPollInterval = value;
                if (primaryPollInterval >= TimeSpan.Zero)
                    primaryPollTime = SysTime.Now;
            }
        }

        /// <summary>
        /// Available for unit tests to directly access the application
        /// store's <see cref="AppPackageFolder" />.
        /// </summary>
        internal AppPackageFolder PackageFolder
        {
            get { return packageFolder; }
        }

        /// <summary>
        /// Available for unit tests to determine the number of packages
        /// downloaded from the store.
        /// </summary>
        internal int DownloadCount
        {
            get { return cDownloads; }
            set { cDownloads = value; }
        }

        /// <summary>
        /// Available for unit tests to simulate a network failure for
        /// this instance.  Set this to <c>true</c> to take the instance
        /// offline by ignoring all non-cluster message traffic.
        /// </summary>
        internal bool NetFail
        {
            get { return netFail; }
            set { netFail = true; }
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <param name="o">Not used.</param>
        private void OnBkTimer(object o)
        {
            bool pollPrimary;
            bool packageScan;

            perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;

            using (TimedLock.Lock(syncLock))
            {
                if (forceSync)
                {
                    pollPrimary = true;
                    forceSync = false;
                }
                else
                    pollPrimary = primaryPollInterval > TimeSpan.Zero && SysTime.Now >= primaryPollTime;

                packageScan = SysTime.Now <= packageScanTime;
            }

            if (pollPrimary)
                SyncWithPrimary();

            if (packageScan)
            {
                try
                {
                    packageFolder.Scan();
                    packageFolder.Purge();
                }
                catch
                {
                    // Ignore errors
                }
            }

            using (TimedLock.Lock(syncLock))
            {
                if (pollPrimary)
                    primaryPollTime = SysTime.Now + primaryPollInterval;

                if (packageScan)
                    packageScanTime = SysTime.Now + packageScanInterval;
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
        /// This is called by the <see cref="ClusterMember" /> instance when the
        /// master's cluster status broadcast is received.
        /// </summary>
        /// <param name="sender">The sending cluster member.</param>
        /// <param name="args">The event arguments.</param>
        private void OnClusterStatusUpdate(ClusterMember sender, ClusterMemberEventArgs args)
        {
            // This is a NOP for now.
        }

        /// <summary>
        /// Returns the <see cref="MsgEP" /> of the primary application store in the
        /// cluster if there is one, <c>null</c> otherwise.
        /// </summary>
        public MsgEP PrimaryEP
        {
            get
            {
                var status = cluster.ClusterStatus;

                for (int i = 0; i < status.Members.Count; i++)
                {
                    if ((AppStoreMode)Enum.Parse(typeof(AppStoreMode), status.Members[i]["mode"]) == AppStoreMode.Primary)
                        return status.Members[i].InstanceEP;
                }

                return null;
            }
        }

        /// <summary>
        /// Downloads an application package from a remote application store instance.
        /// </summary>
        /// <param name="storeEP">The application store endpoint.</param>
        /// <param name="appRef">The <see cref="AppRef" /> for the file.</param>
        /// <param name="path">The path to the output application package file.</param>
        private void DownloadPackage(MsgEP storeEP, AppRef appRef, string path)
        {
            StreamTransferSession session;

            session      = StreamTransferSession.ClientDownload(router, storeEP, path);
            session.Args = "appref=" + appRef.ToString();

            session.Transfer();
        }

        /// <summary>
        /// Returns information about the application packages currently
        /// hosted by an application store.
        /// </summary>
        /// <param name="storeEP">The application store endpoint.</param>
        /// <returns>
        /// An array of <see cref="AppPackageInfo" /> instances describing the
        /// available packages.
        /// </returns>
        private AppPackageInfo[] ListRemotePackages(MsgEP storeEP)
        {
            return ((AppStoreAck)router.Query(storeEP, new AppStoreQuery(AppStoreQuery.ListCmd))).Packages;
        }

        /// <summary>
        /// Synchronizes the set of application packages held by this instance with
        /// the primary application store.
        /// </summary>
        private void SyncWithPrimary()
        {
            MsgEP primaryEP;

            if (mode == AppStoreMode.Primary)
                return;     // This is the primary instance

            primaryEP = this.PrimaryEP;
            if (primaryEP == null)
                return;     // There is no known primary

            // Synchronize

            Dictionary<AppRef, AppPackageInfo> primaryPackages;
            Dictionary<AppRef, AppPackageInfo> localPackages;

            primaryPackages = new Dictionary<AppRef, AppPackageInfo>();
            foreach (AppPackageInfo info in ListRemotePackages(primaryEP))
                primaryPackages.Add(info.AppRef, info);

            localPackages = new Dictionary<AppRef, AppPackageInfo>();
            foreach (AppPackageInfo info in packageFolder.GetPackages())
                localPackages.Add(info.AppRef, info);

            // Delete any packages present in the local folder that
            // are not present on the primary.

            foreach (AppPackageInfo info in localPackages.Values)
                if (!primaryPackages.ContainsKey(info.AppRef))
                    packageFolder.Remove(info.AppRef);

            // Download any packages found on the primary that are
            // not present locally or whose MD5 hash differs from
            // the local file.

            foreach (AppPackageInfo info in primaryPackages.Values)
            {
                bool            downloadPackage = false;
                AppPackageInfo  localInfo;

                if (!localPackages.TryGetValue(info.AppRef, out localInfo))
                    downloadPackage = true;
                else
                    downloadPackage = !Helper.ArrayEquals(info.MD5, localInfo.MD5);

                if (downloadPackage)
                {
                    StreamTransferSession   session;
                    string                  path;

                    path = packageFolder.BeginTransit(info.AppRef);

                    try
                    {
                        session      = StreamTransferSession.ClientDownload(router, primaryEP, path);
                        session.Args = "appref=" + info.AppRef.ToString();
                        session.Transfer();
                        packageFolder.EndTransit(path, true);
                    }
                    catch (Exception e)
                    {
                        packageFolder.EndTransit(path, false);

                        // I'm going to log the exception and then continue
                        // downloading any remaining packages.

                        SysLog.LogException(e);
                    }
                }
            }
        }

        //---------------------------------------------------------------------
        // Message handlers

        /// <summary>
        /// Handles the query/response messages.
        /// </summary>
        /// <param name="msg">The received message.</param>
        [MsgHandler(LogicalEP = MsgEP.Null, DynamicScope = DynamicScope)]
        [MsgSession(Type = SessionTypeID.Query)]
        public void OnMsg(AppStoreQuery msg)
        {
            if (netFail)
                return;

            var ack = new AppStoreAck();

            try
            {
                switch (msg.Command)
                {
                    case AppStoreQuery.GetPrimaryCmd:

                        ack.StoreEP = this.PrimaryEP;
                        break;

                    case AppStoreQuery.ListCmd:

                        ack.Packages = packageFolder.GetPackages();
                        break;

                    case AppStoreQuery.RemoveCmd:

                        packageFolder.Remove(msg.AppRef);
                        break;

                    case AppStoreQuery.SyncCmd:

                        using (TimedLock.Lock(syncLock))
                        {
                            this.forceSync = true;
                            this.primaryPollTime = SysTime.Now;
                        }
                        break;

                    case AppStoreQuery.DownloadCmd:

                        ack.StoreEP = cluster.InstanceEP;

                        MsgEP           primaryEP;
                        PendingDownload pending;

                        using (TimedLock.Lock(syncLock))
                        {
                            if (packageFolder.GetPackageInfo(msg.AppRef) != null)
                                break;  // The package is ready for downloading

                            if (mode == AppStoreMode.Primary || this.PrimaryEP == null)
                                throw SessionException.Create(null, "Package [{0}] cannot be found.", msg.AppRef);

                            primaryEP = this.PrimaryEP;
                            downloads.TryGetValue(msg.AppRef, out pending);
                        }

                        if (pending != null)
                        {
                            // A download is already pending for this package so wait
                            // for it to complete.

                            pending.Wait();
                        }
                        else
                        {
                            // Try downloading the requested package from the primary 
                            // application store.

                            string transitPath = null;

                            pending = new PendingDownload(msg.AppRef);
                            using (TimedLock.Lock(syncLock))
                                downloads.Add(msg.AppRef, pending);

                            try
                            {
                                transitPath = packageFolder.BeginTransit(msg.AppRef);
                                DownloadPackage(primaryEP, msg.AppRef, transitPath);
                                packageFolder.EndTransit(transitPath, true);

                                pending.Done(null);
                            }
                            catch (Exception e)
                            {
                                packageFolder.EndTransit(transitPath, false);
                                pending.Done(e);
                                throw;
                            }
                            finally
                            {
                                using (TimedLock.Lock(syncLock))
                                {
                                    try
                                    {
                                        downloads.Remove(msg.AppRef);
                                    }
                                    catch
                                    {
                                        // Ignore errors
                                    }
                                }
                            }
                        }
                        break;

                    default:

                        throw SessionException.Create("Unexpected AppStore command [{0}].", msg.Command);
                }
            }
            catch (Exception e)
            {
                ack.Exception = e.Message;
                SysLog.LogException(e);
            }

            router.ReplyTo(msg, ack);
        }

        /// <summary>
        /// Handles the broadcast messages.
        /// </summary>
        /// <param name="msg">The received message.</param>
        [MsgHandler(LogicalEP = MsgEP.Null, DynamicScope = DynamicScope)]
        [MsgSession(Type = SessionTypeID.Query)]
        public void OnMsg(AppStoreMsg msg)
        {
            if (netFail)
                return;

            try
            {
                switch (msg.Command)
                {
                    case AppStoreQuery.RemoveCmd:

                        packageFolder.Remove(msg.AppRef);
                        break;

                    case AppStoreQuery.SyncCmd:

                        using (TimedLock.Lock(syncLock))
                        {
                            this.forceSync = true;
                            this.primaryPollTime = SysTime.Now;
                        }
                        break;

                    default:

                        throw SessionException.Create("Unexpected AppStore command [{0}].", msg.Command);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        private sealed class TransferInfo
        {
            public StreamTransferSession    Session;
            public string                   Path;
            public TransferDirection        Direction;

            public TransferInfo(StreamTransferSession session, string path, TransferDirection direction)
            {
                this.Session   = session;
                this.Path      = path;
                this.Direction = direction;
            }
        }

        /// <summary>
        /// Called when a transfer completes.
        /// </summary>
        /// <param name="ar">The operation's <see cref="IAsyncResult" /> instance.</param>
        private void OnTransfer(IAsyncResult ar)
        {
            var info = (TransferInfo)ar.AsyncState;

            try
            {
                if (info.Direction == TransferDirection.Upload)
                {
                    try
                    {
                        info.Session.EndTransfer(ar);
                        packageFolder.EndTransit(info.Path, true);
                    }
                    catch
                    {
                        packageFolder.EndTransit(info.Path, false);
                        throw;
                    }
                }
                else
                {
                    info.Session.EndTransfer(ar);
                    cDownloads++;
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Handles the uploading and downloading of application package files.
        /// </summary>
        /// <param name="msg"></param>
        [MsgHandler(LogicalEP = MsgEP.Null, DynamicScope = AppStoreHandler.DynamicScope)]
        [MsgSession(Type = SessionTypeID.ReliableTransfer)]
        public void OnMsg(ReliableTransferMsg msg)
        {
            if (netFail)
                return;

            StreamTransferSession   session;
            ArgCollection           args;
            AppRef                  appRef;
            AppPackageInfo          packageInfo;
            string                  path;

            args   = ArgCollection.Parse(msg.Args);
            appRef = AppRef.Parse(args["appref"]);

            if (msg.Direction == TransferDirection.Upload)
            {
                path    = packageFolder.BeginTransit(appRef);
                session = StreamTransferSession.ServerUpload(router, msg, path);
                session.BeginTransfer(onTransfer, new TransferInfo(session, path, msg.Direction));
            }
            else
            {
                packageInfo = packageFolder.GetPackageInfo(appRef);
                if (packageInfo == null)
                    throw SessionException.Create(null, "Package [{0}] cannot be found.", appRef);

                path    = packageInfo.FullPath;
                session = StreamTransferSession.ServerDownload(router, msg, path);
                session.BeginTransfer(onTransfer, new TransferInfo(session, path, msg.Direction));
            }
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
