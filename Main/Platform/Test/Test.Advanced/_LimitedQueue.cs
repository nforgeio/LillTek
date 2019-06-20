//-----------------------------------------------------------------------------
// FILE:        _LimitedQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _LimitedQueue
    {
        private class PlainItem
        {
            int value;

            public PlainItem(int value)
            {

                this.value = value;
            }

            public int Value
            {

                get { return value; }
            }
        }

        private class SizedItem : ISizedItem
        {
            int value;
            int size;

            public SizedItem(int value, int size)
            {
                this.value = value;
                this.size = size;
            }

            public int Value
            {
                get { return value; }
            }

            public int Size
            {
                get { return size; }
            }
        }

        private class SizedDisposable : ISizedItem, IDisposable
        {
            bool isDisposed;
            int value;
            int size;

            public SizedDisposable(int value, int size)
            {
                this.isDisposed = false;
                this.value = value;
                this.size = size;
            }

            public int Value
            {
                get { return value; }
            }

            public int Size
            {
                get { return size; }
            }

            public void Dispose()
            {
                isDisposed = true;
            }

            public bool IsDisposed
            {
                get { return isDisposed; }
            }
        }

        private class PlainDisposable : IDisposable
        {
            bool isDisposed;
            int value;

            public PlainDisposable(int value)
            {
                this.isDisposed = false;
                this.value = value;
            }

            public int Value
            {
                get { return value; }
            }

            public void Dispose()
            {
                isDisposed = true;
            }

            public bool IsDisposed
            {
                get { return isDisposed; }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_Basic()
        {
            LimitedQueue<int> queue = new LimitedQueue<int>();

            Assert.AreEqual(0, queue.Count);
            queue.Enqueue(0);
            Assert.AreEqual(1, queue.Count);
            queue.Enqueue(1);
            Assert.AreEqual(2, queue.Count);

            Assert.AreEqual(0, queue.Dequeue());
            Assert.AreEqual(1, queue.Dequeue());
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_SizeLimit()
        {
            LimitedQueue<SizedItem> queue = new LimitedQueue<SizedItem>(10);

            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.Size);
            queue.SizeLimit = 10;

            queue.Enqueue(new SizedItem(0, 3));
            queue.Enqueue(new SizedItem(1, 3));
            queue.Enqueue(new SizedItem(2, 3));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(9, queue.Size);

            Assert.AreEqual(0, queue.Dequeue().Value);
            Assert.AreEqual(6, queue.Size);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(3, queue.Size);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Size);
            Assert.AreEqual(0, queue.Count);

            queue.Enqueue(new SizedItem(0, 3));
            queue.Enqueue(new SizedItem(1, 3));
            queue.Enqueue(new SizedItem(2, 3));
            queue.Enqueue(new SizedItem(3, 3));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(9, queue.Size);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(3, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Size);
            Assert.AreEqual(0, queue.Count);

            queue.Enqueue(new SizedItem(0, 10));
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, queue.Dequeue().Value);

            try
            {
                queue.Enqueue(new SizedItem(0, 11));
            }
            catch
            {
                // Expecting an exception
            }

            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.Size);
            queue.Enqueue(new SizedItem(0, 3));
            queue.Enqueue(new SizedItem(0, 3));
            Assert.AreEqual(6, queue.Size);
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.Size);

            queue.Enqueue(new SizedItem(0, 3));
            queue.Enqueue(new SizedItem(1, 3));
            queue.Enqueue(new SizedItem(2, 3));
            Assert.AreEqual(9, queue.Size);
            queue.SizeLimit = 7;
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(6, queue.Size);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_CountLimit()
        {
            LimitedQueue<PlainItem> queue = new LimitedQueue<PlainItem>();

            queue.CountLimit = 3;
            queue.Enqueue(new PlainItem(0));
            queue.Enqueue(new PlainItem(1));
            queue.Enqueue(new PlainItem(2));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(0, queue.Dequeue().Value);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Count);

            queue.Enqueue(new PlainItem(0));
            queue.Enqueue(new PlainItem(1));
            queue.Enqueue(new PlainItem(2));
            queue.Enqueue(new PlainItem(3));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(3, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Count);

            queue.Enqueue(new PlainItem(0));
            queue.Enqueue(new PlainItem(1));
            queue.Enqueue(new PlainItem(2));
            queue.CountLimit = 2;
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_SizeAndCountLimit()
        {
            LimitedQueue<SizedItem> queue = new LimitedQueue<SizedItem>();

            queue.CountLimit = 3;
            queue.SizeLimit = 10;

            queue.Enqueue(new SizedItem(0, 1));
            queue.Enqueue(new SizedItem(1, 1));
            queue.Enqueue(new SizedItem(2, 1));
            queue.Enqueue(new SizedItem(3, 1));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(3, queue.Size);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(3, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.Size);

            queue.CountLimit = queue.CountLimit + 1;
            queue.Enqueue(new SizedItem(0, 3));
            queue.Enqueue(new SizedItem(1, 3));
            queue.Enqueue(new SizedItem(2, 3));
            queue.Enqueue(new SizedItem(3, 3));
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(9, queue.Size);
            Assert.AreEqual(1, queue.Dequeue().Value);
            Assert.AreEqual(2, queue.Dequeue().Value);
            Assert.AreEqual(3, queue.Dequeue().Value);
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, queue.Size);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_AutoDispose()
        {
            LimitedQueue<PlainDisposable> queue = new LimitedQueue<PlainDisposable>();
            PlainDisposable v0 = new PlainDisposable(0);
            PlainDisposable v1 = new PlainDisposable(1);
            PlainDisposable v2 = new PlainDisposable(2);
            PlainDisposable v3 = new PlainDisposable(3);

            Assert.IsFalse(queue.AutoDispose);
            queue.CountLimit = 3;

            queue.Enqueue(v0);
            queue.Enqueue(v1);
            queue.Enqueue(v2);
            queue.Enqueue(v3);
            Assert.AreEqual(3, queue.Count);

            Assert.IsFalse(v0.IsDisposed);
            Assert.AreEqual(1, queue.Dequeue().Value);
            queue.Clear();
            Assert.IsFalse(v1.IsDisposed);
            Assert.IsFalse(v2.IsDisposed);
            Assert.IsFalse(v3.IsDisposed);

            queue.AutoDispose = true;

            queue.Enqueue(v0);
            queue.Enqueue(v1);
            queue.Enqueue(v2);
            queue.Enqueue(v3);
            Assert.AreEqual(3, queue.Count);

            Assert.IsTrue(v0.IsDisposed);
            Assert.AreEqual(1, queue.Dequeue().Value);
            queue.Clear();
            Assert.IsFalse(v1.IsDisposed);
            Assert.IsTrue(v2.IsDisposed);
            Assert.IsTrue(v3.IsDisposed);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedQueue_AutoDisposeSized()
        {
            LimitedQueue<SizedDisposable> queue = new LimitedQueue<SizedDisposable>();
            SizedDisposable v0 = new SizedDisposable(0, 3);
            SizedDisposable v1 = new SizedDisposable(1, 3);
            SizedDisposable v2 = new SizedDisposable(2, 3);
            SizedDisposable v3 = new SizedDisposable(3, 3);

            Assert.IsFalse(queue.AutoDispose);
            queue.SizeLimit = 10;

            queue.Enqueue(v0);
            queue.Enqueue(v1);
            queue.Enqueue(v2);
            queue.Enqueue(v3);
            Assert.AreEqual(3, queue.Count);

            Assert.IsFalse(v0.IsDisposed);
            Assert.AreEqual(1, queue.Dequeue().Value);
            queue.Clear();
            Assert.IsFalse(v1.IsDisposed);
            Assert.IsFalse(v2.IsDisposed);
            Assert.IsFalse(v3.IsDisposed);

            queue.AutoDispose = true;

            queue.Enqueue(v0);
            queue.Enqueue(v1);
            queue.Enqueue(v2);
            queue.Enqueue(v3);
            Assert.AreEqual(3, queue.Count);

            Assert.IsTrue(v0.IsDisposed);
            Assert.AreEqual(1, queue.Dequeue().Value);
            queue.Clear();
            Assert.IsFalse(v1.IsDisposed);
            Assert.IsTrue(v2.IsDisposed);
            Assert.IsTrue(v3.IsDisposed);
        }
    }
}

