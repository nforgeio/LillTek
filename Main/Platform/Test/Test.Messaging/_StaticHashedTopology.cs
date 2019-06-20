//-----------------------------------------------------------------------------
// FILE:        _StaticHashedTopology.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

// $todo(jeff.lill): Need to add more ParallelQuery tests?

using System;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _StaticHashedTopology
    {
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

        private int[] dynACounts = new int[3];
        private int[] dynBCounts = new int[3];

        private void Clear()
        {
            for (int i = 0; i < 3; i++)
            {
                dynACounts[i] = 0;
                dynBCounts[i] = 0;
            }
        }

        private const string ClusterConfig =
@"
instances[0] = 0
instances[1] = 1
instances[2] = 2

clusterType = {0}:{1}
clusterArgs = cluster-ep={2};instances=instances;this-instance={3}
";

        private static StaticHashedTopology OpenClient(MsgRouter router, string clusterEP)
        {
            System.Type type = typeof(StaticHashedTopology);

            Config.SetConfig(string.Format(ClusterConfig, type.FullName, type.Assembly.Location, clusterEP, ""));
            return (StaticHashedTopology)TopologyHelper.OpenClient(router, "clusterType", "clusterArgs");
        }

        private static StaticHashedTopology OpenServer(MsgRouter router, string dynamicScope, object target, string clusterEP, int instance)
        {
            System.Type type = typeof(StaticHashedTopology);

            Config.SetConfig(string.Format(ClusterConfig, type.FullName, type.Assembly.Location, clusterEP, instance));
            return (StaticHashedTopology)TopologyHelper.OpenServer(router, dynamicScope, target, "clusterType", "clusterArgs");
        }

        private class LeafRouterA : LeafRouter
        {
            private StaticHashedTopology cluster;
            private int[] counts;
            private int index;

            public LeafRouterA(int[] counts, int index)
            {
                this.counts = counts;
                this.index = index;
                this.cluster = null;
            }

            [MsgHandler(LogicalEP = "logical://foo", DynamicScope = "A")]
            public void OnMsg(PropertyMsg msg)
            {
                lock (counts)
                    counts[index]++;
            }

            [MsgHandler(LogicalEP = "logical://foo", DynamicScope = "A")]
            [MsgSession(Type = SessionTypeID.Query)]
            public void OnMsg(BlobPropertyMsg msg)
            {
                PropertyMsg reply = new PropertyMsg();

                lock (counts)
                    counts[index]++;

                reply["value"] = "A";
                ReplyTo(msg, reply);
            }

            public new void Start()
            {
                base.Start();

                cluster = OpenServer(this, "A", this, "logical://A", index);
            }

            public new void Stop()
            {
                if (cluster != null)
                {
                    cluster.Close();
                    cluster = null;
                }

                base.Stop();
            }
        }

        private class LeafRouterB : LeafRouter
        {
            private StaticHashedTopology cluster;
            private int[] counts;
            private int index;

            public LeafRouterB(int[] counts, int index)
            {
                this.counts = counts;
                this.index = index;
                this.cluster = null;
            }

            [MsgHandler(LogicalEP = "logical://foo", DynamicScope = "B")]
            public void OnMsg(PropertyMsg msg)
            {
                lock (counts)
                    counts[index]++;
            }

            [MsgHandler(LogicalEP = "logical://foo", DynamicScope = "B")]
            [MsgSession(Type = SessionTypeID.Query)]
            public void OnMsg(BlobPropertyMsg msg)
            {
                PropertyMsg reply = new PropertyMsg();

                lock (counts)
                    counts[index]++;

                reply["value"] = "B";
                ReplyTo(msg, reply);
            }

            public new void Start()
            {
                base.Start();

                cluster = OpenServer(this, "B", this, "logical://B", index);
            }

            public new void Stop()
            {
                if (cluster != null)
                {
                    cluster.Close();
                    cluster = null;
                }

                base.Stop();
            }
        }

        private LeafRouter CreateLeaf(string root, string hub, string name, string cloudEP)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://{0}/{1}/{2}
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

        private LeafRouterA CreateLeafA(string root, string hub, string name, string cloudEP, int[] counts, int index)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://{0}/{1}/{2}
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
            LeafRouterA router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP));
            router = new LeafRouterA(counts, index);
            router.Start();

            return router;
        }

        private LeafRouterB CreateLeafB(string root, string hub, string name, string cloudEP, int[] counts, int index)
        {
            const string settings =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://{0}/{1}/{2}
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
            LeafRouterB router;

            Config.SetConfig(string.Format(settings, root, hub, name, cloudEP));
            router = new LeafRouterB(counts, index);
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StaticHashedTopology_Send()
        {
            LeafRouter router = null;
            LeafRouterA[] serverA = null;
            LeafRouterB[] serverB = null;
            StaticHashedTopology clusterA = null;
            StaticHashedTopology clusterB = null;

            try
            {
                // Crank up the service instances

                serverA = new LeafRouterA[3];
                serverA[0] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 0);
                serverA[1] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 1);
                serverA[2] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 2);

                serverB = new LeafRouterB[3];
                serverB[0] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 0);
                serverB[1] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 1);
                serverB[2] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 2);
                
                // Create the client and its cluster instances

                router = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);

                // Initialize the client clusters

                clusterA = OpenClient(router, "logical://A");
                clusterB = OpenClient(router, "logical://B");

                // Delay a bit to allow for endpoint discovery

                Thread.Sleep(InitDelay);

                // Make sure that the message handler endpoints were dynamically modified

                Clear();
                clusterA.Send(null, new PropertyMsg());
                Thread.Sleep(1000);
                Assert.AreEqual(1, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                Clear();
                clusterB.Send(null, new PropertyMsg());
                Thread.Sleep(1000);
                Assert.AreEqual(0, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(1, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                // Verify that load balancing works

                Clear();
                for (int i = 0; i < 100; i++)
                    clusterA.Send(null, new PropertyMsg());

                Thread.Sleep(2000);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);
                Assert.AreEqual(100, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.IsTrue(dynACounts[0] > 0);
                Assert.IsTrue(dynACounts[1] > 0);
                Assert.IsTrue(dynACounts[2] > 0);
            }
            finally
            {
                if (serverA != null)
                    for (int i = 0; i < serverA.Length; i++)
                        if (serverA[i] != null)
                            serverA[i].Stop();

                if (serverB != null)
                    for (int i = 0; i < serverB.Length; i++)
                        if (serverB[i] != null)
                            serverB[i].Stop();

                if (clusterA != null)
                    clusterA.Close();

                if (clusterB != null)
                    clusterB.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StaticHashedTopology_Broadcast()
        {
            LeafRouter router = null;
            LeafRouterA[] serverA = null;
            LeafRouterB[] serverB = null;
            StaticHashedTopology clusterA = null;
            StaticHashedTopology clusterB = null;

            try
            {
                // Crank up the service instances

                serverA = new LeafRouterA[3];
                serverA[0] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 0);
                serverA[1] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 1);
                serverA[2] = CreateLeafA("detached", "hub", Helper.NewGuid().ToString(), group, dynACounts, 2);

                serverB = new LeafRouterB[3];
                serverB[0] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 0);
                serverB[1] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 1);
                serverB[2] = CreateLeafB("detached", "hub", Helper.NewGuid().ToString(), group, dynBCounts, 2);

                // Create the client and its cluster instances

                router = CreateLeaf("detached", "hub", Helper.NewGuid().ToString(), group);

                // Initialize the client clusters

                clusterA = OpenClient(router, "logical://A");
                clusterB = OpenClient(router, "logical://B");

                // Delay a bit to allow for endpoint discovery

                Thread.Sleep(InitDelay);

                // Verify that broadcasting works

                Clear();
                clusterA.Broadcast(null, new PropertyMsg());
                Thread.Sleep(1000);
                Assert.AreEqual(3, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                Clear();
                clusterB.Broadcast(null, new PropertyMsg());
                Thread.Sleep(1000);
                Assert.AreEqual(0, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(3, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);
            }
            finally
            {
                if (serverA != null)
                    for (int i = 0; i < serverA.Length; i++)
                        if (serverA[i] != null)
                            serverA[i].Stop();

                if (serverB != null)
                    for (int i = 0; i < serverB.Length; i++)
                        if (serverB[i] != null)
                            serverB[i].Stop();

                if (clusterA != null)
                    clusterA.Close();

                if (clusterB != null)
                    clusterB.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StaticHashedTopology_Query()
        {
            LeafRouter router = null;
            LeafRouterA[] serverA = null;
            LeafRouterB[] serverB = null;
            StaticHashedTopology clusterA = null;
            StaticHashedTopology clusterB = null;
            PropertyMsg reply;

            try
            {
                // Crank up the service instances

                serverA = new LeafRouterA[3];
                serverA[0] = CreateLeafA("detached", "hub", "a-0", group, dynACounts, 0);
                serverA[1] = CreateLeafA("detached", "hub", "a-1", group, dynACounts, 1);
                serverA[2] = CreateLeafA("detached", "hub", "a-2", group, dynACounts, 2);

                serverB = new LeafRouterB[3];
                serverB[0] = CreateLeafB("detached", "hub", "b-0", group, dynBCounts, 0);
                serverB[1] = CreateLeafB("detached", "hub", "b-1", group, dynBCounts, 1);
                serverB[2] = CreateLeafB("detached", "hub", "b-2", group, dynBCounts, 2);

                // Create the client and its cluster instances

                router = CreateLeaf("detached", "hub", "client", group);

                // Initialize the client clusters

                clusterA = OpenClient(router, "logical://A");
                clusterB = OpenClient(router, "logical://B");

                // Delay a bit to allow for endpoint discovery

                Thread.Sleep(InitDelay);

                // Make sure that the message handler endpoints were dynamically modified

                Clear();
                reply = (PropertyMsg)clusterA.Query(null, new BlobPropertyMsg());
                Assert.AreEqual("A", reply["value"]);
                Assert.AreEqual(1, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                Clear();
                reply = (PropertyMsg)clusterB.Query(null, new BlobPropertyMsg());
                Assert.AreEqual("B", reply["value"]);
                Assert.AreEqual(0, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(1, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                // Verify that load balancing works

                Clear();
                for (int i = 0; i < 100; i++)
                {
                    reply = (PropertyMsg)clusterA.Query(null, new BlobPropertyMsg());
                    Assert.AreEqual("A", reply["value"]);
                }

                Thread.Sleep(2000);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);
                Assert.AreEqual(100, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.IsTrue(dynACounts[0] > 0);
                Assert.IsTrue(dynACounts[1] > 0);
                Assert.IsTrue(dynACounts[2] > 0);
            }
            finally
            {
                if (serverA != null)
                    for (int i = 0; i < serverA.Length; i++)
                        if (serverA[i] != null)
                            serverA[i].Stop();

                if (serverB != null)
                    for (int i = 0; i < serverB.Length; i++)
                        if (serverB[i] != null)
                            serverB[i].Stop();

                if (clusterA != null)
                    clusterA.Close();

                if (clusterB != null)
                    clusterB.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StaticHashedTopology_ParallelQuery()
        {
            LeafRouter router = null;
            LeafRouterA[] serverA = null;
            LeafRouterB[] serverB = null;
            StaticHashedTopology clusterA = null;
            StaticHashedTopology clusterB = null;
            ParallelQuery parallelQuery;

            try
            {
                // Crank up the service instances

                serverA = new LeafRouterA[3];
                serverA[0] = CreateLeafA("detached", "hub", "a-0", group, dynACounts, 0);
                serverA[1] = CreateLeafA("detached", "hub", "a-1", group, dynACounts, 1);
                serverA[2] = CreateLeafA("detached", "hub", "a-2", group, dynACounts, 2);

                serverB = new LeafRouterB[3];
                serverB[0] = CreateLeafB("detached", "hub", "b-0", group, dynBCounts, 0);
                serverB[1] = CreateLeafB("detached", "hub", "b-1", group, dynBCounts, 1);
                serverB[2] = CreateLeafB("detached", "hub", "b-2", group, dynBCounts, 2);

                // Create the client and its cluster instances

                router = CreateLeaf("detached", "hub", "client", group);

                // Initialize the client clusters

                clusterA = OpenClient(router, "logical://A");
                clusterB = OpenClient(router, "logical://B");

                // Delay a bit to allow for endpoint discovery

                Thread.Sleep(InitDelay);

                Clear();

                parallelQuery = new ParallelQuery();
                parallelQuery.Operations.Add(new ParallelOperation(new BlobPropertyMsg()));
                parallelQuery.Operations.Add(new ParallelOperation(new BlobPropertyMsg()));

                parallelQuery = clusterA.ParallelQuery(null, parallelQuery);
                foreach (var operation in parallelQuery.Operations)
                    Assert.AreEqual("A", ((PropertyMsg)operation.ReplyMsg)["value"]);

                Assert.AreEqual(2, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(0, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);

                Clear();

                parallelQuery = new ParallelQuery();
                parallelQuery.Operations.Add(new ParallelOperation(new BlobPropertyMsg()));
                parallelQuery.Operations.Add(new ParallelOperation(new BlobPropertyMsg()));

                parallelQuery = clusterB.ParallelQuery(null, parallelQuery);
                foreach (var operation in parallelQuery.Operations)
                    Assert.AreEqual("B", ((PropertyMsg)operation.ReplyMsg)["value"]);

                Assert.AreEqual(0, dynACounts[0] + dynACounts[1] + dynACounts[2]);
                Assert.AreEqual(2, dynBCounts[0] + dynBCounts[1] + dynBCounts[2]);
            }
            finally
            {
                if (serverA != null)
                    for (int i = 0; i < serverA.Length; i++)
                        if (serverA[i] != null)
                            serverA[i].Stop();

                if (serverB != null)
                    for (int i = 0; i < serverB.Length; i++)
                        if (serverB[i] != null)
                            serverB[i].Stop();

                if (clusterA != null)
                    clusterA.Close();

                if (clusterB != null)
                    clusterB.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

