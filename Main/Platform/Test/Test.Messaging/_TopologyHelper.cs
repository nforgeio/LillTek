//-----------------------------------------------------------------------------
// FILE:        _TopologyHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _TopologyHelper
    {
        private const string group = "231.222.0.1:45001";

        private LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP)
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
MsgRouter.EnableP2P             = yes
MsgRouter.AdvertiseTime			= 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";
            LeafRouter router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP));
            router = new LeafRouter();
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TopologyHelper_Client_PlugIn()
        {
            LeafRouter router = null;
            ITopologyProvider cluster = null;
            System.Type type;

            try
            {
                router = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);

                type = typeof(BasicTopology);
                Config.SetConfig(string.Format("topology-type={0}:{1}\r\nargs=cluster-ep=logical://foo", type.FullName, type.Assembly.Location));
                cluster = TopologyHelper.OpenClient(router, "topology-type", "args");
                Assert.AreEqual(type.FullName, cluster.GetType().FullName);
                Assert.IsTrue(cluster.IsClient);
            }
            finally
            {
                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TopologyHelper_Client_Serialize()
        {
            LeafRouter router = null;
            ITopologyProvider cluster = null;
            System.Type type;
            string serialized;

            try
            {
                router = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);

                type = typeof(BasicTopology);
                Config.SetConfig(string.Format("topology-type={0}:{1}\r\nargs=cluster-ep=logical://foo", type.FullName, Helper.GetAssemblyPath(type.Assembly)));
                cluster = TopologyHelper.OpenClient(router, "topology-type", "args");
                Assert.AreEqual(type.FullName, cluster.GetType().FullName);
                Assert.IsTrue(cluster.IsClient);

                serialized = cluster.SerializeClient();
                cluster.Close();
                cluster = null;

                cluster = TopologyHelper.OpenClient(router, serialized);
                Assert.AreEqual(type.FullName, cluster.GetType().FullName);
                Assert.AreEqual(type.FullName, cluster.GetType().FullName);
                Assert.IsTrue(cluster.IsClient);
                Assert.AreEqual("logical://foo", cluster.ClusterEP.ToString());
            }
            finally
            {
                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [MsgHandler(LogicalEP = "logical://foo", DynamicScope = "A")]
        [MsgSession(Type = SessionTypeID.Query)]
        public void OnMsg(PropertyMsg msg)
        {
            PropertyMsg reply = new PropertyMsg();

            reply["value"] = "Hello World!";
            msg._Session.Router.ReplyTo(msg, reply);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TopologyHelper_Server_PlugIn()
        {
            LeafRouter client = null;
            LeafRouter server = null;
            ITopologyProvider clientCluster = null;
            ITopologyProvider serverCluster = null;
            System.Type type;
            PropertyMsg reply;

            try
            {
                client = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);
                server = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);

                type = typeof(BasicTopology);
                Config.SetConfig(string.Format("topology-type={0}:{1}\r\nargs=cluster-ep=logical://foo", type.FullName, type.Assembly.Location));

                clientCluster = TopologyHelper.OpenClient(client, "topology-type", "args");
                Assert.AreEqual(type.FullName, clientCluster.GetType().FullName);
                Assert.IsTrue(clientCluster.IsClient);

                Config.SetConfig(string.Format("topology-type={0}:{1}\r\nargs=cluster-ep=logical://foo", type.FullName, type.Assembly.Location));
                serverCluster = TopologyHelper.OpenServer(client, "A", this, "topology-type", "args");
                Assert.AreEqual(type.FullName, serverCluster.GetType().FullName);
                Assert.IsFalse(serverCluster.IsClient);

                Thread.Sleep(2000);

                reply = (PropertyMsg)clientCluster.Query(null, new PropertyMsg());
                Assert.AreEqual("Hello World!", reply["value"]);
            }
            finally
            {
                if (client != null)
                    client.Stop();

                if (server != null)
                    server.Stop();

                if (clientCluster != null)
                    clientCluster.Close();

                if (serverCluster != null)
                    serverCluster.Close();

                Config.SetConfig(null);
            }
        }
    }
}

