//-----------------------------------------------------------------------------
// FILE:        _GlobalLock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the GlobalLock class.

using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.LowLevel;
using LillTek.Windows;

namespace LillTek.LowLevel.Test
{
    [TestClass]
    public class _GlobalLock
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void GlobalLock_Lock()
        {
            using (GlobalLock lock1 = new GlobalLock("a"),
                              lock2 = new GlobalLock("a"))
            {
                Assert.IsFalse(lock1.IsHeld);
                Assert.IsFalse(lock2.IsHeld);

                lock1.Lock();
                Assert.IsTrue(lock1.IsHeld);
                Assert.IsFalse(lock2.IsHeld);
                lock1.Release();
                Assert.IsFalse(lock1.IsHeld);

                lock1.Lock();
                Assert.IsTrue(lock1.IsHeld);

                try
                {
                    lock2.Lock();
                    Assert.Fail();
                }
                catch (GlobalLockException)
                {
                    // I'm expecting this to fail
                }

                lock1.Lock();
                Assert.IsTrue(lock1.IsHeld);

                lock1.Release();
                Assert.IsTrue(lock1.IsHeld);

                try
                {
                    lock2.Lock();
                    Assert.Fail();
                }
                catch (GlobalLockException)
                {
                    // I'm expecting this to fail
                }

                lock1.Release();
                Assert.IsFalse(lock1.IsHeld);

                lock2.Lock();
                Assert.IsTrue(lock2.IsHeld);

                try
                {
                    lock1.Lock();
                    Assert.Fail();
                }
                catch (GlobalLockException)
                {
                    // I'm expecting this to fail
                }

                lock2.Release();
                Assert.IsFalse(lock2.IsHeld);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void GlobalLock_Multiple()
        {
            using (GlobalLock lock1 = new GlobalLock("a"),
                              lock2 = new GlobalLock("b"))
            {
                lock1.Lock();
                lock2.Lock();
                lock2.Release();
                lock1.Release();
            }
        }
    }
}

