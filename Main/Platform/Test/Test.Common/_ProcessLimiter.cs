//-----------------------------------------------------------------------------
// FILE:        _ProcessLimiter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ProcessLimiter
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ProcessLimiter_Notify()
        {
            // Run the:
            //
            //      VEGOMATIC MEMORY ALLOC 500
            //
            // command to allocate 500MB of memory and then configure a
            // process limit of 250MB and verify that we get a notification.

            Process process = null;

            try
            {
                ProcessLimiter.PollInterval = TimeSpan.FromSeconds(1);

                var notified = false;
                process = Process.Start("vegomatic", "memory alloc 500");
                var limits = new ProcessLimits(process, (l, m) => { notified = true; }) { PagedMemorySize = 250 * 1024 * 1024 };

                ProcessLimiter.Add(limits);

                Helper.WaitFor(() => notified, TimeSpan.FromSeconds(30));
            }
            finally
            {
                ProcessLimiter.Reset();

                if (process != null && !process.HasExited)
                    process.Kill();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ProcessLimiter_AutoTerminate()
        {
            // Run the:
            //
            //      VEGOMATIC MEMORY ALLOC 500
            //
            // command to allocate 500MB of memory and then verify that
            // a process limiter will automatically terminate it.

            Process process = null;

            try
            {
                ProcessLimiter.PollInterval = TimeSpan.FromSeconds(1);
                ProcessLimiter.LogFlushInterval = TimeSpan.FromSeconds(1);

                process = Process.Start("vegomatic", "memory alloc 500");
                var limits = new ProcessLimits(process) { PagedMemorySize = 250 * 1024 * 1024 };

                ProcessLimiter.Add(limits);

                Helper.WaitFor(() => process.HasExited, TimeSpan.FromSeconds(30));
            }
            finally
            {
                ProcessLimiter.Reset();

                if (process != null && !process.HasExited)
                    process.Kill();
            }
        }
    }
}

