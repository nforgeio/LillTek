//-----------------------------------------------------------------------------
// FILE:        _BlobPropertyMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests for the BlobPropertyMsg class.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test.Messages
{
    public class _BlobPropMsg : BlobPropertyMsg
    {

        public string Value;

        public _BlobPropMsg()
        {
            this.Value = "";
        }

        protected override void WritePayload(EnhancedStream es)
        {
            es.WriteString16(Value);
        }

        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            Value = es.ReadString16();
        }
    }

    [TestClass]
    public class _BlobPropertyMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void BlobPropertyMsg_Access()
        {
            BlobPropertyMsg msg = new BlobPropertyMsg();

            msg._Set("string", "hello");
            msg._Set("bool", true);
            msg._Set("int", 10);
            msg._Set("timespan", new TimeSpan(0, 0, 0, 0, 55));
            msg._Set("endpoint", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 56));
            msg._Set("bytes", new byte[] { 5, 6, 7, 0x1F });
            msg._Data = new byte[] { 0, 1, 2 };

            Assert.AreEqual("hello", msg._Get("string"));
            Assert.AreEqual("hello", msg["string"]);
            Assert.AreEqual("hello", msg["STRING"]);
            Assert.IsNull(msg["foobar"]);
            Assert.AreEqual("foobar", msg._Get("foobar", "foobar"));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, msg._Data);

            msg["test"] = "test";
            Assert.AreEqual("test", msg["test"]);
            msg["test"] = "test #2";
            Assert.AreEqual("test #2", msg["test"]);

            Assert.IsTrue(msg._Get("bool", false));
            Assert.IsFalse(msg._Get("foobar", false));
            Assert.AreEqual("1", msg["bool"]);
            msg._Set("bool", false);
            Assert.IsFalse(msg._Get("bool", true));
            Assert.AreEqual("0", msg["bool"]);
            Assert.IsTrue(msg._Get("test", true));

            Assert.AreEqual(10, msg._Get("int", 0));
            Assert.AreEqual(20, msg._Get("foobar", 20));
            msg._Set("int", -566);
            Assert.AreEqual(-566, msg._Get("int", 0));
            Assert.AreEqual("-566", msg["int"]);
            Assert.AreEqual(666, msg._Get("test", 666));

            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 55), msg._Get("timespan", TimeSpan.FromSeconds(60)));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 60), msg._Get("foobar", TimeSpan.FromSeconds(60)));
            msg._Set("timespan", TimeSpan.FromSeconds(77));
            Assert.AreEqual(TimeSpan.FromSeconds(77), LillTek.Common.Serialize.Parse(msg["timespan"], TimeSpan.Zero));
            Assert.AreEqual(TimeSpan.FromSeconds(1), msg._Get("test", TimeSpan.FromSeconds(1)));

            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 56), msg._Get("endpoint", (IPEndPoint)null));
            Assert.AreEqual("127.0.0.1:56", msg["endpoint"]);
            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 57), msg._Get("foobar", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 57)));
            msg._Set("endpoint", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 58));
            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 58), msg._Get("endpoint", (IPEndPoint)null));
            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 58), msg._Get("test", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 58)));

            CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 0x1F }, msg._Get("bytes", (byte[])null));
            CollectionAssert.AreEqual(new byte[] { 10, 11 }, msg._Get("foobar", new byte[] { 10, 11 }));
            Assert.AreEqual("0506071f", msg["bytes"]);
            msg._Set("bytes", new byte[] { 0x0a, 0x1b, 0x2c, 0x3d, 0x4e, 0x5f });
            CollectionAssert.AreEqual(new byte[] { 0x0a, 0x1b, 0x2c, 0x3d, 0x4e, 0x5f }, msg._Get("bytes", (byte[])null));
            CollectionAssert.AreEqual(new byte[] { 10 }, msg._Get("test", new byte[] { 10 }));

            var id = Helper.NewGuid();

            msg._Set("id", id);
            Assert.AreEqual(id, msg._Get("id", Guid.Empty));
            Assert.AreEqual(Guid.Empty, msg._Get("_id", Guid.Empty));
            msg._Set("_id", "sss s s s");
            Assert.AreEqual(Guid.Empty, msg._Get("_id", Guid.Empty));

            var now = Helper.UtcNowRounded;

            msg._Set("date", now);
            Assert.AreEqual(now, msg._Get("date", DateTime.MaxValue));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void BlobPropertyMsg_Clone()
        {
            BlobPropertyMsg msg1, msg2;

            msg1 = new BlobPropertyMsg();
            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;
            msg1["foo"] = "bar";

            Assert.IsNull(msg1._Data);

            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);
            Assert.AreEqual("bar", msg2["foo"]);
            Assert.AreEqual(msg1._Data, msg2._Data);

            msg1._Data = new byte[] { 0, 1, 2 };
            msg2 = (BlobPropertyMsg)msg1.Clone();
            CollectionAssert.AreEqual(msg1._Data, msg2._Data);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void BlobPropertyMsg_Serialize()
        {
            BlobPropertyMsg msgOut, msgIn;
            EnhancedMemoryStream ms = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new BlobPropertyMsg();
            msgOut._Set("string", "hello");
            msgOut._Set("bool", true);
            msgOut._Set("int", 10);
            msgOut._Set("timespan", new TimeSpan(0, 0, 0, 0, 55));
            msgOut._Set("endpoint", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 56));
            msgOut._Set("bytes", new byte[] { 5, 6, 7, 8 });
            msgOut._Data = null;

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (BlobPropertyMsg)Msg.Load(ms);

            Assert.AreEqual(msgOut["string"], msgIn["string"]);
            Assert.AreEqual(msgOut["bool"], msgIn["bool"]);
            Assert.AreEqual(msgOut["int"], msgIn["int"]);
            Assert.AreEqual(msgOut["timespan"], msgIn["timespan"]);
            Assert.AreEqual(msgOut["endpoint"], msgIn["endpoint"]);
            Assert.AreEqual(msgOut["bytes"], msgIn["bytes"]);
            Assert.IsNull(msgIn._Data);

            ms.SetLength(0);
            msgOut._Data = new byte[] { 0, 1, 2 };

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (BlobPropertyMsg)Msg.Load(ms);

            Assert.AreEqual(msgOut["string"], msgIn["string"]);
            Assert.AreEqual(msgOut["bool"], msgIn["bool"]);
            Assert.AreEqual(msgOut["int"], msgIn["int"]);
            Assert.AreEqual(msgOut["timespan"], msgIn["timespan"]);
            Assert.AreEqual(msgOut["endpoint"], msgIn["endpoint"]);
            Assert.AreEqual(msgOut["bytes"], msgIn["bytes"]);
            CollectionAssert.AreEqual(msgOut._Data, msgIn._Data);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void BlobPropertyMsg_Serialize_Large()
        {
            BlobPropertyMsg msgOut, msgIn;
            EnhancedMemoryStream ms = new EnhancedMemoryStream();
            byte[] buf;

            buf = new byte[70000];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)i;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new BlobPropertyMsg();
            msgOut._Set("string", "hello");
            msgOut._Set("bool", true);
            msgOut._Set("int", 10);
            msgOut._Set("timespan", new TimeSpan(0, 0, 0, 0, 55));
            msgOut._Set("endpoint", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 56));
            msgOut._Set("bytes", new byte[] { 5, 6, 7, 8 });
            msgOut._Data = buf;

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (BlobPropertyMsg)Msg.Load(ms);

            Assert.AreEqual(msgOut["string"], msgIn["string"]);
            Assert.AreEqual(msgOut["bool"], msgIn["bool"]);
            Assert.AreEqual(msgOut["int"], msgIn["int"]);
            Assert.AreEqual(msgOut["timespan"], msgIn["timespan"]);
            Assert.AreEqual(msgOut["endpoint"], msgIn["endpoint"]);
            Assert.AreEqual(msgOut["bytes"], msgIn["bytes"]);
            CollectionAssert.AreEqual(msgOut._Data, msgIn._Data);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void BlobPropertyMsg_SerializeBase()
        {
            _BlobPropMsg msgOut, msgIn;
            EnhancedMemoryStream ms = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new _BlobPropMsg();
            msgOut._Set("string", "hello");
            msgOut._Set("bool", true);
            msgOut._Set("int", 10);
            msgOut._Set("timespan", new TimeSpan(0, 0, 0, 0, 55));
            msgOut._Set("endpoint", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 56));
            msgOut._Set("bytes", new byte[] { 5, 6, 7, 8 });
            msgOut._Data = new byte[] { 0, 1, 2 };
            msgOut.Value = "foobar";

            Msg.Save(ms, msgOut);

            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (_BlobPropMsg)Msg.Load(ms);

            Assert.AreEqual(msgOut["string"], msgIn["string"]);
            Assert.AreEqual(msgOut["bool"], msgIn["bool"]);
            Assert.AreEqual(msgOut["int"], msgIn["int"]);
            Assert.AreEqual(msgOut["timespan"], msgIn["timespan"]);
            Assert.AreEqual(msgOut["endpoint"], msgIn["endpoint"]);
            Assert.AreEqual(msgOut["bytes"], msgIn["bytes"]);
            CollectionAssert.AreEqual(msgOut._Data, msgIn._Data);
            Assert.AreEqual("foobar", msgIn.Value);
        }
    }
}

