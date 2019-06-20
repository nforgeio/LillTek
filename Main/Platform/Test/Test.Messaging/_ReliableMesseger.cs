//-----------------------------------------------------------------------------
// FILE:        _ReliableMessenger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ReliableMessenger
    {
        private const string group = "231.222.0.1:45001";

        private LeafRouter CreateLeaf(string root, string hub, string cloudEP)
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

            Config.SetConfig(string.Format(settings, root, hub, Helper.NewGuid().ToString(), cloudEP));
            router = new LeafRouter();
            router.Start();

            return router;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessenger_Client_PlugIn()
        {
            LeafRouter router = null;
            IReliableMessenger messenger = null;
            System.Type type;

            try
            {
                router = CreateLeaf("detached", "hub", group);

                type = typeof(LazyMessenger);
                Config.SetConfig(string.Format("args=messenger-type={0}:{1};confirm-ep=logical://foo", type.FullName, type.Assembly.Location));
                messenger = ReliableMessenger.OpenClient(router, "args", null);
                Assert.IsInstanceOfType(messenger, typeof(LazyMessenger));
                Assert.IsTrue(messenger.IsClient);
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableMessenger_Server_PlugIn()
        {
            // I need to implement this once I've got an IReliableMessenger implementation
            // that actually implements a server side.

            Assert.Inconclusive("Not Implemented");
        }
    }
}

