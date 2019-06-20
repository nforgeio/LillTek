//-----------------------------------------------------------------------------
// FILE:        _EnhancedStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for EnhancedStream

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _EnhancedStream
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Bool()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteBool(true);
            es.WriteBool(false);

            es.Seek(0, SeekOrigin.Begin);

            Assert.IsTrue(es.ReadBool());
            Assert.IsFalse(es.ReadBool());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Byte()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteByte(77);
            es.WriteByte(99);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(77, es.ReadByte());
            Assert.AreEqual(99, es.ReadByte());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int16()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt16(0);
            es.WriteInt16(55);
            es.WriteInt16(-1);
            es.WriteInt16(-5000);
            es.WriteInt16(0x1234);
            es.WriteInt16(0xAABB);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt16());
            Assert.AreEqual(55, es.ReadInt16());
            Assert.AreEqual(-1, es.ReadInt16());
            Assert.AreEqual(-5000, es.ReadInt16());
            Assert.AreEqual(0x1234, es.ReadInt16());
            CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, es.ReadBytes(2));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int16Le()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt16Le(0);
            es.WriteInt16Le(55);
            es.WriteInt16Le(-1);
            es.WriteInt16Le(-5000);
            es.WriteInt16Le(0x1234);
            es.WriteInt16Le(0xAABB);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt16Le());
            Assert.AreEqual(55, es.ReadInt16Le());
            Assert.AreEqual(-1, es.ReadInt16Le());
            Assert.AreEqual(-5000, es.ReadInt16Le());
            Assert.AreEqual(0x1234, es.ReadInt16Le());
            CollectionAssert.AreEqual(new byte[] { 0xBB, 0xAA }, es.ReadBytes(2));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int32()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt32(0);
            es.WriteInt32(65121);
            es.WriteInt32(0x12345678);
            es.WriteInt32(0x01020304);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt32());
            Assert.AreEqual(65121, es.ReadInt32());
            Assert.AreEqual(0x12345678, es.ReadInt32());
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, es.ReadBytes(4));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int32Le()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt32Le(0);
            es.WriteInt32Le(65121);
            es.WriteInt32Le(0x12345678);
            es.WriteInt32Le(0x01020304);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt32Le());
            Assert.AreEqual(65121, es.ReadInt32Le());
            Assert.AreEqual(0x12345678, es.ReadInt32Le());
            CollectionAssert.AreEqual(new byte[] { 0x04, 0x03, 0x02, 0x01 }, es.ReadBytes(4));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int64()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt64(0);
            es.WriteInt64(65121);
            es.WriteInt64(0x12345678);
            es.WriteInt64((((long)0x12345678) << 32) | (long)0xaabbccddee);
            es.WriteInt64((((long)0x01020304) << 32) | (long)0x05060708);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt64());
            Assert.AreEqual(65121, es.ReadInt64());
            Assert.AreEqual(0x12345678, es.ReadInt64());
            Assert.AreEqual((((long)0x12345678) << 32) | (long)0xaabbccddee, es.ReadInt64());
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, es.ReadBytes(8));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Int64Le()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt64Le(0);
            es.WriteInt64Le(65121);
            es.WriteInt64Le(0x12345678);
            es.WriteInt64Le((((long)0x12345678) << 32) | (long)0xaabbccddee);
            es.WriteInt64Le((((long)0x01020304) << 32) | (long)0x05060708);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt64Le());
            Assert.AreEqual(65121, es.ReadInt64Le());
            Assert.AreEqual(0x12345678, es.ReadInt64Le());
            Assert.AreEqual((((long)0x12345678) << 32) | (long)0xaabbccddee, es.ReadInt64Le());
            CollectionAssert.AreEqual(new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, es.ReadBytes(8));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Float()
        {
            var es = new EnhancedStream(new MemoryStream());
            float f;

            es.WriteFloat((float)123.456);
            es.Seek(0, SeekOrigin.Begin);
            f = es.ReadFloat();
            Assert.AreEqual((float)123.456, f);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Bytes()
        {
            var es = new EnhancedStream(new MemoryStream());
            byte[] read, write;

            es.WriteBytes16(null);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes16();
            Assert.IsNull(read);

            write = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            es.Seek(0, SeekOrigin.Begin);
            es.WriteBytes16(write);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes16();
            CollectionAssert.AreEqual(write, read);

            write = new byte[40000];
            for (int i = 0; i < write.Length; i++)
                write[i] = (byte)i;

            es.Seek(0, SeekOrigin.Begin);
            es.WriteBytes16(write);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes16();
            CollectionAssert.AreEqual(write, read);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Bytes32()
        {
            var es = new EnhancedStream(new MemoryStream());
            byte[] read, write;

            es.WriteBytes32(null);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes32();
            Assert.IsNull(read);

            write = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            es.Seek(0, SeekOrigin.Begin);
            es.WriteBytes32(write);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes32();
            CollectionAssert.AreEqual(write, read);

            write = new byte[40000];
            for (int i = 0; i < write.Length; i++)
                write[i] = (byte)i;

            es.Seek(0, SeekOrigin.Begin);
            es.WriteBytes32(write);
            es.Seek(0, SeekOrigin.Begin);
            read = es.ReadBytes32();
            CollectionAssert.AreEqual(write, read);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_String()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteString16(null);
            es.WriteString16("Hello World!");
            es.WriteString16(new String('a', 45000));

            es.Seek(0, SeekOrigin.Begin);
            Assert.IsNull(es.ReadString16());
            Assert.AreEqual("Hello World!", es.ReadString16());
            Assert.AreEqual(new String('a', 45000), es.ReadString16());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_ReadStringCb()
        {
            var es = new EnhancedStream(new MemoryStream());
            int cb;

            es.WriteStringNoLen("Hello World!");
            cb = (int)es.Position;

            es.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual("Hello World!", es.ReadString(cb));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_String32()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteString32(null);
            es.WriteString32("Hello World!");
            es.WriteString32(new String('a', 45000));

            es.Seek(0, SeekOrigin.Begin);
            Assert.IsNull(es.ReadString32());
            Assert.AreEqual("Hello World!", es.ReadString32());
            Assert.AreEqual(new String('a', 45000), es.ReadString32());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_ReadWriteBytesNoLen()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteBytesNoLen(new byte[] { 0, 1, 2, 3 });
            es.Seek(0, SeekOrigin.Begin);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, es.ReadBytes(4));
            es.Seek(0, SeekOrigin.Begin);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, es.ReadBytes(100));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Eof()
        {
            var es = new EnhancedStream(new MemoryStream());

            Assert.IsTrue(es.Eof);
            es.WriteBytes16(new byte[] { 0, 1, 2, 4, 5 });
            Assert.IsTrue(es.Eof);
            es.Position = 0;
            Assert.IsTrue(!es.Eof);
            es.Seek(0, SeekOrigin.End);
            Assert.IsTrue(es.Eof);
            es.SetLength(5000);
            Assert.IsTrue(!es.Eof);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_VerifyBufLength()
        {
            var es = new EnhancedStream(new MemoryStream());

            es.WriteInt16(5000);
            es.Seek(0, SeekOrigin.Begin);
            try
            {
                es.ReadBytes16();
                Assert.Fail();
            }
            catch
            {
            }

            es.Seek(0, SeekOrigin.Begin);
            try
            {
                es.ReadString16();
                Assert.Fail();
            }
            catch
            {
            }

            es.Seek(0, SeekOrigin.Begin);
            es.WriteInt32(500000);
            try
            {
                es.ReadBytes32();
                Assert.Fail();
            }
            catch
            {
            }

            es.Seek(0, SeekOrigin.Begin);
            try
            {
                es.ReadString32();
                Assert.Fail();
            }
            catch
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_CopyTo()
        {
            var esSrc = new EnhancedStream(new MemoryStream());
            var esDst = new EnhancedStream(new MemoryStream());
            byte[] buf;

            Assert.AreEqual(0, esSrc.CopyTo(esDst, 10));

            esSrc.WriteBytesNoLen(new byte[] { 0, 1, 2, 3, 4 });
            esSrc.Position = 0;
            Assert.AreEqual(5, esSrc.CopyTo(esDst, 10));
            Assert.AreEqual(5, esSrc.Position);
            Assert.AreEqual(5, esDst.Position);
            esDst.Position = 0;
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, esDst.ReadBytes(5));

            buf = new byte[37000];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)i;

            esSrc.Position = 0;
            esDst.Position = 0;
            esSrc.WriteBytesNoLen(buf);
            esSrc.Position = 0;
            Assert.AreEqual(buf.Length, esSrc.CopyTo(esDst, buf.Length));
            esDst.Position = 0;
            CollectionAssert.AreEqual(buf, esDst.ReadBytes(buf.Length));

            esSrc.WriteBytesNoLen(new byte[] { 0, 1, 2, 3, 4 });
            esSrc.Position = 0;
            esDst.Position = 0;
            esDst.SetLength(0);
            esSrc.SetLength(5);
            Assert.AreEqual(5, esSrc.CopyTo(esDst, -1));
            Assert.AreEqual(5, esSrc.Position);
            Assert.AreEqual(5, esDst.Position);
            esDst.Position = 0;
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, esDst.ReadBytes(5));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_CopyFrom()
        {
            var esSrc = new EnhancedStream(new MemoryStream());
            var esDst = new EnhancedStream(new MemoryStream());
            byte[] buf;

            Assert.AreEqual(0, esSrc.CopyTo(esDst, 10));

            esSrc.WriteBytesNoLen(new byte[] { 0, 1, 2, 3, 4 });
            esSrc.Position = 0;
            Assert.AreEqual(5, esDst.CopyFrom(esSrc, 10));
            Assert.AreEqual(5, esSrc.Position);
            Assert.AreEqual(5, esDst.Position);
            esDst.Position = 0;
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, esDst.ReadBytes(5));

            buf = new byte[37000];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)i;

            esSrc.Position = 0;
            esDst.Position = 0;
            esSrc.WriteBytesNoLen(buf);
            esSrc.Position = 0;
            Assert.AreEqual(buf.Length, esDst.CopyFrom(esSrc, buf.Length));
            esDst.Position = 0;
            CollectionAssert.AreEqual(buf, esDst.ReadBytes(buf.Length));

            esSrc.Position = 0;
            esDst.Position = 0;
            esDst.SetLength(0);
            esSrc.WriteBytesNoLen(buf);
            esSrc.SetLength(5);
            esSrc.Position = 0;
            Assert.AreEqual(5, esDst.CopyFrom(esSrc, -1));
            esDst.Position = 0;
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, esDst.ReadBytes(5));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_PropertySet()
        {
            var es = new EnhancedStream(new MemoryStream());
            Dictionary<string, string> props;

            props = new Dictionary<string, string>();
            props["hello"] = "world!";
            props["foo"] = "bar";

            es.WriteProperties(props);
            es.Position = 0;

            props = es.ReadProperties(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(2, props.Count);
            Assert.AreEqual("world!", props["hello"]);
            Assert.AreEqual("bar", props["foo"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_Copy()
        {
            var ms1 = new MemoryStream();
            var ms2 = new MemoryStream();
            byte[] buf = new byte[10];

            ms1.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 10);

            ms1.Position = 0;
            ms2.SetLength(0);
            EnhancedStream.Copy(ms1, ms2, 10);
            ms2.Position = 0;
            Assert.AreEqual(10, ms2.Length);
            ms2.Read(buf, 0, 10);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buf);

            ms1.Position = 0;
            ms2.SetLength(0);
            EnhancedStream.Copy(ms1, ms2, int.MaxValue);
            ms2.Position = 0;
            Assert.AreEqual(10, ms2.Length);
            ms2.Read(buf, 0, 10);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buf);

            ms1.Position = 0;
            ms2.SetLength(0);
            EnhancedStream.Copy(ms1, ms2, 5);
            ms2.Position = 0;
            Assert.AreEqual(5, ms2.Length);
            buf = new byte[] { 0, 0, 0, 0, 0, 90, 91, 92, 93, 94 };
            ms2.Read(buf, 0, 5);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 90, 91, 92, 93, 94 }, buf);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedStream_ReadAllText()
        {
            const string test = "This is a test of the emergency broadcasting system.";

            var ms = new EnhancedMemoryStream();

            ms.WriteBytesNoLen(Encoding.Unicode.GetBytes(test));
            ms.Position = 0;
            Assert.AreEqual(test, ms.ReadAllText(Encoding.Unicode));
        }
    }
}

