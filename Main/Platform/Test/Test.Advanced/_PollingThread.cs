//-----------------------------------------------------------------------------
// FILE:        _PollingThread.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _PollingThread
    {
        private object arg;
        private int count;

        private void OnPoll(object arg)
        {

            this.arg = arg;
            this.count++;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PollingThread_Basic()
        {
            PollingThread thread;

            arg = null;
            count = 0;

            thread = new PollingThread(TimeSpan.FromSeconds(1));
            thread.Poll += new MethodArg1Invoker(OnPoll);
            thread.Start("Hello World");

            Thread.Sleep(5100);
            thread.Close(TimeSpan.FromSeconds(2));

            Assert.AreEqual(6, count);
            Assert.AreEqual("Hello World", arg);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PollingThread_Abort_Waiting()
        {
            PollingThread thread;
            DateTime start;

            arg = null;
            count = 0;

            thread = new PollingThread(TimeSpan.FromSeconds(10));
            thread.Poll += new MethodArg1Invoker(OnPoll);
            thread.Start(null);

            Thread.Sleep(100);
            start = SysTime.Now;
            thread.Close(TimeSpan.FromSeconds(1));
            Assert.IsTrue(SysTime.Now - start <= TimeSpan.FromSeconds(1.1));
        }

        private void OnPollSleep(object arg)
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PollingThread_Abort_Sleeping()
        {
            PollingThread thread;
            DateTime start;

            arg = null;
            count = 0;

            thread = new PollingThread(TimeSpan.FromSeconds(0.1));
            thread.Poll += new MethodArg1Invoker(OnPollSleep);
            thread.Start(null);

            Thread.Sleep(1000);
            start = SysTime.Now;
            thread.Close(TimeSpan.FromSeconds(1));
            Assert.IsTrue(SysTime.Now - start <= TimeSpan.FromSeconds(1.1));
        }

        private void OnPollWork(object arg)
        {
            while (true)
                count++;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PollingThread_Abort_Working()
        {
            PollingThread thread;
            DateTime start;

            arg = null;
            count = 0;

            thread = new PollingThread(TimeSpan.FromSeconds(0.1));
            thread.Poll += new MethodArg1Invoker(OnPollWork);
            thread.Start(null);

            Thread.Sleep(1000);
            start = SysTime.Now;
            thread.Close(TimeSpan.FromSeconds(1));
            Assert.IsTrue(SysTime.Now - start <= TimeSpan.FromSeconds(1.1));
        }
    }
}

