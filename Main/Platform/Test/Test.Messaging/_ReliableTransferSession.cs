//-----------------------------------------------------------------------------
// FILE:        _ReliableTransferSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ReliableTransferSession
    {
        private class HandlerError
        {
            public bool OnClient;
            public ReliableTransferEvent TransferEvent;
            public string Error;
            public bool Ignore;

            public HandlerError()
            {
                this.Ignore = true;
            }

            public HandlerError(bool onClient, ReliableTransferEvent transferEvent)
            {
                this.OnClient = onClient;
                this.TransferEvent = transferEvent;
                this.Error = null;
                this.Ignore = false;
            }

            public HandlerError(bool onClient, ReliableTransferEvent transferEvent, string error)
            {
                this.OnClient = onClient;
                this.TransferEvent = transferEvent;
                this.Error = error;
                this.Ignore = false;
            }

            public bool SimulateError(ReliableTransferHandler handler, ReliableTransferArgs args)
            {
                if (this.Ignore)
                    return false;

                if (args.TransferEvent != this.TransferEvent || handler.Session.IsClient != this.OnClient)
                    return false;

                if (Error == null)
                    handler.Session.Cancel();
                else
                    throw SessionException.Create(null, Error);

                return true;
            }
        }

        private const int LargeTransferSize = 1000000;
        private const int HugeTransferSize = 25000000;
        private NetFailMode serverFailMode = NetFailMode.Normal;
        private HandlerError handlerError = new HandlerError();

        [TestInitialize]
        public void Initialize()
        {
            ReliableTransferSession.ClearCachedSettings();
            NetTrace.Start();
            NetTrace.Enable(ReliableTransferSession.TraceSubsystem, 0);

            // NetTrace.Enable(MsgRouter.TraceSubsystem,255);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ReliableTransferSession.ClearCachedSettings();
            Config.SetConfig(null);
            NetTrace.Stop();
        }

        private void SetConfig(string transferSettings)
        {
            string cfg = @"

&section MsgRouter

    AppName                = LillTek.ReliableTransferSession Unit Test
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

        private EnhancedMemoryStream client_msUpload;
        private EnhancedMemoryStream client_msDownload;
        private ReliableTransferArgs client_beginTransferArgs;
        private ReliableTransferArgs client_endTransferArgs;
        private ReliableTransferArgs client_receiveArgs;
        private ReliableTransferArgs client_sendArgs;

        private void Verify(EnhancedMemoryStream ms, int cb)
        {
            byte[] buffer = ms.GetBuffer();

            for (int i = 0; i < cb; i++)
                Assert.AreEqual((byte)i, buffer[i], string.Format("pos={0}", i));
        }

        private ReliableTransferHandler InitClient(ReliableTransferSession session, int cbUpload, int cbDownload)
        {
            ReliableTransferHandler handler = new ReliableTransferHandler(session);

            client_msUpload = new EnhancedMemoryStream();
            for (int i = 0; i < cbUpload; i++)
                client_msUpload.WriteByte((byte)i);
            client_msUpload.Position = 0;

            client_msDownload = new EnhancedMemoryStream();
            for (int i = 0; i < cbDownload; i++)
                client_msDownload.WriteByte((byte)i);
            client_msDownload.Position = 0;

            client_beginTransferArgs = null;
            client_endTransferArgs = null;
            client_receiveArgs = null;
            client_sendArgs = null;

            handler.BeginTransferEvent += new ReliableTransferDelegate(Client_OnTransferBegin);
            handler.EndTransferEvent += new ReliableTransferDelegate(Client_OnTransferEnd);
            handler.ReceiveEvent += new ReliableTransferDelegate(Client_OnReceive);
            handler.SendEvent += new ReliableTransferDelegate(Client_OnSend);

            return handler;
        }

        private void Client_OnTransferBegin(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.BeginTransfer);
            if (handlerError.SimulateError(sender, args))
                return;

            client_beginTransferArgs = args;
        }

        private void Client_OnTransferEnd(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.EndTransfer);
            if (handlerError.SimulateError(sender, args))
                return;

            client_endTransferArgs = args;
        }

        private void Client_OnReceive(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.Receive);
            if (handlerError.SimulateError(sender, args))
                return;

            client_receiveArgs = args;
            client_msDownload.WriteBytesNoLen(args.BlockData);
        }

        private void Client_OnSend(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.Send);
            if (handlerError.SimulateError(sender, args))
                return;

            client_sendArgs = args;
            args.BlockData = client_msUpload.ReadBytes(args.BlockSize);
        }

        private EnhancedMemoryStream server_msUpload;
        private EnhancedMemoryStream server_msDownload;
        private ReliableTransferArgs server_beginTransferArgs;
        private ReliableTransferArgs server_endTransferArgs;
        private ReliableTransferArgs server_receiveArgs;
        private ReliableTransferArgs server_sendArgs;

        private ReliableTransferHandler CreateServerHandler(ReliableTransferSession session)
        {
            ReliableTransferHandler handler = new ReliableTransferHandler(session);

            handler.BeginTransferEvent += new ReliableTransferDelegate(Server_OnTransferBegin);
            handler.EndTransferEvent += new ReliableTransferDelegate(Server_OnTransferEnd);
            handler.ReceiveEvent += new ReliableTransferDelegate(Server_OnReceive);
            handler.SendEvent += new ReliableTransferDelegate(Server_OnSend);

            return handler;
        }

        private void InitServer(int cbUpload, int cbDownload)
        {
            server_msUpload = new EnhancedMemoryStream();
            for (int i = 0; i < cbUpload; i++)
                server_msUpload.WriteByte((byte)i);
            server_msUpload.Position = 0;

            server_msDownload = new EnhancedMemoryStream();
            for (int i = 0; i < cbDownload; i++)
                server_msDownload.WriteByte((byte)i);
            server_msDownload.Position = 0;

            server_beginTransferArgs = null;
            server_endTransferArgs = null;
            server_receiveArgs = null;
            server_sendArgs = null;
        }

        private void Server_OnTransferBegin(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.BeginTransfer);
            if (handlerError.SimulateError(sender, args))
                return;

            server_beginTransferArgs = args;
        }

        private void Server_OnTransferEnd(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.EndTransfer);
            if (handlerError.SimulateError(sender, args))
                return;

            server_endTransferArgs = args;
        }

        private void Server_OnReceive(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.Receive);
            if (handlerError.SimulateError(sender, args))
                return;

            server_receiveArgs = args;
            server_msUpload.WriteBytesNoLen(args.BlockData);
        }

        private void Server_OnSend(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            Common.Assertion.Test(args.TransferEvent == ReliableTransferEvent.Send);
            if (handlerError.SimulateError(sender, args))
                return;

            server_sendArgs = args;
            args.BlockData = server_msDownload.ReadBytes(args.BlockSize);
        }

        private class UploadTarget
        {
            private _ReliableTransferSession test;

            public UploadTarget(_ReliableTransferSession test)
            {
                this.test = test;
            }

            [MsgHandler(LogicalEP = "logical://Test/Upload")]
            [MsgSession(Type = SessionTypeID.ReliableTransfer)]
            public void OnMsgUpload(ReliableTransferMsg msg)
            {
                ReliableTransferSession session = msg.Session;
                ReliableTransferHandler handler;

                if (msg.Direction == TransferDirection.Download)
                    throw new Exception("Download is not available.");

                handler = new ReliableTransferHandler(session);
                session.SessionHandler = test.CreateServerHandler(session);

                if (test.serverFailMode != NetFailMode.Normal)
                    session.NetworkMode = test.serverFailMode;
            }
        }

        private class DownloadTarget
        {
            private _ReliableTransferSession test;

            public DownloadTarget(_ReliableTransferSession test)
            {
                this.test = test;
            }

            [MsgHandler(LogicalEP = "logical://Test/Download")]
            [MsgSession(Type = SessionTypeID.ReliableTransfer)]
            public void OnMsgDownload(ReliableTransferMsg msg)
            {
                ReliableTransferSession session = msg.Session;
                ReliableTransferHandler handler;

                if (msg.Direction == TransferDirection.Upload)
                    throw new Exception("Uploads are not accepted.");

                handler = new ReliableTransferHandler(session);
                session.SessionHandler = test.CreateServerHandler(session);

                if (test.serverFailMode != NetFailMode.Normal)
                    session.NetworkMode = test.serverFailMode;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Download_Basic()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=5");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, 10);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the server handler events were raised and that the argument
                // fields were set correctly.

                Assert.IsNotNull(server_beginTransferArgs);
                Assert.AreEqual(transferID, server_beginTransferArgs.TransferID);
                Assert.AreEqual("test", server_beginTransferArgs.Args);
                Assert.AreEqual(5, server_beginTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, server_beginTransferArgs.Direction);
                Assert.IsNull(server_beginTransferArgs.ErrorMessage);

                Assert.IsNotNull(server_endTransferArgs);
                Assert.AreEqual(transferID, server_endTransferArgs.TransferID);
                Assert.AreEqual("test", server_endTransferArgs.Args);
                Assert.AreEqual(5, server_endTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, server_endTransferArgs.Direction);
                Assert.IsNull(server_endTransferArgs.ErrorMessage);

                Assert.IsNotNull(server_sendArgs);
                Assert.AreEqual(transferID, server_sendArgs.TransferID);
                Assert.AreEqual("test", server_sendArgs.Args);
                Assert.AreEqual(5, server_sendArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, server_sendArgs.Direction);
                Assert.IsNull(server_sendArgs.ErrorMessage);

                Assert.IsNull(server_receiveArgs);

                // Verify that the client handler events were raised and that the argument
                // fields were set correctly.

                Assert.IsNotNull(client_beginTransferArgs);
                Assert.AreEqual(transferID, client_beginTransferArgs.TransferID);
                Assert.AreEqual("test", client_beginTransferArgs.Args);
                Assert.AreEqual(5, client_beginTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, client_beginTransferArgs.Direction);
                Assert.IsNull(client_beginTransferArgs.ErrorMessage);

                Assert.IsNotNull(client_endTransferArgs);
                Assert.AreEqual(transferID, client_endTransferArgs.TransferID);
                Assert.AreEqual("test", client_endTransferArgs.Args);
                Assert.AreEqual(5, client_endTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, client_endTransferArgs.Direction);
                Assert.IsNull(client_endTransferArgs.ErrorMessage);

                Assert.IsNull(client_sendArgs);

                Assert.IsNotNull(client_receiveArgs);
                Assert.AreEqual(transferID, client_receiveArgs.TransferID);
                Assert.AreEqual("test", client_receiveArgs.Args);
                Assert.AreEqual(5, client_receiveArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Download, client_receiveArgs.Direction);
                Assert.IsNull(client_receiveArgs.ErrorMessage);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(10, client_msDownload.Length);
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, }, client_msDownload.ReadBytes(10));
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
        public void ReliableTransferSession_Upload_Basic()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=5");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 10, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the server handler events were raised and that the argument
                // fields were set correctly.

                Assert.IsNotNull(server_beginTransferArgs);
                Assert.AreEqual(transferID, server_beginTransferArgs.TransferID);
                Assert.AreEqual("test", server_beginTransferArgs.Args);
                Assert.AreEqual(5, server_beginTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, server_beginTransferArgs.Direction);
                Assert.IsNull(server_beginTransferArgs.ErrorMessage);

                Assert.IsNotNull(server_endTransferArgs);
                Assert.AreEqual(transferID, server_endTransferArgs.TransferID);
                Assert.AreEqual("test", server_endTransferArgs.Args);
                Assert.AreEqual(5, server_endTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, server_endTransferArgs.Direction);
                Assert.IsNull(server_endTransferArgs.ErrorMessage);

                Assert.IsNull(server_sendArgs);

                Assert.IsNotNull(server_receiveArgs);
                Assert.AreEqual(transferID, server_receiveArgs.TransferID);
                Assert.AreEqual("test", server_receiveArgs.Args);
                Assert.AreEqual(5, server_receiveArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, server_receiveArgs.Direction);
                Assert.IsNull(server_receiveArgs.ErrorMessage);

                // Verify that the client handler events were raised and that the argument
                // fields were set correctly.

                Assert.IsNotNull(client_beginTransferArgs);
                Assert.AreEqual(transferID, client_beginTransferArgs.TransferID);
                Assert.AreEqual("test", client_beginTransferArgs.Args);
                Assert.AreEqual(5, client_beginTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, client_beginTransferArgs.Direction);
                Assert.IsNull(client_beginTransferArgs.ErrorMessage);

                Assert.IsNotNull(client_endTransferArgs);
                Assert.AreEqual(transferID, client_endTransferArgs.TransferID);
                Assert.AreEqual("test", client_endTransferArgs.Args);
                Assert.AreEqual(5, client_endTransferArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, client_endTransferArgs.Direction);
                Assert.IsNull(client_endTransferArgs.ErrorMessage);

                Assert.IsNotNull(client_sendArgs);
                Assert.AreEqual(transferID, client_sendArgs.TransferID);
                Assert.AreEqual("test", client_sendArgs.Args);
                Assert.AreEqual(5, client_sendArgs.BlockSize);
                Assert.AreEqual(TransferDirection.Upload, client_sendArgs.Direction);
                Assert.IsNull(client_sendArgs.ErrorMessage);

                Assert.IsNull(client_receiveArgs);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(10, server_msUpload.Length);
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, }, server_msUpload.ReadBytes(10));
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
        public void ReliableTransferSession_Upload_CrossRouters()
        {
            LeafRouter router1 = null;
            LeafRouter router2 = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=64000");

                router1 = new LeafRouter();
                router1.Start();

                router2 = new LeafRouter();
                router2.Start();
                router2.Dispatcher.AddTarget(new UploadTarget(this));

                session = router1.CreateReliableTransferSession();

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
            }
            finally
            {
                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Download_CrossRouters()
        {
            LeafRouter router1 = null;
            LeafRouter router2 = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=64000");

                router1 = new LeafRouter();
                router1.Start();

                router2 = new LeafRouter();
                router2.Start();
                router2.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router1.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
            }
            finally
            {
                if (router1 != null)
                    router1.Stop();

                if (router2 != null)
                    router2.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Upload_Huge()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=256000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, HugeTransferSize, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(HugeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, HugeTransferSize);
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
        public void ReliableTransferSession_Download_Huge()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig("DefBlockSize=256000");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, HugeTransferSize);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(HugeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, HugeTransferSize);
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
        public void ReliableTransferSession_Upload_Intermittent_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Intermittent;

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
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
        public void ReliableTransferSession_Download_Intermittent_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Intermittent;

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
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
        public void ReliableTransferSession_Upload_Delay_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Delay;

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
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
        public void ReliableTransferSession_Download_Delay_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Delay;

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
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
        public void ReliableTransferSession_Upload_Intermittent_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);
                serverFailMode = NetFailMode.Intermittent;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Download_Intermittent_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);
                serverFailMode = NetFailMode.Intermittent;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Upload_Delay_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);
                serverFailMode = NetFailMode.Delay;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Download_Delay_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);
                serverFailMode = NetFailMode.Delay;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Upload_Duplicates_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Duplicate;

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
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
        public void ReliableTransferSession_Download_Duplicates_Client()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();
                session.NetworkMode = NetFailMode.Duplicate;

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
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
        public void ReliableTransferSession_Upload_Duplicates_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new UploadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, LargeTransferSize, 0);
                InitServer(0, 0);
                serverFailMode = NetFailMode.Duplicate;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Upload", TransferDirection.Upload, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                server_msUpload.Position = 0;
                Assert.AreEqual(LargeTransferSize, server_msUpload.Length);
                Verify(server_msUpload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Download_Duplicates_Server()
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {
                handlerError = new HandlerError();
                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                clientHandler = InitClient(session, 0, 0);
                InitServer(0, LargeTransferSize);
                serverFailMode = NetFailMode.Duplicate;

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer("logical://Test/Download", TransferDirection.Download, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private Exception RunErrorTest(TransferDirection direction)
        {
            LeafRouter router = null;
            ReliableTransferSession session;
            ReliableTransferHandler clientHandler;
            Guid transferID;

            try
            {

                ReliableTransferSession.ClearCachedSettings();
                SetConfig(@"

DefBlockSize = 64000
MaxTries     = 10
");

                router = new LeafRouter();
                router.Start();
                router.Dispatcher.AddTarget(new DownloadTarget(this));

                session = router.CreateReliableTransferSession();

                if (direction == TransferDirection.Upload)
                {
                    router.Dispatcher.AddTarget(new UploadTarget(this));
                    clientHandler = InitClient(session, LargeTransferSize, 0);
                    InitServer(0, 0);
                }
                else
                {
                    router.Dispatcher.AddTarget(new DownloadTarget(this));
                    clientHandler = InitClient(session, 0, 0);
                    InitServer(0, LargeTransferSize);
                }

                transferID = Helper.NewGuid();
                session.SessionHandler = clientHandler;
                session.Transfer(direction == TransferDirection.Upload ? "logical://Test/Upload" : "logical://Test/Download", direction, 0, transferID, "test");
                Thread.Sleep(1000);

                // Verify that the data was downloaded correctly

                client_msDownload.Position = 0;
                Assert.AreEqual(LargeTransferSize, client_msDownload.Length);
                Verify(client_msDownload, LargeTransferSize);
            }
            catch (Exception e)
            {
                return e;
            }
            finally
            {
                serverFailMode = NetFailMode.Normal;

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }

            return null;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Cancel()
        {
            ReliableTransferEvent[] sendEvents = new ReliableTransferEvent[] { ReliableTransferEvent.BeginTransfer, ReliableTransferEvent.Send };
            ReliableTransferEvent[] recvEvents = new ReliableTransferEvent[] { ReliableTransferEvent.BeginTransfer, ReliableTransferEvent.Receive };
            TransferDirection direction;
            Exception e;

            foreach (ReliableTransferEvent transferEvent in sendEvents)
            {
                handlerError = new HandlerError(true, transferEvent);
                e = RunErrorTest(direction = TransferDirection.Upload);
                Assert.IsInstanceOfType(e, typeof(CancelException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));

                handlerError = new HandlerError(false, transferEvent);
                e = RunErrorTest(direction = TransferDirection.Download);
                Assert.IsInstanceOfType(e, typeof(CancelException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
            }

            foreach (ReliableTransferEvent transferEvent in recvEvents)
            {
                handlerError = new HandlerError(true, transferEvent);
                e = RunErrorTest(direction = TransferDirection.Download);
                Assert.IsInstanceOfType(e, typeof(CancelException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));

                handlerError = new HandlerError(false, transferEvent);
                e = RunErrorTest(direction = TransferDirection.Upload);
                Assert.IsInstanceOfType(e, typeof(CancelException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferSession_Error()
        {
            ReliableTransferEvent[] sendEvents = new ReliableTransferEvent[] { ReliableTransferEvent.BeginTransfer, ReliableTransferEvent.Send };
            ReliableTransferEvent[] recvEvents = new ReliableTransferEvent[] { ReliableTransferEvent.BeginTransfer, ReliableTransferEvent.Receive };
            TransferDirection direction;
            Exception e;

            foreach (ReliableTransferEvent transferEvent in sendEvents)
            {
                handlerError = new HandlerError(true, transferEvent, "test error");
                e = RunErrorTest(direction = TransferDirection.Upload);
                Assert.IsInstanceOfType(e, typeof(SessionException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
                Assert.AreEqual("test error", e.Message);

                handlerError = new HandlerError(false, transferEvent, "test error");
                e = RunErrorTest(direction = TransferDirection.Download);
                Assert.IsInstanceOfType(e, typeof(SessionException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
                Assert.AreEqual("test error", e.Message);
            }

            foreach (ReliableTransferEvent transferEvent in recvEvents)
            {
                handlerError = new HandlerError(true, transferEvent, "test error");
                e = RunErrorTest(direction = TransferDirection.Download);
                Assert.IsInstanceOfType(e, typeof(SessionException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
                Assert.AreEqual("test error", e.Message);

                handlerError = new HandlerError(false, transferEvent, "test error");
                e = RunErrorTest(direction = TransferDirection.Upload);
                Assert.IsInstanceOfType(e, typeof(SessionException), string.Format("onClient={0} direction={1} event={2} error={3}", handlerError.OnClient, direction, transferEvent, e.Message));
                Assert.AreEqual("test error", e.Message);
            }
        }
    }
}

