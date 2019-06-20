//-----------------------------------------------------------------------------
// FILE:        App_RouterService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.DataCenter;
using LillTek.Messaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.DataCenter.RouterService.NUnit
{
    [TestClass]
    public class App_RouterService
    {
        private string group1 = "231.222.0.1:45001";
        private string group2 = "231.222.0.1:45002";
        private Msg recvMsg = null;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        private LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP, bool enableP2P)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://{0}/{1}/{2}
MsgRouter.CloudEP	    		= {3}
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

            LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP, enableP2P ? "yes" : "no"));

            router = new LeafRouter();
            router.Dispatcher.AddTarget(this);
            router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnMsg), "logical://" + name, typeof(PropertyMsg), false, null);
            router.Start();

            return router;
        }

        private HubRouter CreateHub(string root, string name, string cloudEP)
        {
            const string settings =
@"
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
MsgRouter.MaxIdle				= 5m
MsgRouter.AdvertiseTime			= 30s
MsgRouter.KeepAliveTime         = 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
MsgRouter.DownlinkEP[0]         = logical://*
";

            HubRouter router;

            Config.SetConfig(string.Format(settings, root, name, cloudEP));
            router = new HubRouter();
            router.Dispatcher.AddTarget(this);
            router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnMsg), "logical://" + name, typeof(PropertyMsg), false, null);
            router.Start();

            return router;
        }

        public void OnMsg(Msg msg)
        {
            recvMsg = msg;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void RouterService_HubMode()
        {
            // Verify that the router service actually deploys a hub router
            // by starting an instance of the application and a couple of
            // non-P2P leaf routers and making sure that the service is
            // routing messages between the two leaves.

            Process svcProcess = null;
            LeafRouter leaf1 = null;
            LeafRouter leaf2 = null;
            ConfigRewriter rewriter;
            Assembly assembly;
            string iniPath;

            assembly = typeof(LillTek.Datacenter.RouterService.Program).Assembly;
            iniPath = Config.GetConfigPath(assembly);
            rewriter = new ConfigRewriter(iniPath);

            try
            {
                // Rewrite the config file to have it start in hub mode
                // and then start the router service as a form application.

                rewriter.Rewrite(new ConfigRewriteTag[] { new ConfigRewriteTag("Mode", "#define HUBMODE\r\n#undef ROOTMODE\r\n#define HUBNAME hub\r\n") });
                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                // Crank up a couple of leaf routers and send a message from
                // one to the other.

                leaf1 = CreateLeaf("detached", Const.DCDefHubName, "leaf1", Const.DCCloudEP.ToString(), false);
                leaf2 = CreateLeaf("detached", Const.DCDefHubName, "leaf2", Const.DCCloudEP.ToString(), false);
                Thread.Sleep(2000);

                recvMsg = null;
                leaf1.SendTo("logical://leaf2", new PropertyMsg());
                Thread.Sleep(1000);

                Assert.IsNotNull(recvMsg);
            }
            finally
            {
                rewriter.Restore();

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (leaf1 != null)
                    leaf1.Stop();

                if (leaf2 != null)
                    leaf2.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void RouterService_RootMode()
        {
            // Verify that the router service actually deploys a root router
            // by starting an instance of the application and a couple of
            // hub routers and making sure that the service is routing messages 
            // between the two hubs.

            Process svcProcess = null;
            HubRouter hub1 = null;
            HubRouter hub2 = null;
            ConfigRewriter rewriter;
            Assembly assembly;
            string iniPath;

            assembly = typeof(LillTek.Datacenter.RouterService.Program).Assembly;
            iniPath = Config.GetConfigPath(assembly);
            rewriter = new ConfigRewriter(iniPath);

            try
            {
                // Rewrite the config file to have it start in root mode
                // and then start the router service as a form application.

                rewriter.Rewrite(new ConfigRewriteTag[] { new ConfigRewriteTag("Mode", "#undef HUBMODE\r\n#define ROOTMODE\r\n#define HUBNAME hub\r\n") });
                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                // Crank up a couple of hub routers and send a message from
                // one to the other.

                hub1 = CreateHub(Helper.MachineName + ":" + Const.DCRootPort.ToString(), "hub1", group1);
                hub2 = CreateHub(Helper.MachineName + ":" + Const.DCRootPort.ToString(), "hub2", group2);
                Thread.Sleep(2000);

                recvMsg = null;
                hub1.SendTo("logical://hub2", new PropertyMsg());
                Thread.Sleep(1000);

                Assert.IsNotNull(recvMsg);
            }
            finally
            {
                rewriter.Restore();

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (hub1 != null)
                    hub1.Stop();

                if (hub2 != null)
                    hub2.Stop();
            }
        }
    }
}

