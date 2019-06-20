//-----------------------------------------------------------------------------
// FILE:        _EnhancedBlockStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for EnhancedBlockStream

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _EnhancedBlockStream
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Bool()
        {
            var es = new EnhancedBlockStream();

            es.WriteBool(true);
            es.WriteBool(false);

            es.Seek(0, SeekOrigin.Begin);

            Assert.IsTrue(es.ReadBool());
            Assert.IsFalse(es.ReadBool());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Byte()
        {
            var es = new EnhancedBlockStream();

            es.WriteByte(77);
            es.WriteByte(99);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(77, es.ReadByte());
            Assert.AreEqual(99, es.ReadByte());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Int16()
        {
            var es = new EnhancedBlockStream();

            es.WriteInt16(0);
            es.WriteInt16(55);
            es.WriteInt16(0x1234);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt16());
            Assert.AreEqual(55, es.ReadInt16());
            Assert.AreEqual(0x1234, es.ReadInt16());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Int32()
        {
            var es = new EnhancedBlockStream();

            es.WriteInt32(0);
            es.WriteInt32(65121);
            es.WriteInt32(0x12345678);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt32());
            Assert.AreEqual(65121, es.ReadInt32());
            Assert.AreEqual(0x12345678, es.ReadInt32());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Int64()
        {
            var es = new EnhancedBlockStream();

            es.WriteInt64(0);
            es.WriteInt64(65121);
            es.WriteInt64(0x12345678);
            es.WriteInt64((((long)0x12345678) << 32) | (long)0xaabbccddee);

            es.Seek(0, SeekOrigin.Begin);

            Assert.AreEqual(0, es.ReadInt64());
            Assert.AreEqual(65121, es.ReadInt64());
            Assert.AreEqual(0x12345678, es.ReadInt64());
            Assert.AreEqual((((long)0x12345678) << 32) | (long)0xaabbccddee, es.ReadInt64());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Float()
        {
            var es = new EnhancedBlockStream();
            float f;

            es.WriteFloat((float)123.456);
            es.Seek(0, SeekOrigin.Begin);
            f = es.ReadFloat();
            Assert.AreEqual((float)123.456, f);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_Bytes()
        {
            var es = new EnhancedBlockStream();
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
        public void EnhancedBlockStream_Bytes32()
        {
            var es = new EnhancedBlockStream();
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
        public void EnhancedBlockStream_String()
        {
            var es = new EnhancedBlockStream();

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
        public void EnhancedBlockStream_ReadStringCb()
        {
            var es = new EnhancedBlockStream();
            int cb;

            es.WriteStringNoLen("Hello World!");
            cb = (int)es.Position;

            es.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual("Hello World!", es.ReadString(cb));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_String32()
        {
            var es = new EnhancedBlockStream();

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
        public void EnhancedBlockStream_ReadWriteBytesNoLen()
        {
            var es = new EnhancedBlockStream();

            es.WriteBytesNoLen(new byte[] { 0, 1, 2, 3 });
            es.Seek(0, SeekOrigin.Begin);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, es.ReadBytes(4));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_VerifyBufLength()
        {
            var es = new EnhancedBlockStream();

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
        public void EnhancedBlockStream_ToByteArray()
        {
            var es = new EnhancedBlockStream();
            byte[] buf;

            es.Write(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 10);
            buf = es.ToArray();
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buf);

            buf[0] = 255;
            es.Position = 0;
            Assert.AreEqual(0, es.ReadByte());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnhancedBlockStream_GetBuffer()
        {
        }
    }
}

