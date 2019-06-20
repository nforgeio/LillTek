//-----------------------------------------------------------------------------
// FILE:        _AuthServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _MsgQueueHandler
    {
        private LeafRouter router = null;
        private string tempFolder = Helper.AddTrailingSlash(Path.GetTempPath() + Guid.NewGuid().ToString());

        [TestInitialize]
        public void Initialize()
        {
            //NetTrace.Start();
            //NetTrace.Enable(MsgRouter.TraceSubsystem,255);
            //NetTrace.Enable(ReliableTransferSession.TraceSubsystem,255);
            //NetTrace.Enable(ClusterMember.TraceSubsystem,255);

            const string settings =
@"
&section MsgRouter

    AppName             = Test
    AppDescription      = Message Queuing
    RouterEP		    = physical://DETACHED/Test/$(Guid)
    CloudEP    			= $(LillTek.DC.CloudEP)
    CloudAdapter        = ANY
    UdpEP			    = ANY:0
    TcpEP			    = ANY:0
    TcpBacklog			= 100
    TcpDelay			= off
    BkInterval			= 1s
    MaxIdle				= 5m
    AdvertiseTime	    = 1m
    DefMsgTTL		    = 5
    SharedKey			= PLAINTEXT
    IV					= 00
    SessionCacheTime    = 2m
    SessionRetries      = 3
    SessionTimeout      = 10s

&endsection

&section MsgQueueClient

    BaseEP     = logical://Test/Queues/MyQueue
    Timeout    = infinite
    MessageTTL = 0s
    Compress   = BEST

&endsection

&section MsgQueueEngine 

    PersistTo = FILE
    Folder = {0}
    LogTo = DISK
    LogFolder = $(AppPath)\Messages\Log
    QueueMap[0] = logical://LillTek/DataCenter/MsgQueue
    FlushInterval = 5m
    DeadLetterTTL = 7d
    MaxDeliveryAttempts = 3    
    KeepAliveInterval = 30s
    SessionTimeout = 90s
    PendingCheckInterval = 60s
    BkTaskInterval = 1s

&endsection
";
            Config.SetConfig(string.Format(settings.Replace('&', '#'), tempFolder));

            router = new LeafRouter();
            router.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Config.SetConfig(null);

            if (router != null)
                router.Stop();

            NetTrace.Stop();
            Helper.DeleteFile(tempFolder, true);
        }

        private void ClearFolder()
        {
            Helper.DeleteFile(tempFolder, true);
            Thread.Sleep(1000);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void MsgQueueHandler_Basic()
        {
            MsgQueueHandler handler = null;
            MsgQueue queue = null;

            ClearFolder();

            try
            {
                handler = new MsgQueueHandler();
                handler.Start(router, null, null, null);

                queue = new MsgQueue(router);

                queue.EnqueueTo("foo", new QueuedMsg("bar"));
                Assert.AreEqual("bar", queue.DequeueFrom("foo", TimeSpan.FromSeconds(1)).Body);
            }
            finally
            {
                if (handler != null)
                    handler.Stop();

                if (queue != null)
                    queue.Close();
            }
        }
    }
}

