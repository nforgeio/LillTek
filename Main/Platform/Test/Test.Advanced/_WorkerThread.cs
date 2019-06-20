//-----------------------------------------------------------------------------
// FILE:        _WorkerThread.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _WorkerThread
    {

        private int methodThreadID;
        private object methodArg;

        private object Method1(object arg)
        {
            methodThreadID = Thread.CurrentThread.ManagedThreadId;
            methodArg = arg;

            return arg;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WorkerThread_Basic()
        {
            WorkerThread worker = new WorkerThread();
            int thisThreadID = Thread.CurrentThread.ManagedThreadId;
            object result;

            try
            {
                result = worker.Invoke(new WorkerThreadMethod(Method1), "Hello World!");

                Assert.AreEqual("Hello World!", methodArg);
                Assert.AreEqual("Hello World!", result);
                Assert.AreNotEqual(thisThreadID, methodThreadID);
            }
            finally
            {
                worker.Close();
            }
        }

        private object Method2(object arg)
        {
            throw new ArgumentException("Hello");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WorkerThread_Exception()
        {
            WorkerThread worker = new WorkerThread();

            try
            {
                worker.Invoke(new WorkerThreadMethod(Method2), null);
                Assert.Fail("Expected an AssertException");
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentException).Name, e.GetType().Name);
                Assert.AreEqual("Hello", e.Message);
            }
            finally
            {
                worker.Close();
            }
        }

        private object Method3(object arg)
        {
            Thread.Sleep(2000);
            return null;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WorkerThread_Close()
        {
            WorkerThread worker = new WorkerThread();
            IAsyncResult ar;

            try
            {
                ar = worker.BeginInvoke(new WorkerThreadMethod(Method3), null, null, null);
                Thread.Sleep(500);
                worker.Close();
                worker.EndInvoke(ar);

                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(CancelException).Name, e.GetType().Name);
            }
            finally
            {
                worker.Close();
            }
        }

        private object Method4(object arg)
        {
            Thread.Sleep((int)arg);
            return SysTime.Now;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WorkerThread_Blocking()
        {
            WorkerThread worker = new WorkerThread();
            IAsyncResult ar1, ar2;
            DateTime start, finish1, finish2;

            try
            {
                // Invoke two operations, one that will wait 1 second
                // followed immediately by one that will wait another second.
                // The first operation should complete approximately one
                // second after starting and the second operation should
                // complete approximately two seconds after starting (because
                // it had to wait for the first operation to complete.

                start = SysTime.Now;
                ar1 = worker.BeginInvoke(new WorkerThreadMethod(Method4), 1050, null, null);
                ar2 = worker.BeginInvoke(new WorkerThreadMethod(Method4), 1050, null, null);

                finish1 = (DateTime)worker.EndInvoke(ar1);
                finish2 = (DateTime)worker.EndInvoke(ar2);

                Assert.IsTrue(finish1 - start >= TimeSpan.FromSeconds(1.0));
                Assert.IsTrue(finish2 - start >= TimeSpan.FromSeconds(2.0));
            }
            finally
            {
                worker.Close();
            }
        }
    }
}

