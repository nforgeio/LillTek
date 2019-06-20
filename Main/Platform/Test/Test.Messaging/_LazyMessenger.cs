//-----------------------------------------------------------------------------
// FILE:        _LazyMessenger.cs
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
    public class _LazyMessenger
    {

        private const string group = "231.222.0.1:45001";
        private const int wait = 2000;

        private DeliveryConfirmation confirmation = null;

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

        [MsgHandler(LogicalEP = "logical://foo")]
        [MsgSession(Type = SessionTypeID.Query, KeepAlive = "1s", IsAsync = true)]
        public void OnQuery(PropertyMsg query)
        {
            string operation = query["operation"];

            if (operation != null)
            {

                switch (operation)
                {

                    case "timeout": return;
                    case "exception": throw new Exception("Test Exception");
                }
            }

            PropertyMsg response;

            response = new PropertyMsg();
            response["data"] = query["data"];

            query._Session.Router.ReplyTo(query, response);
        }

        private void OnDeliveryConfirmation(DeliveryConfirmation confirmation)
        {
            this.confirmation = confirmation;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Endpoint()
        {
            MsgRouter router = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));
                router.Dispatcher.AddTarget(this);
                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver("logical://foo", query, true);
                Thread.Sleep(wait);

                Assert.IsNotNull(confirmation);
                Assert.IsTrue(confirmation.Timestamp >= start);
                Assert.IsTrue(confirmation.Timestamp <= DateTime.UtcNow);
                Assert.AreEqual(MsgEP.Parse("logical://foo"), confirmation.TargetEP);
                Assert.IsInstanceOfType(confirmation.Query, typeof(PropertyMsg));
                Assert.AreEqual("yes", ((PropertyMsg)confirmation.Query)["query"]);
                Assert.IsInstanceOfType(confirmation.Response, typeof(PropertyMsg));
                Assert.AreEqual(query["data"], ((PropertyMsg)confirmation.Response)["data"]);
                Assert.IsNull(confirmation.Exception);
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Endpoint_Exception()
        {
            MsgRouter router = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));
                router.Dispatcher.AddTarget(this);
                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["operation"] = "exception";
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                try
                {
                    messenger.Deliver("logical://foo", query, true);
                    Assert.Fail("Exception expected");
                }
                catch (Exception e)
                {
                    Assert.AreEqual("Test Exception", e.Message);
                }
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
        public void LazyMessenger_DeliverTo_Endpoint_NoConfirm1()
        {
            MsgRouter router = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));
                router.Dispatcher.AddTarget(this);
                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver("logical://foo", query, false);
                Thread.Sleep(wait);

                Assert.IsNull(confirmation);
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
        public void LazyMessenger_DeliverTo_Endpoint_NoConfirm2()
        {
            MsgRouter router = CreateLeaf("detached", "hub", group);
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, null);
                router.Dispatcher.AddTarget(this);
                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver("logical://foo", query, true);
                Thread.Sleep(wait);

                Assert.IsNull(confirmation);
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
        public void LazyMessenger_DeliverTo_Endpoint_Timeout()
        {
            MsgRouter router = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));
                router.Dispatcher.AddTarget(this);
                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["operation"] = "timeout";
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                try
                {
                    messenger.Deliver("logical://foo", query, true);
                    Assert.Fail("TimeoutException expected.");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
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
        public void LazyMessenger_DeliverTo_Cluster()
        {
            MsgRouter router = null;
            BasicTopology cluster = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                router.Dispatcher.AddTarget(this);

                cluster = new BasicTopology();
                cluster.OpenClient(router, "logical://foo", null);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));

                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver(cluster, null, query, true);
                Thread.Sleep(wait);

                Assert.IsNotNull(confirmation);
                Assert.IsTrue(confirmation.Timestamp >= start);
                Assert.IsTrue(confirmation.Timestamp <= DateTime.UtcNow);
                Assert.AreEqual(MsgEP.Parse("logical://foo"), confirmation.TargetEP);
                Assert.IsInstanceOfType(confirmation.Query, typeof(PropertyMsg));
                Assert.AreEqual("yes", ((PropertyMsg)confirmation.Query)["query"]);
                Assert.AreEqual(cluster.InstanceID, confirmation.TopologyID);
                Assert.IsNotNull(confirmation.TopologyInfo);
                Assert.IsNull(confirmation.TopologyParam);
                Assert.IsInstanceOfType(confirmation.Response, typeof(PropertyMsg));
                Assert.AreEqual(query["data"], ((PropertyMsg)confirmation.Response)["data"]);
                Assert.IsNull(confirmation.Exception);
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Cluster_Exception()
        {
            MsgRouter router = null;
            BasicTopology cluster = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                router.Dispatcher.AddTarget(this);

                cluster = new BasicTopology();
                cluster.OpenClient(router, "logical://foo", null);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));

                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["operation"] = "exception";
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;

                try
                {
                    messenger.Deliver(cluster, null, query, true);
                    Assert.Fail("Exception expected");
                }
                catch (Exception e)
                {
                    Assert.AreEqual("Test Exception", e.Message);
                }
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Cluster_NoConfirm1()
        {
            MsgRouter router = null;
            BasicTopology cluster = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                router.Dispatcher.AddTarget(this);

                cluster = new BasicTopology();
                cluster.OpenClient(router, "logical://foo", null);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));

                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver(cluster, null, query, false);
                Thread.Sleep(wait);

                Assert.IsNull(confirmation);
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Cluster_NoConfirm2()
        {
            MsgRouter router = null;
            BasicTopology cluster = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                router.Dispatcher.AddTarget(this);

                cluster = new BasicTopology();
                cluster.OpenClient(router, "logical://foo", null);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, null);

                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                messenger.Deliver(cluster, null, query, true);
                Thread.Sleep(wait);

                Assert.IsNull(confirmation);
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LazyMessenger_DeliverTo_Cluster_Timeout()
        {
            MsgRouter router = null;
            BasicTopology cluster = null;
            LazyMessenger messenger = null;
            DateTime start = DateTime.UtcNow;
            PropertyMsg query;

            try
            {
                router = CreateLeaf("detached", "hub", group);
                router.Dispatcher.AddTarget(this);

                cluster = new BasicTopology();
                cluster.OpenClient(router, "logical://foo", null);

                messenger = new LazyMessenger();
                messenger.OpenClient(router, "logical://confirm", null, new DeliveryConfirmCallback(OnDeliveryConfirmation));

                Thread.Sleep(wait);

                query = new PropertyMsg();
                query["operation"] = "timeout";
                query["data"] = "Hello World!";
                query["query"] = "yes";

                confirmation = null;
                try
                {
                    messenger.Deliver(cluster, null, query, true);
                    Assert.Fail("TimeoutException expected.");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
            }
            finally
            {
                if (messenger != null)
                    messenger.Close();

                if (cluster != null)
                    cluster.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

