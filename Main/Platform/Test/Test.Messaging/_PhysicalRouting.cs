//-----------------------------------------------------------------------------
// FILE:        _PhysicalRouting.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the physical routing of messages between Leaf, Hub and
//              Root routers

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _PhysicalRouting
    {

        private const int BlastCount = 100;
        private const int InitDelay = 2000;
        private const string rootGroup = "231.222.0.1:45000";
        private const string group1 = "231.222.0.1:45001";
        private const string group2 = "231.222.0.1:45002";

        private static TimeSpan maxTime = TimeSpan.FromSeconds(10);

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);

            AsyncTracker.Enable = false;
            AsyncTracker.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
            AsyncTracker.Stop();
        }

        private class _HelloMsg : PropertyMsg
        {
            public _HelloMsg()
            {
            }

            public _HelloMsg(string value)
            {
                this.Value = value;
            }

            public string Value
            {
                get { return this["value"]; }
                set { this["value"] = value; }
            }
        }

        private interface ITestRouter
        {
            MsgEP RouterEP { get; }
            void SendTo(ITestRouter router, Msg msg);
            void Clear();
            int ReceiveCount { get; }
            Msg DequeueReceived();
            void WaitReceived(int count);
        }

        private class _LeafRouter : LeafRouter, ITestRouter
        {
            private Queue recvQueue;

            public _LeafRouter()
                : base()
            {
                recvQueue = new Queue();
            }

            public void SendTo(ITestRouter router, Msg msg)
            {
                SendTo(((MsgRouter)router).RouterEP, msg);
            }

            public void Clear()
            {
                lock (this.SyncRoot)
                {
                    recvQueue.Clear();
                }
            }

            public int ReceiveCount
            {
                get
                {
                    lock (this.SyncRoot)
                        return recvQueue.Count;
                }
            }

            public Msg DequeueReceived()
            {
                Msg msg;
                int count;

                lock (this.SyncRoot)
                {
                    msg = (Msg)recvQueue.Dequeue();
                    count = recvQueue.Count;
                }

                return msg;
            }

            public void WaitReceived(int count)
            {
                DateTime start = SysTime.Now;
                int c;

                while (true)
                {
                    lock (this.SyncRoot)
                        c = recvQueue.Count;

                    if (c >= count)
                        return;

                    if (SysTime.Now - start >= maxTime)
                        throw new TimeoutException();

                    Thread.Sleep(0);
                }
            }

            [MsgHandler(Default = true)]
            public void OnMsg(Msg msg)
            {
                lock (this.SyncRoot)
                    recvQueue.Enqueue(msg);
            }
        }

        private _LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP, bool enableP2P)
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
MsgRouter.EnableP2P             = {4}
MsgRouter.AdvertiseTime			= 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";

            _LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP, enableP2P ? "yes" : "no"));

            router = new _LeafRouter();
            router.Start();

            return router;
        }

        private delegate void RoutePhysicalDelegate(MsgEP physicalEP, Msg msg);

        private class _HubRouter : HubRouter, ITestRouter
        {
            public RoutePhysicalDelegate onRoutePhysical;
            private Queue recvQueue;

            public _HubRouter()
                : base()
            {
                recvQueue = new Queue();
                onRoutePhysical = null;
            }

            public void SendTo(ITestRouter router, Msg msg)
            {
                SendTo(((MsgRouter)router).RouterEP, msg);
            }

            public void Clear()
            {
                lock (this.SyncRoot)
                {
                    recvQueue.Clear();
                }
            }

            public int ReceiveCount
            {
                get
                {
                    lock (this.SyncRoot)
                        return recvQueue.Count;
                }
            }

            public Msg DequeueReceived()
            {
                Msg msg;
                int count;

                lock (this.SyncRoot)
                {
                    msg = (Msg)recvQueue.Dequeue();
                    count = recvQueue.Count;
                }

                return msg;
            }

            public void WaitReceived(int count)
            {
                DateTime start = SysTime.Now;
                int c;

                while (true)
                {
                    lock (this.SyncRoot)
                        c = recvQueue.Count;

                    if (c >= count)
                        return;

                    if (SysTime.Now - start >= maxTime)
                        throw new TimeoutException();

                    Thread.Sleep(0);
                }
            }

            protected override void RoutePhysical(MsgEP physicalEP, Msg msg)
            {
                if (onRoutePhysical != null)
                    onRoutePhysical(physicalEP, msg);

                base.RoutePhysical(physicalEP, msg);
            }

            [MsgHandler(Default = true)]
            public void OnMsg(Msg msg)
            {
                lock (this.SyncRoot)
                    recvQueue.Enqueue(msg);
            }
        }

        private _HubRouter CreateHub(string root, string name, string cloudEP)
        {
            const string settings =
@"
MsgRouter.RouterEP				= physical://{0}/{1}
MsgRouter.ParentEP              = 
MsgRouter.CloudEP    			= {2}
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 5m
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

        private class _RootRouter : RootRouter, ITestRouter
        {
            private Queue recvQueue;

            public _RootRouter()
                : base()
            {
                recvQueue = new Queue();
            }

            public void SendTo(ITestRouter router, Msg msg)
            {
                SendTo(((MsgRouter)router).RouterEP, msg);
            }

            public void Clear()
            {
                lock (this.SyncRoot)
                {
                    recvQueue.Clear();
                }
            }

            public int ReceiveCount
            {
                get
                {
                    lock (this.SyncRoot)
                        return recvQueue.Count;
                }
            }

            public Msg DequeueReceived()
            {
                Msg msg;
                int count;

                lock (this.SyncRoot)
                {
                    msg = (Msg)recvQueue.Dequeue();
                    count = recvQueue.Count;
                }

                return msg;
            }

            public void WaitReceived(int count)
            {
                DateTime start = SysTime.Now;
                int c;

                while (true)
                {

                    lock (this.SyncRoot)
                        c = recvQueue.Count;

                    if (c >= count)
                        return;

                    if (SysTime.Now - start >= maxTime)
                        throw new TimeoutException();

                    Thread.Sleep(0);
                }
            }

            [MsgHandler(Default = true)]
            public void OnMsg(Msg msg)
            {
                lock (this.SyncRoot)
                    recvQueue.Enqueue(msg);
            }
        }

        private _RootRouter CreateRoot(string root, string cloudEP)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.RouterEP				= physical://{0}
MsgRouter.ParentEP              = 
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.CloudEP    			= {1}
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 5m
MsgRouter.AdvertiseTime			= 30s
MsgRouter.KeepAliveTime         = 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";

            _RootRouter router;

            Config.SetConfig(string.Format(settings, root, cloudEP));
            router = new _RootRouter();
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Discover_Hub_Starts_Then_Leaf()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf.RouterEP.ToString(), routes[0].RouterEP.ToString());

                routes = leaf.GetPhysicalRoutes();
                Assert.AreEqual(0, routes.Length);
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
        public void PhysicalRouting_Discover_Hub_Starts_Then_LeafP2P()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf.RouterEP.ToString(), routes[0].RouterEP.ToString());

                routes = leaf.GetPhysicalRoutes();
                Assert.AreEqual(0, routes.Length);
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
        public void PhysicalRouting_Discover_LeafP2P_Starts_Then_Hub()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                Thread.Sleep(500);
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf.RouterEP.ToString(), routes[0].RouterEP.ToString());
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
        public void PhysicalRouting_Discover_Multiple_Leaves()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, false);
                Thread.Sleep(500);
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(2, routes.Length);
                Assert.AreEqual(leaf0.RouterEP.ToString(), routes[0].RouterEP.ToString());
                Assert.AreEqual(leaf1.RouterEP.ToString(), routes[1].RouterEP.ToString());
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Discover_Multiple_LeavesP2P()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true);
                Thread.Sleep(500);
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(2, routes.Length);
                Assert.AreEqual(leaf0.RouterEP.ToString(), routes[0].RouterEP.ToString());
                Assert.AreEqual(leaf1.RouterEP.ToString(), routes[1].RouterEP.ToString());
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_LeafStop()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;
            bool fLeaf0, fLeaf1;

            try
            {
                // Verify the routes are removed when leaf routers are stopped

                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, false);
                Thread.Sleep(500);
                hub = CreateHub("detached", "hub0", group1);
                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(2, routes.Length);

                fLeaf0 = fLeaf1 = false;
                for (int i = 0; i < routes.Length; i++)
                    if (leaf0.RouterEP.ToString() == routes[i].RouterEP.ToString())
                        fLeaf0 = true;
                    else if (leaf1.RouterEP.ToString() == routes[i].RouterEP.ToString())
                        fLeaf1 = true;

                Assert.IsTrue(fLeaf0 && fLeaf1);

                leaf0.Stop();
                leaf0 = null;
                Thread.Sleep(250);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf1.RouterEP.ToString(), routes[0].RouterEP.ToString());

                leaf1.Stop();
                leaf1 = null;
                Thread.Sleep(250);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(0, routes.Length);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_RouteExpire()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                // Verify that routes expire and that leaf routers honor
                // the hub's advertise time

                hub = CreateHub("detached", "hub0", group1);
                hub.PhysRouteTTL = TimeSpan.FromMilliseconds(2000);
                hub.AdvertiseTime = TimeSpan.FromMilliseconds(10000);

                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                Assert.AreEqual(hub.AdvertiseTime, leaf.AdvertiseTime);

                Thread.Sleep(500);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf.RouterEP.ToString(), routes[0].RouterEP.ToString());

                Thread.Sleep(5000);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(0, routes.Length);

                hub.PhysRouteTTL = TimeSpan.FromMilliseconds(60000);
                Thread.Sleep(12000);

                routes = hub.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(leaf.RouterEP.ToString(), routes[0].RouterEP.ToString());
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
        public void PhysicalRouting_Leaf2Self()
        {
            _LeafRouter leaf = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                leaf = CreateLeaf("localhost:47001", "hub0", "leaf0", group1, false);

                leaf.SendTo(leaf.RouterEP, new _HelloMsg("Hello"));
                leaf.WaitReceived(1);
                msg = (_HelloMsg)leaf.DequeueReceived();
                Assert.AreEqual("Hello", msg.Value);
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
        public void PhysicalRouting_Hub2Self()
        {
            _HubRouter hub = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                hub = CreateHub("localhost:47001", "hub0", group1);

                hub.SendTo(hub.RouterEP, new _HelloMsg("Hello"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello", msg.Value);
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
        public void PhysicalRouting_Root2Self()
        {
            _RootRouter root = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                root = CreateRoot("localhost:47001", rootGroup);

                root.SendTo(root.RouterEP, new _HelloMsg("Hello"));
                root.WaitReceived(1);
                msg = (_HelloMsg)root.DequeueReceived();
                Assert.AreEqual("Hello", msg.Value);
            }
            finally
            {
                if (root != null)
                    root.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Hub()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify message routing between a leaf and a hub.

                hub = CreateHub("detached", "hub0", group1);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                Thread.Sleep(InitDelay);

                leaf.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub"));
                hub.WaitReceived(1);

                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub", msg.Value);

                hub.SendTo(leaf.RouterEP, new _HelloMsg("Hello Leaf"));
                leaf.WaitReceived(1);
                msg = (_HelloMsg)leaf.DequeueReceived();
                Assert.AreEqual("Hello Leaf", msg.Value);
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
        public void PhysicalRouting_Leaf2Leaf_ViaHub()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify message routing between a multiple leaves and a hub.

                hub = CreateHub("detached", "hub0", group1);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, false);
                Thread.Sleep(InitDelay);

                leaf0.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub (leaf0)"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub (leaf0)", msg.Value);

                leaf1.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub (leaf1)"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub (leaf1)", msg.Value);

                hub.SendTo(leaf1.RouterEP, new _HelloMsg("Hello Leaf1"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello Leaf1", msg.Value);

                hub.SendTo(leaf0.RouterEP, new _HelloMsg("Hello Leaf0"));
                leaf0.WaitReceived(1);
                msg = (_HelloMsg)leaf0.DequeueReceived();
                Assert.AreEqual("Hello Leaf0", msg.Value);

                leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello from leaf0", msg.Value);

                leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                leaf0.WaitReceived(1);
                msg = (_HelloMsg)leaf0.DequeueReceived();
                Assert.AreEqual("Hello from leaf1", msg.Value);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_ViaHub_Blast()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify message routing between multiple leaves via a hub.

                hub = CreateHub("detached", "hub0", group1);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, false);
                Thread.Sleep(InitDelay);

                for (int i = 0; i < BlastCount; i++)
                {
                    leaf0.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub (leaf0)"));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual("Hello Hub (leaf0)", msg.Value);

                    leaf1.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub (leaf1)"));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual("Hello Hub (leaf1)", msg.Value);

                    hub.SendTo(leaf1.RouterEP, new _HelloMsg("Hello Leaf1"));
                    leaf1.WaitReceived(1);
                    msg = (_HelloMsg)leaf1.DequeueReceived();
                    Assert.AreEqual("Hello Leaf1", msg.Value);

                    hub.SendTo(leaf0.RouterEP, new _HelloMsg("Hello Leaf0"));
                    leaf0.WaitReceived(1);
                    msg = (_HelloMsg)leaf0.DequeueReceived();
                    Assert.AreEqual("Hello Leaf0", msg.Value);

                    leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                    leaf1.WaitReceived(1);
                    msg = (_HelloMsg)leaf1.DequeueReceived();
                    Assert.AreEqual("Hello from leaf0", msg.Value);

                    leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                    leaf0.WaitReceived(1);
                    msg = (_HelloMsg)leaf0.DequeueReceived();
                    Assert.AreEqual("Hello from leaf1", msg.Value);
                }
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Duplicate_Leaves()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify that leaf routers are able to detect when another
                // router is using the same physical endpoint.

                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                leaf1 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                Thread.Sleep(InitDelay);

                Assert.IsTrue(leaf0.DuplicateLeafDetected);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_P2P_NoHub()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify peer-to-peer message routing between two leaves.

                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf1.PhysicalRoutes.Count);

                leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello from leaf0", msg.Value);

                leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                leaf0.WaitReceived(1);
                msg = (_HelloMsg)leaf0.DequeueReceived();
                Assert.AreEqual("Hello from leaf1", msg.Value);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_P2P_NoHub_Blast()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
            leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true);
            Thread.Sleep(InitDelay);

            Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
            Assert.AreEqual(1, leaf1.PhysicalRoutes.Count);

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    // Verify peer-to-peer message routing between two leaves.

                    leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                    leaf1.WaitReceived(1);
                    msg = (_HelloMsg)leaf1.DequeueReceived();
                    Assert.AreEqual("Hello from leaf0", msg.Value);

                    leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                    leaf0.WaitReceived(1);
                    msg = (_HelloMsg)leaf0.DequeueReceived();
                    Assert.AreEqual("Hello from leaf1", msg.Value);
                }
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_NoP2P_NoHub()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify peer-to-peer message routing between two leaves.

                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, false);
                Thread.Sleep(InitDelay);

                leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello from leaf0", msg.Value);

                Assert.Fail("Expected a TimeoutException");
            }
            catch (TimeoutException)
            {
                // We're expecting the message routing to timeout due to the
                // fact the P2P is disabled.
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        private bool sawHelloMsg = false;

        private void OnRoutePhys_Leaf2Leaf_P2P_WithHub(MsgEP physicalRoute, Msg msg)
        {
            if (msg is _HelloMsg)
                sawHelloMsg = true;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_P2P_WithHub()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify peer-to-peer message routing between two leaves are
                // actually delivered P2P and are not routed through a hub.

                hub = CreateHub("detached", "hub0", group1);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true);
                Thread.Sleep(InitDelay);

                sawHelloMsg = false;
                hub.onRoutePhysical = new RoutePhysicalDelegate(OnRoutePhys_Leaf2Leaf_P2P_WithHub);

                leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello from leaf0", msg.Value);

                leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                leaf0.WaitReceived(1);
                msg = (_HelloMsg)leaf0.DequeueReceived();
                Assert.AreEqual("Hello from leaf1", msg.Value);

                Assert.IsFalse(sawHelloMsg);

                // Verify that leaves can send messages to the hub and the
                // hub can respond back while the leaves are in P2P mode.

                leaf0.SendTo(hub.RouterEP, new _HelloMsg("Hello there hub from leaf0!"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello there hub from leaf0!", msg.Value);

                leaf1.SendTo(hub.RouterEP, new _HelloMsg("Hello there hub from leaf1!"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello there hub from leaf1!", msg.Value);

                hub.SendTo(leaf0.RouterEP, new _HelloMsg("Hello there leaf0 from hub!"));
                leaf0.WaitReceived(1);
                msg = (_HelloMsg)leaf0.DequeueReceived();
                Assert.AreEqual("Hello there leaf0 from hub!", msg.Value);

                leaf1.SendTo(leaf1.RouterEP, new _HelloMsg("Hello there leaf1 from hub!"));
                leaf1.WaitReceived(1);
                msg = (_HelloMsg)leaf1.DequeueReceived();
                Assert.AreEqual("Hello there leaf1 from hub!", msg.Value);
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Leaf_P2P_WithHub_Blast()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HelloMsg msg;

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            try
            {
                // Verify peer-to-peer message routing between two leaves are
                // actually delivered P2P and are not routed through a hub.

                hub = CreateHub("detached", "hub0", group1);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true);
                Thread.Sleep(InitDelay);

                hub.onRoutePhysical = new RoutePhysicalDelegate(OnRoutePhys_Leaf2Leaf_P2P_WithHub);

                for (int i = 0; i < BlastCount; i++)
                {

                    sawHelloMsg = false;

                    leaf0.SendTo(leaf1.RouterEP, new _HelloMsg("Hello from leaf0"));
                    leaf1.WaitReceived(1);
                    msg = (_HelloMsg)leaf1.DequeueReceived();
                    Assert.AreEqual("Hello from leaf0", msg.Value);

                    leaf1.SendTo(leaf0.RouterEP, new _HelloMsg("Hello from leaf1"));
                    leaf0.WaitReceived(1);
                    msg = (_HelloMsg)leaf0.DequeueReceived();
                    Assert.AreEqual("Hello from leaf1", msg.Value);

                    Assert.IsFalse(sawHelloMsg);

                    // Verify that leaves can send messages to the hub and the
                    // hub can respond back while the leaves are in P2P mode.

                    leaf0.SendTo(hub.RouterEP, new _HelloMsg("Hello there hub from leaf0!"));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual("Hello there hub from leaf0!", msg.Value);

                    leaf1.SendTo(hub.RouterEP, new _HelloMsg("Hello there hub from leaf1!"));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual("Hello there hub from leaf1!", msg.Value);

                    hub.SendTo(leaf0.RouterEP, new _HelloMsg("Hello there leaf0 from hub!"));
                    leaf0.WaitReceived(1);
                    msg = (_HelloMsg)leaf0.DequeueReceived();
                    Assert.AreEqual("Hello there leaf0 from hub!", msg.Value);

                    leaf1.SendTo(leaf1.RouterEP, new _HelloMsg("Hello there leaf1 from hub!"));
                    leaf1.WaitReceived(1);
                    msg = (_HelloMsg)leaf1.DequeueReceived();
                    Assert.AreEqual("Hello there leaf1 from hub!", msg.Value);
                }
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_HubUplink_Discover()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("localhost:47001", "hub0", group1);
                Thread.Sleep(InitDelay);

                routes = root.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(hub.RouterEP.ToString(), routes[0].RouterEP.ToString());
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Hub2Root()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            _HelloMsg msg;
            PhysicalRoute[] routes;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("localhost:47001", "hub0", group1);
                Thread.Sleep(InitDelay);

                routes = root.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(hub.RouterEP.ToString(), routes[0].RouterEP.ToString());

                hub.SendTo(root.RouterEP, new _HelloMsg("Hello Root"));
                root.WaitReceived(1);
                msg = (_HelloMsg)root.DequeueReceived();
                Assert.AreEqual("Hello Root", msg.Value);

                root.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub", msg.Value);
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Hub2Root_Blast()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            _HelloMsg msg;
            PhysicalRoute[] routes;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("localhost:47001", "hub0", group1);
                Thread.Sleep(InitDelay);

                routes = root.GetPhysicalRoutes();
                Assert.AreEqual(1, routes.Length);
                Assert.AreEqual(hub.RouterEP.ToString(), routes[0].RouterEP.ToString());

                for (int i = 0; i < BlastCount; i++)
                {
                    hub.SendTo(root.RouterEP, new _HelloMsg("Hello Root"));
                    root.WaitReceived(1);
                    msg = (_HelloMsg)root.DequeueReceived();
                    Assert.AreEqual("Hello Root", msg.Value);

                    root.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub"));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual("Hello Hub", msg.Value);
                }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_DetachedHub()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            PhysicalRoute[] routes;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("DETACHED", "hub0", group1);
                Thread.Sleep(InitDelay);

                routes = root.GetPhysicalRoutes();
                Assert.AreEqual(0, routes.Length);

                hub.SendTo(root.RouterEP, new _HelloMsg("Hello Root"));
                root.WaitReceived(1);

                Assert.Fail();
            }
            catch (TimeoutException)
            {
                // The root shouldn't receive the message since the hub is 
                // supposed to be detached.
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Root()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            _LeafRouter leaf = null;
            _HelloMsg msg;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("localhost:47001", "hub0", group1);
                leaf = CreateLeaf("localhost:47001", "hub0", "leaf0", group1, false);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(1, root.GetPhysicalRoutes().Length);
                Assert.AreEqual(1, hub.GetPhysicalRoutes().Length);

                leaf.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub", msg.Value);

                leaf.SendTo(root.RouterEP, new _HelloMsg("Hello Root"));
                root.WaitReceived(1);
                msg = (_HelloMsg)root.DequeueReceived();
                Assert.AreEqual("Hello Root", msg.Value);

                hub.SendTo(root.RouterEP, new _HelloMsg("Hello Root"));
                root.WaitReceived(1);
                msg = (_HelloMsg)root.DequeueReceived();
                Assert.AreEqual("Hello Root", msg.Value);

                hub.SendTo(leaf.RouterEP, new _HelloMsg("Hello Leaf"));
                leaf.WaitReceived(1);
                msg = (_HelloMsg)leaf.DequeueReceived();
                Assert.AreEqual("Hello Leaf", msg.Value);

                root.SendTo(hub.RouterEP, new _HelloMsg("Hello Hub"));
                hub.WaitReceived(1);
                msg = (_HelloMsg)hub.DequeueReceived();
                Assert.AreEqual("Hello Hub", msg.Value);

                root.SendTo(leaf.RouterEP, new _HelloMsg("Hello Leaf"));
                leaf.WaitReceived(1);
                msg = (_HelloMsg)leaf.DequeueReceived();
                Assert.AreEqual("Hello Leaf", msg.Value);
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_Leaf2Root_Blast()
        {
            _RootRouter root = null;
            _HubRouter hub = null;
            _LeafRouter leaf = null;
            _HelloMsg msg;
            string s;

            try
            {
                // Verify that the hub to root uplink is established

                root = CreateRoot("localhost:47001", rootGroup);
                hub = CreateHub("localhost:47001", "hub0", group1);
                leaf = CreateLeaf("localhost:47001", "hub0", "leaf0", group1, false);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(1, root.GetPhysicalRoutes().Length);
                Assert.AreEqual(1, hub.GetPhysicalRoutes().Length);

                for (int i = 0; i < BlastCount; i++)
                {
                    s = "Leaf -> Hub: " + i.ToString();
                    leaf.SendTo(hub.RouterEP, new _HelloMsg(s));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);

                    s = "Leaf -> Root: " + i.ToString();
                    leaf.SendTo(root.RouterEP, new _HelloMsg(s));
                    root.WaitReceived(1);
                    msg = (_HelloMsg)root.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);

                    s = "Hub -> Root: " + i.ToString();
                    hub.SendTo(root.RouterEP, new _HelloMsg(s));
                    root.WaitReceived(1);
                    msg = (_HelloMsg)root.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);

                    s = "Hub -> Leaf: " + i.ToString();
                    hub.SendTo(leaf.RouterEP, new _HelloMsg(s));
                    leaf.WaitReceived(1);
                    msg = (_HelloMsg)leaf.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);

                    s = "Root -> Hub: " + i.ToString();
                    root.SendTo(hub.RouterEP, new _HelloMsg(s));
                    hub.WaitReceived(1);
                    msg = (_HelloMsg)hub.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);

                    s = "Root -> Leaf: " + i.ToString();
                    root.SendTo(leaf.RouterEP, new _HelloMsg(s));
                    leaf.WaitReceived(1);
                    msg = (_HelloMsg)leaf.DequeueReceived();
                    Assert.AreEqual(s, msg.Value);
                }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub != null)
                    hub.Stop();

                if (leaf != null)
                    leaf.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_MultiSubnet()
        {
            _RootRouter root = null;
            _HubRouter hub0 = null;
            _HubRouter hub1 = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            _LeafRouter leaf3 = null;

            try
            {
                // Create two separate hubs, each with a different multicast group
                // and each with 2 leaf routers.  The verify that each of the routers
                // is able to send messages to each of the others.

                root = CreateRoot("localhost:47001", rootGroup);

                hub0 = CreateHub("localhost:47001", "hub0", group1);
                leaf0 = CreateLeaf("localhost:47001", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("localhost:47001", "hub0", "leaf1", group1, false);

                hub1 = CreateHub("localhost:47001", "hub1", group2);
                leaf2 = CreateLeaf("localhost:47001", "hub1", "leaf2", group2, false);
                leaf3 = CreateLeaf("localhost:47001", "hub1", "leaf3", group2, false);

                Thread.Sleep(InitDelay);

                Assert.AreEqual(2, root.GetPhysicalRoutes().Length);
                Assert.AreEqual(2, hub0.GetPhysicalRoutes().Length);
                Assert.AreEqual(2, hub1.GetPhysicalRoutes().Length);

                ITestRouter[] routers = new ITestRouter[] { root, hub0, leaf0, leaf1, hub1, leaf2, leaf3 };

                for (int i = 0; i < routers.Length; i++)
                    for (int j = 0; j < routers.Length; j++)
                    {
                        string s = i.ToString() + ":" + j.ToString();
                        ITestRouter src = routers[i];
                        ITestRouter dest = routers[j];
                        _HelloMsg msg;

                        if (src == null || dest == null)
                            continue;

                        NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Test", string.Format("src={0} dest={1}", src.RouterEP, dest.RouterEP), null);

                        src.SendTo(dest, new _HelloMsg(s));
                        dest.WaitReceived(1);
                        msg = (_HelloMsg)dest.DequeueReceived();
                        Assert.AreEqual(s, msg.Value);
                    }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void PhysicalRouting_MultiSubnet_Blast()
        {
            _RootRouter root = null;
            _HubRouter hub0 = null;
            _HubRouter hub1 = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            _LeafRouter leaf3 = null;

            try
            {
                // Create two separate hubs, each with a different multicast group
                // and each with 2 leaf routers.  The verify that each of the routers
                // is able to send messages to each of the others.

                root = CreateRoot("localhost:47001", rootGroup);

                hub0 = CreateHub("localhost:47001", "hub0", group1);
                leaf0 = CreateLeaf("localhost:47001", "hub0", "leaf0", group1, false);
                leaf1 = CreateLeaf("localhost:47001", "hub0", "leaf1", group1, false);

                hub1 = CreateHub("localhost:47001", "hub1", group2);
                leaf2 = CreateLeaf("localhost:47001", "hub1", "leaf2", group2, false);
                leaf3 = CreateLeaf("localhost:47001", "hub1", "leaf3", group2, false);

                Thread.Sleep(InitDelay);

                Assert.AreEqual(2, root.GetPhysicalRoutes().Length);
                Assert.AreEqual(2, hub0.GetPhysicalRoutes().Length);
                Assert.AreEqual(2, hub1.GetPhysicalRoutes().Length);

                ITestRouter[] routers = new ITestRouter[] { root, hub0, leaf0, leaf1, hub1, leaf2, leaf3 };

                // I'm just going to do this 10 times since 50 takes way too long

                for (int k = 0; k < 10; k++)
                    for (int i = 0; i < routers.Length; i++)
                        for (int j = 0; j < routers.Length; j++)
                        {
                            string s = i.ToString() + ":" + j.ToString();
                            ITestRouter src = routers[i];
                            ITestRouter dest = routers[j];
                            _HelloMsg msg;

                            if (src == null || dest == null)
                                continue;

                            NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Test", string.Format("src={0} dest={1}", src.RouterEP, dest.RouterEP), null);

                            src.SendTo(dest, new _HelloMsg(s));
                            dest.WaitReceived(1);
                            msg = (_HelloMsg)dest.DequeueReceived();
                            Assert.AreEqual(s, msg.Value);
                        }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

