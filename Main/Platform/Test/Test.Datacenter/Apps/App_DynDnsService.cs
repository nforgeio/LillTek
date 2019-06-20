//-----------------------------------------------------------------------------
// FILE:        App_DynDnsService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.DynDnsService.Test
{
    [TestClass]
    public class App_DynDnsService
    {
        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            // NetTrace.Enable(MsgRouter.TraceSubsystem,0);
            NetTrace.Enable(ClusterMember.TraceSubsystem, 255);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        private IPAddress DnsLookup(string host)
        {
            if (!host.EndsWith("."))
                host += ".";

            DnsResponse response;
            A_RR aRR;

            response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, host, DnsQType.A), TimeSpan.FromSeconds(10));
            if (response.RCode != DnsFlag.RCODE_OK || response.Answers.Count == 0)
                return null;

            aRR = (A_RR)response.Answers[0];
            return aRR.Address;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void DynDnsService_EndToEnd()
        {
            LeafRouter router = null;
            Process svcProcess = null;
            Assembly assembly = typeof(LillTek.Datacenter.DynDnsService.Program).Assembly;
            DynDnsClient client = null;

            Helper.InitializeApp(assembly);

            try
            {
                Config.SetConfig(@"

&section MsgRouter

    AppName                = LillTek.DynDNS Service
    AppDescription         = Dynamic DNS
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
    
    // This maps the abstract Dynamic DNS endpoints to their default logical endpoints.

    AbstractMap[abstract://LillTek/DataCenter/DynDNS] = logical://LillTek/DataCenter/DynDNS

&endsection

&section MsgRouter

    AppName                = LillTek.DynDNS Service
    AppDescription         = Dynamic DNS
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
    
    // This maps the abstract Dynamic DNS endpoints to their default logical endpoints.

    AbstractMap[abstract://LillTek/DataCenter/DynDNS] = logical://LillTek/DataCenter/DynDNS

&endsection

//-----------------------------------------------------------------------------
// Dynamic DNS Service Settings

&section LillTek.Datacenter.DynDNS

    // Specifies the network binding the DNS server should listen on.
    
    NetworkBinding = ANY:DNS

    // Specifies the NetworkBinding the DNS server 
    // should listen on to receive UDP host registration messages
    // from DynDnsClients.

    UdpBinding = ANY:DYNAMIC-DNS
    
    // Controls how the server is to be configured to obtain host
    // registrations from dynamic DNS clients.  The possible values
    // are UDP, CLUSTER, or BOTH.
    
    Mode = UDP

    // Shared symmetric encryption key used to decrypt UDP registration messages
    // sent by DNS clients while in UDP or BOTH mode.  This key must match the shared
    // key configured for the client.  This defaults to the same reasonable default 
    //  used by the DNS client class.

    SharedKey = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==

    // The maximum delta to be allowed between the timestamp of messages received from
    // UDP broadcast clients and servers and the current system time.
    //
    // Messages transmitted between clients and servers in the UDP broadcast cluster are
    // timestamped with the time they were sent (UTC) to avoid replay attacks.  This
    // setting controls which messages will be discarded for being having a timestamp
    // too far in the past or too far into the future.
    //
    // Ideally, this value would represent the maximum time a message could realistically
    // remain in transit on the network (a few seconds), but this setting also needs to
    // account for the possibility that the server system clocks may be out of sync.  So,
    // this value is a tradeoff between security and reliability.

    MessageTTL = 15m

    // Specifies the time-to-live setting to use when replying to
    // DNS queries.  This indicates how long the operating system
    // on the client side should cache the response.
    
    ResponseTTL = 5s
    
    // Indicates whether DNS host lookup failures should be logged
    // as warnings.
    
    LogFailures = yes
    
    &section Cluster
    
        // The cluster's logical base endpoint.  Instance endpoints will be constructed
        // by appending a GUID segment to this and the cluster broadcast endpoint
        // will be generated by appending '/*'.  This setting is required.
        
        ClusterBaseEP = abstract://LillTek/DataCenter/DynDNS
        
        // Specifies the startup mode for the instance.  This can be one of
        // the following values:
        // 
        //      NORMAL          Indicates that the cluster member should go through the normal 
        //                      master election cycle and eventually enter into the MASTER or SLAVE
        //                      state.
        //
        //      OBSERVER        Indicates that the cluster member should immediately enter the
        //                      OBSERVER state and remain there.  Cluster observer state information
        //                      is replicated across the cluster so other instances know
        //                      about these instances but observers will never be elected 
        //                      as the master.
        //
        //      MONITOR         Indicates that the cluster member should immediately enter the 
        //                      MONITOR state and remain there.  Monitors collect and maintain 
        //                      cluster status but do not actively participate in the cluster.  
        //                      No member status information about a monitor will be replicated
        //                      across the cluster.
        //
        //      PREFERSLAVE     Indicates that the cluster member prefers to be started as a 
        //                      cluster slave
        //
        //      PREFERMASTER    Indicates that the cluster member prefers to be started as the 
        //                      cluster master.  If a master is already running and it does not 
        //                      have this preference then a master election will be called.
        
        Mode = Normal

        // The interval at which the cluster master should broadcast cluster update
        // messages.  Default is 1m.
        
        MasterBroadcastInterval = 10s

        // The interval at which slaves send their status updates to the master.
        // Default is 1m.

        SlaveUpdateInterval = 10s

        // The time period cluster members will wait while collecting member status
        // broadcasts from peers before concluding a master election.  Default
        // is 5s.

        ElectionInterval = 10s

        // The number of times a cluster slave member should allow for missed
        // cluster status messages before calling for a master election.
        // Default is 3.

        MissingMasterCount = 2

        // The number of times the cluster master should allow for missed
        // slave status transmissions before removing the slave from the cluster 
        // state.  Default is 3.

        MissingSlaveCount = 2

        // The interval at which the cluster master instance should raise its
        // ClusterMember.MasterTask event so that derived classes can implement 
        // custom background processing behavior.  Default is 1s.

        MasterBkInterval = 1s

        // The interval at which the cluster slaves should raise their
        // ClusterMember.SlaveTask event so that derived classes
        // can implement custom background processing behavior.
        // Default is 1s.

        SlaveBkInterval = 1s

        // The background task polling interval.  This value should be less than
        // or equal to the minimum of MasterBroadcastInterval, MasterBkInterval, 
        // and SlaveBkInterval.  Default is 1s.

        BkInterval = 1s

    &endsection

&endsection

".Replace('&', '#'));

                router = new LeafRouter();
                router.Start(); ;

                // Start the Dynamic DNS service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);     // Give the process a chance to spin up

                // Open a dynamic DNS client and register a host and then
                // verify that it worked.

                DynDnsClientSettings clientSettings;

                clientSettings = new DynDnsClientSettings();
                clientSettings.Mode = DynDnsMode.Udp;
                clientSettings.NameServers = new NetworkBinding[] { new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.DynamicDns) };
                clientSettings.UdpRegisterInterval = TimeSpan.FromSeconds(1);

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test.com", IPAddress.Parse("10.1.2.3")));
                client.Register(new DynDnsHostEntry("www.lilltek.com", IPAddress.Parse("192.168.1.202")));
                client.Open(router, clientSettings);
                Thread.Sleep(3000);

                Assert.AreEqual(IPAddress.Parse("10.1.2.3"), DnsLookup("test.com"));

                // Uncomment this for manual interop testing

                // Thread.Sleep(10000000);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }
    }
}

