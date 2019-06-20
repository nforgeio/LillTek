//-----------------------------------------------------------------------------
// FILE:        _ArgCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ArgCollection
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Basic()
        {
            var ps = new ArgCollection();

            Assert.AreEqual(string.Empty, ps.ToString());

            ps.Set("foo", "bar");
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.IsNull(ps["FOOBAR"]);

            Assert.AreEqual("foo=bar;", ps.ToString());

            ps.Set("foobar", "boohoo");
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = new ArgCollection("foo=bar");
            Assert.AreEqual("bar", ps["foo"]);

            ps = new ArgCollection("foo=bar;");
            Assert.AreEqual("bar", ps["foo"]);

            ps = new ArgCollection("foo=bar;foobar=boohoo");
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = new ArgCollection();
            ps.Load("foo=bar;foobar=boohoo");
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = ArgCollection.Parse("foo=bar;foobar=boohoo");
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = new ArgCollection(ps.ToString());
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            Assert.AreEqual("bar", ps.Get("foo", string.Empty));
            Assert.AreEqual("bar", ps.Get("FOO", (string)null));
            Assert.AreEqual("boohoo", ps.Get("FOOBAR", "xx"));
            Assert.AreEqual(null, ps.Get("xxxx", (string)null));
            Assert.AreEqual("abc", ps.Get("xxxx", "abc"));

            Assert.IsTrue(ps.ContainsKey("foo"));
            Assert.IsTrue(ps.ContainsKey("FOO"));
            Assert.IsFalse(ps.ContainsKey("xxxx"));

            string value;

            Assert.IsTrue(ps.TryGetValue("foo", out value));
            Assert.AreEqual("bar", value);
            Assert.IsTrue(ps.TryGetValue("FOO", out value));
            Assert.AreEqual("bar", value);
            Assert.IsFalse(ps.TryGetValue("XXXX", out value));
            Assert.IsNull(value);

            ps.Set("bytes", (byte[])null);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, ps.Get("bytes", new byte[] { 0, 1, 2, 3 }));

            ps.Set("bytes", new byte[] { 4, 5, 6 });
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, ps.Get("bytes", new byte[0]));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Clone()
        {
            ArgCollection ps;
            ArgCollection clone;

            ps = new ArgCollection();
            ps.Set("foo", "bar");
            ps.Set("bar", "foo");

            clone = ps.Clone();
            Assert.AreEqual("bar", clone["foo"]);
            Assert.AreEqual("foo", clone["bar"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Enumerate()
        {
            ArgCollection ps;
            Dictionary<string, string> test;

            ps = new ArgCollection();
            ps.Set("foo", "bar");
            ps.Set("bar", "foo");

            test = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ps)
                test[key] = ps[key];

            Assert.AreEqual(2, test.Count);
            Assert.AreEqual("bar", test["foo"]);
            Assert.AreEqual("foo", test["bar"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_SetNull()
        {
            var ps = new ArgCollection();

            ps.Set("hello", "test");
            ps.Set("hello", (string)null);
            Assert.IsNull(ps.Get("hello"));
            Assert.AreEqual("world", ps.Get("hello", "world"));
        }

        private enum TestEnum
        {
            Unknown = -1,
            Zero,
            One,
            Two,
            Three
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Parsing()
        {
            var ps = new ArgCollection(@"
bool=yes;
int=-10;
timespan=10m;
double=123.45;
guid={28676DD2-1C0F-4217-96DF-1714A7B3BEFA};
ip=192.168.1.1;
ep=192.168.1.2:80;
enum0=zero;
enum1=One;
enum2=TWO;
enum3=xxx;
uri=http://www.lilltek.com/;
type=" + this.GetType().FullName + ":" + GetType().Assembly.Location);

            Assert.AreEqual(true, ps.Get("bool", false));
            Assert.AreEqual(-10, ps.Get("int", 0));
            Assert.AreEqual(TimeSpan.FromMinutes(10), ps.Get("timespan", TimeSpan.Zero));
            Assert.AreEqual(123.45, ps.Get("double", 0.0));
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), ps.Get("ip", IPAddress.Any));
            Assert.AreEqual(new Guid("{28676DD2-1C0F-4217-96DF-1714A7B3BEFA}"), ps.Get("guid", Guid.Empty));
            Assert.IsTrue(new NetworkBinding(IPAddress.Parse("192.168.1.2"), 80).Equals(ps.Get("ep", new IPEndPoint(IPAddress.Any, 0))));
            Assert.IsTrue(object.ReferenceEquals(this.GetType(), ps.Get("type", typeof(int))));

            Assert.AreEqual(TestEnum.Zero, ps.Get<TestEnum>("enum0", TestEnum.Unknown));
            Assert.AreEqual(TestEnum.One, ps.Get<TestEnum>("enum1", TestEnum.Unknown));
            Assert.AreEqual(TestEnum.Two, ps.Get<TestEnum>("enum2", TestEnum.Unknown));
            Assert.AreEqual(TestEnum.Unknown, ps.Get<TestEnum>("enum3", TestEnum.Unknown));

            Assert.AreEqual(new Uri("http://www.lilltek.com/"), ps.Get("uri", (Uri)null));

            DateTime now = Helper.UtcNowRounded;

            ps.Clear();
            ps.Set("bool", true);
            ps.Set("int", -10);
            ps.Set("timespan", TimeSpan.FromMinutes(10));
            ps.Set("date", now);
            ps.Set("double", 123.45);
            ps.Set("ip", IPAddress.Parse("192.168.1.1"));
            ps.Set("guid", new Guid("{28676DD2-1C0F-4217-96DF-1714A7B3BEFA}"));
            ps.Set("ep", new NetworkBinding(IPAddress.Parse("192.168.1.2"), 80));
            ps.Set("long", 1024L * 1024L * 1024L * 1024L);
            ps.Set("uri", new Uri("http://www.lilltek.com/test.htm"));

            Assert.AreEqual(true, ps.Get("bool", false));
            Assert.AreEqual(-10, ps.Get("int", 0));
            Assert.AreEqual(TimeSpan.FromMinutes(10), ps.Get("timespan", TimeSpan.Zero));
            Assert.AreEqual(now, ps.Get("date", DateTime.MinValue));
            Assert.AreEqual(123.45, ps.Get("double", 0.0));
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), ps.Get("ip", IPAddress.Any));
            Assert.AreEqual(new Guid("{28676DD2-1C0F-4217-96DF-1714A7B3BEFA}"), ps.Get("guid", Guid.Empty));
            Assert.IsTrue(new NetworkBinding(IPAddress.Parse("192.168.1.2"), 80).Equals(ps.Get("ep", new IPEndPoint(IPAddress.Any, 0))));
            Assert.AreEqual(Serialize.Parse("1T", 0L), ps.Get("long", -1L));
            Assert.AreEqual(new Uri("http://www.lilltek.com/test.htm"), ps.Get("uri", (Uri)null));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_AltChars()
        {
            ArgCollection ps;

            ps = new ArgCollection(':', '\t');

            Assert.AreEqual(string.Empty, ps.ToString());

            ps.Set("foo", "bar");
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.IsNull(ps["FOOBAR"]);

            Assert.AreEqual("foo:bar\t", ps.ToString());

            ps.Set("foobar", "boohoo");
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = new ArgCollection("foo:bar", ':', '\t');
            Assert.AreEqual("bar", ps["foo"]);

            ps = new ArgCollection("foo:bar\t", ':', '\t');
            Assert.AreEqual("bar", ps["foo"]);

            ps = new ArgCollection("foo:bar\tfoobar:boohoo", ':', '\t');
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = ArgCollection.Parse("foo:bar\tfoobar:boohoo", ':', '\t');
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);

            ps = new ArgCollection(ps.ToString(), ':', '\t');
            Assert.AreEqual("bar", ps["foo"]);
            Assert.AreEqual("bar", ps["FOO"]);
            Assert.AreEqual("boohoo", ps["FOOBAR"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Malformed()
        {
            ArgCollection ps;

            ps = ArgCollection.Parse(";a=1;;b=2; ; c = 3");
            Assert.AreEqual("1", ps["a"]);
            Assert.AreEqual("2", ps["b"]);
            Assert.AreEqual("3", ps["c"]);
            Assert.AreEqual(3, ps.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Cast()
        {
            ArgCollection ps;

            ps = "arg1=a;arg2=b;hello=world!";
            Assert.AreEqual(3, ps.Count);
            Assert.AreEqual("a", ps["arg1"]);
            Assert.AreEqual("b", ps["arg2"]);
            Assert.AreEqual("world!", ps["hello"]);
            Assert.AreEqual(0, ((ArgCollection)(string)null).Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_Remove()
        {
            ArgCollection ps = new ArgCollection();

            ps.Remove("Hello");     // Shouldn't barf if the key doesn't exist

            ps["Hello"] = "World!";
            Assert.IsTrue(ps.ContainsKey("Hello"));
            ps.Remove("Hello");
            Assert.IsFalse(ps.ContainsKey("Hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_ByteSerialize()
        {
            ArgCollection ps;
            byte[] serialized;

            ps = new ArgCollection();
            ps["hello"] = "world!";
            ps["foo"] = "bar";

            serialized = ArgCollection.ToBytes(ps);
            ps = ArgCollection.FromBytes(serialized);

            Assert.AreEqual(2, ps.Count);
            Assert.AreEqual("world!", ps["hello"]);
            Assert.AreEqual("bar", ps["foo"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ArgCollection_ReadOnly()
        {
            ArgCollection ps = new ArgCollection();

            ps["hello"] = "world!";
            Assert.IsFalse(ps.IsReadOnly);
            Assert.AreEqual("world!", ps["hello"]);

            ps.IsReadOnly = true;
            Assert.IsTrue(ps.IsReadOnly);

            Assert.AreEqual("world!", ps["hello"]);
            ExtendedAssert.Throws<InvalidOperationException>(() => ps.IsReadOnly = false);
            ExtendedAssert.Throws<NotSupportedException>(() => ps["foo"] = "bar");
        }
    }
}

