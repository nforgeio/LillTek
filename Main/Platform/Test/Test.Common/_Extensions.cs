//-----------------------------------------------------------------------------
// FILE:        _Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Extensions
    {
        private class Foo
        {
        }

        private class Bar : Foo
        {
        }

        private class FooBar
        {
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_Type_IsDerivedFrom()
        {
            Assert.IsTrue(typeof(Bar).IsDerivedFrom(typeof(Foo)));
            Assert.IsTrue(typeof(Bar).IsDerivedFrom(typeof(Bar)));
            Assert.IsFalse(typeof(FooBar).IsDerivedFrom(typeof(Foo)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XContainter_ElementPath()
        {
            XNamespace ns = XNamespace.None;
            XDocument doc;
            XElement e;

            doc = new XDocument(

                    new XElement(ns + "root",

                        new XElement(ns + "top1",
                            new XElement(ns + "child1", "root/top1/child1"),
                            new XElement(ns + "child2", "root/top1/child2")
                        ),

                        new XElement(ns + "top2",
                            new XElement(ns + "child1", "root/top2/child1"),
                            new XElement(ns + "child2", "root/top2/child2")
                        )
                    )
                );

            e = doc.ElementPath("root/top1/child1");
            Assert.AreEqual("root/top1/child1", e.Value);

            e = doc.ElementPath("root/top2/child2");
            Assert.AreEqual("root/top2/child2", e.Value);

            e = doc.ElementPath("root/top3/child2");
            Assert.IsNull(e);

            e = doc.ElementPath("/root/top1/child1");
            Assert.AreEqual("root/top1/child1", e.Value);

            e = doc.ElementPath("/root/top2/child2");
            Assert.AreEqual("root/top2/child2", e.Value);

            e = doc.ElementPath("/root/top3/child2");
            Assert.IsNull(e);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XContainter_SingleElement()
        {
            XElement element;
            XElement child;

            element =
                new XElement(XNamespace.None + "root",
                    new XElement(XNamespace.None + "child1"),
                    new XElement(XNamespace.None + "child2"),
                    new XElement(XNamespace.None + "child3"),
                    new XElement(XNamespace.None + "dup"),
                    new XElement(XNamespace.None + "dup")
                );

            child = element.SingleElement(XNamespace.None + "child1");
            Assert.AreEqual(XNamespace.None + "child1", child.Name);

            child = element.SingleElement(XNamespace.None + "child2");
            Assert.AreEqual(XNamespace.None + "child2", child.Name);

            child = element.SingleElement(XNamespace.None + "child3");
            Assert.AreEqual(XNamespace.None + "child3", child.Name);

            try
            {
                child = element.SingleElement(XNamespace.None + "child0");
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            try
            {
                child = element.SingleElement(XNamespace.None + "dup");
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_SingleAttribute()
        {
            XElement element;
            XAttribute child;

            element =
                new XElement(XNamespace.None + "root",
                    new XAttribute(XNamespace.None + "child1", "1"),
                    new XAttribute(XNamespace.None + "child2", "2"),
                    new XAttribute(XNamespace.None + "child3", "3")
                );

            child = element.SingleAttribute(XNamespace.None + "child1");
            Assert.AreEqual("1", child.Value);

            child = element.SingleAttribute(XNamespace.None + "child2");
            Assert.AreEqual("2", child.Value);

            child = element.SingleAttribute(XNamespace.None + "child3");
            Assert.AreEqual("3", child.Value);

            try
            {
                child = element.SingleAttribute(XNamespace.None + "child0");
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseString()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", " value "));

            Assert.IsNull(element.ParseAttribute(ns + "foo", (string)null));
            Assert.AreEqual("value", element.ParseAttribute(ns + "test", (string)null));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseInt()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "10"));

            Assert.AreEqual(20, element.ParseAttribute(ns + "foo", 20));
            Assert.AreEqual(10, element.ParseAttribute(ns + "test", 20));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseLong()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "1M"));

            Assert.AreEqual(20, element.ParseAttribute(ns + "foo", 20));
            Assert.AreEqual(1024 * 1024, element.ParseAttribute(ns + "test", 20));
        }

        private enum TestEnum
        {
            Value1,
            Value2,
            Value3
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseEnum()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "value1"));

            Assert.AreEqual(TestEnum.Value3, element.ParseAttribute<TestEnum>(ns + "foo", TestEnum.Value3));
            Assert.AreEqual(TestEnum.Value1, element.ParseAttribute<TestEnum>(ns + "test", TestEnum.Value3));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseUri()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "http://foo.com/"));

            Assert.AreEqual(new Uri("http://foobar.com/"), element.ParseAttribute(ns + "foo", new Uri("http://foobar.com/")));
            Assert.AreEqual(new Uri("http://foo.com/"), element.ParseAttribute(ns + "test", new Uri("http://foobar.com/")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseBool()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "yes"));

            Assert.AreEqual(false, element.ParseAttribute(ns + "foo", false));
            Assert.AreEqual(true, element.ParseAttribute(ns + "test", false));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseDouble()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "1234.5"));

            Assert.AreEqual(0.0, element.ParseAttribute(ns + "foo", 0.0));
            Assert.AreEqual(1234.5, element.ParseAttribute(ns + "test", 0.0));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseTimeSpan()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "500ms"));

            Assert.AreEqual(TimeSpan.Zero, element.ParseAttribute(ns + "foo", TimeSpan.Zero));
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), element.ParseAttribute(ns + "test", TimeSpan.Zero));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseDateTime()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "Sun, 06 Nov 1994 12:00:00 GMT"));

            Assert.AreEqual(DateTime.MinValue, element.ParseAttribute(ns + "foo", DateTime.MinValue));
            Assert.AreEqual(new DateTime(1994, 11, 6, 12, 0, 0), element.ParseAttribute(ns + "test", DateTime.MinValue));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseIPAddress()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "10.20.30.40"));

            Assert.AreEqual(IPAddress.Any, element.ParseAttribute(ns + "foo", IPAddress.Any));
            Assert.AreEqual(IPAddress.Parse("10.20.30.40"), element.ParseAttribute(ns + "test", IPAddress.Any));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseNetworkBinding()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "10.20.30.40:HTTP"));

            Assert.AreEqual(NetworkBinding.Any, element.ParseAttribute(ns + "foo", NetworkBinding.Any));
            Assert.AreEqual(NetworkBinding.Parse("10.20.30.40:HTTP"), element.ParseAttribute(ns + "test", NetworkBinding.Any));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XAttribute_ParseGuid()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XAttribute(XNamespace.None + "test", "D95566F4-D450-4551-890C-2C21ABABB069"));

            Assert.AreEqual(Guid.Empty, element.ParseAttribute(ns + "foo", Guid.Empty));
            Assert.AreEqual(new Guid("D95566F4-D450-4551-890C-2C21ABABB069"), element.ParseAttribute(ns + "test", Guid.Empty));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseString()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", " value "));

            Assert.IsNull(element.ParseElement(ns + "foo", (string)null));
            Assert.AreEqual("value", element.ParseElement(ns + "test", (string)null));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseInt()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "10"));

            Assert.AreEqual(20, element.ParseElement(ns + "foo", 20));
            Assert.AreEqual(10, element.ParseElement(ns + "test", 20));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseLong()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "1M"));

            Assert.AreEqual(20, element.ParseElement(ns + "foo", 20));
            Assert.AreEqual(1024 * 1024, element.ParseElement(ns + "test", 20));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseEnum()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "value1"));

            Assert.AreEqual(TestEnum.Value3, element.ParseElement<TestEnum>(ns + "foo", TestEnum.Value3));
            Assert.AreEqual(TestEnum.Value1, element.ParseElement<TestEnum>(ns + "test", TestEnum.Value3));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseUri()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "http://foo.com/"));

            Assert.AreEqual(new Uri("http://foobar.com/"), element.ParseElement(ns + "foo", new Uri("http://foobar.com/")));
            Assert.AreEqual(new Uri("http://foo.com/"), element.ParseElement(ns + "test", new Uri("http://foobar.com/")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseBool()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "yes"));

            Assert.AreEqual(false, element.ParseElement(ns + "foo", false));
            Assert.AreEqual(true, element.ParseElement(ns + "test", false));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseDouble()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "1234.5"));

            Assert.AreEqual(0.0, element.ParseElement(ns + "foo", 0.0));
            Assert.AreEqual(1234.5, element.ParseElement(ns + "test", 0.0));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseTimeSpan()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "500ms"));

            Assert.AreEqual(TimeSpan.Zero, element.ParseElement(ns + "foo", TimeSpan.Zero));
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), element.ParseElement(ns + "test", TimeSpan.Zero));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseDateTime()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "Sun, 06 Nov 1994 12:00:00 GMT"));

            Assert.AreEqual(DateTime.MinValue, element.ParseElement(ns + "foo", DateTime.MinValue));
            Assert.AreEqual(new DateTime(1994, 11, 6, 12, 0, 0), element.ParseElement(ns + "test", DateTime.MinValue));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseIPAddress()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "10.20.30.40"));

            Assert.AreEqual(IPAddress.Any, element.ParseElement(ns + "foo", IPAddress.Any));
            Assert.AreEqual(IPAddress.Parse("10.20.30.40"), element.ParseElement(ns + "test", IPAddress.Any));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseNetworkBinding()
        {
            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "10.20.30.40:HTTP"));

            Assert.AreEqual(NetworkBinding.Any, element.ParseElement(ns + "foo", NetworkBinding.Any));
            Assert.AreEqual(NetworkBinding.Parse("10.20.30.40:HTTP"), element.ParseElement(ns + "test", NetworkBinding.Any));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_XElement_ParseGuid()
        {

            XNamespace ns = XNamespace.None;
            XElement element;

            element = new XElement(ns + "element");
            element.Add(new XElement(XNamespace.None + "test", "D95566F4-D450-4551-890C-2C21ABABB069"));

            Assert.AreEqual(Guid.Empty, element.ParseElement(ns + "foo", Guid.Empty));
            Assert.AreEqual(new Guid("D95566F4-D450-4551-890C-2C21ABABB069"), element.ParseElement(ns + "test", Guid.Empty));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_IList_Shuffle()
        {
            List<int> list;

            list = new List<int>();

            // Verify that shuffling empty list doesn't crash

            list.Shuffle();
            Assert.AreEqual(0, list.Count);

            // Verify that shuffling one element list doesn't crash

            list.Add(10);
            list.Shuffle();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(10, list[0]);

            // Initialize the list with 100K elements, shuffle it and then
            // verify that the element order changed.
            //
            // Note that there's a 1 in 100K chance that a valid shuffle
            // might result in the original order and cause this test
            // to fail.

            list.Clear();
            for (int i = 0; i < 100000; i++)
                list.Add(i);

            list.Shuffle();

            Assert.AreEqual(100000, list.Count);

            for (int i = 0; i < list.Count; i++)
                if (list[i] != i)
                    return;     // Test passed

            Assert.Fail("Unlikely failure (1:100K chance)");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_CopyTo()
        {
            List<int> list;

            list = new List<int>();
            new int[] { 0, 1, 2, 3 }.CopyTo(list);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3 }, list.ToArray());

            // Make sure that CopyTo() clears the collection.

            new int[] { 0, 1, 2, 3 }.CopyTo(list);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3 }, list.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_AppendTo()
        {
            List<int> list;

            list = new List<int>();
            new int[] { 0, 1, 2, 3 }.AppendTo(list);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3 }, list.ToArray());

            // Make sure that AppendTo() does not clear the collection.

            new int[] { 4, 5, 6, 7 }.AppendTo(list);
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }, list.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_Array_Extract()
        {
            string[] input = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            string[] output;

            output = input.Extract<string>(1, 2);
            Assert.AreEqual(2, output.Length);
            Assert.AreEqual("1", output[0]);
            Assert.AreEqual("2", output[1]);

            output = input.Extract<string>(8);
            Assert.AreEqual(2, output.Length);
            Assert.AreEqual("8", output[0]);
            Assert.AreEqual("9", output[1]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_StringBuilder_ClearLine()
        {
            var sb = new StringBuilder();

            sb.Clear();
            sb.ClearLine();
            Assert.AreEqual("", sb.ToString());

            sb.Clear();
            sb.Append("This is a test.\r\n");
            sb.ClearLine();
            sb.Append("Hello World!");
            Assert.AreEqual("This is a test.\r\nHello World!", sb.ToString());

            sb.Clear();
            sb.Append("This is a test.");
            sb.ClearLine();
            sb.Append("Hello World!");
            Assert.AreEqual("This is a test.\r\nHello World!", sb.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_Dictionary_RemovePredicate()
        {
            var d = new Dictionary<string, string>();

            d["hello"] = "world";
            d["foo"] = "bar";
            d["test1"] = "delete";
            d["test2"] = "delete";

            d.Remove(entry => entry.Value == "delete");

            Assert.AreEqual(2, d.Count);
            Assert.IsFalse(d.ContainsKey("test1"));
            Assert.IsFalse(d.ContainsKey("test2"));
            Assert.IsTrue(d.ContainsKey("hello"));
            Assert.IsTrue(d.ContainsKey("foo"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_Dictionary_Clone()
        {
            Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> clone;

            clone = dictionary.Clone(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(0, clone.Count);

            dictionary.Add("Foo", 0);
            dictionary.Add("Bar", 1);
            dictionary.Add("Hello", 2);

            clone = dictionary.Clone(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(3, clone.Count);
            Assert.AreEqual(0, clone["FOO"]);
            Assert.AreEqual(1, clone["BAR"]);
            Assert.AreEqual(2, clone["HELLO"]);

            clone = dictionary.Clone(null);
            Assert.AreEqual(3, clone.Count);
            Assert.AreEqual(0, clone["Foo"]);
            Assert.AreEqual(1, clone["Bar"]);
            Assert.AreEqual(2, clone["Hello"]);

            clone = dictionary.Clone(StringComparer.OrdinalIgnoreCase, entry => new KeyValuePair<string, int>(entry.Key + "-A", entry.Value + 10));
            Assert.AreEqual(3, clone.Count);
            Assert.AreEqual(10, clone["Foo-A"]);
            Assert.AreEqual(11, clone["Bar-A"]);
            Assert.AreEqual(12, clone["Hello-A"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_List_RemovePredicate()
        {
            var list = new List<string>();

            list.Add("one");
            list.Add("two");
            list.Add("three");
            list.Add("four");
            list.Add("five");
            list.Add("six");

            list.Remove(item => item == "two" || item == "four");

            Assert.AreEqual(4, list.Count);
            Assert.AreEqual("one", list[0]);
            Assert.AreEqual("three", list[1]);
            Assert.AreEqual("five", list[2]);
            Assert.AreEqual("six", list[3]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_List_Clone()
        {
            List<int> list = new List<int>();
            List<int> clone;

            clone = list.Clone();
            Assert.AreEqual(0, clone.Count);

            list.AddRange(new int[] { 0, 1, 2, 3, 4, 5 });
            clone = list.Clone();
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3, 4, 5 }, clone.ToArray());

            clone = list.Clone(item => item + 10);
            CollectionAssert.AreEqual(new int[] { 10, 11, 12, 13, 14, 15 }, clone.ToArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_List_Find()
        {
            var intList = new List<int>();

            intList.AddRange(new int[] { 1, 2, 3, 4, 5 });
            Assert.AreEqual(4, intList.Find(i => i == 4));
            Assert.AreEqual(2, intList.Find(i => i == 2));
            Assert.AreEqual(2, intList.Find(i => i == 2));
            Assert.AreEqual(0, intList.Find(i => i == 100));

            var stringList = new List<string>();

            stringList.Add("hello");
            stringList.Add("world");
            Assert.AreEqual("hello", stringList.Find(s => s == "hello"));
            Assert.AreEqual("world", stringList.Find(s => s == "world"));
            Assert.IsNull(stringList.Find(s => s == "foobar"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_ObservableCollection_Find()
        {
            var intList = new ObservableCollection<int>();

            intList.Add(1);
            intList.Add(2);
            intList.Add(3);
            intList.Add(4);
            intList.Add(5);
            Assert.AreEqual(4, intList.Find(i => i == 4));
            Assert.AreEqual(2, intList.Find(i => i == 2));
            Assert.AreEqual(2, intList.Find(i => i == 2));
            Assert.AreEqual(0, intList.Find(i => i == 100));

            var stringList = new ObservableCollection<string>();

            stringList.Add("hello");
            stringList.Add("world");
            Assert.AreEqual("hello", stringList.Find(s => s == "hello"));
            Assert.AreEqual("world", stringList.Find(s => s == "world"));
            Assert.IsNull(stringList.Find(s => s == "foobar"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Extensions_DateTime_ThisLastNext()
        {
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 10, 1, 10, 20, 30).ThisMonth());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 10, 15, 10, 20, 30).ThisMonth());

            Assert.AreEqual(new DateTime(2011, 9, 1), new DateTime(2011, 10, 1).LastMonth());
            Assert.AreEqual(new DateTime(2010, 12, 1), new DateTime(2011, 1, 1).LastMonth());
            Assert.AreEqual(new DateTime(2011, 9, 1), new DateTime(2011, 10, 15).LastMonth());
            Assert.AreEqual(new DateTime(2010, 12, 1), new DateTime(2011, 1, 15).LastMonth());

            Assert.AreEqual(new DateTime(2011, 11, 1), new DateTime(2011, 10, 1).NextMonth());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 12, 1).NextMonth());
            Assert.AreEqual(new DateTime(2011, 11, 1), new DateTime(2011, 10, 15).NextMonth());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 12, 15).NextMonth());

            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 1, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 2, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 3, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 4, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 5, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 6, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 7, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 8, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 9, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 10, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 11, 15).ThisQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 12, 15).ThisQuarter());

            Assert.AreEqual(new DateTime(2010, 10, 1), new DateTime(2011, 1, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2010, 10, 1), new DateTime(2011, 2, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2010, 10, 1), new DateTime(2011, 3, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 4, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 5, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 6, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 7, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 8, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 9, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 10, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 11, 15).LastQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 12, 15).LastQuarter());

            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 1, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 2, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 4, 1), new DateTime(2011, 3, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 4, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 5, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 7, 1), new DateTime(2011, 6, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 7, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 8, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2011, 10, 1), new DateTime(2011, 9, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 10, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 11, 15).NextQuarter());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 12, 15).NextQuarter());

            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 1, 15).ThisYear());
            Assert.AreEqual(new DateTime(2011, 1, 1), new DateTime(2011, 7, 15).ThisYear());
            Assert.AreEqual(new DateTime(2010, 1, 1), new DateTime(2011, 1, 15).LastYear());
            Assert.AreEqual(new DateTime(2012, 1, 1), new DateTime(2011, 1, 15).NextYear());
        }
    }
}

