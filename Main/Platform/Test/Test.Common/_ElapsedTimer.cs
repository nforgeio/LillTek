//-----------------------------------------------------------------------------
// FILE:        _ElapsedTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ElapsedTimer
    {
        private bool Compare(int milliseconds, TimeSpan elapsed)
        {
            // Return true if the elapsed time is within approximately a
            // timeslice or two (30ms) of the time in milliseconds passed.

            return milliseconds - 30.0 <= elapsed.TotalMilliseconds && elapsed.TotalMilliseconds <= milliseconds + 30.0;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_Basic()
        {
            ElapsedTimer timer;

            // Timer not started #1

            timer = new ElapsedTimer();
            Thread.Sleep(200);
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);
            Thread.Sleep(1000);
            timer.Stop();
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);

            // Timer not started #2

            timer = new ElapsedTimer(false);
            Thread.Sleep(200);
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);
            Thread.Sleep(1000);
            timer.Stop();
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);

            // Timer started manually

            timer = new ElapsedTimer(false);
            Thread.Sleep(200);
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);
            timer.Start();
            Thread.Sleep(1000);
            timer.Stop();
            Assert.IsTrue(Compare(1000, timer.ElapsedTime));

            // Timer started automatically

            timer = new ElapsedTimer(true);
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);
            timer.Start();
            Thread.Sleep(1000);
            timer.Stop();
            Assert.IsTrue(Compare(1000, timer.ElapsedTime));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_Reset()
        {
            ElapsedTimer timer;

            timer = new ElapsedTimer(true);
            Thread.Sleep(1000);
            timer.Stop();
            Assert.IsTrue(Compare(1000, timer.ElapsedTime));

            timer.Reset();
            Assert.AreEqual(TimeSpan.Zero, timer.ElapsedTime);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_MultiRun()
        {
            ElapsedTimer timer;

            timer = new ElapsedTimer(true);
            Thread.Sleep(1000);
            timer.Stop();

            Thread.Sleep(1000);

            timer.Start();
            Thread.Sleep(1000);
            timer.Stop();

            Assert.IsTrue(Compare(2000, timer.ElapsedTime));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_Restart()
        {
            ElapsedTimer timer;

            timer = new ElapsedTimer(true);
            Thread.Sleep(1000);
            timer.Stop();

            Thread.Sleep(1000);

            timer.Restart();
            Thread.Sleep(1000);
            timer.Stop();

            Assert.IsTrue(Compare(1000, timer.ElapsedTime));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_Using()
        {
            ElapsedTimer timer;

            timer = new ElapsedTimer(true);
            using (timer)
            {

                Thread.Sleep(1000);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(Compare(1000, timer.ElapsedTime));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ElapsedTimer_Running()
        {
            var timer = new ElapsedTimer(true);

            Thread.Sleep(1000);
            Assert.IsTrue(Compare(1000, timer.ElapsedTime));
            Thread.Sleep(1000);
            Assert.IsTrue(Compare(2000, timer.ElapsedTime));
        }
    }
}

