//-----------------------------------------------------------------------------
// FILE:        _PolledTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _PolledTimer
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PolledTimer_Normal()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(0.5));
            Assert.IsFalse(timer.HasFired);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
            Assert.IsTrue(timer.HasFired);

            timer.Reset();
            Assert.IsFalse(timer.HasFired);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
            Assert.IsTrue(timer.HasFired);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PolledTimer_ResetImmediate()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(0.5));
            timer.ResetImmediate();
            Assert.IsTrue(timer.HasFired);
            Assert.IsTrue(timer.HasFired);

            timer.Reset();
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
            Assert.IsTrue(timer.HasFired);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PolledTimer_AutoReset()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(0.5), true);
            Assert.IsFalse(timer.HasFired);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
            Assert.IsFalse(timer.HasFired);

            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
            Assert.IsFalse(timer.HasFired);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PolledTimer_FireNow()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(10), true);
            Assert.IsFalse(timer.HasFired);
            timer.FireNow();
            Assert.IsTrue(timer.HasFired);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void PolledTimer_Disable()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(0.5));
            Assert.IsFalse(timer.HasFired);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            timer.Disable();
            Assert.IsFalse(timer.HasFired);
            timer.Reset();
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);

            timer.Reset();
            Assert.IsFalse(timer.HasFired);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), timer.Interval);
            Assert.IsTrue(timer.FireTime >= SysTime.Now + timer.Interval);
            Thread.Sleep(1000);
            timer.Disable();
            Assert.IsFalse(timer.HasFired);
            timer.Interval = TimeSpan.FromSeconds(0.5);
            Thread.Sleep(1000);
            Assert.IsTrue(timer.HasFired);
        }
    }
}

