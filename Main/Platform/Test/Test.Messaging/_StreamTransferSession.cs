//-----------------------------------------------------------------------------
// FILE:        _StreamTransferSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

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
    public class _StreamTransferSession
    {
        private const int TransferSize = 100000;

        private EnhancedMemoryStream msServer;
        private bool simServerError;
        private bool simServerCancel;
        private bool serverNotify;
        private string serverArgs;
        private Exception serverException;
        private Exception clientException;
        private AutoResetEvent clientWait;

        [TestInitialize]
        public void Initialize()
        {
            ReliableTransferSession.ClearCachedSettings();
            NetTrace.Start();
            NetTrace.Enable(ReliableTransferSession.TraceSubsystem, 0);

            // NetTrace.Enable(MsgRouter.TraceSubsystem,255);

            clientWait = new AutoResetEvent(false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ReliableTransferSession.ClearCachedSettings();
            Config.SetConfig(null);
            NetTrace.Stop();

            clientWait.Close();
        }

        private void SetConfig(string transferSettings)
        {
            string cfg = @"

&section MsgRouter

    AppName                = LillTek.ClusterMember Unit Test
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
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

    &section ReliableTransfer
{0}
    &endsection

&endsection
";
            cfg = string.Format(cfg, transferSettings);
            Config.SetConfig(cfg.Replace('&', '#'));
        }

        private void InitServer()
        {
            msServer = null;
            simServerError = false;
            simServerCancel = false;
            serverNotify = false;
            serverException = null;
            serverArgs = null;
        }

        private void Verify(EnhancedMemoryStream ms, int cb)
        {
            byte[] buffer = ms.GetBuffer();

            for (int i = 0; i < cb; i++)
                Assert.AreEqual((byte)i, buffer[i], string.Format("pos={0}", i));
        }

        private EnhancedMemoryStream CreateStream(int cb)
        {
            EnhancedMemoryStream ms;

            ms = new EnhancedMemoryStream();

            for (int i = 0; i < cb; i++)
                ms.WriteByte((byte)i);

            ms.Position = 0;
            return ms;
        }

        private void OnServerDone(IAsyncResult ar)
        {
            serverNotify = true;

            try
            {
                StreamTransferSession session;

                session = (StreamTransferSession)ar.AsyncState;
                session.EndTransfer(ar);
            }
            catch (Exception e)
            {
                serverException = e;
            }
        }

        private void OnClientDone(IAsyncResult ar)
        {
            clientException = null;

            try
            {
                StreamTransferSession session;

                session = (StreamTransferSession)ar.AsyncState;
                session.EndTransfer(ar);
            }
            catch (Exception e)
            {
                clientException = e;
            }

            clientWait.Set();
        }

        [MsgHandler(LogicalEP = "logical://Test/Upload")]
        [MsgSession(Type = SessionTypeID.ReliableTransfer)]
        public void OnMsgUpload(ReliableTransferMsg msg)
        {
            StreamTransferSession session;

            session = StreamTransferSession.ServerUpload(msg._Session.Router, msg, msServer = new EnhancedMemoryStream());
            session.SimulateCancel = simServerCancel;
            session.SimulateError = simServerError;
            session.BeginTransfer(new AsyncCallback(OnServerDone), session);
            serverArgs = msg.Args;
        }

        [MsgHandler(LogicalEP = "logical://Test/Download")]
        [MsgSession(Type = SessionTypeID.ReliableTransfer)]
        public void OnMsgDownload(ReliableTransferMsg msg)
        {
            StreamTransferSession session;

            session = StreamTransferSession.ServerDownload(msg._Session.Router, msg, msServer);
            session.SimulateCancel = simServerCancel;
            session.SimulateError = simServerError;
            session.BeginTransfer(new AsyncCallback(OnServerDone), session);
            serverArgs = msg.Args;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Upload_Sync()
        {
            StreamTransferSession clientSession;
            LeafRouter router;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.Args = "Upload";
                clientSession.Transfer();

                Verify(msServer, TransferSize);
                Assert.IsTrue(serverNotify);
                Assert.IsNull(serverException);
                Assert.AreEqual("Upload", serverArgs);
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
        public void StreamTransferSession_Download_Sync()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                clientSession = StreamTransferSession.ClientDownload(router, "logical://Test/Download", msClient);
                clientSession.Args = "Download";
                clientSession.Transfer();

                Verify(msClient, TransferSize);
                Assert.IsTrue(serverNotify);
                Assert.IsNull(serverException);
                Assert.AreEqual("Download", serverArgs);
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
        public void StreamTransferSession_Upload_Async()
        {
            StreamTransferSession clientSession;
            LeafRouter router;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.BeginTransfer(new AsyncCallback(OnClientDone), clientSession);
                clientWait.WaitOne(TimeSpan.FromSeconds(30), false);

                Verify(msServer, TransferSize);
                Assert.IsTrue(serverNotify);
                Assert.IsNull(serverException);
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
        public void StreamTransferSession_Download_Async()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                clientSession = StreamTransferSession.ClientDownload(router, "logical://Test/Download", msClient);
                clientSession.BeginTransfer(new AsyncCallback(OnClientDone), clientSession);
                clientWait.WaitOne(TimeSpan.FromSeconds(30), false);

                Verify(msClient, TransferSize);
                Assert.IsTrue(serverNotify);
                Assert.IsNull(serverException);
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
        public void StreamTransferSession_Cancel_OnClient()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            IAsyncResult ar;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.Delay = 500;
                ar = clientSession.BeginTransfer(null, null);

                Thread.Sleep(1000);
                clientSession.Cancel();

                clientSession.EndTransfer(ar);
                Assert.Fail("Expected a CancelException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(CancelException));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Cancel_OnServer()
        {
            StreamTransferSession clientSession;
            LeafRouter router;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                simServerCancel = true;

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.Delay = 500;
                clientSession.Transfer();
                Assert.Fail("Expected a CancelException");
            }
            catch (SessionException e)
            {
                SysLog.LogException(e);
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CancelException));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(CancelException));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Upload_ErrorOnClient()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            IAsyncResult ar;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.Delay = 500;
                clientSession.SimulateError = true;
                ar = clientSession.BeginTransfer(null, null);

                Thread.Sleep(1000);
                clientSession.Cancel();

                clientSession.EndTransfer(ar);
                Assert.Fail("Expected an exception");
            }
            catch
            {
                // Expecting an exception
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(SessionException));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Upload_ErrorOnServer()
        {
            StreamTransferSession clientSession;
            LeafRouter router;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                simServerError = true;

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Upload", CreateStream(TransferSize));
                clientSession.Delay = 500;
                clientSession.Transfer();
                Assert.Fail("Expected an exception");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(Exception));
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Download_ErrorOnClient()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                clientSession = StreamTransferSession.ClientDownload(router, "logical://Test/Download", msClient);
                clientSession.Delay = 500;
                clientSession.SimulateError = true;
                clientSession.Transfer();

                Assert.Fail("Expected an exception");
            }
            catch
            {
                // Expecting an exception
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(SessionException));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_Download_ErrorOnServer()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                simServerError = true;

                clientSession = StreamTransferSession.ClientDownload(router, "logical://Test/Download", msClient);
                clientSession.Delay = 500;
                clientSession.Transfer();
                Assert.Fail("Expecting a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
            }
            finally
            {
                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            Thread.Sleep(1000);
            Assert.IsTrue(serverNotify);
            Assert.IsInstanceOfType(serverException, typeof(Exception));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void StreamTransferSession_WrongDirection_Download()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                clientSession = StreamTransferSession.ClientDownload(router, "logical://Test/Upload", msClient);
                clientSession.Transfer();
                Assert.Fail("Expected a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
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
        public void StreamTransferSession_WrongDirection_Upload()
        {
            StreamTransferSession clientSession;
            LeafRouter router;
            EnhancedMemoryStream msClient;

            InitServer();

            router = null;
            try
            {
                SetConfig("DefBlockSize=1000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(this);

                msServer = CreateStream(TransferSize);
                msClient = new EnhancedMemoryStream();

                clientSession = StreamTransferSession.ClientUpload(router, "logical://Test/Download", msClient);
                clientSession.Transfer();
                Assert.Fail("Expected a SessionException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SessionException));
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

