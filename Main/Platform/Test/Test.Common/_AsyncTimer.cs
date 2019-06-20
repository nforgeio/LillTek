//-----------------------------------------------------------------------------
// FILE:        _AsyncTimer.cs
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
    public class _AsyncTimer
    {
        private DateTime fireTime;
        private object state;

        private void OnTimer(IAsyncResult ar)
        {
            fireTime = SysTime.Now;
            state = ar.AsyncState;
            AsyncTimer.EndTimer(ar);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_Callback()
        {
            DateTime start = SysTime.Now;

            fireTime = start;
            AsyncTimer.BeginTimer(TimeSpan.FromSeconds(1), new AsyncCallback(OnTimer), "1");
            Thread.Sleep(2000);

            Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
            Assert.AreEqual("1", state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_Explicit_Wait()
        {
            DateTime start = SysTime.Now;
            object state;
            IAsyncResult ar;

            ar = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(1), null, "2");
            state = ar.AsyncState;

            ar.AsyncWaitHandle.WaitOne();
            AsyncTimer.EndTimer(ar);

            Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
            Assert.AreEqual("2", state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_Implicit_Wait()
        {
            DateTime start = SysTime.Now;
            object state;
            IAsyncResult ar;

            ar = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(1), null, "3");
            state = ar.AsyncState;

            AsyncTimer.EndTimer(ar);

            Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
            Assert.AreEqual("3", state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_PollInterval()
        {
            DateTime start = SysTime.Now;
            object state;
            IAsyncResult ar;

            ar = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(1), null, "4");
            state = ar.AsyncState;

            AsyncTimer.EndTimer(ar);

            Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
            Assert.AreEqual("4", state);

            AsyncTimer.PollInterval = TimeSpan.FromSeconds(1);

            start = SysTime.Now;
            ar = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(1), null, "5");
            state = ar.AsyncState;

            AsyncTimer.EndTimer(ar);

            Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
            Assert.AreEqual("5", state);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_CancelTimer()
        {
            DateTime start = SysTime.Now;
            IAsyncResult ar;

            ar = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(20), null, null);
            Thread.Sleep(5000);
            AsyncTimer.CancelTimer(ar);

            try
            {
                AsyncTimer.EndTimer(ar);
                Assert.Fail("Expected a CancelException");
            }
            catch (CancelException)
            {
            }

            Assert.IsTrue(SysTime.Now >= start + TimeSpan.FromSeconds(5) - SysTime.Resolution);
            Assert.IsTrue(SysTime.Now <= start + TimeSpan.FromSeconds(6) - SysTime.Resolution);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncTimer_CancelAll()
        {
            DateTime start = SysTime.Now;
            IAsyncResult ar1, ar2;

            ar1 = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(20), null, null);
            ar2 = AsyncTimer.BeginTimer(TimeSpan.FromSeconds(30), null, null);
            Thread.Sleep(5000);
            AsyncTimer.CancelTimer(ar1);
            AsyncTimer.CancelTimer(ar2);

            try
            {
                AsyncTimer.EndTimer(ar1);
                Assert.Fail("Expected a CancelException");
                AsyncTimer.EndTimer(ar2);
                Assert.Fail("Expected a CancelException");
            }
            catch (CancelException)
            {
            }

            Assert.IsTrue(SysTime.Now >= start + TimeSpan.FromSeconds(5) - SysTime.Resolution);
            Assert.IsTrue(SysTime.Now <= start + TimeSpan.FromSeconds(6) - SysTime.Resolution);
        }
    }
}

