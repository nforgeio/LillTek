//-----------------------------------------------------------------------------
// FILE:        _LogicalRoutingViaUdpBroadcast.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the logical routing between Leaf, Hub and
//              Root routers while using UDPBROADCAST discovery mode.

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

// $todo(jeff.lill): 
//
// Implement a test to verify that MsgDispatcher.RemoveTarget()
// actually works and broadcasts a new logical endpoint set ID
// for the associated router.

// $todo(jeff.lill): 
//
// Add a test to verify that messages routed to logical://null/*
// are never actually dispatched.

// $todo(jeff.lill): 
//
// Figure out how to implement closest routing tests.  This may
// require the configuration of multiple virtual IP addresses.

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _LogicalRoutingViaUdpBroadcast
    {
        public const int BlastCount = 100;
        public const int InitDelay = 2000;
        public const int PropDelay = 2000;
        public const int SendDelay = 250;
        public const string rootHost = "localhost:45000";
        public const string rootGroup = "231.222.0.1:45001";
        public const string group1 = "231.222.0.1:45002";
        public const string group2 = "231.222.0.1:45003";

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

        private class CaptureInfo : IComparable
        {
            public MsgEP RouterEP;
            public MsgEP LogicalEP;
            public Msg Msg;

            public CaptureInfo(MsgEP routerEP, MsgEP logicalEP, Msg msg)
            {
                this.RouterEP = routerEP;
                this.LogicalEP = logicalEP;
                this.Msg = msg;
            }

            public int CompareTo(object o)
            {
                CaptureInfo info = (CaptureInfo)o;

                return String.Compare(this.RouterEP.ToString(), info.RouterEP.ToString(), true);
            }
        }

        private class MsgCapture
        {
            private object syncLock = new object();
            private List<CaptureInfo> capture;

            public MsgCapture()
            {
                capture = new List<CaptureInfo>();
            }

            public int Count
            {
                get
                {
                    lock (syncLock)
                        return capture.Count;
                }
            }

            public void Clear()
            {
                lock (syncLock)
                    capture.Clear();
            }

            public void Add(MsgEP routerEP, MsgEP logicalEP, Msg msg)
            {
                lock (syncLock)
                    capture.Add(new CaptureInfo(routerEP, logicalEP, msg));
            }

            public List<CaptureInfo> Find(MsgEP routerEP)
            {
                List<CaptureInfo> list = new List<CaptureInfo>();

                lock (syncLock)
                {
                    for (int i = 0; i < capture.Count; i++)
                        if (routerEP.Equals(capture[i].RouterEP))
                            list.Add(capture[i]);
                }

                return list;
            }

            public List<CaptureInfo> Find(MsgEP routerEP, MsgEP logicalEP)
            {
                List<CaptureInfo> list = new List<CaptureInfo>();

                lock (syncLock)
                {
                    for (int i = 0; i < capture.Count; i++)
                        if ((routerEP == null || routerEP.Equals(capture[i].RouterEP)) && logicalEP.LogicalMatch(capture[i].LogicalEP))
                            list.Add(capture[i]);
                }

                return list;
            }

            public List<CaptureInfo> Find(MsgEP routerEP, MsgEP logicalEP, System.Type type)
            {
                List<CaptureInfo> list = new List<CaptureInfo>();

                lock (syncLock)
                {
                    for (int i = 0; i < capture.Count; i++)
                        if ((routerEP == null || routerEP.Equals(capture[i].RouterEP)) && logicalEP.LogicalMatch(capture[i].LogicalEP) && type == capture[i].Msg.GetType())
                            list.Add(capture[i]);
                }

                return list;
            }

            public bool Exists(MsgEP routerEP)
            {
                return Find(routerEP).Count > 0;
            }

            public bool Exists(MsgEP routerEP, MsgEP logicalEP)
            {
                return Find(routerEP, logicalEP).Count > 0;
            }

            public bool Exists(MsgEP routerEP, MsgEP logicalEP, System.Type type)
            {
                return Find(routerEP, logicalEP, type).Count > 0;
            }

            public void Dump()
            {
                capture.Sort();
                Debug.WriteLine("============================");

                if (capture.Count == 0)
                    Debug.WriteLine("Nothing captured");
                else
                    foreach (CaptureInfo info in capture)
                        Debug.WriteLine(string.Format("{0}: {1}", info.RouterEP.ToString(), info.LogicalEP.ToString()));

                Debug.WriteLine("============================");
            }
        }

        private LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP, bool enableP2P, int maxAdvertiseEPs, Target[] targets, string configText)
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
MsgRouter.SessionCacheTime       = 2m
MsgRouter.SessionRetries         = 3
MsgRouter.SessionTimeout         = 10s
MsgRouter.MaxLogicalAdvertiseEPs = {5}
MsgRouter.DeadRouterTTL          = 2s
{6}

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

            LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP, enableP2P ? "yes" : "no", maxAdvertiseEPs, configText == null ? "" : configText));

            router = new LeafRouter();

            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    target.Router = router;
                    router.Dispatcher.AddTarget(target);
                }
            }

            router.Start();

            return router;
        }

        private HubRouter CreateHub(string root, string name, string cloudEP, int maxAdvertiseEPs, Target[] targets, string configText)
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

            HubRouter router;

            Config.SetConfig(string.Format(settings, root, name, cloudEP, maxAdvertiseEPs, configText == null ? "" : configText));
            router = new HubRouter();

            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    target.Router = router;
                    router.Dispatcher.AddTarget(target);
                }
            }

            router.Start();

            return router;
        }

        private RootRouter CreateRoot(string root, string cloudEP, int maxAdvertiseEPs, Target[] targets, string configText)
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

            RootRouter router;

            Config.SetConfig(string.Format(settings, root, cloudEP, maxAdvertiseEPs, configText == null ? "" : configText));
            router = new RootRouter();

            if (targets != null)
            {
                foreach (Target target in targets)
                {
                    target.Router = router;
                    router.Dispatcher.AddTarget(target);
                }
            }

            router.Start();

            return router;
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

        private class _Msg1 : PropertyMsg
        {
            public _Msg1()
            {
            }

            public _Msg1(string value)
            {
                this.Value = value;
            }

            public string Value
            {
                get { return this["value"]; }
                set { this["value"] = value; }
            }
        }

        private class _Msg2 : PropertyMsg
        {
            public _Msg2()
            {
            }

            public _Msg2(string value)
            {
                this.Value = value;
            }

            public string Value
            {
                get { return this["value"]; }
                set { this["value"] = value; }
            }
        }

        private class _Msg3 : PropertyMsg
        {
            public _Msg3()
            {
            }

            public _Msg3(string value)
            {
                this.Value = value;
            }

            public string Value
            {
                get { return this["value"]; }
                set { this["value"] = value; }
            }
        }

        private class Target
        {
            public MsgRouter Router;
            public MsgCapture Capture;

            public Target(MsgCapture capture)
            {

                this.Capture = capture;
            }
        }

        private class AbstractTarget : Target
        {
            public AbstractTarget(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "abstract://name")]
            public void OnMsg0(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, msg._ToEP, msg);
            }
        }

        private class Target1 : Target
        {
            public Target1(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/1")]
            public void OnMsg0(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/1", msg);
            }

            [MsgHandler(LogicalEP = "logical://foo/2")]
            public void OnMsg1(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/2", msg);
            }

            [MsgHandler(LogicalEP = "logical://foo/3")]
            public void OnMsg2(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/3", msg);
            }

            [MsgHandler(LogicalEP = "logical://bar/1")]
            public void OnMsg3(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/1", msg);
            }

            [MsgHandler(LogicalEP = "logical://bar/2")]
            public void OnMsg4(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/2", msg);
            }

            [MsgHandler(LogicalEP = "logical://bar/3")]
            public void OnMsg5(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/3", msg);
            }
        }

        private class Target2 : Target
        {
            public Target2(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo")]
            public void OnMsg(_Msg1 msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/1", msg);
            }

            [MsgHandler(LogicalEP = "logical://foo")]
            public void OnMsg(_Msg2 msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/2", msg);
            }

            [MsgHandler(LogicalEP = "logical://foo", Default = true)]
            public void OnMsg(Msg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/3", msg);
            }
        }

        private class TargetFoo0 : Target
        {
            public TargetFoo0(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/0")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/0", msg);
            }
        }

        private class TargetFoo1 : Target
        {
            public TargetFoo1(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/1")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/1", msg);
            }
        }

        private class TargetFoo2 : Target
        {
            public TargetFoo2(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/2")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/2", msg);
            }
        }

        private class TargetFoo3 : Target
        {
            public TargetFoo3(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://foo/3")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://foo/3", msg);
            }
        }


        private class TargetBar1 : Target
        {
            public TargetBar1(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://bar/1")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/1", msg);
            }
        }

        private class TargetBar2 : Target
        {
            public TargetBar2(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://bar/2")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/2", msg);
            }
        }

        private class TargetBar3 : Target
        {
            public TargetBar3(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://bar/3")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://bar/3", msg);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Leaf2Self()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter router = null;

            try
            {
                router = CreateLeaf(rootHost, "hub", "leaf", group1, true, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Leaf2Self_Dispatch()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter router = null;

            try
            {
                router = CreateLeaf(rootHost, "hub", "leaf", group1, true, 256, new Target[] { new Target2(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Leaf2Self_Wildcard()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter router = null;

            try
            {
                router = CreateLeaf(rootHost, "hub", "leaf", group1, true, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://foo/*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                    capture.Exists(router.RouterEP, "logical://foo/2") &&
                    capture.Exists(router.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the three handlers.");
                }

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                    capture.Exists(router.RouterEP, "logical://foo/2") &&
                    capture.Exists(router.RouterEP, "logical://foo/3") &&
                    capture.Exists(router.RouterEP, "logical://bar/1") &&
                    capture.Exists(router.RouterEP, "logical://bar/2") &&
                    capture.Exists(router.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
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
        public void LogicalRoutingViaUdpBroadcast_Leaf2Self_Broadcast()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter router = null;

            try
            {
                router = CreateLeaf(rootHost, "hub", "leaf", group1, true, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                router.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));

                capture.Clear();

                router.BroadcastTo("logical://*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(6, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://bar/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/3"));
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
        public void LogicalRoutingViaUdpBroadcast_Hub2Self()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter router = null;

            try
            {
                router = CreateHub(rootHost, "hub", group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Hub2Self_Dispatch()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter router = null;

            try
            {
                router = CreateHub(rootHost, "hub", group1, 256, new Target[] { new Target2(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Hub2Self_Wildcard()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter router = null;

            try
            {
                router = CreateHub(rootHost, "hub", group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://foo/*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                        capture.Exists(router.RouterEP, "logical://foo/2") &&
                        capture.Exists(router.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the three handlers.");
                }

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                        capture.Exists(router.RouterEP, "logical://foo/2") &&
                        capture.Exists(router.RouterEP, "logical://foo/3") &&
                        capture.Exists(router.RouterEP, "logical://bar/1") &&
                        capture.Exists(router.RouterEP, "logical://bar/2") &&
                        capture.Exists(router.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
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
        public void LogicalRoutingViaUdpBroadcast_Hub2Self_Broadcast()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter router = null;

            try
            {
                router = CreateHub(rootHost, "hub", group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                router.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));

                capture.Clear();

                router.BroadcastTo("logical://*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(6, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://bar/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/3"));
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
        public void LogicalRoutingViaUdpBroadcast_Root2Self()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter router = null;

            try
            {
                router = CreateRoot(rootHost, group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Root2Self_Dispatch()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter router = null;

            try
            {
                router = CreateRoot(rootHost, group1, 256, new Target[] { new Target2(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.SendTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg2("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/2").Count);

                capture.Clear();
                router.BroadcastTo("logical://foo", new _Msg3("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/3").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Root2Self_Wildcard()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter router = null;

            try
            {
                router = CreateRoot(rootHost, group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://foo/*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                    capture.Exists(router.RouterEP, "logical://foo/2") &&
                    capture.Exists(router.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the three handlers.");
                }

                capture.Clear();
                for (int i = 0; ; i++)
                {
                    router.SendTo("logical://*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(router.RouterEP, "logical://*").Count);

                    if (capture.Exists(router.RouterEP, "logical://foo/1") &&
                    capture.Exists(router.RouterEP, "logical://foo/2") &&
                    capture.Exists(router.RouterEP, "logical://foo/3") &&
                    capture.Exists(router.RouterEP, "logical://bar/1") &&
                    capture.Exists(router.RouterEP, "logical://bar/2") &&
                    capture.Exists(router.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
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
        public void LogicalRoutingViaUdpBroadcast_Root2Self_Broadcast()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter router = null;

            try
            {
                router = CreateRoot(rootHost, group1, 256, new Target[] { new Target1(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                router.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));

                capture.Clear();

                router.BroadcastTo("logical://*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(6, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://foo/*").Count);
                Assert.AreEqual(3, capture.Find(router.RouterEP, "logical://bar/*").Count);

                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://foo/3"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/1"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/2"));
                Assert.IsTrue(capture.Exists(router.RouterEP, "logical://bar/3"));
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
        public void LogicalRoutingViaUdpBroadcast_Leaf2Leaf_P2P()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter leaf0 = null;
            LeafRouter leaf1 = null;

            try
            {
                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, true, 256, new Target[] { new TargetFoo1(capture), new TargetFoo2(capture), new TargetFoo3(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetBar1(capture), new TargetBar2(capture), new TargetBar3(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf0.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://bar/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf0.SendTo("logical://bar/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf1.RouterEP, "logical://bar/1") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/2") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
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
        public void LogicalRoutingViaUdpBroadcast_Leaf2Leaf_AbstractEP()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter leaf0 = null;
            LeafRouter leaf1 = null;

            try
            {
                Config.SetConfig(null);
                MsgEP.LoadAbstractMap();

                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, true, 256, new Target[] { new AbstractTarget(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, null, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.SendTo("abstract://name", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "abstract://name").Count);

                leaf0.Stop();
                leaf0 = null;

                leaf1.Stop();
                leaf1 = null;

                Thread.Sleep(PropDelay);

                Config.SetConfig("MsgRouter.AbstractMap[abstract://name] = logical://foobar");
                MsgEP.LoadAbstractMap();

                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, true, 256, new Target[] { new AbstractTarget(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, null, null);
                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.SendTo("abstract://name", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foobar").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Subnet_P2P_WithHub()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf0 = null;
            LeafRouter leaf1 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, null, null);
                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, true, 256, new Target[] { new TargetFoo1(capture), new TargetFoo2(capture), new TargetFoo3(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetBar1(capture), new TargetBar2(capture), new TargetBar3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf1.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf0.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://bar/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf0.SendTo("logical://bar/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf1.RouterEP, "logical://bar/1") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/2") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_NoP2P_Send()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf0 = null;
            LeafRouter leaf1 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, null, null);
                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, false, 256, new Target[] { new TargetFoo1(capture), new TargetFoo2(capture), new TargetFoo3(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, false, 256, new Target[] { new TargetBar1(capture), new TargetBar2(capture), new TargetBar3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf1.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf0.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://bar/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf0.SendTo("logical://bar/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf1.RouterEP, "logical://bar/1") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/2") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_Mixed_Send()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf0 = null;
            LeafRouter leaf1 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, null, null);
                leaf0 = CreateLeaf(rootHost, "hub", "leaf0", group1, false, 256, new Target[] { new TargetFoo1(capture), new TargetFoo2(capture), new TargetFoo3(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetBar1(capture), new TargetBar2(capture), new TargetBar3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf1.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf0.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf1.SendTo("logical://foo/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf0.RouterEP, "logical://foo/1") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/2") &&
                        capture.Exists(leaf0.RouterEP, "logical://foo/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }

                capture.Clear();

                leaf0.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://bar/1").Count);

                capture.Clear();

                for (int i = 0; ; i++)
                {
                    leaf0.SendTo("logical://bar/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay);
                    Assert.AreEqual(i + 1, capture.Find(null, "logical://*").Count);

                    if (capture.Exists(leaf1.RouterEP, "logical://bar/1") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/2") &&
                        capture.Exists(leaf1.RouterEP, "logical://bar/3"))

                        break;

                    if (i == 100)
                        Assert.Fail("Expected the message to be distributed evenly across the all handlers.");
                }
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf0 != null)
                    leaf0.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_P2P_Broadcast_From_Hub()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, true, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_NoP2P_Broadcast_From_Hub()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, false, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, false, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_Mixed_Broadcast_From_Hub()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, false, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                hub.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_P2P_Broadcast_From_Leaf()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, true, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                leaf2.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                leaf3.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                leaf4.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_NoP2P_Broadcast_From_Leaf()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, false, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, false, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                leaf2.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                leaf3.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                leaf4.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_Mixed_Broadcast_From_P2PLeaf()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, false, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                leaf3.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                leaf3.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                leaf1.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Subnet_Mixed_Broadcast_From_Leaf()
        {
            MsgCapture capture = new MsgCapture();
            HubRouter hub = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            LeafRouter leaf3 = null;
            LeafRouter leaf4 = null;

            try
            {
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetFoo0(capture) }, null);
                leaf1 = CreateLeaf(rootHost, "hub", "leaf1", group1, true, 256, new Target[] { new TargetFoo1(capture) }, null);
                leaf2 = CreateLeaf(rootHost, "hub", "leaf2", group1, false, 256, new Target[] { new TargetFoo2(capture) }, null);
                leaf3 = CreateLeaf(rootHost, "hub", "leaf3", group1, true, 256, new Target[] { new TargetFoo3(capture) }, null);
                leaf4 = CreateLeaf(rootHost, "hub", "leaf4", group1, false, 256, new Target[] { new TargetFoo3(capture) }, null);

                Thread.Sleep(PropDelay);

                capture.Clear();

                leaf2.BroadcastTo("logical://foo/0", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);

                capture.Clear();

                leaf4.BroadcastTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                leaf2.BroadcastTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);

                capture.Clear();

                leaf4.BroadcastTo("logical://foo/3", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(2, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);

                capture.Clear();

                leaf2.BroadcastTo("logical://foo/*", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(5, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://foo/0").Count);
                Assert.AreEqual(1, capture.Find(leaf1.RouterEP, "logical://foo/1").Count);
                Assert.AreEqual(1, capture.Find(leaf2.RouterEP, "logical://foo/2").Count);
                Assert.AreEqual(1, capture.Find(leaf3.RouterEP, "logical://foo/3").Count);
                Assert.AreEqual(1, capture.Find(leaf4.RouterEP, "logical://foo/3").Count);
            }
            finally
            {
                if (hub != null)
                    hub.Stop();

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();

                if (leaf3 != null)
                    leaf3.Stop();

                if (leaf4 != null)
                    leaf4.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Hub2Root()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub = null;

            try
            {
                root = CreateRoot(rootHost, group2, 256, new Target[] { new TargetFoo1(capture) }, "MsgRouter.UplinkEP[0]=logical://*");
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetBar1(capture) }, "MsgRouter.DownlinkEP[0]=logical://*");

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(root.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                root.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(hub.RouterEP, "logical://bar/1").Count);
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
        public void LogicalRoutingViaUdpBroadcast_Hub2Root_Blocked()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub = null;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetFoo1(capture), new TargetFoo2(capture) }, "MsgRouter.UplinkEP[0]=logical://foo/1");
                hub = CreateHub(rootHost, "hub", group1, 256, new Target[] { new TargetBar1(capture), new TargetBar2(capture) }, "MsgRouter.DownlinkEP[0]=logical://bar/1");

                Thread.Sleep(PropDelay);

                capture.Clear();

                hub.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(root.RouterEP, "logical://foo/1").Count);

                capture.Clear();

                hub.SendTo("logical://foo/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(0, capture.Find(null, "logical://*").Count);

                capture.Clear();

                root.SendTo("logical://bar/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(null, "logical://*").Count);

                capture.Clear();

                root.SendTo("logical://bar/2", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(0, capture.Find(null, "logical://*").Count);
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

        private class TargetRoot : Target
        {
            public TargetRoot(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://root")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root", msg);
            }
        }

        private class TargetHub0 : Target
        {
            public TargetHub0(MsgCapture capture)
                : base(capture)
            {

            }

            [MsgHandler(LogicalEP = "logical://root/hub0")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub0", msg);
            }
        }

        private class TargetLeaf0_1 : Target
        {
            public TargetLeaf0_1(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://root/hub0/leaf1")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub0/leaf1", msg);
            }
        }

        private class TargetLeaf0_2 : Target
        {
            public TargetLeaf0_2(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://root/hub0/leaf2")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub0/leaf2", msg);
            }
        }

        private class TargetHub1 : Target
        {
            public TargetHub1(MsgCapture capture)
                : base(capture)
            {

            }

            [MsgHandler(LogicalEP = "logical://root/hub1")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub1", msg);
            }
        }

        private class TargetLeaf1_1 : Target
        {
            public TargetLeaf1_1(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://root/hub1/leaf1")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub1/leaf1", msg);
            }
        }

        private class TargetLeaf1_2 : Target
        {
            public TargetLeaf1_2(MsgCapture capture)
                : base(capture)
            {
            }

            [MsgHandler(LogicalEP = "logical://root/hub1/leaf2")]
            public void OnMsg(_HelloMsg msg)
            {
                Capture.Add(Router.RouterEP, "logical://root/hub1/leaf2", msg);
            }
        }

        private class TestInfo
        {
            public MsgRouter Router;
            public MsgEP LogicalEP;

            public TestInfo(MsgRouter router, string logicalEP)
            {
                this.Router = router;
                this.LogicalEP = logicalEP;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_NoP2P()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, false, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, false, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, false, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, new _HelloMsg("Hello"));
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_P2P()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, true, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, true, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, true, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, new _HelloMsg("Hello"));
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }

            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_Mixed()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, false, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, true, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, new _HelloMsg("Hello"));
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }

            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_Broadcast_NoP2P()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, false, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, false, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, false, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Broadcast a message from each router in the tree to all of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                {
                    capture.Clear();

                    testMatrix[i].Router.BroadcastTo("logical://root/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay * 4);

                    message = string.Format("[{0}] source=[{1}]", i, testMatrix[i].LogicalEP.ToString());
                    Assert.AreEqual(7, capture.Find(null, "logical://*").Count, message);

                    for (j = 0; j < testMatrix.Count; j++)
                    {

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }
                }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_Broadcast_P2P()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, true, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, true, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, true, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Broadcast a message from each router in the tree to all of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                {
                    capture.Clear();

                    testMatrix[i].Router.BroadcastTo("logical://root/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay * 4);

                    message = string.Format("[{0}] source=[{1}]", i, testMatrix[i].LogicalEP.ToString());
                    Assert.AreEqual(7, capture.Find(null, "logical://*").Count, message);

                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }
                }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_Broadcast_Mixed()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, true, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, false, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Broadcast a message from each router in the tree to all of the other
                // routers and verify that the messages were delivered.

                for (i = 0; i < testMatrix.Count; i++)
                {
                    capture.Clear();

                    testMatrix[i].Router.BroadcastTo("logical://root/*", new _HelloMsg("Hello"));
                    Thread.Sleep(SendDelay * 4);

                    message = string.Format("[{0}] source=[{1}]", i, testMatrix[i].LogicalEP.ToString());
                    Assert.AreEqual(7, capture.Find(null, "logical://*").Count, message);

                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }
                }
            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_NoP2P_Closest()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, false, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, false, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, false, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.  For this
                // test, we're setting the message's MsgFlag.ClosestRoute flay bit
                // to ensure that it doesn't cause problems.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        Msg msg;

                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        msg = new _HelloMsg("Hello");
                        msg._Flags |= MsgFlag.ClosestRoute;

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, msg);
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }

            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_P2P_Closest()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, true, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, true, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, true, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.  For this
                // test, we're setting the message's MsgFlag.ClosestRoute flay bit
                // to ensure that it doesn't cause problems.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        Msg msg;

                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        msg = new _HelloMsg("Hello");
                        msg._Flags |= MsgFlag.ClosestRoute;

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, msg);
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }

            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_MultiSubnet_Mixed_Closest()
        {
            MsgCapture capture = new MsgCapture();
            RootRouter root = null;
            HubRouter hub0 = null;
            LeafRouter leaf0_1 = null;
            LeafRouter leaf0_2 = null;
            HubRouter hub1 = null;
            LeafRouter leaf1_1 = null;
            LeafRouter leaf1_2 = null;

            List<TestInfo> testMatrix;
            int i, j;
            string message;

            try
            {
                root = CreateRoot(rootHost, null, 256, new Target[] { new TargetRoot(capture) }, "MsgRouter.UplinkEP[0]=logical://root/*");

                hub0 = CreateHub(rootHost, "hub0", group1, 256, new Target[] { new TargetHub0(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub0/*");
                leaf0_1 = CreateLeaf(rootHost, "hub0", "leaf1", group1, true, 256, new Target[] { new TargetLeaf0_1(capture) }, null);
                leaf0_2 = CreateLeaf(rootHost, "hub0", "leaf2", group1, false, 256, new Target[] { new TargetLeaf0_2(capture) }, null);

                hub1 = CreateHub(rootHost, "hub1", group2, 256, new Target[] { new TargetHub1(capture) }, "MsgRouter.DownlinkEP[0]=logical://root/hub1/*");
                leaf1_1 = CreateLeaf(rootHost, "hub1", "leaf1", group2, false, 256, new Target[] { new TargetLeaf1_1(capture) }, null);
                leaf1_2 = CreateLeaf(rootHost, "hub1", "leaf2", group2, true, 256, new Target[] { new TargetLeaf1_2(capture) }, null);

                Thread.Sleep(PropDelay);

                // Verify that the routers have discovered each other's routes.

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub0/*", hub0.RouterEP));
                Assert.IsTrue(hub0.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf1", leaf0_1.RouterEP));
                Assert.IsTrue(hub0.LogicalRoutes.HasRoute("logical://root/hub0/leaf2", leaf0_2.RouterEP));

                Assert.IsTrue(root.LogicalRoutes.HasRoute("logical://root/hub1/*", hub1.RouterEP));
                Assert.IsTrue(hub1.UplinkRoutes.HasRoute("logical://root/*", root.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf1", leaf1_1.RouterEP));
                Assert.IsTrue(hub1.LogicalRoutes.HasRoute("logical://root/hub1/leaf2", leaf1_2.RouterEP));

                // Initialize the test matrix

                testMatrix = new List<TestInfo>();
                testMatrix.Add(new TestInfo(root, "logical://root"));
                testMatrix.Add(new TestInfo(hub0, "logical://root/hub0"));
                testMatrix.Add(new TestInfo(leaf0_1, "logical://root/hub0/leaf1"));
                testMatrix.Add(new TestInfo(leaf0_2, "logical://root/hub0/leaf2"));
                testMatrix.Add(new TestInfo(hub1, "logical://root/hub1"));
                testMatrix.Add(new TestInfo(leaf1_1, "logical://root/hub1/leaf1"));
                testMatrix.Add(new TestInfo(leaf1_2, "logical://root/hub1/leaf2"));

                // Send a message from each router in the tree to each of the other
                // routers and verify that the messages were delivered.  For this
                // test, we're setting the message's MsgFlag.ClosestRoute flay bit
                // to ensure that it doesn't cause problems.

                for (i = 0; i < testMatrix.Count; i++)
                    for (j = 0; j < testMatrix.Count; j++)
                    {
                        Msg msg;

                        capture.Clear();

                        message = string.Format("[{0},{1}] source=[{2}] target=[{3}]",
                                                i, j,
                                                testMatrix[i].LogicalEP.ToString(),
                                                testMatrix[j].LogicalEP.ToString());

                        msg = new _HelloMsg("Hello");
                        msg._Flags |= MsgFlag.ClosestRoute;

                        testMatrix[i].Router.SendTo(testMatrix[j].LogicalEP, msg);
                        Thread.Sleep(SendDelay);
                        Assert.AreEqual(1, capture.Find(null, "logical://*").Count, message);
                        Assert.AreEqual(1, capture.Find(testMatrix[j].Router.RouterEP, testMatrix[j].LogicalEP).Count, message);
                    }

            }
            finally
            {
                if (root != null)
                    root.Stop();

                if (hub0 != null)
                    hub0.Stop();

                if (leaf0_1 != null)
                    leaf0_1.Stop();

                if (leaf0_2 != null)
                    leaf0_2.Stop();

                if (hub1 != null)
                    hub1.Stop();

                if (leaf1_1 != null)
                    leaf1_1.Stop();

                if (leaf1_2 != null)
                    leaf1_2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_Default_RouterEP()
        {
            MsgCapture capture = new MsgCapture();
            LeafRouter router0 = null;
            LeafRouter router1 = null;
            Target target;

            try
            {
                router0 = new LeafRouter();
                router0.Start();

                router1 = new LeafRouter();
                router1.Start();

                target = new Target1(capture);
                target.Router = router1;
                router1.Dispatcher.AddTarget(target);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router0.SendTo("logical://foo/1", new _HelloMsg("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router1.RouterEP, "logical://foo/1").Count);
            }
            finally
            {
                if (router0 != null)
                    router0.Stop();

                if (router1 != null)
                    router1.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRoutingViaUdpBroadcast_BroadcastEP()
        {
            // Verify that logical endpoints with "broadcast" query strings work.

            MsgCapture capture = new MsgCapture();
            LeafRouter router = null;

            try
            {
                router = CreateLeaf(rootHost, "hub", "leaf", group1, true, 256, new Target[] { new Target2(capture) }, null);
                Thread.Sleep(PropDelay);

                capture.Clear();
                router.SendTo("logical://foo?broadcast", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                capture.Clear();
                router.SendTo("logical://foo?broadcast", "logical://test?broadcast", new _Msg1("Hello"));
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);

                Msg msg = new _Msg1("Hello");

                msg._ToEP = "logical://foo?broadcast";
                msg._FromEP = "logical://test?broadcast";

                capture.Clear();
                router.Send(msg);
                Thread.Sleep(SendDelay);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://*").Count);
                Assert.AreEqual(1, capture.Find(router.RouterEP, "logical://foo/1").Count);
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

