//-----------------------------------------------------------------------------
// FILE:        _AppLogRecord.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _AppLogRecord
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogRecord_Equals()
        {
            AppLogRecord r1, r2;

            r1 = new AppLogRecord();
            r2 = new AppLogRecord();
            Assert.IsTrue(r1.Equals(r2));

            r1.Add("foo", "bar");
            Assert.IsFalse(r1.Equals(r2));

            r2.Add("foo", "bar");
            Assert.IsTrue(r1.Equals(r2));

            r1.Add("array", new byte[] { 1, 2, 3 });
            Assert.IsFalse(r1.Equals(r2));

            r2.Add("array", new byte[] { 1, 2, 3 });
            r1.Equals(r2);
            Assert.IsTrue(r1.Equals(r2));

            r1.Add("test", "string");
            r2.Add("test", new byte[] { 0 });
            Assert.IsFalse(r1.Equals(r2));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogRecord_ReadWrite()
        {
            EnhancedMemoryStream es = new EnhancedMemoryStream();
            AppLogRecord r;

            for (int i = 0; i < 1000; i++)
            {
                byte[] arr;

                arr = new byte[i];
                for (int j = 0; j < i; j++)
                    arr[j] = (byte)j;

                r = new AppLogRecord();
                r.Add("index", i.ToString());
                r.Add("bytes", arr);

                r.Write(es);
            }

            es.Position = 0;

            for (int i = 0; i < 1000; i++)
            {
                byte[] arr;

                arr = new byte[i];
                for (int j = 0; j < i; j++)
                    arr[j] = (byte)j;

                r = new AppLogRecord();
                r.Read(es);

                Assert.AreEqual(i.ToString(), (string)r["index"]);
                CollectionAssert.AreEqual(arr, (byte[])r["bytes"]);
            }

            Assert.IsTrue(es.Eof);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogRecord_ReadWrite_Indexer()
        {
            EnhancedMemoryStream es = new EnhancedMemoryStream();
            AppLogRecord r;

            for (int i = 0; i < 1000; i++)
            {
                byte[] arr;

                arr = new byte[i];
                for (int j = 0; j < i; j++)
                    arr[j] = (byte)j;

                r = new AppLogRecord();
                r["index"] = i;
                r["bytes"] = arr;
                r["bool"] = true;

                r.Write(es);
            }

            es.Position = 0;

            for (int i = 0; i < 1000; i++)
            {
                byte[] arr;

                arr = new byte[i];
                for (int j = 0; j < i; j++)
                    arr[j] = (byte)j;

                r = new AppLogRecord();
                r.Read(es);

                Assert.AreEqual(i.ToString(), (string)r["index"]);
                CollectionAssert.AreEqual(arr, (byte[])r["bytes"]);
                Assert.AreEqual("True", (string)r["bool"]);
            }

            Assert.IsTrue(es.Eof);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogRecord_ReadWrite_LargeByteArray()
        {
            EnhancedMemoryStream es = new EnhancedMemoryStream();
            AppLogRecord r;
            byte[] arr;

            arr = new byte[1000000];
            for (int j = 0; j < arr.Length; j++)
                arr[j] = (byte)j;

            r = new AppLogRecord();
            r.Add("bytes", arr);
            r.Write(es);

            es.Position = 0;

            r = new AppLogRecord();
            r.Read(es);
            CollectionAssert.AreEqual(arr, (byte[])r["bytes"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogRecord_ReadWrite_LargeString()
        {
            EnhancedMemoryStream es = new EnhancedMemoryStream();
            AppLogRecord r;
            string s;

            s = new String('x', 1000000);

            r = new AppLogRecord();
            r.Add("string", s);
            r.Write(es);

            es.Position = 0;

            r = new AppLogRecord();
            r.Read(es);
            Assert.AreEqual(s, (string)r["string"]);
        }
    }
}

