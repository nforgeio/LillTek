//-----------------------------------------------------------------------------
// FILE:        _LRUList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;
using System.Reflection;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _LRUList
    {
        private sealed class V : IDLElement
        {
            public int Value;
            private object previous;
            private object next;

            public V(int value)
            {
                this.Value = value;
            }

            public object Previous
            {
                get { return previous; }
                set { previous = value; }
            }

            public object Next
            {
                get { return next; }
                set { next = value; }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LRUList_Basic()
        {
            LRUList list;
            V v, v0, v1, v2, v3;

            list = new LRUList();
            Assert.AreEqual(0, list.Count);

            list.Add(v0 = new V(0));
            list.Add(v1 = new V(1));
            list.Add(v2 = new V(2));
            list.Add(v3 = new V(3));

            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);
            Assert.AreEqual(3, ((V)list[3]).Value);

            list.Touch(v0);

            Assert.AreEqual(1, ((V)list[0]).Value);
            Assert.AreEqual(2, ((V)list[1]).Value);
            Assert.AreEqual(3, ((V)list[2]).Value);
            Assert.AreEqual(0, ((V)list[3]).Value);

            list.Remove(v2);

            v = (V)list.RemoveLRU();
            Assert.AreEqual(1, v.Value);
            v = (V)list.RemoveLRU();
            Assert.AreEqual(3, v.Value);
            v = (V)list.RemoveLRU();
            Assert.AreEqual(0, v.Value);
            v = (V)list.RemoveLRU();
            Assert.IsNull(v);
        }
    }
}

