//-----------------------------------------------------------------------------
// FILE:        AppService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The core NeonSwitch application service.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Data;
using LillTek.Messaging;
using LillTek.Service;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// The core NeonSwitch application service.
    /// </summary>
    public class CoreAppService : ServiceBase, IService
    {
        private object          syncLock = new object();
        private IServiceHost    serviceHost;    // The associated service host
        private LeafRouter      router;         // The message router
        private ServiceState    state;          // Current service state
        private DateTime        startTime;      // Service start time (UTC)
        private GatedTimer      bkTimer;        // Background task timer

        /// <summary>
        /// Constructor.
        /// </summary>
        public CoreAppService()
        {
            this.router  = null;
            this.state   = ServiceState.Stopped;
            this.bkTimer = null;

            // Prepare the service to accept remote ServiceControl commands.

            base.Open();
        }

        /// <summary>
        /// Returns the unique name of the service.  Note that this name must
        /// conform to the limitations of a Win32 file name.  It should not include
        /// any special characters such as colons, forward slashes (/), or back
        /// slashes (\) etc.  The name may include periods.
        /// </summary>
        public string Name
        {
            get { return SwitchConst.NeonSwitchName; }
        }

        /// <summary>
        /// Returns the display name of the service.
        /// </summary>
        public string DisplayName
        {
            get { return SwitchConst.NeonSwitchName; }
        }

        /// <summary>
        /// Returns human readable status text.  This will be called periodically
        /// by the service host user interface.
        /// </summary>
        public string DisplayStatus
        {
            get { return state.ToString(); }
        }

        /// <summary>
        /// Indicates how the service was started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return serviceHost.StartedAs; }
        }

        /// <summary>
        /// Returns the current state of the service.
        /// </summary>
        public ServiceState State
        {
            get { return state; }
        }

        /// <summary>
        /// Loads/reloads the service's configuration settings.
        /// </summary>
        public void Configure()
        {
            // Not implemented
        }

        /// <summary>
        /// Starts the service, associating it with an <see cref="IServiceHost" /> instance.
        /// </summary>
        /// <param name="serviceHost">The service user interface.</param>
        /// <param name="args">Command line arguments.</param>
        public void Start(IServiceHost serviceHost, string[] args)
        {
            lock (syncLock)
            {
                if (router != null)
                    return;     // Already started

                // Global initialization

                startTime = DateTime.UtcNow;

                NetTrace.Start();
                CoreApp.InstallPerfCounters();

                // Service initialization

                this.serviceHost = serviceHost;

                try
                {
                    SysLog.LogInformation("NeonSwitch v{0} Start", Helper.GetVersionString());

                    router = new LeafRouter();
                    router.Start();

                    state = ServiceState.Running;

                    bkTimer = new GatedTimer(OnBkTimer, null, CoreApp.BkTaskInterval);
                    SpeechEngine.Start(SpeechEngineSettings.LoadConfig("NeonSwitch.Speech"));
#if DEBUG
                    // $todo(jeff.lill): Delete this.

                    SwitchTest.Test();
#endif
                    // Indicate that the switch core service is open for business.  NeonSwitch
                    // application instance loaders will spin, waiting for this to be set before
                    // calling the application's main function.

                    Switch.SetGlobal(SwitchGlobal.NeonSwitchReady, "true");
                }
                catch (Exception e)
                {
                    SpeechEngine.Stop();

                    if (bkTimer != null)
                    {
                        bkTimer.Dispose();
                        bkTimer = null;
                    }

                    if (router != null)
                    {
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
        public void Shutdown()
        {
            SysLog.LogInformation("NeonSwitch Shutdown");
            serviceHost.OnShutdown(this);
        }

        /// <summary>
        /// Stops the service immediately, terminating any user activity.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (state == ServiceState.Stopped)
                    return;

                SysLog.LogInformation("NeonSwitch Stop");
                SysLog.Flush();

                base.Close();
                state = ServiceState.Stopped;

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (router != null)
                {
                    router.Stop();
                    router = null;
                }
            }
        }

        /// <summary>
        /// Handles background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTimer(object state)
        {
            // Update performance counters.

            CorePerf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;

            // Handle global background activities.

            SpeechEngine.OnBkTask();
        }
    }
}
