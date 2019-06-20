//-----------------------------------------------------------------------------
// FILE:        _DeadRouterDetection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit test to verify ReceiptMsg delivery and dead router detection.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _DeadRouterDetection
    {
        private const int BlastCount = 100;
        private const int InitDelay = 2000;
        private const int PropDelay = 500;
        private const int ReceiptDelay = 3000;
        private const string rootName = "localhost:45000";
        private const string group1 = "231.222.0.1:45001";
        private const string group2 = "231.222.0.1:45002";

        private sealed class DeadRouter
        {
            public MsgEP DeadRouterEP;
            public Guid LogicalEndpointSetID;
            public MsgRouter Router;

            public DeadRouter(MsgEP deadRouterEP, Guid logicalEndpointSetID, MsgRouter router)
            {

                this.DeadRouterEP = deadRouterEP;
                this.LogicalEndpointSetID = logicalEndpointSetID;
                this.Router = router;
            }
        }

        private object syncLock = new object();
        private Dictionary<MsgEP, DeadRouter> deadRouters = null;
        private Dictionary<MsgEP, Msg> recvMsgs = null;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 1);

            AsyncTracker.Enable = false;
            AsyncTracker.Start();

            deadRouters = new Dictionary<MsgEP, DeadRouter>();
            recvMsgs = new Dictionary<MsgEP, Msg>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
            AsyncTracker.Stop();

            deadRouters = null;
            recvMsgs = null;
        }

        private void AddDeadRouter(MsgEP deadRouterEP, Guid logicalEndpointSetID, MsgRouter router)
        {
            lock (syncLock)
            {
                if (deadRouters == null)
                    deadRouters = new Dictionary<MsgEP, DeadRouter>();

                deadRouters.Add(deadRouterEP, new DeadRouter(deadRouterEP, logicalEndpointSetID, router));
            }
        }

        private void AddMsg(MsgRouter router, Msg msg)
        {
            lock (syncLock)
            {
                recvMsgs.Add(router.RouterEP, msg);
            }
        }

        private class _RootRouter : RootRouter
        {
            private _DeadRouterDetection test;

            public _RootRouter(_DeadRouterDetection test)
                : base()
            {
                this.test = test;
            }

            public override void OnDeadRouterDetected(MsgEP deadRouterEP, Guid logicalEndpointSetID)
            {
                test.AddDeadRouter(deadRouterEP, logicalEndpointSetID, this);
                base.OnDeadRouterDetected(deadRouterEP, logicalEndpointSetID);
            }

            [MsgHandler]
            public void OnMsg(TestMsg msg)
            {
                test.AddMsg(this, msg);
            }

            public void OnPhysical(Msg msg)
            {
                test.AddMsg(this, msg);
            }
        }

        private class _HubRouter : HubRouter
        {
            private _DeadRouterDetection test;

            public _HubRouter(_DeadRouterDetection test)
                : base()
            {
                this.test = test;
            }

            public override void OnDeadRouterDetected(MsgEP deadRouterEP, Guid logicalEndpointSetID)
            {
                test.AddDeadRouter(deadRouterEP, logicalEndpointSetID, this);
                base.OnDeadRouterDetected(deadRouterEP, logicalEndpointSetID);
            }

            [MsgHandler]
            public void OnMsg(TestMsg msg)
            {
                test.AddMsg(this, msg);
            }

            public void OnPhysical(Msg msg)
            {
                test.AddMsg(this, msg);
            }
        }

        private class _LeafRouter : LeafRouter
        {
            private _DeadRouterDetection test;

            public _LeafRouter(_DeadRouterDetection test)
                : base()
            {
                this.test = test;
            }

            public override void OnDeadRouterDetected(MsgEP deadRouterEP, Guid logicalEndpointSetID)
            {
                test.AddDeadRouter(deadRouterEP, logicalEndpointSetID, this);
                base.OnDeadRouterDetected(deadRouterEP, logicalEndpointSetID);
            }

            [MsgHandler]
            public void OnMsg(TestMsg msg)
            {
                test.AddMsg(this, msg);
            }

            public void OnPhysical(Msg msg)
            {
                test.AddMsg(this, msg);
            }
        }

        private _LeafRouter CreateLeaf(string root, string hub, string name, string cloudGroup, bool enableP2P, int maxAdvertiseEPs)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}/{1}/{2}
MsgRouter.DiscoveryMode          = MULTICAST
MsgRouter.CloudEP	    		 = {3}
MsgRouter.CloudAdapter		     = ANY
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
MsgRouter.SessionCacheTime       = 2m
MsgRouter.SessionRetries         = 3
MsgRouter.SessionTimeout         = 10s
MsgRouter.MaxLogicalAdvertiseEPs = {5}
MsgRouter.DeadRouterTTL          = 1s
";
            _LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudGroup, enableP2P ? "yes" : "no", maxAdvertiseEPs));

            router = new _LeafRouter(this);
            router.Dispatcher.AddTarget(this);
            router.Dispatcher.AddLogical(new MsgHandlerDelegate(router.OnPhysical),
                                         string.Format("logical://root/{0}/{1}", hub, name),
                                         typeof(TestMsg), false, null, true);
            router.Start();

            return router;
        }

        private _HubRouter CreateHub(string root, string name, string cloudGroup, int maxAdvertiseEPs, string downlinkEPs)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}/{1}
MsgRouter.ParentEP               = 
MsgRouter.DiscoveryMode          = MULTICAST
MsgRouter.CloudEP			     = {2}
MsgRouter.CloudAdapter		     = ANY
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
MsgRouter.DeadRouterTTL          = 1s
{4}
";
            _HubRouter router;

            Config.SetConfig(string.Format(settings, root, name, cloudGroup, maxAdvertiseEPs, downlinkEPs == null ? "" : downlinkEPs));
            router = new _HubRouter(this);
            router.Dispatcher.AddTarget(this);
            router.Dispatcher.AddLogical(new MsgHandlerDelegate(router.OnPhysical),
                                         string.Format("logical://root/{0}", name),
                                         typeof(TestMsg), false, null, true);
            router.Start();

            return router;
        }

        private _RootRouter CreateRoot(string root, string cloudEP, int maxAdvertiseEPs, string uplinkEPs)
        {
            const string settings =
@"
MsgRouter.AppName                = Test
MsgRouter.AppDescription         = Test Description
MsgRouter.RouterEP				 = physical://{0}
MsgRouter.ParentEP               = 
MsgRouter.DiscoveryMode          = MULTICAST
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
MsgRouter.DeadRouterTTL          = 1s
{3}
";
            _RootRouter router;

            Config.SetConfig(string.Format(settings, root, cloudEP, maxAdvertiseEPs, uplinkEPs == null ? "" : uplinkEPs));
            router = new _RootRouter(this);
            router.Dispatcher.AddTarget(this);
            router.Dispatcher.AddLogical(new MsgHandlerDelegate(router.OnPhysical),
                                         "logical://root",
                                         typeof(TestMsg), false, null, true);
            router.Start();

            return router;
        }

        public class TestMsg : PropertyMsg
        {
            public TestMsg()
            {
            }
        }

        private void Clear()
        {
            deadRouters.Clear();
            recvMsgs.Clear();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DeadRouterDetection_Physical_Leaf2Leaf_P2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, true, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, true, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, true, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf0.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf0.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(1, leaf2.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Physical_Leaf2Leaf_NoP2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, false, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, false, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, false, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf0.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf0.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Logical_Leaf2Leaf_P2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, true, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, true, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, true, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf0", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf0.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(1, leaf2.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Logical_Leaf2Leaf_NoP2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, false, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, false, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, false, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf0", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf0.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Physical_Hub2Leaf_P2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, true, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, true, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, true, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(hub.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[hub.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(1, leaf2.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Physical_Hub2Leaf_NoP2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, false, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, false, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, false, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(hub.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[hub.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo(leaf1.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Logical_Hub2Leaf_P2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, true, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, true, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, true, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[hub.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);

                Assert.AreEqual(1, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(1, leaf2.LogicalRoutes.Count);

                Assert.IsNull(leaf0.PhysicalRoutes[leaf1.RouterEP]);
                Assert.IsNull(leaf2.PhysicalRoutes[leaf1.RouterEP]);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Logical_Hub2Leaf_NoP2P()
        {
            _HubRouter hub = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            Msg msg;

            try
            {
                hub = CreateHub(rootName, "hub", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub", "leaf0", group1, false, 256);
                leaf1 = CreateLeaf(rootName, "hub", "leaf1", group1, false, 256);
                leaf2 = CreateLeaf(rootName, "hub", "leaf2", group1, false, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[hub.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf1.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub.LogicalRoutes.Count);

                // Pause leaf1, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf1.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                hub.SendTo("logical://root/hub/leaf1", msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(1, deadRouters.Count);
                Assert.IsNotNull(deadRouters[leaf1.RouterEP]);

                Assert.AreEqual(2, hub.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub.LogicalRoutes.Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

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
        public void DeadRouterDetection_Physical_CrossSubnet_P2P()
        {
            _RootRouter root = null;
            _HubRouter hub0 = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            _HubRouter hub1 = null;
            _LeafRouter leaf3 = null;
            _LeafRouter leaf4 = null;
            _LeafRouter leaf5 = null;
            Msg msg;

            try
            {
                root = CreateRoot(rootName, null, 256, "MsgRouter.UplinkEP[0]=logical://*");

                hub0 = CreateHub(rootName, "hub0", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub0", "leaf0", group1, true, 256);
                leaf1 = CreateLeaf(rootName, "hub0", "leaf1", group1, true, 256);
                leaf2 = CreateLeaf(rootName, "hub0", "leaf2", group1, true, 256);

                hub1 = CreateHub(rootName, "hub1", group2, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf3 = CreateLeaf(rootName, "hub1", "leaf3", group2, true, 256);
                leaf4 = CreateLeaf(rootName, "hub1", "leaf4", group2, true, 256);
                leaf5 = CreateLeaf(rootName, "hub1", "leaf5", group2, true, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf3.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf4.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf5.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf3.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf4.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf5.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                root.SendTo(root.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[root.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf3.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf4.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf5.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf3.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf4.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf5.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf4.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf4.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf3.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf4.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf5.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf3.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf4.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf5.LogicalRoutes.Count);

                // Pause leaf4, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf4.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf4.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(2, leaf0.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf1.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf2.PhysicalRoutes.Count);
                Assert.AreEqual(2, leaf0.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf1.LogicalRoutes.Count);
                Assert.AreEqual(2, leaf2.LogicalRoutes.Count);

                Assert.AreEqual(2, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub1.LogicalRoutes.Count);

                Assert.AreEqual(1, leaf3.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf3.LogicalRoutes.Count);
                Assert.AreEqual(1, leaf5.PhysicalRoutes.Count);
                Assert.AreEqual(1, leaf5.LogicalRoutes.Count);

                Assert.IsNull(leaf3.PhysicalRoutes[leaf4.RouterEP]);
                Assert.IsNull(leaf5.PhysicalRoutes[leaf4.RouterEP]);
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                if (leaf5 != null)
                    leaf5.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DeadRouterDetection_Physical_CrossSubnet_NoP2P()
        {
            _RootRouter root = null;
            _HubRouter hub0 = null;
            _LeafRouter leaf0 = null;
            _LeafRouter leaf1 = null;
            _LeafRouter leaf2 = null;
            _HubRouter hub1 = null;
            _LeafRouter leaf3 = null;
            _LeafRouter leaf4 = null;
            _LeafRouter leaf5 = null;
            Msg msg;

            try
            {
                root = CreateRoot(rootName, null, 256, "MsgRouter.UplinkEP[0]=logical://*");

                hub0 = CreateHub(rootName, "hub0", group1, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf0 = CreateLeaf(rootName, "hub0", "leaf0", group1, false, 256);
                leaf1 = CreateLeaf(rootName, "hub0", "leaf1", group1, false, 256);
                leaf2 = CreateLeaf(rootName, "hub0", "leaf2", group1, false, 256);

                hub1 = CreateHub(rootName, "hub1", group2, 256, "MsgRouter.DownlinkEP[0]=logical://*");
                leaf3 = CreateLeaf(rootName, "hub1", "leaf3", group2, false, 256);
                leaf4 = CreateLeaf(rootName, "hub1", "leaf4", group2, false, 256);
                leaf5 = CreateLeaf(rootName, "hub1", "leaf5", group2, false, 256);
                Thread.Sleep(InitDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                root.SendTo(root.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[root.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                Clear();

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf4.RouterEP, msg);
                Thread.Sleep(PropDelay);

                Assert.AreEqual(1, recvMsgs.Count);
                Assert.IsNotNull(recvMsgs[leaf4.RouterEP]);
                Assert.AreEqual(0, deadRouters.Count);

                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(3, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub1.LogicalRoutes.Count);

                // Pause leaf4, send it a message from leaf0 with receipt requested, and
                // then verify that leaf1 was detected as a dead router and that
                // leaf2 was notified that leaf1 was dead as well.

                Clear();

                leaf4.Paused = true;

                msg = new TestMsg();
                msg._Flags |= MsgFlag.ReceiptRequest;
                leaf0.SendTo(leaf4.RouterEP, msg);
                Thread.Sleep(ReceiptDelay);

                Assert.AreEqual(0, recvMsgs.Count);
                Assert.AreEqual(3, hub0.PhysicalRoutes.Count);
                Assert.AreEqual(3, hub0.LogicalRoutes.Count);

                Assert.AreEqual(2, hub1.PhysicalRoutes.Count);
                Assert.AreEqual(2, hub1.LogicalRoutes.Count);

                Assert.IsNull(leaf3.PhysicalRoutes[leaf4.RouterEP]);
                Assert.IsNull(leaf5.PhysicalRoutes[leaf4.RouterEP]);
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                if (leaf5 != null)
                    leaf5.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

