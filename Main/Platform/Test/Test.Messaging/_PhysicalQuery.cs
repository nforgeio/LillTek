//-----------------------------------------------------------------------------
// FILE:        _PhysicalQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for Queries directed at physical endpoints while
//              operating in UDPBROADCAST discovery node.

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
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _PhysicalQuery
    {
        private static object syncLock = new object();

        private const int BlastCount = 100;
        private const int InitDelay = 2000;
        private const string group = "231.222.0.1:45001";

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 1);

            AsyncTracker.Enable = false;
            AsyncTracker.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
            AsyncTracker.Stop();
        }

        private class BaseMsg : PropertyMsg
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

            [MsgHandler]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
            public void OnMsg(NotCachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }

            [MsgHandler]
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
MsgRouter.DiscoveryMode         = MULTICAST
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

            [MsgHandler]
            [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
            public void OnMsg(NotCachedMsg msg)
            {
                lock (this.SyncRoot)
                    recvCount++;

                DoAction(this, msg);
            }

            [MsgHandler]
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
MsgRouter.DiscoveryMode         = MULTICAST
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
";

            _HubRouter router;

            Config.SetConfig(string.Format(settings, root, name, cloudEP));
            router = new _HubRouter();
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalQuery_Basic_Self()
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
                ack = (TestAck)leaf.Query(leaf.RouterEP, new NotCachedMsg("Normal", s));
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
        public void PhysicalQuery_Basic_Self_Blast()
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
                    ack = (TestAck)leaf.Query(leaf.RouterEP, new NotCachedMsg("Normal", s));
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
        public void PhysicalQuery_Basic_Remote()
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
                ack = (TestAck)hub.Query(leaf.RouterEP, new NotCachedMsg("Normal", s));
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
        public void PhysicalQuery_LongQuery_Self()
        {
            _HubRouter hub = null;
            TestAck ack;
            string s;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("detached", "hub0", group);
                Thread.Sleep(InitDelay);

                // Verify a long running q/r between a single router, 
                // verifying that query was not sent multiple times.

                s = "Hello World!";
                ack = (TestAck)hub.Query(hub.RouterEP, new NotCachedMsg("Sleep", s));
                Assert.AreEqual(s, ack.Value);
                Assert.IsTrue(hub.ReceiveCount == 1);
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
        public void PhysicalQuery_LongQuery_Remote()
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

                // Verify a long running q/r between two routers,
                // verifying that the query was not sent multiple times.

                s = "Hello World!";
                ack = (TestAck)hub.Query(leaf.RouterEP, new NotCachedMsg("Sleep", s));
                Assert.AreEqual(s, ack.Value);
                Assert.IsTrue(leaf.ReceiveCount == 1);
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
        public void PhysicalQuery_Basic_Remote_Blast()
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
                    ack = (TestAck)hub.Query(leaf.RouterEP, new NotCachedMsg("Normal", s));
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
        public void PhysicalQuery_Timeout_Self()
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
                    ack = (TestAck)leaf.Query(leaf.RouterEP, new NotCachedMsg("Ignore", string.Empty));
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
        public void PhysicalQuery_Timeout_Remote()
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
                    ack = (TestAck)leaf.Query(hub.RouterEP, new NotCachedMsg("Ignore", string.Empty));
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
        public void PhysicalQuery_Timeout_Self_Cached()
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
                    ack = (TestAck)leaf.Query(leaf.RouterEP, new CachedMsg("Ignore", string.Empty));
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
        public void PhysicalQuery_Timeout_Remote_Cached()
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
                    ack = (TestAck)leaf.Query(hub.RouterEP, new CachedMsg("Ignore", string.Empty));
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
        public void PhysicalQuery_Cached_Self()
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

                ack1 = (TestAck)leaf.Query(leaf.RouterEP, query);
                Assert.AreEqual(1, leaf.ReceiveCount);

                // Simulate the resending of the query message and wait 
                // for another reply.  The second reply should hold the 
                // same value as the first.

                query = new CachedMsg("ActionCount", string.Empty);
                query._ToEP = leaf.RouterEP;
                query._SessionID = ack1._SessionID;
                query._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession | MsgFlag.KeepSessionID;

                ack2 = (TestAck)leaf.Query(leaf.RouterEP, query);
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
        public void PhysicalQuery_Cached_Remote()
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

                ack1 = (TestAck)leaf.Query(hub.RouterEP, query);
                Assert.AreEqual(1, hub.ReceiveCount);

                // Simulate the resending of the query message and wait 
                // for another reply.  The second reply should hold the 
                // same value as the first.

                query = new CachedMsg("ActionCount", string.Empty);
                query._ToEP = leaf.RouterEP;
                query._SessionID = ack1._SessionID;
                query._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession | MsgFlag.KeepSessionID;

                ack2 = (TestAck)leaf.Query(hub.RouterEP, query);
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
        public void PhysicalQuery_Exception()
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
                    ack = (TestAck)leaf.Query(hub.RouterEP, new NotCachedMsg("Exception", "Test Exception"));
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
    }
}

