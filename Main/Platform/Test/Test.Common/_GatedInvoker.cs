//-----------------------------------------------------------------------------
// FILE:        _GatedInvoker.cs
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
    public class _GatedInvoker
    {
        private object[] args;

        private void Method1(int i, int j, string s)
        {
            args = new object[] { i, j, s };
        }

        public string Method2(int i, int j, string s)
        {
            args = new object[] { i, j, s };
            return "foobar";
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedInvoker_Invoke_ReturnsVoid()
        {
            var invoker = new GatedInvoker(this);
            object result;

            result = invoker.Invoke("Method1", 10, 20, "hello world!");
            Assert.IsNull(result);
            Assert.AreEqual(10, args[0]);
            Assert.AreEqual(20, args[1]);
            Assert.AreEqual("hello world!", args[2]);

            // Do it again to make sure that MethodInfo caching 
            // doesn't mess things up.

            result = invoker.Invoke("Method1", 10, 20, "hello world!");
            Assert.IsNull(result);
            Assert.AreEqual(10, args[0]);
            Assert.AreEqual(20, args[1]);
            Assert.AreEqual("hello world!", args[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedInvoker_Invoke_ReturnsString()
        {
            var invoker = new GatedInvoker(this);
            object result;

            result = invoker.Invoke("Method2", 10, 20, "hello world!");
            Assert.AreEqual("foobar", result);
            Assert.AreEqual(10, args[0]);
            Assert.AreEqual(20, args[1]);
            Assert.AreEqual("hello world!", args[2]);

            // Do it again to make sure that MethodInfo caching 
            // doesn't mess things up.

            result = invoker.Invoke("Method2", 10, 20, "hello world!");
            Assert.AreEqual("foobar", result);
            Assert.AreEqual(10, args[0]);
            Assert.AreEqual(20, args[1]);
            Assert.AreEqual("hello world!", args[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedInvoker_MethodNotFound()
        {
            var invoker = new GatedInvoker(this);

            try
            {
                invoker.Invoke("not found");
                Assert.Fail("Expected an ArgumentException(\"Method not found\")");
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentNullException).Name, e.GetType().Name);
            }
        }

        //---------------------------------------------------------------------

        private GatedInvoker invoker;
        private int count;
        private bool failed;

        public void Method3()
        {
            int save;

            save = count;
            Thread.Sleep(0);
            if (save != count)
                failed = true;

            count++;
        }

        public void Method4()
        {
            count++;
        }

        private void Thread1()
        {
            for (int i = 0; i < 5000; i++)
                invoker.Invoke("Method3");
        }

        private void Thread2()
        {
            for (int i = 0; i < 5000; i++)
                invoker.Invoke("Method4");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GatedInvoker_Invoke_MultipleThreads()
        {
            var thread1 = new Thread(new ThreadStart(Thread1));
            var thread2 = new Thread(new ThreadStart(Thread2));

            invoker = new GatedInvoker(this);
            count = 0;
            failed = false;

            thread1.Start();
            thread2.Start();

            thread1.Join();
            thread2.Join();

            Assert.IsFalse(failed);
            Assert.AreEqual(10000, count);
        }
    }
}

