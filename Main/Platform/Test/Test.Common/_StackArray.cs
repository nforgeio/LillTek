//-----------------------------------------------------------------------------
// FILE:        _StackArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _StackArray
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_Basic()
        {
            var stack = new StackArray<int>();

            Assert.AreEqual(0, stack.Count);

            stack.Push(0);
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(0, stack.Peek());
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(0, stack.Pop());
            Assert.AreEqual(0, stack.Count);

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);
            Assert.AreEqual(2, stack.Pop());
            Assert.AreEqual(1, stack.Pop());
            Assert.AreEqual(0, stack.Pop());

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);
            stack.Clear();
            Assert.AreEqual(0, stack.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_Indexing()
        {
            var stack = new StackArray<int>();

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);

            Assert.AreEqual(2, stack[0]);
            Assert.AreEqual(1, stack[1]);
            Assert.AreEqual(0, stack[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_Discard()
        {
            var stack = new StackArray<int>();

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);
            stack.Discard(0);
            Assert.AreEqual(3, stack.Count);
            Assert.AreEqual(2, stack.Peek());

            stack.Discard(2);
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(0, stack.Peek());

            stack.Push(1);
            stack.Push(2);
            stack.Discard(3);
            Assert.AreEqual(0, stack.Count);

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);
            stack.Discard(100);
            Assert.AreEqual(0, stack.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_IndexOf()
        {
            var stack = new StackArray<int>();

            stack.Push(2);
            stack.Push(10);
            stack.Push(3);
            stack.Push(2);
            stack.Push(1);
            stack.Push(0);

            Assert.AreEqual(0, stack.IndexOf(0));
            Assert.AreEqual(1, stack.IndexOf(1));
            Assert.AreEqual(2, stack.IndexOf(2));
            Assert.AreEqual(3, stack.IndexOf(3));
            Assert.AreEqual(4, stack.IndexOf(10));
            Assert.AreEqual(-1, stack.IndexOf(11));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_Enumerate()
        {
            var stack = new StackArray<int>();
            var list = new List<int>();

            stack.Push(0);
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);

            foreach (int value in stack)
                list.Add(value);

            CollectionAssert.AreEqual(new int[] { 3, 2, 1, 0 }, list.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_Insert()
        {
            var stack = new StackArray<int>();

            stack.Insert(0, 0);
            stack.Push(1);
            Assert.AreEqual(1, stack.Peek());
            Assert.AreEqual(1, stack[0]);
            Assert.AreEqual(0, stack[1]);

            stack.Insert(2, 2);
            Assert.AreEqual(1, stack[0]);
            Assert.AreEqual(0, stack[1]);
            Assert.AreEqual(2, stack[2]);

            stack.Insert(stack.Count, 100);
            Assert.AreEqual(1, stack[0]);
            Assert.AreEqual(0, stack[1]);
            Assert.AreEqual(2, stack[2]);
            Assert.AreEqual(100, stack[3]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void StackArray_RemoveAt()
        {
            var stack = new StackArray<int>();

            stack.Push(3);
            stack.Push(2);
            stack.Push(1);
            stack.Push(0);
            Assert.AreEqual(0, stack[0]);
            Assert.AreEqual(1, stack[1]);
            Assert.AreEqual(2, stack[2]);
            Assert.AreEqual(3, stack[3]);

            stack.RemoveAt(1);
            Assert.AreEqual(0, stack[0]);
            Assert.AreEqual(2, stack[1]);
            Assert.AreEqual(3, stack[2]);

            stack.RemoveAt(0);
            Assert.AreEqual(2, stack[0]);
            Assert.AreEqual(3, stack[1]);

            stack.RemoveAt(1);
            Assert.AreEqual(2, stack[0]);

            stack.RemoveAt(0);
            Assert.AreEqual(0, stack.Count);
        }
    }
}

