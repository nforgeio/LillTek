//-----------------------------------------------------------------------------
// FILE:        HeartbeatHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core functionality of the Heartbeat Service.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements the core functionality of the Heartbeat service which
    /// is designed to be used by load balancers to determine the health
    /// of a server and its local websites.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Application load balancers provided by Amazon Web Services,
    /// Netscaler, F5 and the like will be configured to perform health
    /// periodic health checks on the web servers managed by the load balancer.
    /// These checks typically take the form of an HTTP ping request to
    /// a specified port on the server.
    /// </para>
    /// <para>
    /// The LillTek Heartbeat service is designed to perform health checks
    /// for a specific web server and then respond to the load balancer's
    /// HTTP queries.  You simply need to configure the heartbeat service
    /// with the URIs for the websites to be tested on the local machine.
    /// These URIs should be fully qualified and include the host name
    /// that will be presented in requests to the load balancer and the
    /// port that will be presented in requests from the load balancer to
    /// the web server (this might differ from the global port if the
    /// load balancer is configured to do port translation).
    /// </para>
    /// <para>
    /// The heartbeat service works by polling the configured <b>MonitorUri</b> 
    /// settings on the the interval specified by <b>PollInterval</b> with a
    /// simple GET request that includes a <b>X-Health-Check: true</b> HTTP
    /// request header.  The heartbeat server will consider the monitored
    /// service to be unhealthy if the HTTP fails due to a network issue
    /// or if the HTTP response status code is anything but <b>OK (200)</b>.
    /// Monitored services can use the presence of the <b>X-Health-Check</b>
    /// header to avoid logging health related activites.
    /// </para>
    /// <para>
    /// Note that the HTTP health queries will be submitted to the local
    /// machine's loopback address <b>127.0.0.1</b> but that the <b>Host</b> header
    /// will be set to the host name specified in the monitor URI.  The heartbeat
    /// service does not perform a DNS lookup on the host name because it's
    /// designed to test the functioning of the site on the local machine
    /// rather than route the request back through the load balancer (which
    /// would be bad).
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, Heartbeat Service settings are prefixed by 
    /// <b>LillTek.Datacenter.HeartbeatService</b> (a custom prefix can be
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
    ///     <td>NetworkBinding</td>
    ///     <td>ANY:HTTP-HEARTBEAT</td>
    ///     <td>
    ///     Specifies the network binding the heartbeat service will use to
    ///     receive inbound HTTP health status requests.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Service[#]</td>
    ///     <td>1s</td>
    ///     <td>
    ///     <para>
    ///     Specifies the URIs of the services to be monitored and optionally, whether
    ///     the service each URI references should be considered critical when determining 
    ///     the health of the host server. The setting is formatted as:
    ///     </para>
    ///     <code lang="none">
    ///     &lt;uri&gt; [ "," ( "CRITICAL" | "NONCRITICAL" ) ]
    ///     </code>
    ///     <para>
    ///     Where the <b>CRITICAL</b> and <b>NONCRITICAL</b> attributes are optional.
    ///     If none of these are specified then <b>CRITICAL</b> will be assumed.
    ///     </para>
    ///     <para>
    ///     Note that for application URI's that implement the <b>Heartbeat.aspx</b> 
    ///     convention, you should probably specify the <b>global=0</b> query string 
    ///     parameter to avoid undue  performance impacts on global resources such 
    ///     as SQL Server database.  The URI should be something like: 
    ///     </para>
    ///     <para>
    ///     http://mysite.com:80/Heartbeat.aspx?global=0
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PollInterval</td>
    ///     <td>15s</td>
    ///     <td>
    ///     The interval between health checks.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BkInterval</td>
    ///     <td>1s</td>
    ///     <td>
    ///     Minimum interval for which background activities will be processed.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class HeartbeatHandler : IHttpModule, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string Queries_Name = "Queries/sec";
            const string Status_Name  = "Health Status";
            const string Runtime_Name = "Runtime (min)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "Number of health check queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Status_Name, "Health Status (1=healthy)", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Queries;    // # queries/sec
            public PerfCounter Status;     // health status
            public PerfCounter Runtime;    // Service runtime in minutes

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
                    Queries = perfCounters[perfPrefix + Queries_Name];
                    Status  = perfCounters[perfPrefix + Status_Name];
                    Runtime = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {
                    Queries =
                    Status  =
                    Runtime = PerfCounter.Stub;
                }
            }
        }

        /// <summary>
        /// Used to track the state of the monitored websites.
        /// </summary>
        private class MonitoredSite
        {
            public MonitoredService     Service;        // The service information
            public bool                 IsHealthy;      // True if the site is healthy
            public int                  StatusCode;     // HTTP status code,  0 for timeout, or -1 for unknown

            public MonitoredSite(MonitoredService service)
            {
                this.Service    = service;
                this.IsHealthy  = false;
                this.StatusCode = -1;
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
        public const string ConfigPrefix = "LillTek.Datacenter.HeartbeatService";

        /// <summary>
        /// <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.Heartbeat";

        private MsgRouter               router;             // The associated router (or null)
        private bool                    isRunning;          // True if the handler is running
        private object                  syncLock;           // Instance used for thread synchronization
        private Perf                    perf;               // Performance counters
        private DateTime                startTime;          // Time the service was started (UTC)
        private TimeSpan                bkInterval;         // Interval for background task execution
        private List<MonitoredSite>     services;           // Monitored service information
        private GatedTimer              bkTimer;            // Background task timer
        private NetworkBinding          binding;            // HTTP server binding
        private HttpServer              httpServer;         // HTTP server
        private PolledTimer             pollTimer;          // Fires when it's time to poll the monitored services
        private bool                    isHealthy;          // Health status

        /// <summary>
        /// Constructs a heartbeat service handler instance.
        /// </summary>
        public HeartbeatHandler()
        {
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router (or <c>null</c>).</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.Heartbeat</b>).</param>
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

            if (this.isRunning)
                throw new InvalidOperationException("This handler has already been started.");

            // Make sure the syncLock is set early.

            this.syncLock = router != null ? router.SyncRoot : this;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // General initialization

            this.isHealthy  = false;
            this.binding    = config.Get("NetworkBinding", new NetworkBinding("ANY:HTTP-HEARTBEAT"));
            this.bkInterval = config.Get("BkInterval", TimeSpan.FromSeconds(1));

            this.pollTimer  = new PolledTimer(config.Get("PollInterval", TimeSpan.FromSeconds(15)), false);
            this.pollTimer.FireNow();

            this.services = new List<MonitoredSite>();
            foreach (var v in config.GetArray("Service"))
            {
                try
                {
                    this.services.Add(new MonitoredSite(new MonitoredService(v)));
                }
                catch
                {
                    SysLog.LogWarning("Heartbeat Handler: Invalid service reference in configuration: [{0}]", v);
                }
            }

            // Initialize the performance counters

            this.startTime            = DateTime.UtcNow;
            this.perf                 = new Perf(perfCounters, perfPrefix);
            this.perf.Status.RawValue = isHealthy ? 1 : 0;

            try
            {
                // Indicate that we're running.

                this.isRunning = true;

                // Initialize the HTTP server

                this.httpServer = new HttpServer(new IPEndPoint[] { binding }, new IHttpModule[] { this }, 10, 10, 8192);
                this.httpServer.Start();

                // Initialize the router

                this.router = router;

                // Start a background timer.

                this.bkTimer = new GatedTimer(new TimerCallback(OnBkTask), null, bkInterval);
            }
            catch
            {
                Stop();
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
            if (!isRunning)
                return;

            using (TimedLock.Lock(syncLock))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (httpServer != null)
                {
                    httpServer.Stop();
                    httpServer = null;
                }

                router = null;
                isRunning = false;
            }
        }

        /// <summary>
        /// Implements the background tasks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            if (!isRunning)
                return;

            try
            {
                // Update the service run time.

                perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;

                if (pollTimer.HasFired)
                {
                    // Execute HTTP requests for all of the configured monitor URIs
                    // and then wait for them to return or timeout before setting the health status.
                    // Note that I'm configuring the request so the Host header will be set to
                    // the header in the URI but the request will actually be submitted to the
                    // local host.

                    try
                    {
                        if (services.Count == 0)
                            isHealthy = true;
                        else
                        {
                            bool fail = false;

                            for (int i = 0; i < this.services.Count; i++)
                            {
                                var site = services[i];

                                try
                                {
                                    using (var httpConnection = new HttpConnection(HttpOption.None))
                                    {
                                        HttpRequest request;
                                        HttpResponse response;

                                        request = new HttpRequest("GET", site.Service.Uri, null);
                                        request["X-Health-Check"] = "true";

                                        httpConnection.Connect(new IPEndPoint(IPAddress.Loopback, site.Service.Uri.Port));
                                        response = httpConnection.Query(request, SysTime.Now + TimeSpan.FromSeconds(5));

                                        if (response.Status != HttpStatus.OK)
                                        {
                                            if (site.IsHealthy)
                                            {
                                                if (site.Service.IsCritical)
                                                    SysLog.LogError("Critical local service [{0}] transitioned to unhealthy status with code [{1}={2}].", site.Service.Uri, response.Status, (int)response.Status);
                                                else
                                                    SysLog.LogWarning("Noncritical local service [{0}] transitioned to unhealthy status with code [{1}={2}].", site.Service.Uri, response.Status, (int)response.Status);
                                            }

                                            if (site.Service.IsCritical)
                                                fail = true;

                                            site.IsHealthy = false;
                                            site.StatusCode = (int)response.Status;
                                        }
                                        else
                                        {
                                            if (!site.IsHealthy)
                                            {
                                                if (site.Service.IsCritical)
                                                    SysLog.LogInformation("Critical local service [{0}] transitioned to healthy status.", site.Service.Uri);
                                                else
                                                    SysLog.LogInformation("Noncritical local service [{0}] transitioned to healthy status.", site.Service.Uri);
                                            }

                                            site.IsHealthy  = true;
                                            site.StatusCode = (int)response.Status;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (site.IsHealthy)
                                    {
                                        if (site.Service.IsCritical)
                                            SysLog.LogError("Critical local service [{0}] transitioned to unhealthy status with exception [{1}]: {2}.", site.Service.Uri, e.GetType().Name, e.Message);
                                        else
                                            SysLog.LogWarning("Noncritical local service [{0}] transitioned to unhealthy status with exception [{1}]: {2}.", site.Service.Uri, e.GetType().Name, e.Message);
                                    }

                                    if (site.Service.IsCritical)
                                        fail = true;

                                    site.IsHealthy  = false;
                                    site.StatusCode = 0;
                                }
                            }

                            isHealthy = !fail;
                        }
                    }
                    finally
                    {
                        pollTimer.Reset();
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                isHealthy = false;
            }
            finally
            {
                // Update the status performance counter.

                this.perf.Status.RawValue = isHealthy ? 1 : 0;
            }
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        //---------------------------------------------------------------------
        // IHttpModule implementation

        /// <summary>
        /// Called when HTTP requests are received on the internal HTTP server.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="request"></param>
        /// <param name="firstRequest"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        public HttpResponse OnRequest(HttpServer server, HttpRequest request, bool firstRequest, out bool close)
        {
            StringBuilder   sb = new StringBuilder(2048);
            HttpResponse    response;

            perf.Queries.Increment();

            close = true;

            if (isHealthy)
                response = new HttpResponse(HttpStatus.OK, "Server is healthy");
            else
                response = new HttpResponse(HttpStatus.ServiceUnavailable, "Server is unhealthy");

            if (services.Count == 0)
                sb.AppendFormat("[health=1, status-code=NA]: No services are configured to be monitored\r\n");
            else
            {
                foreach (var site in services)
                {
                    string status;

                    switch (site.StatusCode)
                    {
                        case -1:

                            status = "NA";
                            break;

                        case 0:

                            status = "TIMEOUT";
                            break;

                        default:

                            status = site.StatusCode.ToString();
                            break;
                    }

                    sb.AppendFormat("[healthy={0}, status-code={1}, {2}]: {3}\r\n", site.IsHealthy ? 1 : 0, status, site.Service.IsCritical ? "CRITICAL" : "NONCRITICAL", site.Service.Uri);
                }
            }

            response["Content-Type"] = "text";
            response.Content         = new BlockArray(Helper.ToUTF8(sb.ToString()));

            return response;
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
