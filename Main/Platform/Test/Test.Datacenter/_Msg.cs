//-----------------------------------------------------------------------------
// FILE:        _Msg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Message related unit tests

using System;
using System.IO;
using System.Reflection;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _Msg
    {
        private void TestBaseCloning(PropertyMsg msg1)
        {
            PropertyMsg msg2;

            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;

            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        private void TestBaseCloning(BlobPropertyMsg msg1)
        {
            BlobPropertyMsg msg2;

            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;

            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        [TestInitialize]
        public void Initialize()
        {
            Msg.LoadTypes(typeof(LillTek.Datacenter.Global).Assembly);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Msg.ClearTypes();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void Msg_Serialize_GetConfigMsg()
        {
            GetConfigMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            msgOut = new GetConfigMsg("server", "foo.exe", new Version("5.1.2.3"), "usage");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (GetConfigMsg)Msg.Load(es);

            Assert.AreEqual("server", msgIn.MachineName);
            Assert.AreEqual("foo.exe", msgIn.ExeFile);
            Assert.AreEqual(new Version("5.1.2.3"), msgIn.ExeVersion);
            Assert.AreEqual("usage", msgIn.Usage);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void Msg_Clone_GetConfigMsg()
        {
            GetConfigMsg msgIn, msgOut;

            msgOut = new GetConfigMsg("server", "foo.exe", new Version("5.1.2.3"), "usage");
            msgIn = (GetConfigMsg)msgOut.Clone();

            Assert.AreEqual(msgOut.MachineName, msgIn.MachineName);
            Assert.AreEqual(msgOut.ExeFile, msgIn.ExeFile);
            Assert.AreEqual(msgOut.ExeVersion, msgIn.ExeVersion);
            Assert.AreEqual(msgOut.Usage, msgIn.Usage);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void Msg_Serialize_GetConfigAck()
        {
            GetConfigAck msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            msgOut = new GetConfigAck(new Exception("Test exception"));

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (GetConfigAck)Msg.Load(es);

            Assert.AreEqual("Test exception", msgIn.Exception);
            Assert.IsNull(msgIn.ConfigText);

            msgOut = new GetConfigAck("config info");
            es.SetLength(0);
            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (GetConfigAck)Msg.Load(es);

            Assert.IsNull(msgIn.Exception);
            Assert.AreEqual("config info", msgIn.ConfigText);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void Msg_Clone_GetConfigAck()
        {
            GetConfigAck msgIn, msgOut;

            msgOut = new GetConfigAck(new Exception("Test exception"));
            msgIn = (GetConfigAck)msgOut.Clone();

            Assert.AreEqual(msgOut.Exception, msgIn.Exception);
            Assert.AreEqual(msgOut.ConfigText, msgIn.ConfigText);

            msgOut = new GetConfigAck("config info");
            msgIn = (GetConfigAck)msgOut.Clone();

            Assert.AreEqual(msgOut.Exception, msgIn.Exception);
            Assert.AreEqual(msgOut.ConfigText, msgIn.ConfigText);

            TestBaseCloning(msgOut);
        }
    }
}

