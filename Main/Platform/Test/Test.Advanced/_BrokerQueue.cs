//-----------------------------------------------------------------------------
// FILE:        _BrokerQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

// Disable Thread Suspend/Resume methods are obsolete warnings

#pragma warning disable 0618

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _BrokerQueue
    {
        //---------------------------------------------------------------------

        private class Item
        {
            public int ListID;
            public int Value;
            public int Delay;
            public int ConsumerThreadID;

            public Item(int listID, int value)
            {
                this.ListID = listID;
                this.Value = value;
                this.Delay = 0;
            }

            public Item(int listID, int value, int delay)
            {
                this.ListID = listID;
                this.Value = value;
                this.Delay = delay;
            }
        }

        private class ProducerArgs
        {
            public int ListID;
            public int Start;
            public int Count;
            public int Delay;

            public ProducerArgs(int listID, int start, int count, int delay)
            {
                this.ListID = listID;
                this.Start = start;
                this.Count = count;
                this.Delay = delay;
            }
        }

        //---------------------------------------------------------------------

        private BrokerQueue<Item> queue;
        private Dictionary<int, Item>[] lists;
        private Exception threadException;

        private void Init(int cLists)
        {
            threadException = null;
            queue = new BrokerQueue<Item>();

            lists = new Dictionary<int, Item>[cLists];
            for (int i = 0; i < cLists; i++)
                lists[i] = new Dictionary<int, Item>();
        }

        private void ConsumerThread()
        {
            Item item;

            try
            {
                while (true)
                {
                    item = queue.Dequeue();
                    if (item == null)
                        break;

                    item.ConsumerThreadID = Thread.CurrentThread.ManagedThreadId;

                    lock (lists[item.ListID])
                        lists[item.ListID].Add(item.Value, item);

                    if (item.Delay >= 0)
                        Thread.Sleep(item.Delay);
                }
            }
            catch (Exception e)
            {
                threadException = e;
            }
        }

        private void ProducerThread(object o)
        {
            try
            {
                ProducerArgs args = (ProducerArgs)o;

                for (int i = 0; i < args.Count; i++)
                {
                    queue.Enqueue(new Item(args.ListID, args.Start + i, args.Delay));
                    Thread.Sleep(0);
                }
            }
            catch (Exception e)
            {
                threadException = e;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test01()
        {
            // Verify that a single consumer will wait for a producer
            // to add an item.

            Thread consumer;

            Init(1);

            consumer = new Thread(new ThreadStart(ConsumerThread));
            consumer.Start();
            Thread.Sleep(250);

            try
            {
                queue.Enqueue(new Item(0, 1001));
                Thread.Sleep(250);  // Give the consumer a chance to dequeue

                Assert.AreEqual(1, lists[0].Count);
                Assert.IsTrue(lists[0].ContainsKey(1001));
            }
            finally
            {
                queue.Close();
                consumer.Join();
            }

            Assert.IsNull(threadException);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test02()
        {
            // Verify that a single consumer will retrieve an already
            // queued item.

            Thread consumer;

            Init(1);

            consumer = new Thread(new ThreadStart(ConsumerThread));

            try
            {
                queue.Enqueue(new Item(0, 666));

                consumer.Start();
                Thread.Sleep(250);  // Give the consumer a chance to dequeue

                Assert.AreEqual(1, lists[0].Count);
                Assert.IsTrue(lists[0].ContainsKey(666));
            }
            finally
            {
                queue.Close();
                consumer.Join();
            }

            Assert.IsNull(threadException);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test03()
        {
            // Verify that a single consumer will wait for the producer to
            // add multiple items one at a time.

            Thread consumer;

            Init(1);

            consumer = new Thread(new ThreadStart(ConsumerThread));
            consumer.Start();
            Thread.Sleep(250);  // Give the consumer a chance to start

            try
            {
                for (int i = 0; i < 10; i++)
                {
                    queue.Enqueue(new Item(0, i));
                    Thread.Sleep(250);  // Give the consumer a chance to dequeue
                }

                Assert.AreEqual(10, lists[0].Count);
                for (int i = 0; i < 10; i++)
                    Assert.IsTrue(lists[0].ContainsKey(i));
            }
            finally
            {
                queue.Close();
                consumer.Join();
            }

            Assert.IsNull(threadException);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test04()
        {
            // Verify that a single consumer that is blocked on an
            // empty queue will dequeue several items queued 
            // while the consumer remains blocked.

            Thread consumer;

            Init(1);

            consumer = new Thread(new ThreadStart(ConsumerThread));
            consumer.Start();
            Thread.Sleep(250);  // Give the consumer a chance to start

            try
            {
                consumer.Suspend();     // Keep the consumer blocked

                for (int i = 0; i < 10; i++)
                    queue.Enqueue(new Item(0, i));

                consumer.Resume();      // Unblock the consumer
                Thread.Sleep(250);      // Give the consumer a chance to dequeue

                Assert.AreEqual(10, lists[0].Count);
                for (int i = 0; i < 10; i++)
                    Assert.IsTrue(lists[0].ContainsKey(i));
            }
            finally
            {
                queue.Close();
                consumer.Join();
            }

            Assert.IsNull(threadException);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test05()
        {
            // Verify that a single consumer will process items queued
            // by multiple producer threads.

            const int ProducerCount = 10;
            const int ItemCount = 100;

            Thread[] producers = new Thread[ProducerCount];
            Thread consumer;

            Init(ProducerCount);

            consumer = new Thread(new ThreadStart(ConsumerThread));
            consumer.Start();
            Thread.Sleep(250);  // Give the consumer a chance to start

            try
            {
                for (int i = 0; i < ProducerCount; i++)
                {
                    producers[i] = new Thread(new ParameterizedThreadStart(ProducerThread));
                    producers[i].Start(new ProducerArgs(i, i * 1000, ItemCount, -1));
                }

                // Wait for the producers to finish

                foreach (Thread producer in producers)
                    producer.Join();

                // Give the consumer a chance to dequeue

                Thread.Sleep(1000);

                for (int i = 0; i < ProducerCount; i++)
                {
                    Assert.AreEqual(ItemCount, lists[i].Count);
                    for (int j = 0; j < ItemCount; j++)
                        Assert.IsTrue(lists[i].ContainsKey(i * 1000 + j));
                }
            }
            finally
            {
                queue.Close();
                consumer.Join();
            }

            Assert.IsNull(threadException);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BrokerQueue_Test06()
        {
            // Verify that multiple consumers will process items queued
            // by multiple producer threads.

            const int ProducerCount = 10;
            const int ConsumerCount = 2;
            const int ItemCount = 100;

            Thread[] producers = new Thread[ProducerCount];
            Thread[] consumers;

            Init(ProducerCount);

            consumers = new Thread[ConsumerCount];
            for (int i = 0; i < ConsumerCount; i++)
            {
                consumers[i] = new Thread(new ThreadStart(ConsumerThread));
                consumers[i].Start();
            }

            Thread.Sleep(250);  // Give the consumers a chance to start

            try
            {
                for (int i = 0; i < ProducerCount; i++)
                {
                    producers[i] = new Thread(new ParameterizedThreadStart(ProducerThread));
                    producers[i].Start(new ProducerArgs(i, i * 1000, ItemCount, 0));
                }

                // Wait for the producers to finish

                foreach (Thread producer in producers)
                    producer.Join();

                // Give the consumers a chance to dequeue

                Thread.Sleep(1000);

                for (int i = 0; i < ProducerCount; i++)
                {
                    Assert.AreEqual(ItemCount, lists[i].Count);
                    for (int j = 0; j < ItemCount; j++)
                        Assert.IsTrue(lists[i].ContainsKey(i * 1000 + j));
                }

                // Verify that more than one thread was actually able
                // to dequeue items.

                Dictionary<int, bool> threadIDs;

                threadIDs = new Dictionary<int, bool>();
                for (int i = 0; i < ProducerCount; i++)
                    for (int j = 0; j < ItemCount; j++)
                        threadIDs[lists[i][i * 1000 + j].ConsumerThreadID] = true;

                Assert.IsTrue(threadIDs.Count > 1);
            }
            finally
            {
                queue.Close();

                foreach (Thread consumer in consumers)
                    consumer.Join();
            }

            Assert.IsNull(threadException);
        }
    }
}

