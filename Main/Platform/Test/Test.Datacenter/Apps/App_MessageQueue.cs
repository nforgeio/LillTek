//-----------------------------------------------------------------------------
// FILE:        App_MessageQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.MessageQueue.NUnit
{
    [TestClass]
    public class App_MessageQueue
    {
        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            //NetTrace.Enable(MsgQueueEngine.TraceSubsystem,0);
            NetTrace.Enable(MsgRouter.TraceSubsystem, 255);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void MessageQueue_EndToEnd_Disk()
        {
            // This test peforms a simple end-to-end test of the Message
            // Queue Service by starting the service and processing
            // some messages.

            Process svcProcess = null;
            LeafRouter router = null;
            MsgQueue queue = null;
            Assembly assembly;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());
            assembly = typeof(LillTek.Datacenter.MessageQueue.Program).Assembly;

            try
            {
                // Start a local router and open a client.

                Config.SetConfig(@"

//-----------------------------------------------------------------------------
// LeafRouter Settings

&section MsgRouter

    AppName                = LillTek.Test Router
    AppDescription         = Unit Test
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

&endsection

".Replace('&', '#'));

                router = new LeafRouter();
                router.Start();

                // Start the application store service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                // Send and receive some messages 

                queue = new MsgQueue(router);
                queue.EnqueueTo("logical://LillTek/DataCenter/MsgQueue/10", new QueuedMsg(10));

                //for (int i=0;i<10;i++)
                //    queue.EnqueueTo("logical://LillTek/DataCenter/MsgQueue/" + i.ToString(),new QueuedMsg(i));

                //for (int i=0;i<10;i++)
                //    Assert.AreEqual(i,queue.DequeueFrom("logical://LillTek/DataCenter/MsgQueue/" + i.ToString(),TimeSpan.FromSeconds(1)).Body);
            }
            finally
            {
                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void MessageQueue_EndToEnd_Memory()
        {
            // This test peforms a simple end-to-end test of the Message
            // Queue Service by starting the service and processing
            // some messages.

            Process svcProcess = null;
            LeafRouter router = null;
            MsgQueue queue = null;
            Assembly assembly;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());
            assembly = typeof(LillTek.Datacenter.MessageQueue.Program).Assembly;

            try
            {
                // Start a local router and open a client.

                Config.SetConfig(@"

//-----------------------------------------------------------------------------
// LeafRouter Settings

&section MsgRouter

    AppName                = LillTek.Test Router
    AppDescription         = Unit Test
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

&endsection

&section LillTek.Datacenter.MessageQueue

    PersistTo = DISK
    LogTo     = DISK

&endsection

".Replace('&', '#'));

                router = new LeafRouter();
                router.Start();

                // Start the application store service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                // Send and receive some messages 

                queue = new MsgQueue(router);
                queue.EnqueueTo("logical://LillTek/DataCenter/MsgQueue/10", new QueuedMsg(10));

                //for (int i=0;i<10;i++)
                //    queue.EnqueueTo("logical://LillTek/DataCenter/MsgQueue/" + i.ToString(),new QueuedMsg(i));

                //for (int i=0;i<10;i++)
                //    Assert.AreEqual(i,queue.DequeueFrom("logical://LillTek/DataCenter/MsgQueue/" + i.ToString(),TimeSpan.FromSeconds(1)).Body);
            }
            finally
            {
                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (queue != null)
                    queue.Close();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

