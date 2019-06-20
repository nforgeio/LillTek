//-----------------------------------------------------------------------------
// FILE:        AppService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the GeoTracker Service.

using System;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Service;
using LillTek.Messaging;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Service {

    /// <summary>
    /// Implements the GeoTracker Service.
    /// </summary>
    public sealed class AppService : ServiceBase, IService {

        private object                  syncLock = new object();
        private IServiceHost            serviceHost;    // The associated service host
        private LeafRouter              router;         // The message router
        private GeoTrackerNode          node;           // The GeoTracker service node
        private ServiceState            state;          // Current service state

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppService() {

            this.router = null;
            this.node   = null;
            this.state  = ServiceState.Stopped;

            // Prepare the service to accept remote ServiceControl commands.

            base.Open();
        }

        /// <summary>
        /// Returns the unique name of the service.  Note that this name must
        /// conform to the limitations of a Win32 file name.  It should not include
        /// any special characters such as colons, forward slashes (/), or back
        /// slashes (\) etc.  The name may include periods.
        /// </summary>
        public string Name {

            get { return Const.GeoTrackerName; }
        }

        /// <summary>
        /// Returns the display name of the service.
        /// </summary>
        public string DisplayName {

            get { return Const.GeoTrackerName; }
        }

        /// <summary>
        /// Returns human readable status text.  This will be called periodically
        /// by the service host user interface.
        /// </summary>
        public string DisplayStatus {

            get {return state.ToString();}
        }

        /// <summary>
        /// Indicates how the service was started.
        /// </summary>
        public StartAs StartedAs {

            get {return serviceHost.StartedAs;}
        }

        /// <summary>
        /// Returns the current state of the service.
        /// </summary>
        public ServiceState State {

            get {return state;}
        }

        /// <summary>
        /// Loads/reloads the service's configuration settings.
        /// </summary>
        public void Configure() {

            // Not implemented
        }

        /// <summary>
        /// Starts the service, associating it with an <see cref="IServiceHost" /> instance.
        /// </summary>
        /// <param name="serviceHost">The service user interface.</param>
        /// <param name="args">Command line arguments.</param>
        public void Start(IServiceHost serviceHost,string[] args) {

            lock (syncLock) {

                if (router != null)
                    return;     // Already started

                // Global initialization

                NetTrace.Start();
                Program.Config = new Config("LillTek.GeoTracker.Service");
                Program.InstallPerfCounters();

                // Service initialization

                this.serviceHost = serviceHost;
                SysLog.LogInformation("GeoTracker Service v{0} Start",Helper.GetVersionString());

                try {

                    router = new LeafRouter();
                    router.Start();

                    node = new GeoTrackerNode();
                    node.Start(router,GeoTrackerNode.ConfigPrefix,Program.PerfCounters,null);
                    
                    state = ServiceState.Running;
                }
                catch (Exception e) {

                    if (node != null) {

                        node.Stop();
                        node = null;
                    }

                    if (router != null) {

                        router.Stop();
                        router = null;
                    }

                    SysLog.LogException(e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Begins a graceful shut down process by disallowing any new user
        /// connections and monitoring the users still using the system.
        /// Once the last user has disconnected, the service will call the
        /// associated service host's OnShutdown() method.
        /// </summary>
        public void Shutdown() {

            SysLog.LogInformation("GeoTracker Service Shutdown");
            serviceHost.OnShutdown(this);
        }

        /// <summary>
        /// Stops the service immediately, terminating any user activity.
        /// </summary>
        public void Stop() {

            lock (syncLock) {

                if (state == ServiceState.Stopped)
                    return;

                SysLog.LogInformation("GeoTracker Service Stop");
                SysLog.Flush();

                base.Close();
                state = ServiceState.Stopped;
            }

            if (node != null) {

                node.Stop();
                node = null;
            }

            if (router != null) {

                router.Stop();
                router = null;
            }

            Program.PerfCounters.Zero();
        }
    }
}

