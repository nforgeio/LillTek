//-----------------------------------------------------------------------------
// FILE:        _GatedTimer.cs
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
    public class _GatedTimer
    {
        GatedTimer timer;
        private int wait;
        private int count;
        private object state;
        private bool dispose;
        private int change;

        private void OnTimer(object state)
        {
            this.count++;
            this.state = state;

            Thread.Sleep(wait);

            if (dispose)
                timer.Dispose();

            if (change > 0)
                timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(change));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedTimer_Basic()
        {
            count = 0;
            state = null;
            wait = 1000;
            dispose = false;
            change = 0;
            timer = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            timer.Dispose();
            Assert.AreEqual(1, count);
            Assert.AreEqual(10, (int)state);

            count = 0;
            state = null;
            wait = 0;
            dispose = false;
            change = 0;
            timer = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            timer.Dispose();
            Assert.AreEqual(10, count);
            Assert.AreEqual(10, (int)state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedTimer_Dispose()
        {
            count = 0;
            state = null;
            wait = 0;
            dispose = true;
            change = 0;
            timer = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            Assert.AreEqual(1, count);
            Assert.AreEqual(10, (int)state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedTimer_Change()
        {
            count = 0;
            state = null;
            wait = 0;
            dispose = true;
            change = 2000;
            timer = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            timer.Dispose();
            Assert.AreEqual(1, count);
            Assert.AreEqual(10, (int)state);
        }
    }
}

