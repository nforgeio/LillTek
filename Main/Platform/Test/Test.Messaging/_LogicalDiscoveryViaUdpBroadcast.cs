//-----------------------------------------------------------------------------
// FILE:        _LogicalDiscoveryViaBroadcast.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the logical discovery of Leaf, Hub and
//              Root routers via the UDP Broascast Server.

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
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _LogicalDiscoveryViaUdpBroadcast
    {
        private const int BlastCount = 100;
        private const int InitDelay = 2000;
        private const int PropDelay = 2000;
        private const string group1 = "231.222.0.1:45001";
        private const string group2 = "231.222.0.1:45002";

        private static TimeSpan maxTime = TimeSpan.FromSeconds(10);
        private static UdpBroadcastServer broadcastServer;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);

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
            private Queue<Msg> recvQueue;

            public _LeafRouter()
                : base()
            {
                recvQueue = new Queue<Msg>();
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

        private _LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP, bool enableP2P, int maxAdvertiseEPs, object target)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}/{1}/{2}
MsgRouter.DiscoveryMode          = UDPBROADCAST
MsgRouter.CloudEP    			 = {3}
MsgRouter.CloudAdapter    		 = ANY
MsgRouter.UdpEP					 = ANY:0
MsgRouter.TcpEP					 = ANY:0
MsgRouter.TcpBacklog			 = 100
MsgRouter.TcpDelay				 = off
MsgRouter.BkInterval			 = 1s
MsgRouter.MaxIdle				 = 5m
MsgRouter.EnableP2P              = {4}
MsgRouter.AdvertiseTime			 = 1m
MsgRouter.DefMsgTTL				 = 5
MsgRouter.SharedKey 		 	 = PLAINTEXT
MsgRouter.SessionRetries         = 3
MsgRouter.SessionTimeout         = 10s
MsgRouter.MaxLogicalAdvertiseEPs = {5}
MsgRouter.DeadRouterTTL          = 2s

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

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP, enableP2P ? "yes" : "no", maxAdvertiseEPs));

            router = new _LeafRouter();

            if (target != null)
                router.Dispatcher.AddTarget(target);

            router.Start();

            return router;
        }

        private delegate void RoutePhysicalDelegate(MsgEP physicalEP, Msg msg);

        private class _HubRouter : HubRouter, ITestRouter
        {
            public RoutePhysicalDelegate onRoutePhysical;
            private Queue<Msg> recvQueue;

            public _HubRouter()
                : base()
            {
                recvQueue = new Queue<Msg>();
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

        private _HubRouter CreateHub(string root, string name, string cloudEP, int maxAdvertiseEPs, object target, string downlinkEPs)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}/{1}
MsgRouter.ParentEP               = 
MsgRouter.DiscoveryMode          = UDPBROADCAST
MsgRouter.CloudEP    			 = {2}
MsgRouter.CloudAdapter    		 = ANY
MsgRouter.UdpEP					 = ANY:0
MsgRouter.TcpEP					 = ANY:0
MsgRouter.TcpBacklog			 = 100
MsgRouter.TcpDelay				 = off
MsgRouter.BkInterval			 = 1s
MsgRouter.MaxIdle				 = 5m
MsgRouter.AdvertiseTime			 = 30s
MsgRouter.KeepAliveTime          = 1m
MsgRouter.DefMsgTTL				 = 5
MsgRouter.SharedKey 		     = PLAINTEXT
MsgRouter.SessionCacheTime       = 2m
MsgRouter.SessionRetries         = 3
MsgRouter.SessionTimeout         = 10s
MsgRouter.MaxLogicalAdvertiseEPs = {3}
MsgRouter.DeadRouterTTL          = 2s
{4}

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

            Config.SetConfig(string.Format(settings, root, name, cloudEP, maxAdvertiseEPs, downlinkEPs == null ? "" : downlinkEPs));
            router = new _HubRouter();

            if (target != null)
                router.Dispatcher.AddTarget(target);

            router.Start();

            return router;
        }

        private class _RootRouter : RootRouter, ITestRouter
        {
            private Queue<Msg> recvQueue;

            public _RootRouter()
                : base()
            {
                recvQueue = new Queue<Msg>();
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

        private _RootRouter CreateRoot(string root, string cloudEP, int maxAdvertiseEPs, object target, string uplinkEPs)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}
MsgRouter.ParentEP               = 
MsgRouter.DiscoveryMode          = UDPBROADCAST
MsgRouter.CloudEP    			 = {1}
MsgRouter.CloudAdapter    		 = ANY
MsgRouter.UdpEP					 = ANY:0
MsgRouter.TcpEP					 = ANY:0
MsgRouter.TcpBacklog			 = 100
MsgRouter.TcpDelay				 = off
MsgRouter.BkInterval			 = 1s
MsgRouter.MaxIdle				 = 5m
MsgRouter.AdvertiseTime			 = 30s
MsgRouter.KeepAliveTime          = 1m
MsgRouter.DefMsgTTL				 = 5
MsgRouter.SharedKey 			 = PLAINTEXT
MsgRouter.SessionCacheTime       = 2m
MsgRouter.SessionRetries         = 3
MsgRouter.SessionTimeout         = 10s
MsgRouter.MaxLogicalAdvertiseEPs = {2}
MsgRouter.DeadRouterTTL          = 2s
{3}

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

            _RootRouter router;

            Config.SetConfig(string.Format(settings, root, cloudEP, maxAdvertiseEPs, uplinkEPs == null ? "" : uplinkEPs));
            router = new _RootRouter();

            if (target != null)
                router.Dispatcher.AddTarget(target);

            router.Start();

            return router;
        }

        private class Target0
        {
            [MsgHandler(LogicalEP = "logical://foo")]
            public void OnMsg0(Msg msg)
            {
            }
        }

        private class Target1
        {
            [MsgHandler(LogicalEP = "logical://foo/0")]
            public void OnMsg0(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/1")]
            public void OnMsg1(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/2")]
            public void OnMsg2(Msg msg)
            {
            }
        }

        private class Target2
        {
            [MsgHandler(LogicalEP = "logical://bar/0")]
            public void OnMsg0(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://bar/1")]
            public void OnMsg1(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://bar/2")]
            public void OnMsg2(Msg msg)
            {
            }
        }

        private class Target3
        {
            [MsgHandler(LogicalEP = "logical://foobar/0")]
            public void OnMsg0(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://foobar/1")]
            public void OnMsg1(Msg msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://foobar/2")]
            public void OnMsg2(Msg msg)
            {
            }
        }

        private Dictionary<string, MsgEP> LoadLogicalEPs(LogicalAdvertiseMsg[] msgs)
        {
            Dictionary<string, MsgEP> logicalEPs = new Dictionary<string, MsgEP>();

            foreach (LogicalAdvertiseMsg msg in msgs)
                foreach (MsgEP logicalEP in msg.LogicalEPs)
                    logicalEPs.Add(logicalEP.ToString(), logicalEP);

            return logicalEPs;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_GenLogicalAdvertiseMsgs1()
        {
            _LeafRouter router = null;
            List<LogicalRoute> routes = new List<LogicalRoute>();
            LogicalAdvertiseMsg[] msgs;
            Dictionary<string, MsgEP> eps;
            PhysicalRoute routerRoute;

            try
            {
                router = CreateLeaf("detached", "hub", "leaf", group1, true, 5, new Target1());

                routerRoute = new PhysicalRoute(router.RouterEP, "", "", MsgRouterInfo.Default, Guid.Empty, router.UdpEP, router.TcpEP, SysTime.Now + TimeSpan.FromMinutes(1));

                msgs = router.GenLogicalAdvertiseMsgs(null, false);
                Assert.AreEqual(0, msgs.Length);

                msgs = router.GenLogicalAdvertiseMsgs(new List<LogicalRoute>(), true);
                Assert.AreEqual(1, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(3, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));

                routes.Clear();
                routes.Add(new LogicalRoute("logical://bar/0", routerRoute));
                routes.Add(new LogicalRoute("logical://bar/1", routerRoute));

                msgs = router.GenLogicalAdvertiseMsgs(routes, true);
                Assert.AreEqual(1, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(5, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/0"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/1"));

                routes.Add(new LogicalRoute("logical://bar/2", routerRoute));
                routes.Add(new LogicalRoute("logical://bar/3", routerRoute));

                msgs = router.GenLogicalAdvertiseMsgs(routes, true);
                Assert.AreEqual(2, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(7, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/0"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/1"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/3"));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_GenLogicalAdvertiseMsgs2()
        {
            _HubRouter router = null;
            List<LogicalRoute> routes = new List<LogicalRoute>();
            LogicalAdvertiseMsg[] msgs;
            Dictionary<string, MsgEP> eps;
            PhysicalRoute routerRoute;

            try
            {
                router = CreateHub("detached", "hub", group1, 5, null, null);

                routerRoute = new PhysicalRoute(router.RouterEP, "", "", MsgRouterInfo.Default, Guid.Empty, router.UdpEP, router.TcpEP, SysTime.Now + TimeSpan.FromMinutes(1));

                msgs = router.GenLogicalAdvertiseMsgs(new MsgEP[0]);
                Assert.AreEqual(0, msgs.Length);

                msgs = router.GenLogicalAdvertiseMsgs(new MsgEP[] { "logical://foo/0", "logical://foo/1", "logical://foo/2" });
                Assert.AreEqual(1, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(3, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));

                msgs = router.GenLogicalAdvertiseMsgs(new MsgEP[] { "logical://foo/0", "logical://foo/1", "logical://foo/2", "logical://bar/0", "logical://bar/1" });
                Assert.AreEqual(1, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(5, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/0"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/1"));

                msgs = router.GenLogicalAdvertiseMsgs(new MsgEP[] { "logical://foo/0", "logical://foo/1", "logical://foo/2", "logical://bar/0", "logical://bar/1", "logical://bar/2", "logical://bar/3" });
                Assert.AreEqual(2, msgs.Length);
                eps = LoadLogicalEPs(msgs);
                Assert.AreEqual(7, eps.Count);
                Assert.IsTrue(eps.ContainsKey("logical://foo/0"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/1"));
                Assert.IsTrue(eps.ContainsKey("logical://foo/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/0"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/1"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/2"));
                Assert.IsTrue(eps.ContainsKey("logical://bar/3"));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_Hub_Discovers_Leaf()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, false, 256, new Target1());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", leaf.RouterEP));

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_Hub_Discovers_LeafP2P()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", leaf.RouterEP));

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_Hub_Discovers_LeafP2P_Restart()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", leaf.RouterEP));

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);

                // Stop and then restart the leaf router with new 
                // logical endpoints to verify that the hub is smart
                // enough to realize what happened and purge the
                // old logical endpoints for the router.

                leaf.Stop();
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", leaf.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", leaf.RouterEP));

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_Leaf_Discovers_Hub()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, new Target1(), null);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, false, 256, null);

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);    // Since the leaf is not P2P enabled
                // it doesn't keep track of logical routes
                Assert.AreEqual(0, hub.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_LeafP2P_Discovers_Hub()
        {
            _LeafRouter leaf = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, new Target1(), null);
                leaf = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, null);

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf.LogicalRoutes.Count);    // Even P2P leaf routers don't keep
                // track of hub logical routes
                Assert.AreEqual(0, hub.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_LeafP2P_Discovers_LeafP2P()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;

            try
            {

                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
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
        public void LogicalDiscoveryViaUdpBroadcast_LeafP2P_Discovers_LeafP2P_Restart()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                // Now restart leaf0, assign it a different set of logical endpoints
                // and the verify that the new endpoints are present in the
                // routing tables of the hub and the other leaf.

                leaf0.Stop();
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target3());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                // Restart leaf0 once more, this time disabling P2P and
                // then verify the routes.

                leaf0.Stop();
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false, 256, new Target1());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(0, leaf0.LogicalRoutes.Count);

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
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
        public void LogicalDiscoveryViaUdpBroadcast_LeafP2P_Discovers_LeafP2P_NoHub()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;

            try
            {
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
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
        public void LogicalDiscoveryViaUdpBroadcast_LeafP2P_Discovers_LeafP2P_Restart_NoHub()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;

            try
            {
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                // Now restart leaf0, assign it a different set of logical endpoints
                // and the verify that the new endpoints are present in the
                // routing tables of the hub and the other leaf.

                leaf0.Stop();
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target3());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                // Restart leaf0 once more, this time disabling P2P and
                // then verify the routes.

                leaf0.Stop();
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, false, 256, new Target1());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(0, leaf0.LogicalRoutes.Count);
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
        public void LogicalDiscoveryViaUdpBroadcast_Mixed_P2P()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, new Target2());
                leaf2 = CreateLeaf("detached", "hub0", "leaf2", group1, false, 256, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf2.LogicalRoutes.Count);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                Assert.AreEqual(9, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf2"));

                // Now restart leaf2 in P2P mode and make sure that the other routers are
                // aware of the change.

                leaf2.Stop();
                leaf2 = CreateLeaf("detached", "hub0", "leaf2", group1, true, 256, new Target3());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(6, leaf2.LogicalRoutes.Count);
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf2.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));

                Assert.AreEqual(6, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf2"));

                Assert.AreEqual(6, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf2"));

                Assert.AreEqual(9, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", "physical://detached/hub0/leaf0"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", "physical://detached/hub0/leaf1"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/0", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/1", "physical://detached/hub0/leaf2"));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foobar/2", "physical://detached/hub0/leaf2"));
            }
            finally
            {
                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_Multiple_LogicalAdvertiseMsgs()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;

            try
            {
                // Pass maxAdvertiseEPs=1 to all of the router creation methods
                // to force the routers to send one RouterAdvertiseMsg per
                // logical endpoint to verify that this functionality actually
                // works.

                hub = CreateHub("detached", "hub0", group1, 1, null, null);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 1, new Target1());
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 1, new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", leaf0.RouterEP));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", leaf0.RouterEP));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", leaf0.RouterEP));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", leaf1.RouterEP));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", leaf1.RouterEP));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", leaf1.RouterEP));

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", leaf1.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", leaf1.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", leaf1.RouterEP));
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
        public void LogicalDiscoveryViaUdpBroadcast_LogicalEndpointSetID_Update()
        {
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _HubRouter hub = null;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);
                leaf0 = CreateLeaf("detached", "hub0", "leaf0", group1, true, 256, null);
                leaf1 = CreateLeaf("detached", "hub0", "leaf1", group1, true, 256, null);

                Thread.Sleep(PropDelay);

                Assert.AreEqual(0, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(0, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(0, hub.LogicalRoutes.Count);

                leaf0.Dispatcher.AddTarget(new Target1());
                leaf1.Dispatcher.AddTarget(new Target2());

                Thread.Sleep(PropDelay);

                Assert.AreEqual(3, leaf1.LogicalRoutes.Count);
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/0", leaf0.RouterEP));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/1", leaf0.RouterEP));
                Assert.IsTrue(leaf1.LogicalRoutes.HasRoute("logical://foo/2", leaf0.RouterEP));

                Assert.AreEqual(3, leaf0.LogicalRoutes.Count);
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/0", leaf1.RouterEP));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/1", leaf1.RouterEP));
                Assert.IsTrue(leaf0.LogicalRoutes.HasRoute("logical://bar/2", leaf1.RouterEP));

                Assert.AreEqual(6, hub.LogicalRoutes.Count);
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/0", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/1", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo/2", leaf0.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/0", leaf1.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/1", leaf1.RouterEP));
                Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://bar/2", leaf1.RouterEP));
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
        public void LogicalDiscoveryViaUdpBroadcast_Twenty_Leaves()
        {
            _LeafRouter[] leaves = new _LeafRouter[20];
            _HubRouter hub = null;
            DateTime start;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);

                for (int i = 0; i < leaves.Length; i++)
                    leaves[i] = null;

                for (int i = 0; i < leaves.Length; i++)
                    leaves[i] = CreateLeaf("detached", "hub0", "leaf" + i.ToString(), group1, false, 256, new Target0());

                // Wait up to a minute for all of the connections and routes
                // to be established (this can take a bit of time since something
                // upwards of 625 network connections will need to be established between
                // all of the P2P routers).

                start = SysTime.Now;
                while (hub.LogicalRoutes.Count < leaves.Length)
                {
                    if (SysTime.Now - start >= TimeSpan.FromMinutes(1))
                        throw new TimeoutException();

                    Thread.Sleep(250);
                }

                Thread.Sleep(PropDelay);    // Wait a bit more just to be sure

                Assert.AreEqual(leaves.Length, hub.LogicalRoutes.Count);

                for (int i = 0; i < leaves.Length; i++)
                {
                    Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo", leaves[i].RouterEP), leaves[i].RouterEP);
                    Assert.AreEqual(0, leaves[i].LogicalRoutes.Count);

                    for (int j = 0; j < leaves.Length; j++)
                    {
                        if (j == i)
                            continue;

                        Assert.AreEqual(0, leaves[0].LogicalRoutes.Count);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < leaves.Length; i++)
                    if (leaves[i] != null)
                        leaves[i].Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_Twenty_Leaves_P2P()
        {
            _LeafRouter[] leaves = new _LeafRouter[20];
            _HubRouter hub = null;
            DateTime start;

            try
            {
                hub = CreateHub("detached", "hub0", group1, 256, null, null);

                for (int i = 0; i < leaves.Length; i++)
                    leaves[i] = null;

                for (int i = 0; i < leaves.Length; i++)
                    leaves[i] = CreateLeaf("detached", "hub0", "leaf" + i.ToString(), group1, true, 256, new Target0());

                // Wait a minute for all of the connections and routes
                // to be established (this can take a bit of time since something
                // upwards of 625 network connections will need to be established between
                // all of the P2P routers).

                start = SysTime.Now;
                while (leaves[leaves.Length - 1].LogicalRoutes.Count < leaves.Length - 1 || hub.LogicalRoutes.Count < leaves.Length)
                {
                    if (SysTime.Now - start >= TimeSpan.FromMinutes(1))
                        throw new TimeoutException();

                    Thread.Sleep(250);
                }

                Thread.Sleep(PropDelay);    // Wait a bit more just to be sure

                for (int i = 0; i < leaves.Length; i++)
                {
                    Assert.IsTrue(hub.LogicalRoutes.HasRoute("logical://foo", leaves[i].RouterEP), leaves[i].RouterEP);
                    Assert.AreEqual(leaves.Length - 1, leaves[i].LogicalRoutes.Count, "Leaf[" + i.ToString() + "]");

                    for (int j = 0; j < leaves.Length; j++)
                    {
                        if (j == i)
                            continue;

                        Assert.IsTrue(leaves[i].LogicalRoutes.HasRoute("logical://foo", leaves[j].RouterEP), leaves[i].RouterEP);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < leaves.Length; i++)
                    if (leaves[i] != null)
                        leaves[i].Stop();

                if (hub != null)
                    hub.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalDiscoveryViaUdpBroadcast_Root2Hub_Uplink()
        {
            const string RootEPs =
@"
MsgRouter.UplinkEP[0] = logical://root/foo
MsgRouter.UplinkEP[1] = logical://root/bar
";
            const string HubEPs =
@"
MsgRouter.DownlinkEP[0] = logical://hub/foo
MsgRouter.DownlinkEP[1] = logical://hub/bar
";

            RootRouter root = null;
            HubRouter hub = null;

            try
            {
                root = CreateRoot("localhost:45077", group2, 1, null, RootEPs);
                hub = CreateHub("localhost:45077", "hub0", group1, 1, null, HubEPs);

                Thread.Sleep(PropDelay);

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://hub/foo", hub.RouterEP));
                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://hub/bar", hub.RouterEP));

                Assert.IsTrue(hub.UplinkRoutes.HasRoute("logical://root/foo", root.RouterEP));
                Assert.IsTrue(hub.UplinkRoutes.HasRoute("logical://root/bar", root.RouterEP));
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
    }
}

