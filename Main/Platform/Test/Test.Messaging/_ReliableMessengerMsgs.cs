//-----------------------------------------------------------------------------
// FILE:        _ReliableMessengerMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ReliableMessengerMsgs
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

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessengerMsgs_DeliveryMsg_Serialize()
        {
            DeliveryMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();
            DateTime now = Helper.UtcNowRounded;
            Guid id = Helper.NewGuid();
            PropertyMsg query;
            PropertyMsg response;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            query = new PropertyMsg();
            query["hello"] = "world";
            response = new PropertyMsg();
            response["foo"] = "bar";

            msgOut = new DeliveryMsg(DeliveryOperation.Attempt, now, "logical://foo", "logical://bar", query, id,
                                     "clusterInfo", "clusterParam", new TimeoutException("Timeout"), response);
            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (DeliveryMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(DeliveryOperation.Attempt, msgIn.Operation);
            Assert.AreEqual(now, msgIn.Timestamp);
            Assert.AreEqual(MsgEP.Parse("logical://foo"), msgIn.TargetEP);
            Assert.AreEqual(MsgEP.Parse("logical://bar"), msgIn.ConfirmEP);
            Assert.IsInstanceOfType(msgIn.Query, typeof(PropertyMsg));
            Assert.AreEqual("world", ((PropertyMsg)msgIn.Query)["hello"]);
            Assert.AreEqual(id, msgIn.TopologyID);
            Assert.AreEqual("clusterInfo", msgIn.TopologyInfo);
            Assert.AreEqual("clusterParam", msgIn.TopologyParam);
            Assert.IsInstanceOfType(msgIn.Exception, typeof(TimeoutException));
            Assert.AreEqual("Timeout", msgIn.Exception.Message);
            Assert.IsInstanceOfType(msgIn.Response, typeof(PropertyMsg));
            Assert.AreEqual("bar", ((PropertyMsg)msgIn.Response)["foo"]);

            msgOut = new DeliveryMsg(DeliveryOperation.Confirmation, now, "logical://foo", null, query, id,
                                     null, null, new ArgumentException("Test"), null);
            es.SetLength(0);
            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (DeliveryMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(DeliveryOperation.Confirmation, msgIn.Operation);
            Assert.AreEqual(now, msgIn.Timestamp);
            Assert.AreEqual(MsgEP.Parse("logical://foo"), msgIn.TargetEP);
            Assert.IsNull(msgIn.ConfirmEP);
            Assert.IsInstanceOfType(msgIn.Query, typeof(PropertyMsg));
            Assert.AreEqual("world", ((PropertyMsg)msgIn.Query)["hello"]);
            Assert.AreEqual(id, msgIn.TopologyID);
            Assert.IsNull(msgIn.TopologyInfo);
            Assert.IsNull(msgIn.TopologyParam);
            Assert.IsInstanceOfType(msgIn.Exception, typeof(SessionException));
            Assert.AreEqual("Test", msgIn.Exception.Message);
            Assert.IsNull(msgIn.Response);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessengerMsgs_DeliveryMsg_Clone()
        {
            DeliveryMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();
            DateTime now = Helper.UtcNowRounded;
            Guid id = Helper.NewGuid();
            PropertyMsg query;
            PropertyMsg response;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            query = new PropertyMsg();
            query["hello"] = "world";
            response = new PropertyMsg();
            response["foo"] = "bar";

            msgOut = new DeliveryMsg(DeliveryOperation.Attempt, now, "logical://foo", "logical://bar", query, id,
                                     "clusterInfo", "clusterParam", new TimeoutException("Timeout"), response);
            msgIn = (DeliveryMsg)msgOut.Clone();

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(DeliveryOperation.Attempt, msgIn.Operation);
            Assert.AreEqual(now, msgIn.Timestamp);
            Assert.AreEqual(MsgEP.Parse("logical://foo"), msgIn.TargetEP);
            Assert.AreEqual(MsgEP.Parse("logical://bar"), msgIn.ConfirmEP);
            Assert.IsInstanceOfType(msgIn.Query, typeof(PropertyMsg));
            Assert.AreEqual("world", ((PropertyMsg)msgIn.Query)["hello"]);
            Assert.AreEqual(id, msgIn.TopologyID);
            Assert.AreEqual("clusterInfo", msgIn.TopologyInfo);
            Assert.AreEqual("clusterParam", msgIn.TopologyParam);
            Assert.IsInstanceOfType(msgIn.Exception, typeof(TimeoutException));
            Assert.AreEqual("Timeout", msgIn.Exception.Message);
            Assert.IsInstanceOfType(msgIn.Response, typeof(PropertyMsg));
            Assert.AreEqual("bar", ((PropertyMsg)msgIn.Response)["foo"]);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessengerMsgs_DeliveryAck_Serialize()
        {
            DeliveryAck msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new DeliveryAck();

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (DeliveryAck)Msg.Load(es);
            Assert.IsNotNull(msgIn);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessengerMsgs_DeliveryAck_Clone()
        {
            DeliveryAck msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new DeliveryAck();
            msgIn = (DeliveryAck)msgOut.Clone();
            Assert.IsNotNull(msgIn);

            TestBaseCloning(msgOut);
        }
    }
}

