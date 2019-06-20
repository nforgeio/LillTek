//-----------------------------------------------------------------------------
// FILE:        _GeoTracker_Cluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: End-to-end tests for multiple instance clusters.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Test
{
    /// <summary>
    /// End-to-end tests for multiple instance clusters.
    /// </summary>
    [TestClass]
    public class _GeoTracker_Cluster
    {
        //--------------------------------------------------------------------
        // Private types

        private class ClusterInstance
        {
            public LeafRouter Router { get; private set; }
            public GeoTrackerNode Node { get; private set; }

            public ClusterInstance()
            {
            }

            public void Start()
            {
                Router = new LeafRouter();
                Router.Start();
                Thread.Sleep(1000);

                Node = new GeoTrackerNode();
                Node.Start(Router, new GeoTrackerServerSettings(), null, null);
            }

            public void Stop()
            {
                if (Node != null)
                    Node.Stop();

                if (Router != null)
                    Router.Stop();
            }
        }

        //--------------------------------------------------------------------
        // Implementation

        private const int InstanceCount = 4;

        private LeafRouter router;
        private GeoTrackerClient client;
        private ClusterInstance[] clusterInstances;

        private void TestInit()
        {
            const string cfg =
@"
Diagnostics.TraceEnable[-] = 0:LillTek.Messaging
--Diagnostics.TraceEnable[-] = 1:LillTek.GeoTracker

MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
MsgRouter.CloudEP    			= $(LillTek.DC.CloudEP)
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 5m
MsgRouter.EnableP2P             = yes
MsgRouter.AdvertiseTime			= 1s
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";
            Config.SetConfig(cfg);
            //NetTrace.Start();

            router = new LeafRouter();
            router.Start();
            Thread.Sleep(1000);

            client = new GeoTrackerClient(router, null);

            var serverSettings = new GeoTrackerServerSettings();

            serverSettings.IPGeocodeEnabled = false;

            // Crank up the cluster instances on separate threads.

            var threads = new List<Thread>();
            var fail = false;

            clusterInstances = new ClusterInstance[InstanceCount];

            for (int i = 0; i < InstanceCount; i++)
                clusterInstances[i] = new ClusterInstance();

            for (int i = 0; i < InstanceCount; i++)
            {
                threads.Add(
                    Helper.StartThread(null, i,
                        index =>
                        {
                            try
                            {
                                clusterInstances[(int)index].Start();
                            }
                            catch (Exception e)
                            {
                                SysLog.LogException(e);
                                fail = true;
                            }
                        }));
            }

            foreach (var thread in threads)
                thread.Join();

            if (fail)
                Assert.Fail("Instance Creation Failure");

            Thread.Sleep(5000);     // Wait for routes to be replicated
        }

        private void TestCleanup()
        {
            client = null;

            foreach (var instance in clusterInstances)
                instance.Stop();

            clusterInstances = null;

            if (router != null)
            {
                router.Stop();
                router = null;
            }

            //NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTracker_Cluster_SubmitFix_Single()
        {
            // Submit a single location fix and verify that one of the instances has it.

            try
            {
                TestInit();

                client.SubmitEntityFix("jeff", null, new GeoFix() { Latitude = 10, Longitude = 20 });

                int cFound = 0;

                foreach (var instance in clusterInstances)
                {
                    cFound += instance.Node.FixCache.EntityCount;

                    var fix = instance.Node.FixCache.GetCurrentEntityFix("jeff");

                    if (fix == null)
                        continue;

                    Assert.AreEqual(10.0, fix.Latitude);
                    Assert.AreEqual(20.0, fix.Longitude);
                }

                Assert.AreEqual(1, cFound);
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTracker_Cluster_SubmitFix_Multiple()
        {
            // Submit multiple location fixes for several entities and verify.

            try
            {
                const int cEntities = 100;
                const int cFixes = 10;

                int cSubmitted;
                bool fail;

                TestInit();

                // Submit the fixes in parallel.

                cSubmitted = 0;
                fail = false;

                for (int i = 0; i < cFixes; i++)
                    for (int j = 0; j < cEntities; j++)
                    {
                        var fix = new GeoFix() { Latitude = 10 + i, Longitude = 20 };

                        client.BeginSubmitEntityFix(j.ToString(), "group", fix,
                            ar =>
                            {
                                try
                                {
                                    client.EndSubmitEntityFix(ar);
                                }
                                catch (Exception e)
                                {
                                    SysLog.LogException(e);
                                    fail = true;
                                }
                                finally
                                {
                                    Interlocked.Increment(ref cSubmitted);
                                }
                            },
                            null);
                    }

                // Wait for the submissions to complete.

                Helper.WaitFor(() => cSubmitted == cEntities * cFixes, TimeSpan.FromMinutes(2));
                Assert.IsFalse(fail);

                // Verify that the fixes were distributed across the cluster.

                var instanceFixes = new int[clusterInstances.Length];

                for (int i = 0; i < clusterInstances.Length; i++)
                {
                    instanceFixes[i] = clusterInstances[i].Node.FixCache.EntityCount;
                    Assert.IsTrue(instanceFixes[i] >= cEntities / InstanceCount * 0.75);
                }
            }
            finally
            {
                TestCleanup();
            }
        }
    }
}

