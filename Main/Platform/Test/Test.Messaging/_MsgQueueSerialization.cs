//-----------------------------------------------------------------------------
// FILE:        _MsgQueueSerialization.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

namespace LillTek.Messaging.Queuing.Test
{
    [TestClass]
    public class _MsgQueueSerialization
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueSerialization_Test()
        {
            QueuedMsgInfo msgInfo;
            QueuedMsg msg, msgTest;
            MsgQueueCmd cmd;

            msg = new QueuedMsg();
            msg.TargetEP = "logical://target";
            msg.ResponseEP = "logical://response";
            msg.SessionID = Helper.NewGuid();
            msg.SendTime = new DateTime(2000, 1, 1);
            msg.ExpireTime = new DateTime(2000, 1, 2);
            msg.Body = "Hello World!";

            cmd = new MsgQueueCmd(MsgQueueCmd.EnqueueCmd);
            cmd.MessageHeader = msg.GetMessageHeader(new MsgQueueSettings());
            cmd.MessageBody = msg.GetMessageBody(Compress.None);

            msgTest = new QueuedMsg(cmd, true);
            Assert.AreEqual(msg, msgTest);
            Assert.AreEqual(msg.TargetEP, msgTest.TargetEP);
            Assert.AreEqual(msg.ResponseEP, msgTest.ResponseEP);
            Assert.AreEqual(msg.SessionID, msgTest.SessionID);
            Assert.AreEqual(msg.SendTime, msgTest.SendTime);
            Assert.AreEqual(msg.ExpireTime, msgTest.ExpireTime);
            Assert.AreEqual(msg.Body, msgTest.Body);

            msgInfo = new QueuedMsgInfo(null, msgTest);
            Assert.AreEqual(msg.TargetEP, msgInfo.TargetEP);
            Assert.AreEqual(msg.ResponseEP, msgInfo.ResponseEP);
            Assert.AreEqual(msg.SessionID, msgInfo.SessionID);
            Assert.AreEqual(msg.SendTime, msgInfo.SendTime);
            Assert.AreEqual(msg.ExpireTime, msgInfo.ExpireTime);
            Assert.AreEqual(msg.BodyRaw.Length, msgInfo.BodySize);
        }
    }
}

