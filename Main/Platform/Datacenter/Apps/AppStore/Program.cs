//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Main application entry point.

using System;
using System.Threading;
using System.Reflection;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Service;

namespace LillTek.Datacenter.AppStore
{
    /// <summary>
    /// Main application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the application's configuration settings.
        /// </summary>
        public static Config Config { get; set; }

        /// <summary>
        /// Returns the server's performance counters.
        /// </summary>
        public static PerfCounterSet PerfCounters { get; set; }

        /// <summary>
        /// Handles the performance counter installation if they don't
        /// already exist and then assigning the set to <see cref="Program.PerfCounters" />.
        /// </summary>
        public static void InstallPerfCounters()
        {
            PerfCounters = AppInstaller.InstallPerfCounters();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        static void Main(string[] args)
        {
            Helper.Init(Assembly.GetExecutingAssembly());
            Config.SetConfigPath(Helper.GetEntryAssembly());

            args = CommandLine.ExpandFiles(args);
            ServiceHost.Run(new AppService(), null, args);
        }
    }
}
