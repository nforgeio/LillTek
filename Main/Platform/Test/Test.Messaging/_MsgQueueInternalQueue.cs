//-----------------------------------------------------------------------------
// FILE:        _MsgQueueInternalQueue.cs
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
using LillTek.Messaging;
using LillTek.Messaging.Queuing;
using LillTek.Testing;
using LillTek.Transactions;

namespace LillTek.Messaging.Queuing.Test
{
    [TestClass]
    public class _MsgQueueInternalQueue
    {
        private QueuedMsgInfo GetMsgInfo(DeliveryPriority priority, int value)
        {
            QueuedMsgInfo msgInfo = new QueuedMsgInfo(null);

            msgInfo.ID = Helper.NewGuid();
            msgInfo.Priority = priority;
            msgInfo.ProviderData = value;

            return msgInfo;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueInternalQueue_Basic()
        {
            InternalQueue queue = new InternalQueue("logical://Test");

            Assert.AreEqual("logical://Test", queue.Endpoint);

            // Verify that priority trumps enqueue order

            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryLow, 0));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Low, 1));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 2));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.High, 3));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryHigh, 4));
            Assert.AreEqual(5, queue.Count);

            Assert.AreEqual(4, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(3, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(2, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(1, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(0, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(0, queue.Count);

            // Verify that messages are dequeued in order of enqueuing
            // when the priorities are the same.

            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryLow, 0));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryLow, 1));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Low, 2));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Low, 3));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 4));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 5));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.High, 6));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.High, 7));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryHigh, 8));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.VeryHigh, 9));
            Assert.AreEqual(10, queue.Count);

            Assert.AreEqual(8, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(9, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(6, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(7, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(4, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(5, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(2, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(3, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(0, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(1, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(0, queue.Count);

            // Verify that peek works

            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 0));
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, queue.Peek(null).ProviderData);
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueInternalQueue_Transactional()
        {
            InternalQueue queue;
            BaseTransaction trans0 = new BaseTransaction(Stub.Param);
            BaseTransaction trans1 = new BaseTransaction(Stub.Param);

            // Verify that messages enqueued in a transaction are not
            // returned for other transactions.

            queue = new InternalQueue("logical://Test");
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 0));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 1));
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 2));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 3));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 4));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 5));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 6));

            Assert.AreEqual(4, queue.Dequeue(null).ProviderData);
            Assert.AreEqual(1, queue.Dequeue(trans1).ProviderData);
            Assert.AreEqual(0, queue.Dequeue(trans0).ProviderData);
            Assert.AreEqual(3, queue.Dequeue(trans1).ProviderData);
            Assert.AreEqual(2, queue.Dequeue(trans0).ProviderData);
            Assert.AreEqual(5, queue.Dequeue(trans1).ProviderData);
            Assert.AreEqual(6, queue.Dequeue(trans0).ProviderData);

            // Verify that Unlock() works.

            queue = new InternalQueue("logical://Test");
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 0));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 1));
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 2));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 3));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 4));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 5));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 6));
            queue.Unlock(trans0);
            queue.Unlock(trans1);

            for (int i = 0; i <= 6; i++)
                Assert.AreEqual(i, queue.Dequeue(null).ProviderData);

            // Verify that messages peeked from within a transaction
            // are not returned for other transactions.

            queue = new InternalQueue("logical://Test");
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 0));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 1));
            queue.Enqueue(trans0, GetMsgInfo(DeliveryPriority.Normal, 2));
            queue.Enqueue(trans1, GetMsgInfo(DeliveryPriority.Normal, 3));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 4));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 5));
            queue.Enqueue(null, GetMsgInfo(DeliveryPriority.Normal, 6));
            queue.Unlock(trans0);
            queue.Unlock(trans1);

            Assert.AreEqual(0, queue.Peek(trans0).ProviderData);
            Assert.AreEqual(1, queue.Peek(trans1).ProviderData);
            Assert.AreEqual(0, queue.Peek(trans0).ProviderData);
            Assert.AreEqual(1, queue.Peek(trans1).ProviderData);

            Assert.AreEqual(2, queue.Peek(null).ProviderData);
            Assert.AreEqual(2, queue.Dequeue(null).ProviderData);

            Assert.AreEqual(0, queue.Dequeue(trans0).ProviderData);
            Assert.AreEqual(1, queue.Dequeue(trans1).ProviderData);
            Assert.AreEqual(3, queue.Dequeue(trans0).ProviderData);
            Assert.AreEqual(4, queue.Dequeue(trans1).ProviderData);
        }
    }
}

