//-----------------------------------------------------------------------------
// FILE:        CorePerf.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages core NeonSwitch performance counters.

using System;
using System.Diagnostics;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Manages core NeonSwitch performance counters.
    /// </summary>
    public static class CorePerf
    {
        // Performance counter names

        const string Runtime_Name       = "Runtime (min)";
        const string TotalCalls_Name    = "Total Calls";
        const string TotalCallRate_Name = "Total Call Rate";

        /// <summary>
        /// Installs the service's performance counters by adding them to the
        /// performance counter set passed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        public static void Install(PerfCounterSet perfCounters)
        {
            if (perfCounters == null)
                return;

            perfCounters.Add(new PerfCounter(Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            perfCounters.Add(new PerfCounter(TotalCalls_Name, "Total active calls", PerformanceCounterType.NumberOfItems32));
            perfCounters.Add(new PerfCounter(TotalCallRate_Name, "Total Call Rate", PerformanceCounterType.RateOfCountsPerSecond32));
        }

        //-----------------------------------------------------------------

        /// <summary>
        /// Service runtime in minutes.
        /// </summary>
        public static PerfCounter Runtime;

        /// <summary>
        /// Total number of active calls.
        /// </summary>
        public static PerfCounter TotalCalls;

        /// <summary>
        /// Total call originate/answer rate.
        /// </summary>
        public static PerfCounter TotalCallRate;

        /// <summary>
        /// Initializes the service's performance counters.
        /// </summary>
        public static void Initialize()
        {
            var perfCounters = new PerfCounterSet(false, true, SwitchConst.NeonSwitchPerf, SwitchConst.NeonSwitchName);

            Install(perfCounters);

            if (perfCounters != null)
            {
                Runtime       = perfCounters[Runtime_Name];
                TotalCalls    = perfCounters[TotalCalls_Name];
                TotalCallRate = perfCounters[TotalCallRate_Name];
            }
            else
            {
                Runtime       =
                TotalCalls    =
                TotalCallRate = PerfCounter.Stub;
            }
        }
    }
}
