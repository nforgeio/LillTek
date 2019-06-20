//-----------------------------------------------------------------------------
// FILE:        _DoubleList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _DoubleList
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
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_AddToEnd()
        {
            var list = new DoubleList();

            Assert.AreEqual(0, list.Count);
            list.AddToEnd(new V(0));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);

            list.AddToEnd(new V(1));
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);

            list.AddToEnd(new V(2));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_AddToFront()
        {
            var list = new DoubleList();

            Assert.AreEqual(0, list.Count);
            list.AddToFront(new V(0));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);

            list.AddToFront(new V(1));
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1, ((V)list[0]).Value);
            Assert.AreEqual(0, ((V)list[1]).Value);

            list.AddToFront(new V(2));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(2, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(0, ((V)list[2]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_Insert()
        {
            var list = new DoubleList();

            Assert.AreEqual(0, list.Count);
            list.InsertAfter(null, new V(0));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);

            list.InsertAfter(list[list.Count - 1], new V(1));
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);

            list.InsertAfter(list[list.Count - 1], new V(2));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.InsertAfter(null, new V(3));
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(3, ((V)list[0]).Value);
            Assert.AreEqual(0, ((V)list[1]).Value);
            Assert.AreEqual(1, ((V)list[2]).Value);
            Assert.AreEqual(2, ((V)list[3]).Value);

            list.InsertAfter(list[0], new V(4));
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(3, ((V)list[0]).Value);
            Assert.AreEqual(4, ((V)list[1]).Value);
            Assert.AreEqual(0, ((V)list[2]).Value);
            Assert.AreEqual(1, ((V)list[3]).Value);
            Assert.AreEqual(2, ((V)list[4]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_InsertAt()
        {
            var list = new DoubleList();

            Assert.AreEqual(0, list.Count);
            list.InsertAt(0, new V(0));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);

            list.InsertAt(1, new V(1));
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);

            list.InsertAt(2, new V(2));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.InsertAt(0, new V(3));
            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(3, ((V)list[0]).Value);
            Assert.AreEqual(0, ((V)list[1]).Value);
            Assert.AreEqual(1, ((V)list[2]).Value);
            Assert.AreEqual(2, ((V)list[3]).Value);

            list.InsertAt(1, new V(4));
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(3, ((V)list[0]).Value);
            Assert.AreEqual(4, ((V)list[1]).Value);
            Assert.AreEqual(0, ((V)list[2]).Value);
            Assert.AreEqual(1, ((V)list[3]).Value);
            Assert.AreEqual(2, ((V)list[4]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_RemoveFromFront()
        {
            var list = new DoubleList();

            list.AddToEnd(new V(0));
            list.Remove(list[0]);
            Assert.AreEqual(0, list.Count);

            list.AddToEnd(new V(0));
            list.AddToEnd(new V(1));
            list.AddToEnd(new V(2));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.Remove(list[0]);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1, ((V)list[0]).Value);
            Assert.AreEqual(2, ((V)list[1]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_RemoveFromEnd()
        {
            var list = new DoubleList();

            list.AddToFront(new V(0));
            list.Remove(list[0]);
            Assert.AreEqual(0, list.Count);

            list.AddToFront(new V(2));
            list.AddToFront(new V(1));
            list.AddToFront(new V(0));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.Remove(list[2]);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);

            list.Remove(list[1]);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);

            list.Remove(list[0]);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_RemoveFromMiddle()
        {
            var list = new DoubleList();

            list.AddToFront(new V(0));
            list.Remove(list[0]);
            Assert.AreEqual(0, list.Count);

            list.AddToFront(new V(2));
            list.AddToFront(new V(1));
            list.AddToFront(new V(0));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.Remove(list[1]);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(2, ((V)list[1]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_RemoveAt()
        {
            var list = new DoubleList();
            V v;

            v = new V(0);
            list.AddToFront(v);
            list.RemoveAt(0);
            Assert.AreEqual(0, list.Count);
            list.AddToEnd(v);
            Assert.AreEqual(1, list.Count);
            list.Clear();

            list.AddToFront(new V(2));
            list.AddToFront(new V(1));
            list.AddToFront(new V(0));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(1, ((V)list[1]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);

            list.RemoveAt(1);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(2, ((V)list[1]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_Clear()
        {
            var list = new DoubleList();

            list.AddToFront(new V(2));
            list.AddToFront(new V(1));
            list.AddToFront(new V(0));
            list.Clear();
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexForwardScan()
        {
            var list = new DoubleList();

            for (int i = 0; i < 200; i++)
                list.AddToEnd(new V(i));

            for (int i = 0; i < list.Count; i++)
                Assert.AreEqual(i, ((V)list[i]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexReverseScan()
        {
            var list = new DoubleList();

            for (int i = 0; i < 200; i++)
                list.AddToEnd(new V(i));

            for (int i = list.Count - 1; i >= 0; i--)
                Assert.AreEqual(i, ((V)list[i]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexRandom()
        {
            var list = new DoubleList();

            for (int i = 0; i < 200; i++)
                list.AddToEnd(new V(i));

            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(100, ((V)list[100]).Value);
            Assert.AreEqual(77, ((V)list[77]).Value);
            Assert.AreEqual(21, ((V)list[21]).Value);
            Assert.AreEqual(90, ((V)list[90]).Value);
            Assert.AreEqual(5, ((V)list[5]).Value);
            Assert.AreEqual(199, ((V)list[199]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexWithAdd()
        {
            var list = new DoubleList();

            list.AddToEnd(new V(0));
            Assert.AreEqual(0, ((V)list[0]).Value);
            list.AddToEnd(new V(1));
            list.AddToEnd(new V(2));
            Assert.AreEqual(0, ((V)list[0]).Value);
            Assert.AreEqual(2, ((V)list[2]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexWithRemove()
        {
            var list = new DoubleList();

            list.AddToEnd(new V(0));
            list.AddToEnd(new V(1));
            list.AddToEnd(new V(2));
            Assert.AreEqual(0, ((V)list[0]).Value);
            list.Remove(list[0]);
            Assert.AreEqual(1, ((V)list[0]).Value);
            list.Remove(list[0]);
            Assert.AreEqual(2, ((V)list[0]).Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_Enumerate()
        {
            var list = new DoubleList();
            int c;

            for (int i = 0; i < 200; i++)
                list.AddToEnd(new V(i));

            c = 0;
            foreach (object o in list)
            {

                Assert.AreEqual(c, ((V)o).Value);
                c++;
            }

            Assert.AreEqual(200, c);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_EnumerateWithModify()
        {
            var list = new DoubleList();

            list.AddToEnd(new V(0));
            list.AddToEnd(new V(1));
            list.AddToEnd(new V(2));

            try
            {
                foreach (V v in list)
                {
                    v.Value = 10;
                    list.AddToEnd(new V(100));
                }

                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
                Assert.Fail();
            }

            try
            {
                foreach (V v in list)
                {
                    v.Value = 10;
                    list.AddToFront(new V(100));
                }

                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
                Assert.Fail();
            }

            try
            {
                foreach (V v in list)
                {
                    v.Value = 10;
                    list.RemoveAt(0);
                }

                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
                Assert.Fail();
            }

            try
            {
                foreach (V v in list)
                {
                    v.Value = 10;
                    list.Clear();
                }

                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void DoubleList_IndexExceptions()
        {
            var list = new DoubleList();
            object o;

            list.AddToEnd(new V(0));

            try
            {
                o = list[-1];
                Assert.Fail();
            }
            catch (IndexOutOfRangeException)
            {
            }

            try
            {
                o = list[1];
                Assert.Fail();
            }
            catch (IndexOutOfRangeException)
            {
            }

            try
            {
                list.RemoveAt(-1);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException)
            {
            }

            try
            {
                list.RemoveAt(1);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException)
            {
            }
        }
    }
}

