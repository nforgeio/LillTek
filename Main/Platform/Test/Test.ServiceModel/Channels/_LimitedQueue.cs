//-----------------------------------------------------------------------------
// FILE:        _LimitedQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.ServiceModel.Channels.Test
{
    [TestClass]
    public class _LimitedQueue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void LimitedQueue_Basic()
        {
            LimitedQueue<int> queue;

            try
            {
                queue = new LimitedQueue<int>(0);
                Assert.Fail("Expected ArgumentException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }

            queue = new LimitedQueue<int>(5);
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            queue.Enqueue(5);
            Assert.AreEqual(5, queue.Count);

            queue.Enqueue(6);
            Assert.AreEqual(5, queue.Count);

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(i + 2, queue.Dequeue());
        }

        private class Test : IDisposable
        {
            public bool Disposed = false;
            public int Value;

            public Test(int value)
            {
                this.Value = value;
            }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void LimitedQueue_Dispose()
        {
            LimitedQueue<Test> queue;
            Test item1 = new Test(1);
            Test item2 = new Test(2);
            Test item3 = new Test(3);

            queue = new LimitedQueue<Test>(2);
            queue.Enqueue(item1);
            queue.Enqueue(item2);
            queue.Enqueue(item3);

            Assert.AreEqual(2, queue.Count);
            Assert.IsTrue(item1.Disposed);
            Assert.IsFalse(item2.Disposed);
            Assert.IsFalse(item2.Disposed);
        }
    }
}

