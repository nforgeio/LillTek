//-----------------------------------------------------------------------------
// FILE:        _SharedMem.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the SharedMem class.

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
    public unsafe class _SharedMem
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_Basic()
        {
            SharedMem mem1 = null;
            SharedMem mem2 = null;
            byte* pBuf;

            try
            {
                mem1 = new SharedMem();
                mem2 = new SharedMem();

                mem1.Open("Shared", 100, SharedMem.OpenMode.CREATE_ONLY);
                mem2.Open("Shared", 100, SharedMem.OpenMode.OPEN_ONLY);

                pBuf = mem1.Lock(true);
                for (int i = 0; i < 100; i++)
                    Assert.AreEqual(0, pBuf[i]);

                pBuf[0] = 77;
                pBuf[99] = 99;

                mem1.Unlock();

                pBuf = mem2.Lock(true);

                Assert.AreEqual(77, pBuf[0]);
                Assert.AreEqual(99, pBuf[99]);
            }
            finally
            {
                if (mem1 != null)
                    mem1.Close();

                if (mem2 != null)
                    mem2.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_CreateOpen()
        {
            SharedMem mem1 = null;
            SharedMem mem2 = null;
            byte* pBuf;

            try
            {
                mem1 = new SharedMem();
                mem2 = new SharedMem();

                mem1.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);
                mem2.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);

                pBuf = mem1.Lock(true);
                for (int i = 0; i < 100; i++)
                    Assert.AreEqual(0, pBuf[i]);

                pBuf[0] = 77;
                pBuf[99] = 99;

                mem1.Unlock();

                pBuf = mem2.Lock(true);

                Assert.AreEqual(77, pBuf[0]);
                Assert.AreEqual(99, pBuf[99]);
            }
            finally
            {
                if (mem1 != null)
                    mem1.Close();

                if (mem2 != null)
                    mem2.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_OpenFail()
        {
            SharedMem mem1 = null;

            try
            {
                mem1 = new SharedMem();
                mem1.Open("Shared", 100, SharedMem.OpenMode.OPEN_ONLY);
                Assert.Fail();
            }
            catch
            {
                // This is supposed to throw an exception
            }
            finally
            {
                if (mem1 != null)
                    mem1.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_CreateFail()
        {
            SharedMem mem1 = null;
            SharedMem mem2 = null;

            try
            {
                mem1 = new SharedMem();
                mem1.Open("Shared", 100, SharedMem.OpenMode.CREATE_ONLY);

                mem2 = new SharedMem();
                mem2.Open("Shared", 100, SharedMem.OpenMode.CREATE_ONLY);
                Assert.Fail();
            }
            catch
            {
                // This is supposed to throw an exception
            }
            finally
            {
                if (mem1 != null)
                    mem1.Close();

                if (mem2 != null)
                    mem2.Close();
            }
        }

        private SharedMem sm1;
        private SharedMem sm2;
        private Thread thread;
        private bool locked;
        private int a, b;

        private void LockThread()
        {
            sm2 = new SharedMem();

            try
            {
                sm2.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);
                locked = sm2.Lock(false) == null;
            }
            finally
            {
                sm2.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_Lock()
        {
            sm1 = null;
            sm2 = null;

            try
            {
                locked = false;
                thread = new Thread(new ThreadStart(LockThread));

                sm1 = new SharedMem();
                sm1.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);
                sm1.Lock(true);

                thread.Start();
                Thread.Sleep(1000);
                sm1.Unlock();
                thread.Join();

                Assert.IsTrue(locked);
            }
            finally
            {
                if (sm1 != null)
                    sm1.Close();
            }
        }

        private void WaitThread()
        {
            sm2 = new SharedMem();

            try
            {
                sm2.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);
                locked = sm2.Lock() != null;
                b = a;
                sm2.Unlock();
            }
            finally
            {
                sm2.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMem_Wait()
        {
            sm1 = null;
            sm2 = null;

            try
            {
                a = 0;
                b = 0;
                thread = new Thread(new ThreadStart(WaitThread));

                sm1 = new SharedMem();
                sm1.Open("Shared", 100, SharedMem.OpenMode.CREATE_OPEN);
                sm1.Lock(true);

                thread.Start();
                Thread.Sleep(1000);

                a = 1;
                sm1.Unlock();
                thread.Join();

                Assert.IsTrue(locked);
                Assert.AreEqual(1, b);
            }
            finally
            {
                if (sm1 != null)
                    sm1.Close();
            }
        }
    }
}

