//-----------------------------------------------------------------------------
// FILE:        _MsgQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Transactions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Queuing.Test
{
    [TestClass]
    public class _MsgQueue
    {
        const int BlastQueues = 10;
        const int BlastMessages = 100;

        private string folder;
        private Dictionary<string, bool> messages;
        private TimeSpan defTimedLockTime;

        [TestInitialize]
        public void Initialize()
        {
            // folder = Helper.AddTrailingSlash(Path.GetTempPath()) + Helper.NewGuid().ToString();
            folder = "C:\\Temp\\Test";

            Helper.SetLocalGuidMode(GuidMode.CountUp);

            NetTrace.Start();
            NetTrace.Enable(MsgQueueEngine.TraceSubsystem, 0);
            // NetTrace.Enable(LillTek.Transactions.TransactionManager.DefTraceSubsystem,0);
            // NetTrace.Enable(DuplexSession.TraceSubsystem,0);
            // NetTrace.Enable(MsgRouter.TraceSubsystem,0);

            defTimedLockTime = TimedLock.DefaultTimeout;
            if (Debugger.IsAttached)
                TimedLock.DefaultTimeout = TimeSpan.FromHours(24);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
            ClearFolder();

            TimedLock.DefaultTimeout = defTimedLockTime;
        }

        private void ClearFolder()
        {
            if (Directory.Exists(folder))
            {
                Helper.DeleteFile(folder + "\\*.*");
                Helper.DeleteFile(folder, true);
                Thread.Sleep(2000);
            }
        }

        private void ClearMessages()
        {
            messages = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        private void AddMessage(string value)
        {
            messages[value] = true;
        }

        private bool MessageExists(string value)
        {
            return messages.ContainsKey(value);
        }

        private void SetConfig()
        {
            string cfg = @"

&section MsgRouter

    AppName             = Test
    AppDescription      = Message Queuing
    RouterEP		    = physical://{0}/Test/$(Guid)
    CloudEP    			= $(LillTek.DC.CloudEP)
    CloudAdapter        = ANY
    UdpEP			    = ANY:0
    TcpEP			    = ANY:0
    TcpBacklog			= 100
    TcpDelay			= off
    BkInterval			= 1s
    MaxIdle				= 5m
    AdvertiseTime	    = 1m
    DefMsgTTL		    = 5
    SharedKey 			= PLAINTEXT
    SessionCacheTime    = 2m
    SessionRetries      = 3
    SessionTimeout      = 10s

&endsection

&section MsgQueueClient

    BaseEP     = logical://Test/Queues/MyQueue
    Timeout    = infinite
    MessageTTL = 0s
    Compress   = BEST

&endsection

&section MsgQueueEngine 

    QueueMap[0]          = logical://Test/Queues
    FlushInterval        = 5m
    DeadLetterTTL        = 7d
    MaxDeliveryAttempts  = 3
    KeepAliveInterval    = 30s
    SessionTimeout       = 95s
    PendingCheckInterval = 60s
    BkTaskInterval       = 1s

&endsection
";
            Config.SetConfig(cfg.Replace('&', '#'));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Basic_Default()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.Enqueue(new QueuedMsg(10));
                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Dequeue(TimeSpan.Zero).Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.Enqueue(new QueuedMsg(10));
                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Dequeue(TimeSpan.Zero).Body);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Basic_Relative()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.EnqueueTo("test", new QueuedMsg(10));
                Assert.AreEqual(10, queue.PeekFrom("test").Body);
                Assert.AreEqual(10, queue.PeekFrom("test").Body);
                Assert.AreEqual(10, queue.DequeueFrom("test").Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.EnqueueTo("test", new QueuedMsg(10));
                Assert.AreEqual(10, queue.PeekFrom("test").Body);
                Assert.AreEqual(10, queue.PeekFrom("test").Body);
                Assert.AreEqual(10, queue.DequeueFrom("test").Body);

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Basic_Blast()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();
            ClearMessages();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                // Queue a bunch of messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        queue.EnqueueTo(string.Format("{0:0##}", i), new QueuedMsg(string.Format("{0:0##}:{1:0##}", i, j)));

                // Dequeue the messages in a different order

                for (int j = 0; j < BlastMessages; j++)
                    for (int i = 0; i < BlastQueues; i++)
                        AddMessage((string)queue.DequeueFrom(string.Format("{0:0##}", i)).Body);

                // Verify that we got all of the messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        Assert.IsTrue(MessageExists(string.Format("{0:0##}:{1:0##}", i, j)), string.Format("{0:0##}:{1:0##}", i, j));

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                // Queue a bunch of messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        queue.EnqueueTo(string.Format("{0:0##}", i), new QueuedMsg(string.Format("{0:0##}:{1:0##}", i, j)));

                // Dequeue the messages in a different order

                for (int j = 0; j < BlastMessages; j++)
                    for (int i = 0; i < BlastQueues; i++)
                        AddMessage((string)queue.DequeueFrom(string.Format("{0:0##}", i)).Body);

                // Verify that we got all of the messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        Assert.IsTrue(MessageExists(string.Format("{0:0##}:{1:0##}", i, j)), string.Format("{0:0##}:{1:0##}", i, j));

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Basic_Blast_Restart()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();
            ClearMessages();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                // Queue a bunch of messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        queue.EnqueueTo(string.Format("{0:0##}", i), new QueuedMsg(string.Format("{0:0##}:{1:0##}", i, j)));

                // Restart the client and server to verify that the messages were
                // actually persisted.

                queue.Close();
                queue = null;

                engine.Stop();
                engine = null;

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                // Dequeue the messages in a different order

                for (int j = 0; j < BlastMessages; j++)
                    for (int i = 0; i < BlastQueues; i++)
                        AddMessage((string)queue.DequeueFrom(string.Format("{0:0##}", i)).Body);

                // Verify that we got all of the messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        Assert.IsTrue(MessageExists(string.Format("{0:0##}:{1:0##}", i, j)), string.Format("{0:0##}:{1:0##}", i, j));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Basic_BlastLarge_Restart()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            MsgQueueSettings settings;

            SetConfig();
            ClearFolder();
            ClearMessages();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                settings = MsgQueueSettings.LoadConfig("MsgQueueClient");
                settings.Compress = Compress.None;      // Disable compression so we'll have big messages

                queue = new MsgQueue(router, null, settings);

                // Queue a bunch of large messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                    {
                        MemoryStream ms = new MemoryStream(10000);
                        StreamWriter writer = new StreamWriter(ms, Helper.AnsiEncoding);

                        writer.WriteLine("{0:0##}:{1:0##}", i, j);
                        for (int k = 0; k < 1000; k++)
                            writer.WriteLine("0123456789");

                        writer.Flush();
                        queue.EnqueueTo(string.Format("{0:0##}", i), new QueuedMsg(ms.ToArray()));

                        writer.Close();
                        ms.Close();
                    }

                // Restart the client and server to verify that the messages were
                // actually persisted.

                queue.Close();
                queue = null;

                engine.Stop();
                engine = null;

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                // Dequeue the messages in a different order

                for (int j = 0; j < BlastMessages; j++)
                    for (int i = 0; i < BlastQueues; i++)
                    {
                        QueuedMsg msg;
                        MemoryStream ms;
                        StreamReader reader;

                        msg = queue.DequeueFrom(string.Format("{0:0##}", i));
                        ms = new MemoryStream((byte[])msg.Body);
                        reader = new StreamReader(ms, Helper.AnsiEncoding);

                        AddMessage(reader.ReadLine());

                        for (int k = 0; k < 1000; k++)
                            Assert.AreEqual("0123456789", reader.ReadLine());

                        reader.Close();
                        ms.Close();
                    }

                // Verify that we got all of the messages

                for (int i = 0; i < BlastQueues; i++)
                    for (int j = 0; j < BlastMessages; j++)
                        Assert.IsTrue(MessageExists(string.Format("{0:0##}:{1:0##}", i, j)), string.Format("{0:0##}:{1:0##}", i, j));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Priority()
        {
            // Confirm that the message queue will take message priority into
            // account when dequeuing messages.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryLow, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 40));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 50));

                for (int i = 50; i >= 10; i -= 10)
                {
                    Assert.AreEqual(i, queue.Peek().Body);
                    Assert.AreEqual(i, queue.Dequeue().Body);
                }

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryLow, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 40));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 50));

                for (int i = 50; i >= 10; i -= 10)
                {
                    Assert.AreEqual(i, queue.Peek().Body);
                    Assert.AreEqual(i, queue.Dequeue().Body);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Wait_Dequeue()
        {
            // Force a dequeue operation to wait for a message to be enqueued.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue1 = null;
            MsgQueue queue2 = null;
            IAsyncResult ar;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                ar = queue1.BeginDequeue(TimeSpan.FromMilliseconds(1000), null, null);
                Thread.Sleep(100);
                queue2.Enqueue(new QueuedMsg("Hello World!"));
                Assert.AreEqual("Hello World!", queue1.EndDequeue(ar).Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                ar = queue1.BeginDequeue(TimeSpan.FromMilliseconds(1000), null, null);
                Thread.Sleep(100);
                queue2.Enqueue(new QueuedMsg("Hello World!"));
                Assert.AreEqual("Hello World!", queue1.EndDequeue(ar).Body);

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue1 != null)
                    queue1.Close();

                if (queue2 != null)
                    queue2.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Wait_Dequeue_Multiple()
        {
            // Force a dequeue operation to wait for a message to be enqueued.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            MsgQueue[] queues = new MsgQueue[10];
            IAsyncResult[] arWaiting = new IAsyncResult[queues.Length]; ;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                for (int i = 0; i < queues.Length; i++)
                {
                    queues[i] = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                    arWaiting[i] = queues[i].BeginDequeue(TimeSpan.FromMilliseconds(5000), null, null);
                }

                Thread.Sleep(1000);
                for (int i = 0; i < queues.Length; i++)
                    queue.Enqueue(new QueuedMsg("Hello World!"));

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].EndDequeue(arWaiting[i]).Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                for (int i = 0; i < queues.Length; i++)
                {
                    queues[i] = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                    arWaiting[i] = queues[i].BeginDequeue(TimeSpan.FromMilliseconds(5000), null, null);
                }

                Thread.Sleep(1000);
                for (int i = 0; i < queues.Length; i++)
                    queue.Enqueue(new QueuedMsg("Hello World!"));

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].EndDequeue(arWaiting[i]).Body);

            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                for (int i = 0; i < queues.Length; i++)
                    if (queues[i] != null)
                        queues[i].Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Timeout_Dequeue()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue.Dequeue(TimeSpan.FromMilliseconds(500));
                Assert.Fail("Expected a Timeout");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Timeout_Dequeue_Immediate()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue.Dequeue(TimeSpan.Zero);
                Assert.Fail("Expected a Timeout");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Wait_Peek()
        {
            // Force a peek operation to wait for a message to be enqueued.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue1 = null;
            MsgQueue queue2 = null;
            IAsyncResult ar;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                ar = queue1.BeginPeek(TimeSpan.FromMilliseconds(1000), null, null);
                Thread.Sleep(100);
                queue2.Enqueue(new QueuedMsg("Hello World!"));
                Assert.AreEqual("Hello World!", queue1.EndPeek(ar).Body);
                Assert.AreEqual("Hello World!", queue2.Dequeue().Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                ar = queue1.BeginPeek(TimeSpan.FromMilliseconds(1000), null, null);
                Thread.Sleep(100);
                queue2.Enqueue(new QueuedMsg("Hello World!"));
                Assert.AreEqual("Hello World!", queue1.EndPeek(ar).Body);
                Assert.AreEqual("Hello World!", queue2.Dequeue().Body);

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue1 != null)
                    queue1.Close();

                if (queue2 != null)
                    queue2.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Wait_Peek_Multiple()
        {
            // Force a peek operation to wait for a message to be enqueued.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            MsgQueue[] queues = new MsgQueue[10];
            IAsyncResult[] arWaiting = new IAsyncResult[queues.Length]; ;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                // ----------------------------------------
                // File based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                for (int i = 0; i < queues.Length; i++)
                {
                    queues[i] = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                    arWaiting[i] = queues[i].BeginPeek(TimeSpan.FromMilliseconds(5000), null, null);
                }

                Thread.Sleep(1000);
                for (int i = 0; i < queues.Length; i++)
                    queue.Enqueue(new QueuedMsg("Hello World!"));

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].EndPeek(arWaiting[i]).Body);

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].Dequeue().Body);

                engine.Stop();
                engine = null;

                // ----------------------------------------
                // Memory based store and transaction log

                engine = new MsgQueueEngine(new MsgQueueMemoryStore(), new LillTek.Transactions.MemoryTransactionLog());
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                for (int i = 0; i < queues.Length; i++)
                {
                    queues[i] = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                    arWaiting[i] = queues[i].BeginPeek(TimeSpan.FromMilliseconds(5000), null, null);
                }

                Thread.Sleep(1000);
                for (int i = 0; i < queues.Length; i++)
                    queue.Enqueue(new QueuedMsg("Hello World!"));

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].EndPeek(arWaiting[i]).Body);

                for (int i = 0; i < queues.Length; i++)
                    Assert.AreEqual("Hello World!", queues[i].Dequeue().Body);

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                for (int i = 0; i < queues.Length; i++)
                    if (queues[i] != null)
                        queues[i].Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Timeout_Peek()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue.Peek(TimeSpan.FromMilliseconds(500));
                Assert.Fail("Expected a Timeout");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Timeout_Peek_Immediate()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private class ParallelInfo
        {
            public MsgRouter Router;
            public Dictionary<string, bool> Received;
            public int QueueID;
            public int QueueCount;
            public int SendCount;

            public ParallelInfo(MsgRouter router, int queueID, int queueCount, int sendCount)
            {
                this.Router = router;
                this.Received = new Dictionary<string, bool>();
                this.QueueID = queueID;
                this.QueueCount = queueCount;
                this.SendCount = sendCount;
            }
        }

        private int cSentMessages = 0;

        private void OnEnqueueParallel(MsgQueueEngine engine)
        {
            Interlocked.Increment(ref cSentMessages);
        }

        private void ParallelSend(object arg)
        {
            ParallelInfo info = (ParallelInfo)arg;

            try
            {
                using (MsgQueue queue = new MsgQueue(info.Router, info.QueueID.ToString(), MsgQueueSettings.LoadConfig("MsgQueueClient")))
                {
                    for (int i = 0; i < info.QueueCount; i++)
                        for (int j = 0; j < info.SendCount; j++)
                            queue.EnqueueTo(i.ToString(), new QueuedMsg(string.Format("{0}:{1}", info.QueueID, j)));
                }
            }
            catch
            {
            }
        }

        private void ParallelReceive(object arg)
        {
            ParallelInfo info = (ParallelInfo)arg;

            try
            {
                using (MsgQueue queue = new MsgQueue(info.Router, info.QueueID.ToString(), MsgQueueSettings.LoadConfig("MsgQueueClient")))
                {
                    while (info.Received.Count < info.QueueCount * info.SendCount)
                        info.Received.Add((string)queue.Dequeue(TimeSpan.FromMinutes(2)).Body, true);
                }
            }
            catch
            {
            }
        }

        private void DoParallel(LeafRouter router, int cQueues, int cMessages)
        {
            ParallelInfo[] info = new ParallelInfo[cQueues];
            Thread[] sendThreads = new Thread[cQueues];
            Thread[] recvThreads = new Thread[cQueues];

            for (int i = 0; i < cQueues; i++)
            {
                info[i] = new ParallelInfo(router, i, cQueues, cMessages);
                sendThreads[i] = new Thread(new ParameterizedThreadStart(ParallelSend));
                recvThreads[i] = new Thread(new ParameterizedThreadStart(ParallelReceive));
            }

            for (int i = 0; i < cQueues; i++)
            {
                sendThreads[i].Start(info[i]);
                recvThreads[i].Start(info[i]);
            }

            for (int i = 0; i < cQueues; i++)
            {
                sendThreads[i].Join();
                recvThreads[i].Join();
            }

            for (int i = 0; i < cQueues; i++)
                for (int j = 0; j < cMessages; j++)
                    Assert.IsTrue(info[i].Received.ContainsKey(string.Format("{0}:{1}", i, j)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Parallel()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                DoParallel(router, 2, 5);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Parallel_Blast()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.EnqueueEvent += new MsgQueueEngineDelegate(OnEnqueueParallel);
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                cSentMessages = 0;
                for (int i = 0; i < 10; i++)
                {
                    long startTime;
                    int cMsgs;
                    TimeSpan duration;

                    startTime = HiResTimer.Count;
                    cMsgs = cSentMessages;
                    DoParallel(router, 20, 10);
                    duration = HiResTimer.CalcTimeSpan(startTime);

                    Console.WriteLine("Messages Sent: {0} Messages/sec: {1:0.00}", cSentMessages, (cSentMessages - cMsgs) / duration.TotalSeconds);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Expire_Message()
        {
            // Queue a message with a short TTL and the verify that it expires and
            // is added to the dead letter queue.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            MsgQueueEngineSettings settings;
            QueuedMsg msg;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                settings = MsgQueueEngineSettings.LoadConfig("MsgQueueEngine");
                settings.FlushInterval = TimeSpan.FromSeconds(1);
                settings.DeadLetterTTL = TimeSpan.FromSeconds(30);
                engine.Start(router, settings);

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                msg = new QueuedMsg("Hello World!");
                msg.ExpireTime = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                queue.Enqueue(msg);

                Assert.AreEqual("Hello World!", queue.Peek().Body);  // Verify that the message was queued OK
                Thread.Sleep(3000);                                 // Wait enough time for the message to be flushed

                Assert.IsNull(queue.Peek(TimeSpan.Zero), "Message has not been moved to the dead letter queue.");

                // Confirm that the message made it to the dead letter queue.

                Assert.AreEqual("Hello World!", queue.DequeueFrom(MsgQueueEngine.DeadLetterQueueEP, TimeSpan.Zero).Body);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Expire_DeadLetter()
        {
            // Queue a message with a short TTL and the verify that it expires and
            // is added to the dead letter queue, and then that the dead letter
            // expires.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            MsgQueueEngineSettings settings;
            QueuedMsg msg;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                settings = MsgQueueEngineSettings.LoadConfig("MsgQueueEngine");
                settings.FlushInterval = TimeSpan.FromSeconds(1);
                settings.DeadLetterTTL = TimeSpan.FromSeconds(3);
                engine.Start(router, settings);

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                msg = new QueuedMsg("Hello World!");
                msg.ExpireTime = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                queue.Enqueue(msg);

                Assert.AreEqual("Hello World!", queue.Peek().Body);  // Verify that the message was queued OK
                Thread.Sleep(3000);                                 // Wait enough time for the message to be flushed

                Assert.IsNull(queue.Peek(TimeSpan.Zero), "Message has not been moved to the dead letter queue.");

                // Confirm that the message made it to the dead letter queue.

                Assert.AreEqual("Hello World!", queue.PeekFrom(MsgQueueEngine.DeadLetterQueueEP, TimeSpan.Zero).Body);

                // Wait long enough that the message expires from the dead letter
                // queue and verify that it's gone.

                Thread.Sleep(5000);

                Assert.IsNull(queue.PeekFrom(MsgQueueEngine.DeadLetterQueueEP, TimeSpan.Zero),
                              "Message has not been flushed from the dead letter queue.");
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Commit()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();
                queue.Enqueue(new QueuedMsg(10));
                queue.Commit();

                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Peek().Body);
                Assert.AreEqual(10, queue.Dequeue().Body);
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Rollback()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();
                queue.Enqueue(new QueuedMsg(10));
                queue.Rollback();

                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Nested_00()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                queue.BeginTransaction();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);

                queue.Rollback();

                queue.Commit();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Nested_01()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 10));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 20));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));

                queue.BeginTransaction();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(30)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));

                queue.Rollback();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(30)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));

                queue.Rollback();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));

                queue.Rollback();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 40));
                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(40, queue.Peek(TimeSpan.FromSeconds(10)).Body);

                queue.Commit();

                Assert.AreEqual(40, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Nested_02()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                queue.BeginTransaction();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);

                queue.Commit();

                queue.Commit();

                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Nested_03()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                queue.BeginTransaction();

                Assert.AreEqual(10, queue.Peek(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Peek(TimeSpan.FromSeconds(10)).Body);

                queue.Commit();

                queue.Commit();

                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private class ParallelTransInfo
        {
            public MsgRouter Router;
            public int ThreadID;
            public int MessageCount;

            public ParallelTransInfo(MsgRouter router, int threadID, int cMessages)
            {

                this.Router = router;
                this.ThreadID = threadID;
                this.MessageCount = cMessages;
            }
        }

        private void ParallelTransFunc(object arg)
        {
            ParallelTransInfo info = (ParallelTransInfo)arg;
            MsgQueue queue;

            using (queue = new MsgQueue(info.Router, null, MsgQueueSettings.LoadConfig("MsgQueueClient")))
            {
                for (int i = 0; i < info.MessageCount; i++)
                {
                    queue.BeginTransaction();

                    queue.EnqueueTo("logical://Test/Queues/" + info.ThreadID.ToString(), new QueuedMsg(i));

                    queue.BeginTransaction();

                    queue.EnqueueTo("logical://Test/Queues/" + info.ThreadID.ToString(), new QueuedMsg(i + 100000));
                    queue.EnqueueTo("logical://Test/Queues/" + info.ThreadID.ToString(), new QueuedMsg(-i));

                    queue.Rollback();

                    queue.Commit();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_Parallel_Blast()
        {
            // Crank up 10 threads that each submit 500 messages to a queue using transactions
            // and then dequeue all of the messages and verify that everything worked OK.

            const int ThreadCount = 10;
            const int MessageCount = 500;

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;
            Thread[] threads = new Thread[ThreadCount];
            Dictionary<string, bool> msgs;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(new ParameterizedThreadStart(ParallelTransFunc));
                    threads[i].Start(new ParallelTransInfo(router, i, MessageCount));
                }

                for (int i = 0; i < threads.Length; i++)
                    threads[i].Join();

                msgs = new Dictionary<string, bool>();
                for (int i = 0; i < ThreadCount; i++)
                {
                    string queueEP = "logical://Test/Queues/" + i.ToString();

                    while (queue.PeekFrom(queueEP, TimeSpan.Zero) != null)
                        msgs.Add(i.ToString() + ":" + queue.DequeueFrom(queueEP).Body.ToString(), true);
                }

                Assert.AreEqual(ThreadCount * MessageCount, msgs.Count);
                for (int i = 0; i < ThreadCount; i++)
                    for (int j = 0; j < MessageCount; j++)
                        Assert.IsTrue(msgs.ContainsKey(i.ToString() + ":" + j.ToString()));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_RollbackAll()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 10));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 20));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));

                queue.BeginTransaction();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);

                queue.BeginTransaction();

                queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 40));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 50));
                queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 60));

                queue.RollbackAll();

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Explicit_Transaction_RollbackOnClose()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                for (int i = 0; i < 10; i++)
                {
                    queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                    queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 10));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 20));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));

                    queue.BeginTransaction();

                    Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);

                    queue.BeginTransaction();

                    queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 40));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 50));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 60));

                    queue.Close();

                    queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                    Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.IsNull(queue.Peek(TimeSpan.Zero));
                }
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Transaction_Isolation_Enqueue()
        {
            // Verify that enqueue is isolated across transactions.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue1 = null;
            MsgQueue queue2 = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue1.BeginTransaction();
                queue1.EnqueueTo("Foo", new QueuedMsg(10));

                Assert.IsNull(queue2.PeekFrom("Foo", TimeSpan.Zero));

                try
                {
                    queue2.DequeueFrom("Foo", TimeSpan.Zero);
                    Assert.Fail("Expecting an exception.");
                }
                catch
                {
                }

                queue1.Commit();

                Assert.AreEqual(10, queue2.DequeueFrom("Foo", TimeSpan.Zero).Body);

                //-----------------------------------------

                queue1.BeginTransaction();
                queue2.BeginTransaction();

                queue1.EnqueueTo("Foo", new QueuedMsg(10));
                Assert.IsNull(queue2.PeekFrom("Foo", TimeSpan.Zero));

                try
                {
                    queue2.DequeueFrom("Foo", TimeSpan.Zero);
                    Assert.Fail("Expecting an exception.");
                }
                catch
                {
                }

                queue1.Commit();

                Assert.AreEqual(10, queue2.DequeueFrom("Foo", TimeSpan.Zero).Body);

                queue2.Commit();
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue1 != null)
                    queue1.Close();

                if (queue2 != null)
                    queue2.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Transaction_Isolation_Dequeue()
        {
            // Verify that dequeue is isolated across transactions.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue1 = null;
            MsgQueue queue2 = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue1.EnqueueTo("Foo", new QueuedMsg(10));

                queue1.BeginTransaction();
                Assert.AreEqual(10, queue1.DequeueFrom("Foo").Body);

                Assert.IsNull(queue2.PeekFrom("Foo", TimeSpan.Zero));

                try
                {
                    queue2.DequeueFrom("Foo", TimeSpan.Zero);
                    Assert.Fail("Expecting an exception.");
                }
                catch
                {
                }

                queue1.Rollback();

                Assert.AreEqual(10, queue2.DequeueFrom("Foo", TimeSpan.Zero).Body);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue1 != null)
                    queue1.Close();

                if (queue2 != null)
                    queue2.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Transaction_Isolation_Peek()
        {
            // Verify that peek is isolated across transactions.

            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue1 = null;
            MsgQueue queue2 = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue1 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));
                queue2 = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue1.EnqueueTo("Foo", new QueuedMsg(10));

                queue1.BeginTransaction();
                Assert.AreEqual(10, queue1.DequeueFrom("Foo").Body);

                Assert.IsNull(queue1.PeekFrom("Foo", TimeSpan.Zero));
                Assert.IsNull(queue2.PeekFrom("Foo", TimeSpan.Zero));

                queue1.Rollback();

                Assert.AreEqual(10, queue2.DequeueFrom("Foo", TimeSpan.Zero).Body);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue1 != null)
                    queue1.Close();

                if (queue2 != null)
                    queue2.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Commit()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.VeryLow, "Message #0"));

                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {
                    queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.Low, "Message #1"));
                    queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.High, "Message #2"));
                    scope.Complete();
                }

                Assert.AreEqual("Message #2", queue.DequeueFrom("Foo", TimeSpan.Zero).Body);
                Assert.AreEqual("Message #1", queue.DequeueFrom("Foo", TimeSpan.Zero).Body);
                Assert.AreEqual("Message #0", queue.DequeueFrom("Foo", TimeSpan.Zero).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Rollback()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.VeryLow, "Message #0"));

                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {

                    queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.Low, "Message #1"));
                    queue.EnqueueTo("Foo", new QueuedMsg(DeliveryPriority.High, "Message #2"));
                }

                Assert.AreEqual("Message #0", queue.DequeueFrom("Foo", TimeSpan.Zero).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Transaction_Nested_00()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                using (TransactionScope scope0 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                    using (TransactionScope scope1 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                    {
                        Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                        Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);

                        scope1.Dispose();   // Intentional rollback
                    }

                    queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryLow, 30));
                    scope0.Complete();
                }

                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Transaction_Nested_01()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                using (TransactionScope scope0 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.VeryHigh, 10));

                    using (TransactionScope scope1 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                    {
                        queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 20));

                        using (TransactionScope scope2 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                        {
                            queue.Enqueue(new QueuedMsg(DeliveryPriority.Normal, 30));

                            using (TransactionScope scope3 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                            {
                                Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                                Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(30)).Body);
                                Assert.IsNull(queue.Peek(TimeSpan.Zero));

                                scope3.Dispose();   // Intentional Rollback
                            }

                            Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                            Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                            Assert.AreEqual(30, queue.Dequeue(TimeSpan.FromSeconds(30)).Body);
                            Assert.IsNull(queue.Peek(TimeSpan.Zero));

                            scope2.Dispose();   // Intentional Rollback
                        }

                        Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                        Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(20)).Body);
                        Assert.IsNull(queue.Peek(TimeSpan.Zero));

                        scope1.Dispose();   // Intentional rollback
                    }

                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 40));
                    Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                    Assert.AreEqual(40, queue.Peek(TimeSpan.FromSeconds(10)).Body);

                    scope0.Complete();
                }

                Assert.AreEqual(40, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Transaction_Nested_02()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                using (TransactionScope scope0 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                    using (TransactionScope scope1 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                    {
                        Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                        scope1.Complete();
                    }

                    scope0.Complete();
                }

                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueue_Ambient_Transaction_Nested_03()
        {
            LeafRouter router = null;
            MsgQueueEngine engine = null;
            MsgQueue queue = null;

            SetConfig();
            ClearFolder();

            router = new LeafRouter();
            router.Start();

            try
            {
                engine = new MsgQueueEngine(new MsgQueueFileStore(folder), new LillTek.Transactions.FileTransactionLog(folder + "\\Log"));
                engine.Start(router, MsgQueueEngineSettings.LoadConfig("MsgQueueEngine"));

                queue = new MsgQueue(router, null, MsgQueueSettings.LoadConfig("MsgQueueClient"));

                using (TransactionScope scope0 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                {
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.High, 10));
                    queue.Enqueue(new QueuedMsg(DeliveryPriority.Low, 20));

                    using (TransactionScope scope1 = new TransactionScope(TransactionScopeOption.RequiresNew, TimeSpan.FromMinutes(10)))
                    {
                        Assert.AreEqual(10, queue.Peek(TimeSpan.FromSeconds(10)).Body);
                        Assert.AreEqual(10, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                        Assert.AreEqual(20, queue.Peek(TimeSpan.FromSeconds(10)).Body);

                        scope1.Complete();
                    }

                    scope0.Complete();
                }

                Assert.AreEqual(20, queue.Dequeue(TimeSpan.FromSeconds(10)).Body);
                Assert.IsNull(queue.Peek(TimeSpan.Zero));
            }
            finally
            {
                queue.RollbackAll();

                if (engine != null)
                    engine.Stop();

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

