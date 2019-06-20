//-----------------------------------------------------------------------------
// FILE:        _GlobalMutex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the GlobalMutex class.

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
    public class _GlobalMutex
    {
        private GlobalMutex mutex1;
        private GlobalMutex mutex2;
        private DateTime startTime;
        private DateTime finishTime;

        public void ThreadProc()
        {
            startTime = SysTime.Now;
            mutex2.WaitOne();
            finishTime = SysTime.Now;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void GlobalMutex_Basic()
        {
#if WINFULL
            var thread = new Thread(new ThreadStart(ThreadProc));
#else
            var thread = new CEThread(new ThreadStart(ThreadProc));
#endif
            mutex1 = new GlobalMutex("Test");
            mutex2 = new GlobalMutex("Test");

            try
            {
                mutex1.WaitOne();
                thread.Start();
                Thread.Sleep(1100);
                mutex1.ReleaseMutex();
                Thread.Sleep(1000);

                Assert.IsTrue(finishTime - startTime >= TimeSpan.FromMilliseconds(1000));
            }
            finally
            {
                mutex1.Close();
                mutex2.Close();
                thread.Join();
            }
        }
    }
}

