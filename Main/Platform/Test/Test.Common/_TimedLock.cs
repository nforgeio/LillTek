//-----------------------------------------------------------------------------
// FILE:        _TimedLock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the TimedLock class

using System;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _TimedLock : ILockable
    {
        private const int loopCount = 5000;

        private int count;
        private Exception lockException;
        private DateTime lockStartTime;
        private DateTime lockFailTime;
        private TimeSpan lockTime;

        private object lockKey = TimedLock.AllocLockKey();

        public object GetLockKey()
        {
            return lockKey;
        }

        private void LoopThread()
        {
            try
            {
                for (int i = 0; i < loopCount; i++)
                {
                    using (TimedLock.Lock(this))
                    {
                        int c;

                        c = count;
                        Thread.Sleep(0);
                        count = c + 1;
                    }
                }
            }
            catch (Exception e)
            {
                lockException = e;
            }
        }

        private void TimedBlockThread()
        {
            lockException = null;
            lockFailTime = DateTime.MaxValue;

            try
            {
                using (TimedLock.Lock(this, lockTime))
                {
                }
            }
            catch (Exception e)
            {
                lockException = e;
                lockFailTime = SysTime.Now;
            }
        }

        private void DefBlockThread()
        {
            lockException = null;
            lockFailTime = DateTime.MaxValue;

            try
            {
                using (TimedLock.Lock(this))
                {
                }
            }
            catch (Exception e)
            {
                lockException = e;
                lockFailTime = SysTime.Now;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_NoTrack_Lock()
        {
            Thread thread1, thread2, thread3, thread4;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = false;

            count = 0;
            lockException = null;
            thread1 = new Thread(new ThreadStart(LoopThread));
            thread2 = new Thread(new ThreadStart(LoopThread));
            thread3 = new Thread(new ThreadStart(LoopThread));
            thread4 = new Thread(new ThreadStart(LoopThread));

            try
            {
                using (TimedLock.Lock(this))
                {
                    thread1.Start();
                    thread2.Start();
                    thread3.Start();
                    thread4.Start();
                }

                thread1.Join();
                thread2.Join();
                thread3.Join();
                thread4.Join();

                Assert.IsNull(lockException);
                Assert.AreEqual(loopCount * 4, count);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_NoTrack_DeadLock_Default()
        {
            Thread thread;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = false;

            try
            {
                lockStartTime = SysTime.Now;
                using (TimedLock.Lock(this))
                {
                    thread = new Thread(new ThreadStart(DefBlockThread));
                    thread.Start();
                    thread.Join();
                }

                Assert.IsInstanceOfType(lockException, typeof(DeadlockException));
                Assert.IsTrue(lockFailTime - lockStartTime >= TimedLock.DefaultTimeout - SysTime.Resolution);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_NoTrack_DeadLock_Timeout()
        {
            Thread thread;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = false;

            try
            {
                lockTime = TimedLock.DefaultTimeout + TimeSpan.FromSeconds(1.0);
                lockStartTime = SysTime.Now;
                using (TimedLock.Lock(this))
                {
                    thread = new Thread(new ThreadStart(TimedBlockThread));
                    thread.Start();
                    thread.Join();
                }

                Assert.IsInstanceOfType(lockException, typeof(DeadlockException));
                Assert.IsTrue(lockFailTime - lockStartTime >= lockTime - SysTime.Resolution);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_Track_Lock()
        {
            Thread thread1, thread2, thread3, thread4;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = true;

            count = 0;
            lockException = null;
            thread1 = new Thread(new ThreadStart(LoopThread));
            thread2 = new Thread(new ThreadStart(LoopThread));
            thread3 = new Thread(new ThreadStart(LoopThread));
            thread4 = new Thread(new ThreadStart(LoopThread));

            try
            {
                using (TimedLock.Lock(this))
                {
                    thread1.Start();
                    thread2.Start();
                    thread3.Start();
                    thread4.Start();
                }

                thread1.Join();
                thread2.Join();
                thread3.Join();
                thread4.Join();

                Assert.IsNull(lockException);
                Assert.AreEqual(loopCount * 4, count);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_Track_DeadLock_Default()
        {
            Thread thread;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = true;

            try
            {
                lockStartTime = SysTime.Now;
                using (TimedLock.Lock(this))
                {
                    thread = new Thread(new ThreadStart(DefBlockThread));
                    thread.Start();
                    thread.Join();
                }

                Assert.IsInstanceOfType(lockException, typeof(DeadlockException));
                Assert.IsTrue(lockFailTime - lockStartTime >= TimedLock.DefaultTimeout - SysTime.Resolution);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        private void NoTrack_TimedBlockThread()
        {
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = false;

            lockException = null;
            lockFailTime = DateTime.MaxValue;

            try
            {
                using (TimedLock.Lock(this, lockTime))
                {
                }
            }
            catch (Exception e)
            {
                lockException = e;
                lockFailTime = SysTime.Now;
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_Track_DeadLock_Timeout()
        {
            Thread thread;
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = true;

            try
            {
                lockTime = TimedLock.DefaultTimeout + TimeSpan.FromSeconds(1.0);
                lockStartTime = SysTime.Now;
                using (TimedLock.Lock(this))
                {
                    thread = new Thread(new ThreadStart(TimedBlockThread));
                    thread.Start();
                    thread.Join();
                }

                Assert.IsInstanceOfType(lockException, typeof(DeadlockException));
                Assert.IsTrue(lockFailTime - lockStartTime >= lockTime - SysTime.Resolution);
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimedLock_AssertLocked()
        {
            bool diagMode;

            diagMode = TimedLock.FullDiagnostics;
            TimedLock.FullDiagnostics = true;

            try
            {
                try
                {
                    TimedLock.AssertLocked(this);
#if DEBUG
                    Assert.Fail("Expected an AssertException");
#endif
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(AssertException));
                }

                using (TimedLock.Lock(this))
                {
                    TimedLock.AssertLocked(this);
                }
            }
            finally
            {
                TimedLock.FullDiagnostics = diagMode;
            }
        }
    }
}

