//-----------------------------------------------------------------------------
// FILE:        _Serialize.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Serialize
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_String()
        {
            Assert.AreEqual("", Serialize.ToString(""));
            Assert.AreEqual("abc", Serialize.ToString("abc"));
            Assert.AreEqual("abc", Serialize.ToString("  abc  "));

            Assert.AreEqual("", Serialize.Parse("", "abc"));
            Assert.AreEqual("abc", Serialize.Parse("abc", "xxx"));
            Assert.AreEqual("abc", Serialize.Parse("   abc   ", "xxx"));
            Assert.AreEqual("xxx", Serialize.Parse(null, "xxx"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Int()
        {
            Assert.AreEqual("0", Serialize.ToString(0));
            Assert.AreEqual("10", Serialize.ToString(10));
            Assert.AreEqual("-10", Serialize.ToString(-10));
            Assert.AreEqual("1000000", Serialize.ToString(1000000));
            Assert.AreEqual("-1000000", Serialize.ToString(-1000000));

            Assert.AreEqual((int)short.MinValue, Serialize.Parse("short.min", 0));
            Assert.AreEqual((int)short.MaxValue, Serialize.Parse("short.max", 0));
            Assert.AreEqual((int)int.MinValue, Serialize.Parse("int.min", 0));
            Assert.AreEqual((int)int.MaxValue, Serialize.Parse("int.max", 0));

            Assert.AreEqual(0, Serialize.Parse("0", 5000));
            Assert.AreEqual(10, Serialize.Parse("10", 5000));
            Assert.AreEqual(-10, Serialize.Parse("-10", 5000));
            Assert.AreEqual(1000000, Serialize.Parse("1000000", 5000));
            Assert.AreEqual(-1000000, Serialize.Parse("-1000000", 5000));

            Assert.AreEqual(2 * 1024, Serialize.Parse("2K", 5000));
            Assert.AreEqual(2 * 1024, Serialize.Parse("2k", 5000));
            Assert.AreEqual(2 * 1024 * 1024, Serialize.Parse("2m", 5000));
            Assert.AreEqual(2 * 1024 * 1024, Serialize.Parse("2M", 5000));
            Assert.AreEqual(1 * 1024 * 1024 * 1024, Serialize.Parse("1g", 5000));
            Assert.AreEqual(1 * 1024 * 1024 * 1024, Serialize.Parse("1G", 5000));
            Assert.AreEqual(5000, Serialize.Parse("1Q", 5000));

            Assert.AreEqual(5000, Serialize.Parse(null, 5000));
            Assert.AreEqual(5000, Serialize.Parse("", 5000));
            Assert.AreEqual(5000, Serialize.Parse("xxx", 5000));
            Assert.AreEqual(5000, Serialize.Parse("--10", 5000));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Long()
        {
            Assert.AreEqual("0", Serialize.ToString(0L));
            Assert.AreEqual("10", Serialize.ToString(10L));
            Assert.AreEqual("-10", Serialize.ToString(-10L));
            Assert.AreEqual("1000000", Serialize.ToString(1000000L));
            Assert.AreEqual("-1000000", Serialize.ToString(-1000000L));

            Assert.AreEqual((long)short.MinValue, Serialize.Parse("short.min", 0L));
            Assert.AreEqual((long)short.MaxValue, Serialize.Parse("short.max", 0L));
            Assert.AreEqual((long)int.MinValue, Serialize.Parse("int.min", 0L));
            Assert.AreEqual((long)int.MinValue, Serialize.Parse("int.min", 0L));
            Assert.AreEqual((long)uint.MaxValue, Serialize.Parse("uint.max", 0L));
            Assert.AreEqual((long)long.MinValue, Serialize.Parse("long.min", 0L));
            Assert.AreEqual((long)long.MaxValue, Serialize.Parse("long.max", 0L));

            Assert.AreEqual(0L, Serialize.Parse("0", 5000L));
            Assert.AreEqual(10L, Serialize.Parse("10", 5000L));
            Assert.AreEqual(-10L, Serialize.Parse("-10", 5000L));
            Assert.AreEqual(1000000L, Serialize.Parse("1000000", 5000L));
            Assert.AreEqual(-1000000L, Serialize.Parse("-1000000", 5000L));

            Assert.AreEqual(2 * 1024L, Serialize.Parse("2K", 5000L));
            Assert.AreEqual(2 * 1024L, Serialize.Parse("2k", 5000L));
            Assert.AreEqual(2 * 1024L * 1024L, Serialize.Parse("2m", 5000L));
            Assert.AreEqual(2 * 1024L * 1024L, Serialize.Parse("2M", 5000L));
            Assert.AreEqual(1 * 1024L * 1024L * 1024L, Serialize.Parse("1g", 5000L));
            Assert.AreEqual(1 * 1024L * 1024L * 1024L, Serialize.Parse("1G", 5000L));
            Assert.AreEqual(1 * 1024L * 1024L * 1024L * 1024L, Serialize.Parse("1t", 5000L));
            Assert.AreEqual(2 * 1024L * 1024L * 1024L * 1024L, Serialize.Parse("2T", 5000L));
            Assert.AreEqual(5000L, Serialize.Parse("1Q", 5000L));

            Assert.AreEqual(5000L, Serialize.Parse(null, 5000L));
            Assert.AreEqual(5000L, Serialize.Parse("", 5000L));
            Assert.AreEqual(5000L, Serialize.Parse("xxx", 5000L));
            Assert.AreEqual(5000L, Serialize.Parse("--10", 5000L));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Bool()
        {
            Assert.AreEqual("0", Serialize.ToString(false));
            Assert.AreEqual("1", Serialize.ToString(true));

            Assert.IsTrue(Serialize.Parse("1", false));
            Assert.IsTrue(Serialize.Parse("yes", false));
            Assert.IsTrue(Serialize.Parse("on", false));
            Assert.IsTrue(Serialize.Parse("true", false));
            Assert.IsTrue(Serialize.Parse("high", false));
            Assert.IsTrue(Serialize.Parse("enable", false));

            Assert.IsFalse(Serialize.Parse("0", true));
            Assert.IsFalse(Serialize.Parse("no", true));
            Assert.IsFalse(Serialize.Parse("off", true));
            Assert.IsFalse(Serialize.Parse("low", true));
            Assert.IsFalse(Serialize.Parse("false", true));
            Assert.IsFalse(Serialize.Parse("disable", true));

            Assert.IsFalse(Serialize.Parse(null, false));
            Assert.IsFalse(Serialize.Parse("", false));
            Assert.IsFalse(Serialize.Parse("xx", false));

            Assert.IsTrue(Serialize.Parse(null, true));
            Assert.IsTrue(Serialize.Parse("", true));
            Assert.IsTrue(Serialize.Parse("xx", true));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Double()
        {
            Assert.AreEqual("10", Serialize.ToString(10.0));
            Assert.AreEqual("-10", Serialize.ToString(-10.0));
            Assert.AreEqual("10.1", Serialize.ToString(10.1));

            Assert.AreEqual((double)short.MinValue, Serialize.Parse("short.min", 0.0));
            Assert.AreEqual((double)short.MaxValue, Serialize.Parse("short.max", 0.0));
            Assert.AreEqual((double)int.MinValue, Serialize.Parse("int.min", 0.0));
            Assert.AreEqual((double)int.MinValue, Serialize.Parse("int.min", 0.0));
            Assert.AreEqual((double)uint.MaxValue, Serialize.Parse("uint.max", 0.0));
            Assert.AreEqual((double)long.MinValue, Serialize.Parse("long.min", 0.0));
            Assert.AreEqual((double)long.MaxValue, Serialize.Parse("long.max", 0.0));

            Assert.AreEqual(10.0, Serialize.Parse("10", 5000.0));
            Assert.AreEqual(-10.0, Serialize.Parse("-10", 5000.0));
            Assert.AreEqual(10.1, Serialize.Parse("10.1", 5000.0));

            Assert.AreEqual(2 * 1024, Serialize.Parse("2K", 5000.0));
            Assert.AreEqual(2 * 1024, Serialize.Parse("2k", 5000.0));
            Assert.AreEqual(2 * 1024 * 1024, Serialize.Parse("2m", 5000.0));
            Assert.AreEqual(2 * 1024 * 1024, Serialize.Parse("2M", 5000.0));
            Assert.AreEqual(2.0 * 1024 * 1024 * 1024, Serialize.Parse("2g", 5000.0));
            Assert.AreEqual(2.0 * 1024 * 1024 * 1024, Serialize.Parse("2G", 5000.0));
            Assert.AreEqual(5000.0, Serialize.Parse("1Q", 5000.0));

            Assert.AreEqual(5000.0, Serialize.Parse(null, 5000.0));
            Assert.AreEqual(5000.0, Serialize.Parse("", 5000.0));
            Assert.AreEqual(5000.0, Serialize.Parse("xx", 5000.0));
            Assert.AreEqual(5000.0, Serialize.Parse("--1.0", 5000.0));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Timespan()
        {
            var def = TimeSpan.FromSeconds(5000);

            Assert.AreEqual("0", Serialize.ToString(TimeSpan.Zero));
            Assert.AreEqual("10ms", Serialize.ToString(TimeSpan.FromMilliseconds(10)));
            Assert.AreEqual("10s", Serialize.ToString(TimeSpan.FromSeconds(10)));
            Assert.AreEqual("10m", Serialize.ToString(TimeSpan.FromMinutes(10)));
            Assert.AreEqual("10h", Serialize.ToString(TimeSpan.FromHours(10)));
            Assert.AreEqual("10d", Serialize.ToString(TimeSpan.FromDays(10)));
            Assert.AreEqual("infinite", Serialize.ToString(TimeSpan.MaxValue));

            Assert.AreEqual(TimeSpan.MaxValue, Serialize.Parse("infinite", def));
            Assert.AreEqual(TimeSpan.MaxValue, Serialize.Parse("INFINITE", def));

            Assert.AreEqual(TimeSpan.Zero, Serialize.Parse("0", def));
            Assert.AreEqual(TimeSpan.Zero, Serialize.Parse("0s", def));
            Assert.AreEqual(TimeSpan.FromMilliseconds(10), Serialize.Parse("10ms", def));
            Assert.AreEqual(TimeSpan.FromSeconds(10), Serialize.Parse("10s", def));
            Assert.AreEqual(TimeSpan.FromMinutes(10), Serialize.Parse("10m", def));
            Assert.AreEqual(TimeSpan.FromHours(10), Serialize.Parse("10h", def));
            Assert.AreEqual(TimeSpan.FromDays(10), Serialize.Parse("10d", def));

            Assert.AreEqual(def, Serialize.Parse(null, def));
            Assert.AreEqual(def, Serialize.Parse("", def));
            Assert.AreEqual(def, Serialize.Parse("xx", def));

            Assert.AreEqual(new TimeSpan(0, 1, 30, 0, 0), Serialize.Parse("1:30", def));
            Assert.AreEqual(new TimeSpan(0, 0, 1, 30, 50), Serialize.Parse("00:1:30.050", def));
            Assert.AreEqual(new TimeSpan(0, 1, 2, 3, 400), Serialize.Parse("1:2:3.4", def));
            Assert.AreEqual(new TimeSpan(5, 1, 2, 3, 400), Serialize.Parse("5.1:2:3.4", def));

            Assert.AreEqual("10:11", Serialize.ToTimeString(Serialize.Parse("10:11", TimeSpan.Zero)));
            Assert.AreEqual("-10:11", Serialize.ToTimeString(Serialize.Parse("-10:11", TimeSpan.Zero)));
            Assert.AreEqual("5.10:11", Serialize.ToTimeString(Serialize.Parse("5.10:11", TimeSpan.Zero)));
            Assert.AreEqual("5.00:00", Serialize.ToTimeString(TimeSpan.FromDays(5)));
            Assert.AreEqual("5.10:11:02", Serialize.ToTimeString(Serialize.Parse("5.10:11:02", TimeSpan.Zero)));
            Assert.AreEqual("5.10:11:02.002", Serialize.ToTimeString(Serialize.Parse("5.10:11:02.002", TimeSpan.Zero)));
            Assert.AreEqual("5.01:02:03.150", Serialize.ToTimeString(Serialize.Parse("5.01:02:03.150", TimeSpan.Zero)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_IPAddr()
        {
            var def = IPAddress.Parse("255.255.255.255");

            Assert.AreEqual("1.2.3.4", Serialize.ToString(IPAddress.Parse("1.2.3.4")));

            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), Serialize.Parse("1.2.3.4", def));
            Assert.AreEqual(def, Serialize.Parse(null, def));
            Assert.AreEqual(def, Serialize.Parse("", def));
            Assert.AreEqual(def, Serialize.Parse("xx", def));
            Assert.AreEqual(def, Serialize.Parse("a.b.c.d", def));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_NetworkBinding()
        {
            var def = new IPEndPoint(IPAddress.Parse("10.20.30.40"), 50);

            Assert.AreEqual("1.2.3.4:5", Serialize.ToString(new NetworkBinding(IPAddress.Parse("1.2.3.4"), 5)));

            Assert.AreEqual(new NetworkBinding(IPAddress.Parse("1.2.3.4"), 5), Serialize.Parse("1.2.3.4:5", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse(null, def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("xx", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("a.b.c.d", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("a.b.c.d:e", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("1.2.3.4", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("1.2.3.4:e", def));
            Assert.AreEqual(def, (IPEndPoint)Serialize.Parse("1.2.3.4:", def));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_GUID()
        {
            var def = Helper.NewGuid();
            var v = Helper.NewGuid();

            Assert.AreEqual(v.ToString(), Serialize.ToString(v));

            Assert.AreEqual(v, Serialize.Parse(v.ToString(), def));
            Assert.AreEqual(def, Serialize.Parse(null, def));
            Assert.AreEqual(def, Serialize.Parse("", def));
            Assert.AreEqual(def, Serialize.Parse("hello", def));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_ByteArray()
        {
            var def = new byte[] { 5, 4, 3, 2, 1 };
            var v = new byte[] { 0, 1, 2, 3, 4 };

            Assert.AreEqual(Helper.ToHex(v), Serialize.ToString(v));

            CollectionAssert.AreEqual(v, Serialize.Parse(Helper.ToHex(v), def));
            CollectionAssert.AreEqual(def, Serialize.Parse(null, def));
            CollectionAssert.AreEqual(def, Serialize.Parse("hello", def));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Date()
        {
            var now = DateTime.UtcNow;
            var s = Serialize.ToString(now);

            Assert.AreEqual(now, new DateTime(long.Parse(s)));
            Assert.AreEqual(s, now.Ticks.ToString());
            Assert.AreEqual(now, Serialize.Parse(s, DateTime.MaxValue));

            s = new DateTime(2008, 10, 11, 11, 12, 15).ToString();
            Assert.AreEqual(new DateTime(2008, 10, 11, 11, 12, 15), Serialize.Parse(s, DateTime.MaxValue));

            now = Helper.UtcNowRounded;
            Assert.AreEqual(now, Serialize.Parse(Helper.ToInternetDate(now), DateTime.MaxValue));

            now = Helper.UtcNowRounded;
            Assert.AreEqual(now, Serialize.Parse(now.ToString(), DateTime.MaxValue));
        }

        public enum TestEnums
        {
            Zero,
            One,
            Two,
            Three,
            Four
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Enums()
        {
            Assert.AreEqual(TestEnums.One, Serialize.Parse(null, typeof(TestEnums), TestEnums.One));
            Assert.AreEqual(TestEnums.Two, Serialize.Parse("xxxx", typeof(TestEnums), TestEnums.Two));
            Assert.AreEqual(TestEnums.Three, Serialize.Parse("Three", typeof(TestEnums), TestEnums.One));
            Assert.AreEqual(TestEnums.Four, Serialize.Parse("FOUR", typeof(TestEnums), TestEnums.One));

            Assert.AreEqual(TestEnums.One, Serialize.Parse<TestEnums>(null, TestEnums.One));
            Assert.AreEqual(TestEnums.Two, Serialize.Parse<TestEnums>("xxxx", TestEnums.Two));
            Assert.AreEqual(TestEnums.Three, Serialize.Parse<TestEnums>("Three", TestEnums.One));
            Assert.AreEqual(TestEnums.Four, Serialize.Parse<TestEnums>("FOUR", TestEnums.One));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Uri()
        {
            Assert.AreEqual(new Uri("http://www.lilltek.com/"), Serialize.Parse("http://www.lilltek.com/", (Uri)null));
            Assert.AreEqual(new Uri("http://www.lilltek.com/"), Serialize.Parse(null, new Uri("http://www.lilltek.com/")));
            Assert.AreEqual(new Uri("http://www.lilltek.com/"), Serialize.Parse("bad uri", new Uri("http://www.lilltek.com/")));
            Assert.IsNull(Serialize.Parse("", (Uri)null));

            Assert.AreEqual("http://www.lilltek.com/", Serialize.ToString(new Uri("http://www.lilltek.com/")));
            Assert.AreEqual("", Serialize.ToString((Uri)null));
        }

        [Serializable]
        public class Root
        {
            public string Name;
            public TestEnums Code;
            public Leaf[] Leaves;
        }

        [Serializable]
        public class Leaf
        {
            public string Name;

            public Leaf(string name)
            {
                this.Name = name;
            }
        }

        [Serializable]
        [DataContract]
        public class TestRecord
        {
            [DataMember]
            public string Field1;

            [DataMember]
            public string Field2;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Binary_Null()
        {
            var ms = new MemoryStream();

            Serialize.ToBinary(ms, null, Compress.None);
            ms.Position = 0;
            Assert.IsNull(Serialize.FromBinary(ms));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Binary_ToStream_NoCompress()
        {
            var ms = new MemoryStream();
            Root root;

            root = new Root();
            root.Name = "hello";
            root.Code = TestEnums.Three;
            root.Leaves = new Leaf[] { new Leaf("one"), new Leaf("two"), new Leaf("three") };

            Serialize.ToBinary(ms, root, Compress.None);
            ms.Position = 0;
            root = (Root)Serialize.FromBinary(ms);

            Assert.AreEqual("hello", root.Name);
            Assert.AreEqual(TestEnums.Three, root.Code);

            Assert.AreEqual(3, root.Leaves.Length);
            Assert.AreEqual("one", root.Leaves[0].Name);
            Assert.AreEqual("two", root.Leaves[1].Name);
            Assert.AreEqual("three", root.Leaves[2].Name);

            short[] org, input;

            org = new short[25000];
            for (int i = 0; i < 25000; i++)
                org[i] = (short)i;

            ms.SetLength(0);
            Serialize.ToBinary(ms, org, Compress.None);
            ms.Position = 0;
            input = (short[])Serialize.FromBinary(ms);
            CollectionAssert.AreEqual(org, input);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Binary_ToStream_Compress()
        {
            var ms = new MemoryStream();
            Root root;

            root = new Root();
            root.Name = "hello";
            root.Code = TestEnums.Three;
            root.Leaves = new Leaf[] { new Leaf("one"), new Leaf("two"), new Leaf("three") };

            Serialize.ToBinary(ms, root, Compress.Always);
            ms.Position = 0;
            root = (Root)Serialize.FromBinary(ms);

            Assert.AreEqual("hello", root.Name);
            Assert.AreEqual(TestEnums.Three, root.Code);

            Assert.AreEqual(3, root.Leaves.Length);
            Assert.AreEqual("one", root.Leaves[0].Name);
            Assert.AreEqual("two", root.Leaves[1].Name);
            Assert.AreEqual("three", root.Leaves[2].Name);

            short[] org, input;

            org = new short[25000];
            for (int i = 0; i < 25000; i++)
                org[i] = (short)i;

            ms.SetLength(0);
            Serialize.ToBinary(ms, org, Compress.Always);
            ms.Position = 0;
            input = (short[])Serialize.FromBinary(ms);
            CollectionAssert.AreEqual(org, input);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Binary_ToStream_CompressBest()
        {
            var ms = new MemoryStream();
            Root root;

            root = new Root();
            root.Name = "hello";
            root.Code = TestEnums.Three;
            root.Leaves = new Leaf[] { new Leaf("one"), new Leaf("two"), new Leaf("three") };

            Serialize.ToBinary(ms, root, Compress.Best);
            ms.Position = 0;
            root = (Root)Serialize.FromBinary(ms);

            Assert.AreEqual("hello", root.Name);
            Assert.AreEqual(TestEnums.Three, root.Code);

            Assert.AreEqual(3, root.Leaves.Length);
            Assert.AreEqual("one", root.Leaves[0].Name);
            Assert.AreEqual("two", root.Leaves[1].Name);
            Assert.AreEqual("three", root.Leaves[2].Name);

            short[] org, input;
            int cbCompressed;

            org = new short[25000];
            for (int i = 0; i < 25000; i++)
                org[i] = (short)i;

            ms.SetLength(0);
            Serialize.ToBinary(ms, org, Compress.Best);
            ms.Position = 0;
            cbCompressed = (int)ms.Length;
            input = (short[])Serialize.FromBinary(ms);
            CollectionAssert.AreEqual(org, input);

            // Verify that the compressed version is not larger than the 
            // uncompressed version.

            ms.SetLength(0);
            Serialize.ToBinary(ms, org, Compress.None);
            ms.Position = 0;
            Assert.IsTrue(cbCompressed <= (int)ms.Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Binary_ToArray()
        {
            byte[] serialized;
            Root root;

            root = new Root();
            root.Name = "hello";
            root.Code = TestEnums.Three;
            root.Leaves = new Leaf[] { new Leaf("one"), new Leaf("two"), new Leaf("three") };

            serialized = Serialize.ToBinary(root, Compress.Always);
            root = (Root)Serialize.FromBinary(serialized);

            Assert.AreEqual("hello", root.Name);
            Assert.AreEqual(TestEnums.Three, root.Code);

            Assert.AreEqual(3, root.Leaves.Length);
            Assert.AreEqual("one", root.Leaves[0].Name);
            Assert.AreEqual("two", root.Leaves[1].Name);
            Assert.AreEqual("three", root.Leaves[2].Name);

            short[] org, input;

            org = new short[25000];
            for (int i = 0; i < 25000; i++)
                org[i] = (short)i;

            serialized = Serialize.ToBinary(org, Compress.Always);
            input = (short[])Serialize.FromBinary(serialized);
            CollectionAssert.AreEqual(org, input);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Xml_ToStream()
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            var rec = new TestRecord();
            byte[] buffer;

            rec.Field1 = "Hello";
            rec.Field2 = "World!";

            Serialize.ToXml(writer, rec);
            writer.Flush();
            buffer = ms.ToArray();

            // Verify that no default namespace is generated

            ms.Position = 0;
            using (StreamReader reader = new StreamReader(ms))
            {
                var serialized = reader.ReadToEnd();

                Assert.IsTrue(serialized.IndexOf("xmlns") == -1);
            }

            // Verify that the object can be deserialized

            ms = new MemoryStream(buffer);
            using (StreamReader reader = new StreamReader(ms))
                rec = (TestRecord)Serialize.FromXml(reader, typeof(TestRecord));

            Assert.AreEqual("Hello", rec.Field1);
            Assert.AreEqual("World!", rec.Field2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Serialize_Xml_ToString()
        {
            var rec = new TestRecord();
            string serialized;

            rec.Field1 = "Hello";
            rec.Field2 = "World!";

            serialized = Serialize.ToXml<TestRecord>(rec);
            rec = (TestRecord)Serialize.FromXml<TestRecord>(serialized);

            Assert.AreEqual("Hello", rec.Field1);
            Assert.AreEqual("World!", rec.Field2);
        }
    }
}

