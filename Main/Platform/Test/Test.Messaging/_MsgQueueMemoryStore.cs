//-----------------------------------------------------------------------------
// FILE:        _MsgQueueMemoryStore.cs
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
    public class _MsgQueueMemoryStore
    {
        private const int MessageCount = 1000;

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueMemoryStore_Basic()
        {
            MsgQueueMemoryStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg, msgTest;
            object persistID;

            try
            {
                store = new MsgQueueMemoryStore();
                store.Open();

                // Should initialize with no persisted messages

                Assert.AreEqual(0, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;
                Assert.AreEqual(0, store.Count);

                msg = new QueuedMsg();
                msg.TargetEP = "logical://target";
                msg.ResponseEP = "logical://response";
                msg.SessionID = Helper.NewGuid();
                msg.SendTime = new DateTime(2000, 1, 1);
                msg.ExpireTime = new DateTime(2000, 1, 2);
                msg.Body = "Hello World!";

                msgInfo = new QueuedMsgInfo(null, msg);
                Assert.IsNull(store.GetPersistID(msg.ID));
                store.Add(msgInfo, msg);

                Assert.AreEqual(1, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(1, store.Count);

                persistID = store.GetPersistID(msg.ID);
                Assert.IsNotNull(persistID);

                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(msg, msgTest);

                store.SetDeliveryAttempt(persistID, 10, new DateTime(2001, 1, 1));
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual(10, msgInfo.DeliveryAttempts);
                Assert.AreEqual(new DateTime(2001, 1, 1), msgInfo.DeliveryTime);

                store.SetPriority(persistID, DeliveryPriority.Low);
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual(DeliveryPriority.Low, msgInfo.Priority);
                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(DeliveryPriority.Low, msgTest.Priority);

                store.Modify(persistID, "logical://target2", new DateTime(2002, 1, 1), new DateTime(2002, 1, 2), DeliveryStatus.Poison);
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual((MsgEP)"logical://target2", msgInfo.TargetEP);
                Assert.AreEqual(new DateTime(2002, 1, 1), msgInfo.DeliveryTime);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgInfo.ExpireTime);
                Assert.AreEqual(DeliveryStatus.Poison, msgInfo.Status);
                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(DeliveryPriority.Low, msgTest.Priority);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgTest.ExpireTime);

                Assert.AreEqual((MsgEP)"logical://target2", msgTest.TargetEP);
                Assert.AreEqual(msg.ID, msgTest.ID);
                Assert.AreEqual(msg.SessionID, msgTest.SessionID);
                Assert.AreEqual(msg.SendTime, msgTest.SendTime);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgTest.ExpireTime);
                Assert.AreEqual(msg.Body, msgTest.Body);

                store.Remove(persistID);
                Assert.AreEqual(0, store.Count);
                Assert.IsNull(store.GetPersistID(msg.ID));
                Assert.IsNull(store.Get(persistID));
                Assert.IsNull(store.GetInfo(persistID));
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueMemoryStore_Multiple()
        {
            MsgQueueMemoryStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;

            try
            {
                store = new MsgQueueMemoryStore();
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {

                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                for (int i = 0; i < MessageCount; i++)
                {
                    persistID = store.GetPersistID(ids[i]);
                    Assert.IsNotNull(persistID);

                    store.Remove(persistID);
                    Assert.IsNull(store.GetPersistID(ids[i]));
                    Assert.IsNull(store.Get(persistID));
                    Assert.IsNull(store.GetInfo(persistID));
                }
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }
    }
}

