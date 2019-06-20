//-----------------------------------------------------------------------------
// FILE:        _ParallelQuerySession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class _ParallelQuerySession
    {
        LeafRouter router;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            Helper.SetLocalGuidMode(GuidMode.CountUp);

            NetTrace.Enable(MsgRouter.TraceSubsystem, 255);
            NetTrace.Enable(ParallelQuerySession.TraceSubsystem, 255);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Helper.SetLocalGuidMode(GuidMode.Normal);
            Config.SetConfig(null);
            NetTrace.Stop();
        }

        private void SetConfig()
        {
            string cfg = @"

&section MsgRouter

    AppName                = LillTek.DuplexSession Unit Test
    AppDescription         = 
    RouterEP			   = physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
    CloudEP    			   = $(LillTek.DC.CloudEP)
    CloudAdapter    	   = ANY
    UdpEP				   = ANY:0
    TcpEP				   = ANY:0
    TcpBacklog			   = 100
    TcpDelay			   = off
    BkInterval			   = 1s
    MaxIdle				   = 5m
    EnableP2P              = yes
    AdvertiseTime		   = 1m
    DefMsgTTL			   = 5
    SharedKey		 	   = PLAINTEXT
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 3s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

&endsection
";
            Config.SetConfig(cfg.Replace('&', '#'));
        }

        [MsgHandler(LogicalEP = "logical://parallel/test")]
        [MsgSession(Type = SessionTypeID.Query, KeepAlive = "1s", SessionTimeout = "5s")]
        public void OnMsg(PropertyMsg queryMsg)
        {
            PropertyMsg replyMsg;

            switch (queryMsg._Get("cmd"))
            {
                case "reply":

                    replyMsg = new PropertyMsg();
                    replyMsg["value"] = queryMsg["value"];

                    router.ReplyTo(queryMsg, replyMsg);
                    break;

                case "delay-reply":

                    Thread.Sleep(500);

                    replyMsg = new PropertyMsg();
                    replyMsg["value"] = queryMsg["value"];

                    router.ReplyTo(queryMsg, replyMsg);
                    break;

                case "error":

                    throw new Exception("Error: " + queryMsg["value"]);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_VerifyDefaults()
        {
            var parallelQuery = new ParallelQuery();

            Assert.AreEqual(ParallelWait.ForAll, parallelQuery.WaitMode);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAll_SingleSuccess()
        {
            // Test a single successful query just to make sure it actually works.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery();
                var queryMsg1 = new PropertyMsg();

                queryMsg1["cmd"] = "reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("1", ((PropertyMsg)parallelQuery.Operations[0].ReplyMsg)["value"]);
            }
            finally
            {
                if (router != null)
                {

                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAll_SingleError()
        {
            // Test a single error query just to make sure it actually works.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery();
                var queryMsg1 = new PropertyMsg();

                queryMsg1["cmd"] = "error";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                router.ParallelQuery(parallelQuery);

                Assert.IsNotNull(parallelQuery.Operations[0].Error);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAll_MultipleSuccess()
        {
            // Test 4 parallel queries that all succeed.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery();
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "reply";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "reply";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "reply";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("1", ((PropertyMsg)parallelQuery.Operations[0].ReplyMsg)["value"]);
                Assert.AreEqual("2", ((PropertyMsg)parallelQuery.Operations[1].ReplyMsg)["value"]);
                Assert.AreEqual("3", ((PropertyMsg)parallelQuery.Operations[2].ReplyMsg)["value"]);
                Assert.AreEqual("4", ((PropertyMsg)parallelQuery.Operations[3].ReplyMsg)["value"]);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAll_MultipleFail()
        {
            // Test 4 parallel queries that all fail.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery();
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "error";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "error";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "error";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "error";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("Error: 1", parallelQuery.Operations[0].Error.Message);
                Assert.AreEqual("Error: 2", parallelQuery.Operations[1].Error.Message);
                Assert.AreEqual("Error: 3", parallelQuery.Operations[2].Error.Message);
                Assert.AreEqual("Error: 4", parallelQuery.Operations[3].Error.Message);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAll_PartialFail()
        {
            // Test 4 parallel queries where 2 succeed and two fail.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery();
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "error";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "reply";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "error";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("1", ((PropertyMsg)parallelQuery.Operations[0].ReplyMsg)["value"]);
                Assert.AreEqual("3", ((PropertyMsg)parallelQuery.Operations[2].ReplyMsg)["value"]);

                Assert.AreEqual("Error: 2", parallelQuery.Operations[1].Error.Message);
                Assert.AreEqual("Error: 4", parallelQuery.Operations[3].Error.Message);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAny_SingleSuccess()
        {
            // Test a single successful query just to make sure it actually works.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery() { WaitMode = ParallelWait.ForAny };
                var queryMsg1 = new PropertyMsg();

                queryMsg1["cmd"] = "reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("1", ((PropertyMsg)parallelQuery.Operations[0].ReplyMsg)["value"]);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAny_SingleError()
        {
            // Test a single error query just to make sure it actually works.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery() { WaitMode = ParallelWait.ForAny };
                var queryMsg1 = new PropertyMsg();

                queryMsg1["cmd"] = "error";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                router.ParallelQuery(parallelQuery);

                Assert.AreEqual("Error: 1", parallelQuery.Operations[0].Error.Message);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAny_MultipleSuccess()
        {
            // Test 4 parallel queries that all succeed and verify that
            // only one result is actually returned.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery() { WaitMode = ParallelWait.ForAny };
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "reply";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "reply";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "reply";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                int cCompleted = 0;

                for (int i = 0; i < 4; i++)
                {
                    var operation = parallelQuery.Operations[i];

                    if (operation.IsComplete)
                    {
                        cCompleted++;
                        Assert.AreEqual((i + 1).ToString(), ((PropertyMsg)parallelQuery.Operations[i].ReplyMsg)["value"]);
                    }
                }

                Assert.AreEqual(1, cCompleted);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAny_PartialSuccess()
        {
            // Test 4 parallel queries where two succeed (after a bit of a delay) and
            // two fail immediately and verify that the two failures were recorded and
            // we have a single successful response.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery() { WaitMode = ParallelWait.ForAny };
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "delay-reply";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "delay-reply";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "error";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "error";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                int cCompleted = 0;

                for (int i = 0; i < 2; i++)
                {
                    var operation = parallelQuery.Operations[i];

                    if (operation.IsComplete)
                    {
                        cCompleted++;
                        Assert.AreEqual((i + 1).ToString(), ((PropertyMsg)parallelQuery.Operations[i].ReplyMsg)["value"]);
                    }
                }

                Assert.AreEqual(1, cCompleted);
                Assert.AreEqual("Error: 3", parallelQuery.Operations[2].Error.Message);
                Assert.AreEqual("Error: 4", parallelQuery.Operations[3].Error.Message);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ParallelQuerySession_WaitAny_MultipleFail()
        {
            // Test 4 parallel queries where all fail and verify that we got 
            // all of the error responses.

            try
            {
                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                Thread.Sleep(1000);

                var parallelQuery = new ParallelQuery() { WaitMode = ParallelWait.ForAny };
                var queryMsg1 = new PropertyMsg();
                var queryMsg2 = new PropertyMsg();
                var queryMsg3 = new PropertyMsg();
                var queryMsg4 = new PropertyMsg();

                queryMsg1["cmd"] = "error";
                queryMsg1["value"] = "1";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg1));

                queryMsg2["cmd"] = "error";
                queryMsg2["value"] = "2";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg2));

                queryMsg3["cmd"] = "error";
                queryMsg3["value"] = "3";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg3));

                queryMsg4["cmd"] = "error";
                queryMsg4["value"] = "4";
                parallelQuery.Operations.Add(new ParallelOperation("logical://parallel/test", queryMsg4));

                router.ParallelQuery(parallelQuery);

                int cCompleted = 0;

                for (int i = 0; i < 4; i++)
                {
                    var operation = parallelQuery.Operations[i];

                    if (operation.IsComplete)
                    {
                        cCompleted++;
                        Assert.AreEqual("Error: " + (i + 1).ToString(), parallelQuery.Operations[i].Error.Message);
                    }
                }

                Assert.AreEqual(4, cCompleted);
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                Config.SetConfig(null);
            }
        }
    }
}

