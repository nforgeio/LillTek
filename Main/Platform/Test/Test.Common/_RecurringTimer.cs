//-----------------------------------------------------------------------------
// FILE:        _RecurringTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _RecurringTimer
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Disabled()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Disabled, TimeSpan.FromSeconds(1));
            Assert.IsFalse(timer.HasFired(new DateTime(2010, 10, 23, 10, 10, 0)));   // Should never fire when disabled
            Assert.IsFalse(timer.HasFired(new DateTime(2010, 10, 24, 9, 0, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2010, 10, 24, 10, 1, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2010, 10, 24, 10, 1, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2010, 10, 25, 10, 1, 0)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Hourly()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Hourly, TimeSpan.FromMinutes(4));

            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 10, 0, 0)));    // Never fires on the first poll
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 08, 20, 10, 5, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 1, 0)));    // Still before offset
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 3, 0)));    // Still before offset
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 08, 20, 11, 4, 0)));     // Right at offset
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 4, 0)));    // Doesn't fire until the next hour
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 15, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 30, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 11, 55, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 08, 20, 12, 0, 0)));
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 08, 20, 12, 5, 0)));     // Just past the next firing time
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Daily()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(new TimeOfDay("10:00:00"));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 23, 10, 10, 0)));   // Verify that we don't fire until we see the transition
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 24, 9, 0, 0)));     // Still before the scheduled time
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));     // Should have seen the transition
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));    // Should be false now because we already handled this time
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 10, 25, 10, 1, 0)));     // Should fire for the next day

            timer = new RecurringTimer(RecurringTimerType.Daily, new TimeSpan(10, 0, 0));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 23, 10, 10, 0)));   // Verify that we don't fire until we see the transition
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 24, 9, 0, 0)));     // Still before the scheduled time
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));     // Should have seen the transition
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));    // Should be false now because we already handled this time
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 10, 25, 10, 1, 0)));     // Should fire for the next day
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Interval()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Interval, TimeSpan.FromSeconds(10));
            timer.Start(new DateTime(2011, 8, 26, 0, 0, 0));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 0)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 9)));
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 10)));
            Assert.IsFalse(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 19)));
            Assert.IsTrue(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 21)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Parse()
        {
            RecurringTimer timer;

            timer = new RecurringTimer("Disabled");
            Assert.AreEqual(RecurringTimerType.Disabled, timer.Type);

            timer = new RecurringTimer("Hourly,10");
            Assert.AreEqual(RecurringTimerType.Hourly, timer.Type);
            Assert.AreEqual("00:10:00", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Hourly,10:11");
            Assert.AreEqual(RecurringTimerType.Hourly, timer.Type);
            Assert.AreEqual("00:10:11", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Daily,10:11");
            Assert.AreEqual(RecurringTimerType.Daily, timer.Type);
            Assert.AreEqual("10:11:00", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Daily,10:11:12");
            Assert.AreEqual(RecurringTimerType.Daily, timer.Type);
            Assert.AreEqual("10:11:12", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Interval,10s");
            Assert.AreEqual(RecurringTimerType.Interval, timer.Type);
            Assert.AreEqual("00:00:10", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Interval,01:02:03");
            Assert.AreEqual(RecurringTimerType.Interval, timer.Type);
            Assert.AreEqual("01:02:03", timer.TimeOffset.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RecurringTimer_Config()
        {
            RecurringTimer timer;
            Config config;

            config = new Config();
            config.Add("test", "Daily,10:11:12");
            config.Add("invalid", "");

            timer = config.GetCustom<RecurringTimer>("test", RecurringTimer.Disabled);
            Assert.AreEqual(RecurringTimerType.Daily, timer.Type);
            Assert.AreEqual("10:11:12", timer.TimeOffset.ToString());

            timer = config.GetCustom<RecurringTimer>("not-found", RecurringTimer.Disabled);
            Assert.AreEqual(RecurringTimerType.Disabled, timer.Type);

            timer = config.GetCustom<RecurringTimer>("invalid", new RecurringTimer("Hourly,05:00"));
            Assert.AreEqual(RecurringTimerType.Hourly, timer.Type);
            Assert.AreEqual("00:05:00", timer.TimeOffset.ToString());
        }
    }
}

