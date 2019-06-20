//-----------------------------------------------------------------------------
// FILE:        _DuplexSession.cs
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
    public class _DuplexSession
    {
        [TestInitialize]
        public void Initialize()
        {
            ReliableTransferSession.ClearCachedSettings();
            NetTrace.Start();
            Helper.SetLocalGuidMode(GuidMode.CountUp);

            NetTrace.Enable(DuplexSession.TraceSubsystem, 255);
            NetTrace.Enable(MsgRouter.TraceSubsystem, 255);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Helper.SetLocalGuidMode(GuidMode.Normal);
            ReliableTransferSession.ClearCachedSettings();
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

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSessionHeaderTest()
        {
            DuplexSessionHeader header;
            byte[] bytes;
            Guid id;

            id = Helper.NewGuid();
            header = new DuplexSessionHeader(id, DuplexMessageType.Response);
            Assert.AreEqual(id, header.QueryID);
            Assert.AreEqual(DuplexMessageType.Response, header.Type);
            bytes = header.ToArray();

            header = new DuplexSessionHeader(bytes);
            Assert.AreEqual(id, header.QueryID);
            Assert.AreEqual(DuplexMessageType.Response, header.Type);
        }

        private object syncLock = new object();
        private DuplexSession server;
        private List<PropertyMsg> serverMsgs;
        private List<PropertyMsg> clientMsgs;
        private bool serverClosed;
        private bool clientClosed;
        private bool serverTimeout;
        private bool clientTimeout;

        private void InitTest()
        {
            SetConfig();

            server = null;
            serverMsgs = new List<PropertyMsg>();
            clientMsgs = new List<PropertyMsg>();
            serverClosed = false;
            clientClosed = false;
            serverTimeout = false;
            clientTimeout = false;
        }

        private void CleanupTest()
        {
            Config.SetConfig(null);

            server = null;
            serverMsgs = null;
            clientMsgs = null;
            serverClosed = false;
            clientClosed = false;
            serverTimeout = false;
            clientTimeout = false;
        }

        private bool MessageExists(List<PropertyMsg> list, string cmd, int value)
        {
            foreach (PropertyMsg msg in list)
                if (msg._Get("cmd") == cmd && msg._Get("value", -1) == value)
                    return true;

            return false;
        }

        private void OnReceive(DuplexSession session, Msg msg)
        {
            lock (syncLock)
            {

                if (session.IsClient)
                    clientMsgs.Add((PropertyMsg)msg);
                else
                    serverMsgs.Add((PropertyMsg)msg);
            }
        }

        private PropertyMsg GetNormalMsg(int value)
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "normal");
            msg._Set("value", value);
            return msg;
        }

        private PropertyMsg GetErrorMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "error");
            return msg;
        }

        private PropertyMsg GetExceptionMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "exception");
            return msg;
        }

        private PropertyMsg GetCancelExceptionMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "cancel-exception");
            return msg;
        }

        private PropertyMsg GetTimeoutExceptionMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "timeout-exception");
            return msg;
        }

        private PropertyMsg GetTimeoutMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "timeout");
            return msg;
        }

        private PropertyMsg GetDelayMsg(int value, TimeSpan delay)
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "delay");
            msg._Set("value", value);
            msg._Set("time", delay);
            return msg;
        }

        private PropertyMsg GetCmdMsg(string cmd, int value)
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", cmd);
            msg._Set("value", value);
            return msg;
        }

        private PropertyMsg GetAsyncMsg(int value)
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "async");
            msg._Set("value", value);
            return msg;
        }

        private PropertyMsg GetAsyncDelayMsg(int value, TimeSpan delay)
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "async-delay");
            msg._Set("value", value);
            msg._Set("time", delay);
            return msg;
        }

        private PropertyMsg GetAsyncCancelMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "async-cancel");
            return msg;
        }

        private PropertyMsg GetAsyncAbortMsg()
        {
            PropertyMsg msg;

            msg = new PropertyMsg();
            msg._Set("cmd", "async-abort");
            return msg;
        }

        private Msg OnQuery(DuplexSession session, Msg query, out bool async)
        {
            PropertyMsg msg = (PropertyMsg)query;
            PropertyMsg ack = new PropertyMsg();

            async = false;
            switch (msg._Get("cmd"))
            {
                case "query":
                case "normal":

                    ack._Set("value", msg._Get("value", -1));
                    return ack;

                case "error":

                    return new Ack("Test error");

                case "timeout":

                    return new Ack(new TimeoutException());

                case "exception":

                    throw new Exception("Test exception");

                case "cancel-exception":

                    throw new CancelException("Test exception");

                case "timeout-exception":

                    throw new TimeoutException("Test exception");

                case "delay":

                    Thread.Sleep(msg._Get("time", TimeSpan.Zero));
                    ack._Set("value", msg._Get("value", -1));
                    return ack;

                case "async":
                case "async-delay":
                case "async-cancel":
                case "async-abort":

                    async = true;
                    Helper.UnsafeQueueUserWorkItem(new WaitCallback(OnAsyncQuery), new AsyncState(query.CreateRequestContext(), msg));
                    return null;

                default:

                    throw new NotImplementedException();
            }
        }

        private class AsyncState
        {
            public MsgRequestContext Context;
            public Msg Query;

            public AsyncState(MsgRequestContext context, Msg query)
            {
                this.Context = context;
                this.Query = query;
            }
        }

        private void OnAsyncQuery(object s)
        {
            AsyncState state = (AsyncState)s;
            PropertyMsg query = (PropertyMsg)state.Query;
            PropertyMsg ack = new PropertyMsg();

            switch (query._Get("cmd"))
            {

                case "async":

                    ack._Set("value", query._Get("value", -1));
                    state.Context.Reply(ack);
                    break;

                case "async-delay":

                    Thread.Sleep(query._Get("time", TimeSpan.Zero));

                    ack._Set("value", query._Get("value", -1));
                    state.Context.Reply(ack);
                    break;

                case "async-cancel":

                    state.Context.Cancel();
                    break;

                case "async-abort":

                    state.Context.Abort();
                    break;

                default:

                    state.Context.Reply(new Ack("Not implemented"));
                    break;
            }
        }

        private void OnClose(DuplexSession session, bool timeout)
        {
            if (session.IsClient)
            {
                clientClosed = true;
                clientTimeout = timeout;
            }
            else
            {
                serverClosed = true;
                serverTimeout = timeout;
            }
        }

        [MsgHandler(LogicalEP = "logical://duplex/normal")]
        [MsgSession(Type = SessionTypeID.Duplex, KeepAlive = "1s", SessionTimeout = "5s")]
        public void OnMsg00(DuplexSessionMsg msg)
        {
            server = (DuplexSession)msg._Session;
            server.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
            server.QueryEvent += new DuplexQueryDelegate(OnQuery);
            server.CloseEvent += new DuplexCloseDelegate(OnClose);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Basic()
        {
            // Open a session, send a message in each direction and
            // then close the session.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Send(GetNormalMsg(0));
                server.Send(GetNormalMsg(1000));
                Thread.Sleep(1000);

                Assert.AreEqual(1, clientMsgs.Count);
                Assert.AreEqual("normal", clientMsgs[0]._Get("cmd"));
                Assert.AreEqual(1000, clientMsgs[0]._Get("value", -1));

                Assert.AreEqual(1, serverMsgs.Count);
                Assert.AreEqual("normal", serverMsgs[0]._Get("cmd"));
                Assert.AreEqual(0, serverMsgs[0]._Get("value", -1));

                client.Close();
                client = null;
                Thread.Sleep(1000);

                Assert.IsTrue(clientClosed);
                Assert.IsFalse(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsFalse(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Basic()
        {
            // Open a session, send a message in each direction and
            // then close the session.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router1 = new LeafRouter();
                router1.Start();

                router2 = new LeafRouter();
                router2.Start();
                router2.Dispatcher.AddTarget(this);

                client = router1.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Send(GetNormalMsg(0));
                server.Send(GetNormalMsg(1000));
                Thread.Sleep(1000);

                Assert.AreEqual(1, clientMsgs.Count);
                Assert.AreEqual("normal", clientMsgs[0]._Get("cmd"));
                Assert.AreEqual(1000, clientMsgs[0]._Get("value", -1));

                Assert.AreEqual(1, serverMsgs.Count);
                Assert.AreEqual("normal", serverMsgs[0]._Get("cmd"));
                Assert.AreEqual(0, serverMsgs[0]._Get("value", -1));

                client.Close();
                client = null;
                Thread.Sleep(1000);

                Assert.IsTrue(clientClosed);
                Assert.IsFalse(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsFalse(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Connect_Timeout()
        {
            // Open a session to a non-existent endpoint and
            // verify that we see a TimeoutException.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");
                Assert.Fail("TimeoutException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [MsgHandler(LogicalEP = "logical://duplex/exception")]
        [MsgSession(Type = SessionTypeID.Duplex)]
        public void OnMsg01(DuplexSessionMsg msg)
        {
            throw new Exception("Test error");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Connect_Exception()
        {
            // Open a connection against an endpoint that rejects the
            // connection with an exception.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/exception");
                Assert.Fail("SessionException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
                Assert.AreEqual("Test error", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Connect_Exception()
        {
            // Open a connection against an endpoint that rejects the
            // connection with an exception.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router1 = new LeafRouter();
                router1.Start();
                router1.Dispatcher.AddTarget(this);

                router2 = new LeafRouter();
                router2.Start();

                client = router1.CreateDuplexSession();
                client.Connect("logical://duplex/exception");
                Assert.Fail("SessionException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
                Assert.AreEqual("Test error", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [MsgHandler(LogicalEP = "logical://duplex/keepalive")]
        [MsgSession(Type = SessionTypeID.Duplex, KeepAlive = "1s", SessionTimeout = "5s")]
        public void OnMsg02(DuplexSessionMsg msg)
        {
            server = (DuplexSession)msg._Session;
            server.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
            server.QueryEvent += new DuplexQueryDelegate(OnQuery);
            server.CloseEvent += new DuplexCloseDelegate(OnClose);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Connect_KeepAlive()
        {
            // Open a session and then sleep for a long time to verify
            // that the keepalive messages prevent the sessions from closing.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/keepalive");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                Thread.Sleep(30000);

                client.Send(GetNormalMsg(0));
                server.Send(GetNormalMsg(1000));
                Thread.Sleep(1000);

                Assert.AreEqual(1, clientMsgs.Count);
                Assert.AreEqual("normal", clientMsgs[0]._Get("cmd"));

                Assert.AreEqual(1, serverMsgs.Count);
                Assert.AreEqual("normal", serverMsgs[0]._Get("cmd"));

                client.Close();
                client = null;
                Thread.Sleep(1000);

                Assert.IsTrue(clientClosed);
                Assert.IsFalse(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsFalse(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Connect_KeepAlive()
        {
            // Open a session and then sleep for a long time to verify
            // that the keepalive messages prevent the sessions from closing.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router1 = new LeafRouter();
                router1.Start();
                router1.Dispatcher.AddTarget(this);

                router2 = new LeafRouter();
                router2.Start();

                client = router1.CreateDuplexSession();
                client.Connect("logical://duplex/keepalive");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                Thread.Sleep(30000);

                client.Send(GetNormalMsg(0));
                server.Send(GetNormalMsg(1000));
                Thread.Sleep(1000);

                Assert.AreEqual(1, clientMsgs.Count);
                Assert.AreEqual("normal", clientMsgs[0]._Get("cmd"));

                Assert.AreEqual(1, serverMsgs.Count);
                Assert.AreEqual("normal", serverMsgs[0]._Get("cmd"));

                client.Close();
                client = null;
                Thread.Sleep(1000);

                Assert.IsTrue(clientClosed);
                Assert.IsFalse(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsFalse(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Fail_Client()
        {
            // Open a session then simulate a client failure, sleep for a bit and
            // then verify that the server session closes with a timeout.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/keepalive");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.NetworkMode = NetFailMode.Disconnected;
                Thread.Sleep(9000);

                Assert.IsTrue(clientClosed);
                Assert.IsFalse(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsTrue(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Fail_Server()
        {
            // Open a session then simulate a server failure, sleep for a bit and
            // then verify that the client session closes with a timeout.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/keepalive");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                server.NetworkMode = NetFailMode.Disconnected;
                Thread.Sleep(9000);

                Assert.IsTrue(clientClosed);
                Assert.IsTrue(clientTimeout);

                Assert.IsTrue(serverClosed);
                Assert.IsFalse(serverTimeout);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [MsgHandler(LogicalEP = "logical://duplex/msg-blast")]
        [MsgSession(Type = SessionTypeID.Duplex, KeepAlive = "1s", SessionTimeout = "5s")]
        public void OnMsg03(DuplexSessionMsg msg)
        {
            server = (DuplexSession)msg._Session;
            server.ReceiveEvent += new DuplexReceiveDelegate(OnReceiveBlast);
            server.QueryEvent += new DuplexQueryDelegate(OnQuery);
            server.CloseEvent += new DuplexCloseDelegate(OnClose);
        }

        private void OnReceiveBlast(DuplexSession session, Msg msg)
        {
            PropertyMsg propMsg = (PropertyMsg)msg;

            lock (syncLock)
            {
                if (propMsg._Get("cmd") == "query")
                {
                    session.Send(GetCmdMsg("response", propMsg._Get("value", -1)));
                }
                else
                {
                    lock (syncLock)
                    {
                        if (session.IsClient)
                            clientMsgs.Add((PropertyMsg)msg);
                        else
                            serverMsgs.Add((PropertyMsg)msg);
                    }
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Blast_Messages()
        {
            // Blast messages simultaneously in both directions of a
            // connection and then verify that they all got delivered.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/msg-blast");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceiveBlast);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 100; i++)
                {
                    client.Send(GetCmdMsg("query", i));
                    server.Send(GetCmdMsg("query", i + 1000));
                    Thread.Sleep(10);
                }

                for (int i = 0; i < 100; i++)
                {
                    Assert.IsTrue(MessageExists(clientMsgs, "response", i), string.Format("client: {0}", i));
                    Assert.IsTrue(MessageExists(serverMsgs, "response", i + 1000), string.Format("server: {0}", i));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Blast_Messages()
        {
            // Blast messages simultaneously in both directions of a
            // connection and then verify that they all got delivered.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router1 = new LeafRouter();
                router1.Start();
                router1.Dispatcher.AddTarget(this);

                router2 = new LeafRouter();
                router2.Start();

                client = router2.CreateDuplexSession();
                client.Connect("logical://duplex/msg-blast");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceiveBlast);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 1000; i++)
                {
                    client.Send(GetCmdMsg("query", i));
                    server.Send(GetCmdMsg("query", i + 1000));
                    Thread.Sleep(50);
                }

                for (int i = 0; i < 1000; i++)
                {
                    Assert.IsTrue(MessageExists(clientMsgs, "response", i), string.Format("client: {0}", i));
                    Assert.IsTrue(MessageExists(serverMsgs, "response", i + 1000), string.Format("server: {0}", i));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Basic()
        {
            // Open a session and perform a couple of queries in both directions.

            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                response = (PropertyMsg)client.Query(GetCmdMsg("query", 10));
                Assert.AreEqual(10, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetCmdMsg("query", 20));
                Assert.AreEqual(20, response._Get("value", -1));

                response = (PropertyMsg)client.Query(GetCmdMsg("query", 30));
                Assert.AreEqual(30, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetCmdMsg("query", 40));
                Assert.AreEqual(40, response._Get("value", -1));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Query_Basic()
        {
            // Open a session and perform a couple of queries in both directions.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;
            PropertyMsg response;

            try
            {
                InitTest();

                router1 = new LeafRouter();
                router1.Start();
                router1.AppName = "Client";

                router2 = new LeafRouter();
                router2.Start();
                router2.AppName = "Server";
                router2.Dispatcher.AddTarget(this);

                client = router1.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                response = (PropertyMsg)client.Query(GetCmdMsg("query", 10));
                Assert.AreEqual(10, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetCmdMsg("query", 20));
                Assert.AreEqual(20, response._Get("value", -1));

                response = (PropertyMsg)client.Query(GetCmdMsg("query", 30));
                Assert.AreEqual(30, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetCmdMsg("query", 40));
                Assert.AreEqual(40, response._Get("value", -1));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Blast()
        {
            // Open a session and perform a bunch of queries in both directions.

            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 1000; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_CrossRouter_Query_Blast()
        {
            // Open a session and perform a bunch of queries in both directions.

            LeafRouter router1 = null;
            LeafRouter router2 = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router2 = new LeafRouter();
                router2.Start();
                router2.Dispatcher.AddTarget(this);

                router1 = new LeafRouter();
                router1.Start();

                client = router1.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 1000; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_RemoteTimeout()
        {
            // Verify that a TimeoutException from the remote side gets
            // rethrown as a TimeException on the client side (not
            // a SessionException).

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.Query(GetTimeoutMsg());
                Assert.Fail("Expected a TimeoutException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [MsgHandler(LogicalEP = "logical://duplex/no-handlers")]
        [MsgSession(Type = SessionTypeID.Duplex, KeepAlive = "1s", SessionTimeout = "5s")]
        public void OnMsg04(DuplexSessionMsg msg)
        {
            server = (DuplexSession)msg._Session;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_NoHandler()
        {
            // Send a query to an endpoint with no query handler and verify
            // that we get a SessionException.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/no-handlers");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetCmdMsg("query", 10));
                Assert.Fail("Expected a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Async()
        {
            // Process a query with an async handler.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetAsyncMsg(10));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_AsyncDelay()
        {
            // Process a query with an async handler.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetAsyncDelayMsg(10, TimeSpan.FromMilliseconds(250)));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_AsyncLongDelay()
        {
            // Process a query with an async handler.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetAsyncDelayMsg(10, TimeSpan.FromSeconds(20)));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_AsyncCancel()
        {
            // Verify that cancelling an async query on the server side
            // results in a CancelException on the client.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetAsyncCancelMsg());
                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_AsyncAbort()
        {
            // Verify that aborting an async query on the server side
            // results in a TimeoutException on the client.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetAsyncAbortMsg());
                Assert.Fail("Expected a TimeoutException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Exception()
        {
            // Send a query to an endpoint with a query handler that throws
            // an exception and then verify that we got it.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetExceptionMsg());
                Assert.Fail("Expected a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
                Assert.AreEqual("Test exception", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_CancelException()
        {
            // Send a query to an endpoint with a query handler that throws
            // a CancelException and then verify that we got it.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetCancelExceptionMsg());
                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
                Assert.AreEqual("Test exception", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_TimeoutException()
        {
            // Send a query to an endpoint with a query handler that throws
            // a CancelException and then verify that we got it.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetTimeoutExceptionMsg());
                Assert.Fail("Expected a TimeoutException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
                Assert.AreEqual("Test exception", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Error()
        {
            // Send a query to an endpoint with a query handler that returns
            // and error ack and verify that we got it.

            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetErrorMsg());
                Assert.Fail("Expected a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
                Assert.AreEqual("Test error", e.Message);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_LongDelay()
        {
            // Open a session and perform a couple of queries with long delays
            // in both directions.

            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                response = (PropertyMsg)client.Query(GetDelayMsg(10, TimeSpan.FromSeconds(20)));
                Assert.AreEqual(10, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetDelayMsg(20, TimeSpan.FromSeconds(20)));
                Assert.AreEqual(20, response._Get("value", -1));

                response = (PropertyMsg)client.Query(GetDelayMsg(30, TimeSpan.FromSeconds(20)));
                Assert.AreEqual(30, response._Get("value", -1));

                response = (PropertyMsg)server.Query(GetDelayMsg(40, TimeSpan.FromSeconds(20)));
                Assert.AreEqual(40, response._Get("value", -1));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Client_Fail()
        {
            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.NetworkMode = NetFailMode.Disconnected; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetNormalMsg(10));
                Assert.Fail("TimeoutException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Client_Intermittent()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.NetworkMode = NetFailMode.Intermittent; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Client_Delay()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.NetworkMode = NetFailMode.Delay; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Client_Duplicates()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                client.NetworkMode = NetFailMode.Duplicate; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Server_Fail()
        {
            LeafRouter router = null;
            DuplexSession client = null;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                server.NetworkMode = NetFailMode.Disconnected; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                client.Query(GetNormalMsg(10));
                Assert.Fail("TimeoutException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TimeoutException));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Server_Intermittent()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                server.NetworkMode = NetFailMode.Intermittent; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Server_Delay()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                server.NetworkMode = NetFailMode.Delay; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void DuplexSession_Query_Server_Duplicates()
        {
            LeafRouter router = null;
            DuplexSession client = null;
            PropertyMsg response;
            IAsyncResult arClient;
            IAsyncResult arServer;

            try
            {
                InitTest();

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                client = router.CreateDuplexSession();
                client.Connect("logical://duplex/normal");

                server.NetworkMode = NetFailMode.Duplicate; ;

                client.ReceiveEvent += new DuplexReceiveDelegate(OnReceive);
                client.QueryEvent += new DuplexQueryDelegate(OnQuery);
                client.CloseEvent += new DuplexCloseDelegate(OnClose);

                for (int i = 0; i < 10; i++)
                {
                    arClient = client.BeginQuery(GetCmdMsg("query", i), null, null);
                    arServer = server.BeginQuery(GetCmdMsg("query", i + 1000), null, null);

                    response = (PropertyMsg)client.EndQuery(arClient);
                    Assert.AreEqual(i, response._Get("value", -1));

                    response = (PropertyMsg)server.EndQuery(arServer);
                    Assert.AreEqual(i + 1000, response._Get("value", -1));
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                CleanupTest();
            }
        }
    }
}

