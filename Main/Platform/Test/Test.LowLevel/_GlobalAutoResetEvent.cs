//-----------------------------------------------------------------------------
// FILE:        _GlobalAutoResetEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the GlobalAutoResetEvent class.

using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.LowLevel;
using LillTek.Windows;

// $todo(jeff.lill): Flesh out this suite with more tests

namespace LillTek.LowLevel.Test
{
    [TestClass]
    public class _GlobalAutoResetEvent
    {

        private GlobalAutoResetEvent event1;
        private GlobalAutoResetEvent event2;
        private DateTime startTime;
        private DateTime finishTime;

        public void ThreadProc()
        {
            startTime = SysTime.Now;
            event2.WaitOne();
            finishTime = SysTime.Now;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void GlobalAutoResetEvent_Basic()
        {
#if WINFULL
            var thread = new Thread(new ThreadStart(ThreadProc));
#else
            var thread = new CEThread(new ThreadStart(ThreadProc));
#endif
            event1 = new GlobalAutoResetEvent("Test");
            event2 = new GlobalAutoResetEvent("Test");

            try
            {
                thread.Start();
                Thread.Sleep(1100);
                event1.Set();
                Thread.Sleep(1000);

                Assert.IsTrue(finishTime - startTime >= TimeSpan.FromMilliseconds(1000));
            }
            finally
            {
                event1.Close();
                event2.Close();
                thread.Join();
            }
        }
    }
}

