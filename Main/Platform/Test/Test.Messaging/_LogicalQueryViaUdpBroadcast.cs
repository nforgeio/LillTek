//-----------------------------------------------------------------------------
// FILE:        _LogicalQueryViaUdpBroadcast.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for Queries directed at logical endpoints while
//              the routers are configured for UDP-BROADCAST based discovery.

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
using LillTek.Net.Broadcast;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _LogicalQueryViaUdpBroadcast
    {
        private static object syncLock = new object();

        private const int BlastCount = 100;
        private const int InitDelay = 2000;
        private const string group = "231.222.0.1:45001";

        private static UdpBroadcastServer broadcastServer;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 1);

            AsyncTracker.Enable = false;
            AsyncTracker.Start();

            broadcastServer = new UdpBroadcastServer(new UdpBroadcastServerSettings(), null, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (broadcastServer != null)
            {
                broadcastServer.Close();
                broadcastServer = null;
            }

            NetTrace.Stop();
            AsyncTracker.Stop();
        }

        public class BaseMsg : PropertyMsg
        {
            public BaseMsg()
                : base()
            {
            }

            public BaseMsg(string action, string value)
                : base()
            {
                this.Action = action;
                this.Value = value;
            }

            public string Action
            {
                get { return base["Action"]; }
                set { base["Action"] = value; }
            }

            public string Value
            {
                get { return base["Value"]; }
                set { base["Value"] = value; }
            }
        }

        private class CachedMsg : BaseMsg
        {
            public CachedMsg()
                : base()
            {
            }

            public CachedMsg(string action, string value)
                : base(action, value)
            {
            }
        }

        private class NotCachedMsg : BaseMsg
        {
            public NotCachedMsg()
                : base()
            {
            }

            public NotCachedMsg(string action, string value)
                : base(action, value)
            {
            }
        }

        private class TestAck : Ack
        {
            public TestAck()
                : base()
            {
            }

            public TestAck(string value)
                : base()
            {
                this.Value = value;
            }

            public string Value
            {
                get { return base["Value"]; }
                set { base["Value"] = value; }
            }

            public override Msg Clone()
            {
                TestAck clone;

                clone = new TestAck();
                clone.CopyBaseFields(this, true);
                return clone;
            }
        }

        static int actionCount = 0;

        private static void DoAction(MsgRouter router, BaseMsg msg)
        {
            TestAck ack;

            try
            {
                switch (msg.Action)
                {
                    case "Normal":

                        router.ReplyTo(msg, new TestAck(msg.Value));
                        break;

                    case "Normal-RequestContext":

                        using (MsgRequestContext ctx = msg.CreateRequestContext())
                            ctx.Reply(new TestAck(msg.Value));

                        break;

                    case "Normal-Orphan-RequestContext":

                        msg.CreateRequestContext();
                        break;

                    case "Exception":

                        ack = new TestAck();
                        ack.Exception = msg.Value;

                        router.ReplyTo(msg, ack);
                        break;

                    case "Ignore":

                        break;

                    case "ActionCount":

                        ack = new TestAck();

                        lock (syncLock)
                            ack.Value = msg.Value + ": " + actionCount.ToString();

                        router.ReplyTo(msg, ack);
                        break;

                    case "Throw-Exception":

                        Assert.Fail(msg.Value);
                        break;

                    case "Sleep":

                        Thread.Sleep(TimeSpan.FromSeconds(router.SessionTimeout.TotalSeconds + 5));
                        router.ReplyTo(msg, new TestAck("Hello World!"));
                        break;

                    default:

                        throw new InvalidOperationException("Unexpected action.");
                }
            }
            finally
            {
                lock (syncLock)
                    actionCount++;
            }
        }

        private class _LeafRouter : LeafRouter
        {
            private int recvCount;

            public _LeafRouter()
                : base()
            {
                recvCount = 0;
            }

            public void Clear()
            {
                lock (this.SyncRoot)
                    recvCount = 0;
            }

            public int ReceiveCount
            {
                get
                {
                    lock (this.SyncRoot)
                        return recvCount;
                }
            }

            [MsgHandler(LogicalEP = "logical://leaf")]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
            public void OnMsg(NotCachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }

            [MsgHandler(LogicalEP = "logical://leaf-async")]
            [MsgSession(Type = SessionTypeID.Query, IsAsync = true, Idempotent = false)]
            public void OnMsgAsync(NotCachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }

            [MsgHandler(LogicalEP = "logical://leaf")]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = true)]
            public void OnMsg(CachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }
        }

        private _LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.RouterEP				= physical://{0}/{1}/{2}
MsgRouter.DiscoveryMode         = UDPBROADCAST
MsgRouter.CloudEP    			= {3}
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 5m
MsgRouter.AdvertiseTime			= 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s

MsgRouter.BroadcastSettings.NetworkBinding        = ANY
MsgRouter.BroadcastSettings.SocketBufferSize      = 1M
MsgRouter.BroadcastSettings.Server[0]             = localhost:UDP-BROADCAST
MsgRouter.BroadcastSettings.SharedKey             = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
MsgRouter.BroadcastSettings.MessageTTL            = 15m
MsgRouter.BroadcastSettings.BroadcastGroup        = 0
MsgRouter.BroadcastSettings.BkTaskInterval        = 1s
MsgRouter.BroadcastSettings.KeepAliveInterval     = 30s
MsgRouter.BroadcastSettings.ServerResolveInterval = 5m
";

            _LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP));
            router = new _LeafRouter();
            router.Start();

            return router;
        }

        private class _HubRouter : HubRouter
        {
            private int recvCount;

            public _HubRouter()
                : base()
            {
                recvCount = 0;
            }

            public void Clear()
            {
                lock (this.SyncRoot)
                    recvCount = 0;
            }

            public int ReceiveCount
            {
                get
                {
                    lock (this.SyncRoot)
                        return recvCount;
                }
            }

            [MsgHandler(LogicalEP = "logical://hub")]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
            public void OnMsg(NotCachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }

            [MsgHandler(LogicalEP = "logical://hub")]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = true)]
            public void OnMsg(CachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }
        }

        private _HubRouter CreateHub(string root, string name, string cloudEP)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.RouterEP				= physical://{0}/{1}
MsgRouter.ParentEP              = 
MsgRouter.DiscoveryMode         = UDPBROADCAST
MsgRouter.CloudEP    			= {2}
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 3m
MsgRouter.AdvertiseTime			= 30s
MsgRouter.KeepAliveTime         = 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s

MsgRouter.BroadcastSettings.NetworkBinding        = ANY
MsgRouter.BroadcastSettings.SocketBufferSize      = 1M
MsgRouter.BroadcastSettings.Server[0]             = localhost:UDP-BROADCAST
MsgRouter.BroadcastSettings.SharedKey             = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
MsgRouter.BroadcastSettings.MessageTTL            = 15m
MsgRouter.BroadcastSettings.BroadcastGroup        = 0
MsgRouter.BroadcastSettings.BkTaskInterval        = 1s
MsgRouter.BroadcastSettings.KeepAliveInterval     = 30s
MsgRouter.BroadcastSettings.ServerResolveInterval = 5m
";

            _HubRouter router;

            Config.SetConfig(string.Format(settings, root, name, cloudEP));
            router = new _HubRouter();
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Basic_Self()
        {
            _LeafRouter leaf = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between a single router

                s = "Hello World!";
                ack = (TestAck)leaf.Query("logical://leaf", new NotCachedMsg("Normal", s));
                Assert.AreEqual(s, ack.Value);
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_RequestContext()
        {
            // Verify that using MsgRequestContext to process the request on the
            // server side works.

            _LeafRouter leaf = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between a single router

                s = "Hello World!";
                ack = (TestAck)leaf.Query("logical://leaf-async", new NotCachedMsg("Normal-RequestContext", s));
                Assert.AreEqual(s, ack.Value);
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_RequestContext_Orphaned()
        {
            // Verify that an orphaned MsgRequestContext's finalizer handles
            // transaction cancellation properly.

            _LeafRouter leaf = null;
            IAsyncResult ar;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between a single router

                ar = leaf.BeginQuery("logical://leaf-async", new NotCachedMsg("Normal-Orphan-RequestContext", "test"), null, null);
                Thread.Sleep(500);  // Give the request a chance to be received

                // The delay above should have allowed the request to have been received
                // and the MsgRequestContext to be created and orphaned.  We'll force
                // a garbage collection now which should cause the context's finalizer
                // to be called when should cause the query to fail with a CancelException.

                GC.Collect();
                GC.WaitForPendingFinalizers();

                leaf.EndQuery(ar);
                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Basic_Self_Blast()
        {
            _LeafRouter leaf = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                for (int i = 0; i < BlastCount; i++)
                {

                    // Verify a q/r between a single router

                    s = "Hello World: " + i.ToString();
                    ack = (TestAck)leaf.Query("logical://leaf", new NotCachedMsg("Normal", s));
                    Assert.AreEqual(s, ack.Value);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Basic_Remote()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between two routers

                s = "Hello World!";
                ack = (TestAck)hub.Query("logical://leaf", new NotCachedMsg("Normal", s));
                Assert.AreEqual(s, ack.Value);
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Broadcast_Query()
        {
            // Verify that a query with the broadcast flag set is actually
            // delivered to multiple service instances and that the response
            // from one of them is received.

            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg query;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group);
                leaf2 = CreateLeaf("detached", "hub0", "leaf2", group);
                Thread.Sleep(InitDelay);

                s = "Hello World!";
                query = new NotCachedMsg("Normal", s);
                query._Flags |= MsgFlag.Broadcast;
                ack = (TestAck)leaf0.Query("logical://leaf", query);
                Assert.AreEqual(s, ack.Value);

                Thread.Sleep(1000);

                Assert.AreEqual(1, leaf0.ReceiveCount);
                Assert.AreEqual(1, leaf1.ReceiveCount);
                Assert.AreEqual(1, leaf2.ReceiveCount);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_LongQuery_Self()
        {
            _HubRouter hub = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between two routers

                s = "Hello World!";
                ack = (TestAck)hub.Query("logical://hub", new NotCachedMsg("Sleep", s));
                Assert.AreEqual(s, ack.Value);
                Assert.IsTrue(hub.ReceiveCount > 0);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_LongQuery_Remote()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify a q/r between two routers

                s = "Hello World!";
                ack = (TestAck)hub.Query("logical://leaf", new NotCachedMsg("Sleep", s));
                Assert.AreEqual(s, ack.Value);
                Assert.IsTrue(leaf.ReceiveCount > 0);
            }
            finally
            {

                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Basic_Remote_Blast()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                for (int i = 0; i < BlastCount; i++)
                {
                    // Verify a q/r between two routers

                    s = "Hello World: " + i.ToString();
                    ack = (TestAck)hub.Query("logical://leaf", new NotCachedMsg("Normal", s));
                    Assert.AreEqual(s, ack.Value);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Timeout_Self()
        {
            _LeafRouter leaf = null;
            TestAck ack;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that retries and timeout works

                try
                {
                    ack = (TestAck)leaf.Query("logical://leaf", new NotCachedMsg("Ignore", string.Empty));
                    Assert.Fail("Should have seen a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Verify that since the NotCachedMsg handlers are not tagged with
                    // [MsgHandler[Idempotent=true)] that we should have seen
                    // all of the retry messages as well.

                    Assert.AreEqual(leaf.SessionRetries, leaf.ReceiveCount);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Timeout_Remote()
        {

            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that retries and timeout works

                try
                {
                    ack = (TestAck)leaf.Query("logical://hub", new NotCachedMsg("Ignore", string.Empty));
                    Assert.Fail("Should have seen a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Verify that since the NotCachedMsg handlers are not tagged with
                    // [MsgHandler[Idempotent=true)] that we should have seen
                    // all of the retry messages as well.

                    Assert.AreEqual(leaf.SessionRetries, hub.ReceiveCount);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Timeout_Self_Cached()
        {
            _LeafRouter leaf = null;
            TestAck ack;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that retries and timeout works

                try
                {
                    ack = (TestAck)leaf.Query("logical://leaf", new CachedMsg("Ignore", string.Empty));
                    Assert.Fail("Should have seen a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Verify that since the CachedMsg handlers are tagged with
                    // [MsgHandler[Idempotent=true)] that we have not seen
                    // seen any retry messages

                    Assert.AreEqual(1, leaf.ReceiveCount);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Timeout_Remote_Cached()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that retries and timeout works

                try
                {
                    ack = (TestAck)leaf.Query("logical://hub", new CachedMsg("Ignore", string.Empty));
                    Assert.Fail("Should have seen a TimeoutException");
                }
                catch (TimeoutException)
                {
                    // Verify that since the CachedMsg handlers are tagged with
                    // [MsgHandler[Idempotent=true)] that we have not
                    // seen any retry messages

                    Assert.AreEqual(1, hub.ReceiveCount);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Cached_Self()
        {
            _LeafRouter leaf = null;
            CachedMsg query;
            TestAck ack1;
            TestAck ack2;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that sessions and replies are cached

                query = new CachedMsg("ActionCount", string.Empty);

                ack1 = (TestAck)leaf.Query("logical://leaf", query);
                Assert.AreEqual(1, leaf.ReceiveCount);

                // Simulate the resending of the query message and wait 
                // for another reply.  The second reply should hold the 
                // same value as the first.

                query = new CachedMsg("ActionCount", string.Empty);
                query._ToEP = leaf.RouterEP;
                query._SessionID = ack1._SessionID;
                query._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession | MsgFlag.KeepSessionID;

                ack2 = (TestAck)leaf.Query("logical://leaf", query);
                Assert.AreEqual(1, leaf.ReceiveCount);
                Assert.AreEqual(ack1.Value, ack2.Value);
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Cached_Remote()
        {
            _HubRouter hub = null;
            _LeafRouter leaf = null;
            CachedMsg query;
            TestAck ack1;
            TestAck ack2;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that sessions and replies are cached

                query = new CachedMsg("ActionCount", string.Empty);

                ack1 = (TestAck)leaf.Query("logical://hub", query);
                Assert.AreEqual(1, hub.ReceiveCount);

                // Simulate the resending of the query message and wait 
                // for another reply.  The second reply should hold the 
                // same value as the first.

                query = new CachedMsg("ActionCount", string.Empty);
                query._ToEP = leaf.RouterEP;
                query._SessionID = ack1._SessionID;
                query._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession | MsgFlag.KeepSessionID;

                ack2 = (TestAck)leaf.Query("logical://hub", query);
                Assert.AreEqual(1, hub.ReceiveCount);
                Assert.AreEqual(ack1.Value, ack2.Value);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_Exception()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            TestAck ack;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                Thread.Sleep(InitDelay);

                // Verify that retries and timeout works

                try
                {
                    ack = (TestAck)leaf.Query("logical://hub", new NotCachedMsg("Exception", "Test Exception"));
                    Assert.Fail("Should have seen a TimeoutException");
                }
                catch (SessionException e)
                {
                    Assert.AreEqual("Test Exception", e.Message);
                }
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        public class AsyncMsg : BaseMsg
        {
            public AsyncMsg()
                : base()
            {
            }

            public AsyncMsg(string action, string value)
                : base(action, value)
            {
            }
        }

        private sealed class AsyncState
        {
            public MsgRouter Router;
            public ISession Session;
            public Msg Query;

            public AsyncState(MsgRouter router, ISession session, Msg query)
            {
                this.Router = router;
                this.Session = session;
                this.Query = query;
            }
        }

        private void OnAsyncDone(IAsyncResult ar)
        {
            AsyncState state = (AsyncState)ar.AsyncState;

            state.Router.ReplyTo(state.Query, new TestAck("async-complete"));
            state.Session.OnAsyncFinished();
            AsyncTimer.EndTimer(ar);
        }

        [MsgHandler(LogicalEP = "logical://async-infinite")]
        [MsgSession(Type = SessionTypeID.Query, IsAsync = true, MaxAsyncKeepAlive = "infinite")]
        public void OnAsyncMsg_Infinite(AsyncMsg msg)
        {
            AsyncTimer.BeginTimer(TimeSpan.FromSeconds(20), new AsyncCallback(OnAsyncDone), new AsyncState(msg._Session.Router, msg._Session, msg));
        }

        [MsgHandler(LogicalEP = "logical://async-timeout")]
        [MsgSession(Type = SessionTypeID.Query, IsAsync = true, MaxAsyncKeepAlive = "10s")]
        public void OnAsyncMsg_Timeout(AsyncMsg msg)
        {
        }

        [MsgHandler(LogicalEP = "logical://async-cancel")]
        [MsgSession(Type = SessionTypeID.Query, IsAsync = true, MaxAsyncKeepAlive = "10s")]
        public void OnAsyncMsg_Cancel(AsyncMsg msg)
        {
            msg.CreateRequestContext().Cancel();
        }

        [MsgHandler(LogicalEP = "logical://async-abort")]
        [MsgSession(Type = SessionTypeID.Query, IsAsync = true, MaxAsyncKeepAlive = "10s")]
        public void OnAsyncMsg_Abort(AsyncMsg msg)
        {
            msg.CreateRequestContext().Abort();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_AsyncQuery()
        {
            _LeafRouter leaf = null;
            TestAck ack;
            DateTime start;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // This test verifies that SessionKeepAlive messages continue
                // to be send back to the client even after the message handler
                // has returned an 20 seconds has passed.

                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                leaf.Dispatcher.AddTarget(this);
                Thread.Sleep(InitDelay);

                start = SysTime.Now;
                ack = (TestAck)leaf.Query("logical://async-infinite", new AsyncMsg("Normal", ""));

                Assert.AreEqual("async-complete", ack.Value);
                Assert.IsTrue(SysTime.Now - start >= TimeSpan.FromSeconds(20));
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_AsyncQuery_AutoTimeout()
        {
            _LeafRouter leaf = null;
            DateTime start = SysTime.Now;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // This test verifies that the session is killed automatically

                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                leaf.Dispatcher.AddTarget(this);
                Thread.Sleep(InitDelay);

                leaf.Query("logical://async-timeout", new AsyncMsg("Normal", ""));
                Assert.Fail("Expected a TimeoutException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {

                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_AsyncQuery_Cancel()
        {
            _LeafRouter leaf = null;
            DateTime start = SysTime.Now;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // This test verifies that the session is killed automatically

                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                leaf.Dispatcher.AddTarget(this);
                Thread.Sleep(InitDelay);

                leaf.Query("logical://async-cancel", new AsyncMsg("Normal", ""));
                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalQueryViaUdpBroadcast_AsyncQuery_Abort()
        {
            _LeafRouter leaf = null;
            DateTime start = SysTime.Now;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // This test verifies that the session is killed automatically

                leaf = CreateLeaf("detached", "hub0", "leaf0", group);
                leaf.Dispatcher.AddTarget(this);
                Thread.Sleep(InitDelay);

                leaf.Query("logical://async-abort", new AsyncMsg("Normal", ""));
                Assert.Fail("Expected a TimeoutException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

