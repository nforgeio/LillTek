//-----------------------------------------------------------------------------
// FILE:        _GeoTracker_SingleInstance.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: End-to-end tests for single instance clusters.

using System;
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
    /// End-to-end tests for single instance clusters.
    /// </summary>
    [TestClass]
    public class _GeoTracker_SingleInstance
    {
        private LeafRouter router;
        private GeoTrackerClient client;
        private GeoTrackerNode server;

        private void TestInit()
        {
            const string cfg =
@"
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
MsgRouter.AdvertiseTime			= 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";

            Config.SetConfig(cfg);
            router = new LeafRouter();
            router.Start();

            client = new GeoTrackerClient(router, null);

            var serverSettings = new GeoTrackerServerSettings();

            serverSettings.IPGeocodeEnabled = false;

            server = new GeoTrackerNode();
            server.Start(router, serverSettings, null, null);
        }

        private void TestCleanup()
        {
            client = null;

            if (server != null)
            {
                server.Stop();
                server = null;
            }

            if (router != null)
            {
                router.Stop();
                router = null;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTracker_SingleInstance_SubmitFix_Single()
        {
            // Submit a single location fix and verify.

            try
            {
                TestInit();

                client.SubmitEntityFix("jeff", null, new GeoFix() { Latitude = 10, Longitude = 20 });

                Assert.AreEqual(1, server.FixCache.EntityCount);

                var fix = server.FixCache.GetCurrentEntityFix("jeff");

                Assert.IsNotNull(fix);
                Assert.AreEqual(10.0, fix.Latitude);
                Assert.AreEqual(20.0, fix.Longitude);
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTracker_SingleInstance_SubmitFix_Multiple()
        {
            // Submit multiple location fixes for several entities and verify.

            try
            {
                const int cEntities = 100;
                const int cFixes = 10;

                TestInit();

                for (int i = 0; i < cFixes; i++)
                    for (int j = 0; j < cEntities; j++)
                        client.SubmitEntityFix(j.ToString(), "group", new GeoFix() { Latitude = 10 + i, Longitude = 20 });

                Assert.AreEqual(cEntities, server.FixCache.EntityCount);

                for (int j = 0; j < cEntities; j++)
                {
                    var fix = server.FixCache.GetCurrentEntityFix(j.ToString());

                    Assert.IsNotNull(fix);
                    Assert.AreEqual(10.0 + cFixes - 1, fix.Latitude);
                }
            }
            finally
            {
                TestCleanup();
            }
        }
    }
}

