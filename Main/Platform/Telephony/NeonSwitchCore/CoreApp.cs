//-----------------------------------------------------------------------------
// FILE:        CoreApp.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The core NeonSwitch application entry point.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// The core NeonSwitch application entry point.
    /// </summary>
    /// <remarks>
    /// This application is installed and loaded into every NeonSwitch configuration
    /// to extend the base FreeSWITCH server with NeonSwitch specific commands.
    /// </remarks>
    public class CoreApp : SwitchApp
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the application's configuration settings instance.
        /// </summary>
        public static Config Config { get; private set; }

        /// <summary>
        /// Returns the server's performance counters.
        /// </summary>
        public static PerfCounterSet PerfCounters { get; set; }

        /// <summary>
        /// Returns the time interval to sleep in background tasks before checking again
        /// to see if there's work to be performed.
        /// </summary>
        public static TimeSpan BkTaskInterval { get; private set; }

        /// <summary>
        /// Returns the fully qualified path to the NeonSwitch application installation folder.
        /// </summary>
        public static string InstallPath { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static CoreApp()
        {
            // Initialize this to something reasonable so unit tests will work.
#if DEBUG
            CoreApp.InstallPath = EnvironmentVars.Expand(@"$(LT_ROOT)\Main\Platform\Telephony\NeonSwitchCore\bin\x64\Debug");
#else
            CoreApp.InstallPath = EnvironmentVars.Expand(@"$(LT_ROOT)\Main\Platform\Telephony\NeonSwitchCore\bin\x64\Release");
#endif
        }

        //---------------------------------------------------------------------
        // Instance members

        private SwitchServiceHost serviceHost;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CoreApp()
        {
        }

        /// <summary>
        /// Handles the performance counter installation if they don't
        /// already exist and then assigning the set to <see cref="CoreApp.PerfCounters" />.
        /// </summary>
        public static void InstallPerfCounters()
        {
            PerfCounters = CoreAppInstaller.InstallPerfCounters();
        }

        /// <summary>
        /// The main application entry point.
        /// </summary>
        protected override void Main()
        {
            // Initialize the environment.

            SysLog.LogProvider = new SwitchLogProvider();

            var assembly = Assembly.GetExecutingAssembly();

            Helper.InitializeApp(assembly);
            Config.SetConfigPath(assembly);
            CorePerf.Initialize();

            CoreApp.InstallPath = Helper.GetAssemblyFolder(Helper.GetEntryAssembly());

            // Initialize the global NeonSwitch related variables.

            Switch.SetGlobal(SwitchGlobal.NeonSwitchVersion, Build.Version);
            Switch.SetGlobal(SwitchGlobal.FreeSwitchVersion, Helper.GetVersionString(Assembly.GetAssembly(typeof(freeswitch))));

            // Load the application configuration settings.

            CoreApp.Config         = new Config("NeonSwitch");
            CoreApp.BkTaskInterval = CoreApp.Config.Get("BkTaskInterval", TimeSpan.FromSeconds(1));

            // Load the switch subcommand handlers.

            Switch.RegisterAssemblySubcommands(assembly);

            // Create a service host for the application service and launch it.

            var logProvider =
                new CompositeSysLogProvider(
                    new NativeSysLogProvider(SwitchConst.NeonSwitchName),
                    new SwitchLogProvider());

            serviceHost = new SwitchServiceHost();
            serviceHost.Initialize(new string[0], new CoreAppService(), logProvider, true);
        }

        /// <summary>
        /// Called just before the application is unloaded, giving it a chance to 
        /// perform a graceful shutdown.
        /// </summary>
        protected override void Close()
        {
            if (serviceHost != null)
                serviceHost.Service.Stop();
        }
    }
}
