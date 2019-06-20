//-----------------------------------------------------------------------------
// FILE:        _QueueArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _QueueArray
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void QueueArray_Basic()
        {
            QueueArray<int> queue = new QueueArray<int>();

            queue.Enqueue(0);
            queue.Enqueue(1);
            queue.Enqueue(2);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2 }, queue.ToArray());
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(0, queue[0]);
            Assert.AreEqual(1, queue[1]);
            Assert.AreEqual(2, queue[2]);

            Assert.AreEqual(0, queue.Peek());
            Assert.AreEqual(3, queue.Count);

            Assert.AreEqual(0, queue.Dequeue());
            CollectionAssert.AreEqual(new int[] { 1, 2 }, queue.ToArray());
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(1, queue[0]);
            Assert.AreEqual(2, queue[1]);

            Assert.AreEqual(1, queue.Dequeue());
            CollectionAssert.AreEqual(new int[] { 2 }, queue.ToArray());
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(2, queue[0]);

            Assert.AreEqual(2, queue.Dequeue());
            CollectionAssert.AreEqual(new int[] { }, queue.ToArray());
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void QueueArray_InsertAt()
        {
            QueueArray<int> queue = new QueueArray<int>();

            queue.InsertAt(0, 0);
            CollectionAssert.AreEqual(new int[] { 0 }, queue.ToArray());
            queue.InsertAt(1, 1);
            CollectionAssert.AreEqual(new int[] { 0, 1 }, queue.ToArray());
            queue.InsertAt(2, 2);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2 }, queue.ToArray());
            queue.InsertAt(0, -2);
            CollectionAssert.AreEqual(new int[] { -2, 0, 1, 2 }, queue.ToArray());
            queue.InsertAt(1, -1);
            CollectionAssert.AreEqual(new int[] { -2, -1, 0, 1, 2 }, queue.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void QueueArray_RemoveAt()
        {
            QueueArray<int> queue = new QueueArray<int>();

            queue.Enqueue(0);
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            queue.Enqueue(5);

            queue.RemoveAt(0);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4, 5 }, queue.ToArray());

            queue.RemoveAt(queue.Count - 1);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4 }, queue.ToArray());

            queue.RemoveAt(2);
            CollectionAssert.AreEqual(new int[] { 1, 2, 4 }, queue.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void QueueArray_Enumerate()
        {
            QueueArray<int> queue = new QueueArray<int>();
            List<int> list = new List<int>();

            queue.Enqueue(0);
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            queue.Enqueue(5);

            foreach (int value in queue)
                list.Add(value);

            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3, 4, 5 }, list.ToArray());
        }
    }
}

