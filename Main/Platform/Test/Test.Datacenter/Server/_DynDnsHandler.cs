//-----------------------------------------------------------------------------
// FILE:        _DynDnsHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Net.Sockets;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _DynDnsHandler
    {
        LeafRouter router = null;
        private TimeSpan waitTime = TimeSpan.FromSeconds(15);

        [TestInitialize]
        public void Initialize()
        {
            // Helper.SetLocalGuidMode(GuidMode.CountUp);

            NetTrace.Start();
            // NetTrace.Enable(MsgRouter.TraceSubsystem,255);
            NetTrace.Enable(ClusterMember.TraceSubsystem, 255);

            const string settings =
@"
&section MsgRouter

    AppName                = Test
    AppDescription         = Test Description
    RouterEP			   = physical://detached/test/leaf
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

    AbstractMap[abstract://LillTek/DataCenter/DynDNS] = logical://LillTek/DataCenter/DynDNS

&endsection

&section LillTek.Datacenter.DynDNS

    NetworkBinding   = ANY:DNS
    UdpBinding       = ANY:DYNAMIC-DNS
    Mode             = BOTH
    SharedKey        = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==
    BkInterval       = 1s
    RegistrationTTL  = 185s
    MessageTTL       = 15m
    ResponseTTL      = 5s
    LogFailures      = yes

    // NameServer[0] =

    // Host[0]       =
    
    &section Cluster
    
        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection

&section LillTek.Datacenter.DynDNS.AddressCache

    NetworkBinding   = ANY:DNS
    UdpBinding       = ANY:DYNAMIC-DNS
    Mode             = BOTH
    SharedKey        = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==
    BkInterval       = 1s
    RegistrationTTL  = 185s
    MessageTTL       = 15m
    ResponseTTL      = 5s
    LogFailures      = yes

    // NameServer[0] = 

    Host[-]          = test1.lilltek.com, www.lilltek.com, 1800
    Host[-]          = test2.lilltek.com, www.google.com, 1800

    AddressCache[-]  = www.google.com
    AddressCache[-]  = www.lilltek.com,1800
    
    &section Cluster
    
        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection

&section DynDnsClient

    Enabled               = yes
    NetworkBinding        = ANY
    Mode                  = CLUSTER
    SharedKey             = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==
    BkInterval            = 1s
    DomainRefreshInterval = 15m
    UdpRegisterInterval   = 1m

    // Domain             =
    // NameServer[0]      =
    // Host[0]            =

    &section Cluster
    
        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection

&section DynDnsClient.Disabled

    Enabled               = no
    NetworkBinding        = ANY
    Mode                  = CLUSTER
    SharedKey             = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==
    BkInterval            = 1s
    DomainRefreshInterval = 15m
    UdpRegisterInterval   = 1m

    // Domain             =
    // NameServer[0]      =
    // Host[0]            =

    &section Cluster
    
        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection

&section DynDnsClient.Test

    Enabled               = yes
    NetworkBinding        = ANY
    Mode                  = CLUSTER
    SharedKey             = aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==
    BkInterval            = 1s
    DomainRefreshInterval = 15m
    UdpRegisterInterval   = 1m

    // Domain             =
    // NameServer[0]      =

    Host[0]               = test0.com,10.0.0.1
    Host[1]               = test1.com,10.0.0.2

    &section Cluster
    
        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection
";
            Config.SetConfig(settings.Replace('&', '#'));

            router = new LeafRouter();
            router.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Helper.SetLocalGuidMode(GuidMode.Normal);
            Config.SetConfig(null);

            if (router != null)
                router.Stop();

            NetTrace.Stop();
        }

        private IPAddress DnsLookup(string host)
        {
            if (!host.EndsWith("."))
                host += ".";

            DnsResponse response;
            A_RR aRR;

            response = DnsResolver.Query(NetHelper.GetActiveAdapter(), new DnsRequest(DnsFlag.NONE, host, DnsQType.A), TimeSpan.FromSeconds(10));
            if (response.RCode != DnsFlag.RCODE_OK || response.Answers.Count == 0)
                return null;

            aRR = (A_RR)response.Answers[0];
            return aRR.Address;
        }

        private void WaitForOnline(ClusterMember cluster, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + TimeSpan.FromSeconds(2);

            while (SysTime.Now < exitTime)
            {
                if (cluster.IsOnline)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for [IsOnline]");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Settings()
        {
            DynDnsClientSettings settings;

            settings = new DynDnsClientSettings("DynDnsClient.Test");
            Assert.IsTrue(settings.Enabled);
            Assert.AreEqual(2, settings.Hosts.Length);
            Assert.AreEqual("test0.com.", settings.Hosts[0].Host);
            Assert.AreEqual("10.0.0.1", settings.Hosts[0].Address.ToString());
            Assert.AreEqual("test1.com.", settings.Hosts[1].Host);
            Assert.AreEqual("10.0.0.2", settings.Hosts[1].Address.ToString());

            settings = new DynDnsClientSettings("DynDnsClient.Disabled");
            Assert.IsFalse(settings.Enabled);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Cluster_Basic()
        {
            // Start a Dynamic DNS service handler and then two enabled dynamic DNS 
            // clients and one disabled client.
            //
            // Register 2 hosts with different IP addresses on each client and
            // then query the DNS several times for each host to verify that
            // everything worked.
            //
            // Then stop one of the clients and verify that its host registrations
            // are no longer returned in DNS responses.

            DynDnsHandler handler = null;
            DynDnsClient client1 = null;
            DynDnsClient client2 = null;
            DynDnsClient client3 = null;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                client1 = new DynDnsClient();
                client1.Register(new DynDnsHostEntry("test1.com", IPAddress.Parse("10.0.0.1")));
                client1.Register(new DynDnsHostEntry("test2.com", IPAddress.Parse("10.0.0.2")));
                client1.Open(router, new DynDnsClientSettings("DynDnsClient"));

                client2 = new DynDnsClient();
                client2.Open(router, new DynDnsClientSettings("DynDnsClient"));
                client2.Register(new DynDnsHostEntry("test1.com", IPAddress.Parse("10.0.1.1")));
                client2.Register(new DynDnsHostEntry("test2.com", IPAddress.Parse("10.0.1.2")));

                client3 = new DynDnsClient();
                client3.Open(router, new DynDnsClientSettings("DynDnsClient.Disabled"));
                client3.Register(new DynDnsHostEntry("test1.com", IPAddress.Parse("10.0.3.1")));
                client3.Register(new DynDnsHostEntry("test2.com", IPAddress.Parse("10.0.3.2")));

                // Wait for all cluster members to come online and for
                // the host registrations to be replicated.

                WaitForOnline(handler.Cluster, waitTime);
                WaitForOnline(client1.Cluster, waitTime);
                WaitForOnline(client2.Cluster, waitTime);
                Thread.Sleep(2000);

                // Perform the DNS lookups

                bool found;

                // Test1.com: 10.0.0.1

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.0.1").Equals(DnsLookup("test1.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test1.com: 10.0.1.1

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.1.1").Equals(DnsLookup("test1.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test2.com: 10.0.0.2

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.0.2").Equals(DnsLookup("test2.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test2.com: 10.0.1.2

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.1.2").Equals(DnsLookup("test2.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test case insensitivity

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.1.2").Equals(DnsLookup("TEST2.COM")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test for name not found.

                try
                {
                    Assert.IsNull(DnsLookup("error.com"));
                    Assert.Fail("Expected a DnsException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(DnsException));
                }

                // Make sure that we don't see hosts from client3 since it
                // is disabled.

                // Test1.com: 10.0.3.1

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.3.1").Equals(DnsLookup("test1.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsFalse(found);

                // Close client1 and verify that its registrations are no
                // longer returned but that client2's still are.

                client1.Close();
                client1 = null;
                Thread.Sleep(2000);

                // Test1.com: 10.0.0.1

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.0.1").Equals(DnsLookup("test1.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsFalse(found);

                // Test1.com: 10.0.1.1

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.1.1").Equals(DnsLookup("test1.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);

                // Test2.com: 10.0.0.2

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.0.2").Equals(DnsLookup("test2.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsFalse(found);

                // Test2.com: 10.0.1.2

                found = false;
                for (int i = 0; i < 100; i++)
                {
                    if (IPAddress.Parse("10.0.1.2").Equals(DnsLookup("test2.com")))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);
            }
            finally
            {
                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Cluster_ADDRESS()
        {
            // Start a Dynamic DNS service handler and a client, register two ADDRESS 
            // records for TEST1.COM and confirm that A queries load balance across the two entries.
            // I'm also going to add an ADDRESS record for TEST2.COM and verify that this address
            // is never returned.

            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            A_RR record;
            bool found1;
            bool found2;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.1,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.2,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test2.com,10.0.0.3,1000,ADDRESS"));
                client.Open(router, new DynDnsClientSettings("DynDnsClient"));

                // Wait for all cluster members to come online and for
                // the host registrations to be replicated.

                WaitForOnline(handler.Cluster, waitTime);
                WaitForOnline(client.Cluster, waitTime);
                Thread.Sleep(2000);

                // Loop 1000 times or until we got responses for both entry IP addresses.  Note
                // that there's a 1000:1 chance that this could be working properly and the test
                // still fail.

                found1 = false;
                found2 = false;

                for (int i = 0; i < 1000; i++)
                {
                    response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));

                    Assert.AreEqual("test1.com.", response.QName);
                    Assert.AreEqual(DnsQType.A, response.QType);
                    Assert.AreEqual(1, response.Answers.Count);

                    record = (A_RR)response.Answers[0];
                    Assert.AreNotEqual(IPAddress.Parse("10.0.0.3"), record.Address);

                    if (IPAddress.Parse("10.0.0.1").Equals(record.Address))
                        found1 = true;
                    else if (IPAddress.Parse("10.0.0.2").Equals(record.Address))
                        found2 = true;
                    else
                        Assert.Fail();

                    if (found1 && found2)
                        break;
                }

                Assert.IsTrue(found1);
                Assert.IsTrue(found2);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Cluster_ADDRESSLIST()
        {
            // Start a Dynamic DNS service handler and a client, register two ADDRESSLIST
            // records for TEST1.COM and confirm that A queries return both entries.
            // I'm also going to add an ADDRESS record for TEST2.COM and verify that this address
            // is never returned.

            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.1,1000,ADDRESSLIST"));
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.2,1000,ADDRESSLIST"));
                client.Register(new DynDnsHostEntry("test2.com,10.0.0.3,1000,ADDRESSLIST"));
                client.Open(router, new DynDnsClientSettings("DynDnsClient"));

                // Wait for all cluster members to come online and for
                // the host registrations to be replicated.

                WaitForOnline(handler.Cluster, waitTime);
                WaitForOnline(client.Cluster, waitTime);
                Thread.Sleep(2000);

                // Perform an A query and verify that we get both addresses in response.

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.A, response.QType);
                Assert.AreEqual(2, response.Answers.Count);

                Assert.IsNotNull(response.Answers.SingleOrDefault(r => ((A_RR)r).Address.Equals(IPAddress.Parse("10.0.0.1"))));
                Assert.IsNotNull(response.Answers.SingleOrDefault(r => ((A_RR)r).Address.Equals(IPAddress.Parse("10.0.0.2"))));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Cluster_CNAME()
        {
            // Start a Dynamic DNS service handler and a client, register CNAME 
            // records and confirm that CNAME queries return the proper results.

            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            CNAME_RR record;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,server.test1.com,1000,CNAME"));
                client.Register(new DynDnsHostEntry("test2.com,server.test2.com,2000,CNAME"));
                client.Open(router, new DynDnsClientSettings("DynDnsClient"));

                // Wait for all cluster members to come online and for
                // the host registrations to be replicated.

                WaitForOnline(handler.Cluster, waitTime);
                WaitForOnline(client.Cluster, waitTime);
                Thread.Sleep(2000);

                // Verify the single result for a CNAME query on test1.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.CNAME, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (CNAME_RR)response.Answers[0];
                Assert.AreEqual("test1.com.", record.RName);
                Assert.AreEqual("server.test1.com.", record.CName);
                Assert.AreEqual(1000, record.TTL);
                Thread.Sleep(2000);

                // Verify the single result for a CNAME query on test2.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test2.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test2.com.", response.QName);
                Assert.AreEqual(DnsQType.CNAME, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (CNAME_RR)response.Answers[0];
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("server.test2.com.", record.CName);
                Assert.AreEqual(2000, record.TTL);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Cluster_MX()
        {
            // Start a Dynamic DNS service handler and a client, register MX 
            // records and confirm that MX queries return the proper results.

            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            MX_RR record;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,mail1.test1.com,1000,MX"));
                client.Register(new DynDnsHostEntry("test2.com,mail1.test2.com,2000,MX"));
                client.Register(new DynDnsHostEntry("test2.com,mail2.test2.com,3000,MX"));
                client.Open(router, new DynDnsClientSettings("DynDnsClient"));

                // Wait for all cluster members to come online and for
                // the host registrations to be replicated.

                WaitForOnline(handler.Cluster, waitTime);
                WaitForOnline(client.Cluster, waitTime);
                Thread.Sleep(2000);

                // Verify the single result for a MX query on test1.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.MX), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.MX, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (MX_RR)response.Answers[0];
                Assert.AreEqual("test1.com.", record.RName);
                Assert.AreEqual("mail1.test1.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(1000, record.TTL);
                Thread.Sleep(2000);

                // Verify the two results for a MX query on test2.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test2.com.", DnsQType.MX), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test2.com.", response.QName);
                Assert.AreEqual(DnsQType.MX, response.QType);
                Assert.AreEqual(2, response.Answers.Count);

                record = (MX_RR)response.Answers.Single(r => ((MX_RR)r).Exchange == "mail1.test2.com.");
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("mail1.test2.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(2000, record.TTL);

                record = (MX_RR)response.Answers.Single(r => ((MX_RR)r).Exchange == "mail2.test2.com.");
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("mail2.test2.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(3000, record.TTL);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Udp_ADDRESS()
        {
            // Start a Dynamic DNS service handler and a client, register two ADDRESS 
            // records for TEST1.COM and confirm that A queries load balance across the two entries.
            // I'm also going to add an ADDRESS record for TEST2.COM and verify that this address
            // is never returned.

            DynDnsClientSettings clientSettings;
            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            A_RR record;
            bool found1;
            bool found2;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                clientSettings = new DynDnsClientSettings()
                {

                    Mode = DynDnsMode.Udp,
                    NameServers = new NetworkBinding[] { new NetworkBinding("127.0.0.1:DYNAMIC-DNS") },
                    UdpRegisterInterval = TimeSpan.FromSeconds(1)
                };

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.1,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.2,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test2.com,10.0.0.3,1000,ADDRESS"));
                client.Open(router, clientSettings);

                // Wait for everything to spin up.

                Thread.Sleep(3000);

                // Loop 1000 times or until we got responses for both entry IP addresses.  Note
                // that there's a 1000:1 chance that this could be working properly and the test
                // still fail.

                found1 = false;
                found2 = false;

                for (int i = 0; i < 1000; i++)
                {
                    response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));

                    Assert.AreEqual("test1.com.", response.QName);
                    Assert.AreEqual(DnsQType.A, response.QType);
                    Assert.AreEqual(1, response.Answers.Count);

                    record = (A_RR)response.Answers[0];
                    Assert.AreNotEqual(IPAddress.Parse("10.0.0.3"), record.Address);

                    if (IPAddress.Parse("10.0.0.1").Equals(record.Address))
                        found1 = true;
                    else if (IPAddress.Parse("10.0.0.2").Equals(record.Address))
                        found2 = true;
                    else
                        Assert.Fail();

                    if (found1 && found2)
                        break;
                }

                Assert.IsTrue(found1);
                Assert.IsTrue(found2);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Udp_ADDRESSLIST()
        {
            // Start a Dynamic DNS service handler and a client, register two ADDRESSLIST
            // records for TEST1.COM and confirm that A queries return both entries.
            // I'm also going to add an ADDRESS record for TEST2.COM and verify that this address
            // is never returned.

            DynDnsClientSettings clientSettings;
            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                clientSettings = new DynDnsClientSettings()
                {
                    Mode = DynDnsMode.Udp,
                    NameServers = new NetworkBinding[] { new NetworkBinding("127.0.0.1:DYNAMIC-DNS") },
                    UdpRegisterInterval = TimeSpan.FromSeconds(1)
                };

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.1,1000,ADDRESSLIST"));
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.2,1000,ADDRESSLIST"));
                client.Register(new DynDnsHostEntry("test2.com,10.0.0.3,1000,ADDRESSLIST"));
                client.Open(router, clientSettings);

                // Wait for everything to spin up.

                Thread.Sleep(3000);

                // Perform an A query and verify that we get both addresses in response.

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.A, response.QType);
                Assert.AreEqual(2, response.Answers.Count);

                Assert.IsNotNull(response.Answers.SingleOrDefault(r => ((A_RR)r).Address.Equals(IPAddress.Parse("10.0.0.1"))));
                Assert.IsNotNull(response.Answers.SingleOrDefault(r => ((A_RR)r).Address.Equals(IPAddress.Parse("10.0.0.2"))));
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Udp_CNAME()
        {
            // Start a Dynamic DNS service handler and a client, register CNAME 
            // records and confirm that CNAME queries return the proper results.

            DynDnsClientSettings clientSettings;
            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            CNAME_RR record;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                clientSettings = new DynDnsClientSettings()
                {
                    Mode = DynDnsMode.Udp,
                    NameServers = new NetworkBinding[] { new NetworkBinding("127.0.0.1:DYNAMIC-DNS") },
                    UdpRegisterInterval = TimeSpan.FromSeconds(1)
                };

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,server.test1.com,1000,CNAME"));
                client.Register(new DynDnsHostEntry("test2.com,server.test2.com,2000,CNAME"));
                client.Open(router, clientSettings);

                // Wait for everything to spin up.

                Thread.Sleep(3000);

                // Verify the single result for a CNAME query on test1.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.CNAME, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (CNAME_RR)response.Answers[0];
                Assert.AreEqual("test1.com.", record.RName);
                Assert.AreEqual("server.test1.com.", record.CName);
                Assert.AreEqual(1000, record.TTL);
                Thread.Sleep(2000);

                // Verify the single result for a CNAME query on test2.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test2.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test2.com.", response.QName);
                Assert.AreEqual(DnsQType.CNAME, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (CNAME_RR)response.Answers[0];
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("server.test2.com.", record.CName);
                Assert.AreEqual(2000, record.TTL);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Udp_MX()
        {
            // Start a Dynamic DNS service handler and a client, register MX 
            // records and confirm that MX queries return the proper results.

            DynDnsClientSettings clientSettings;
            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            MX_RR record;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                clientSettings = new DynDnsClientSettings()
                {
                    Mode = DynDnsMode.Udp,
                    NameServers = new NetworkBinding[] { new NetworkBinding("127.0.0.1:DYNAMIC-DNS") },
                    UdpRegisterInterval = TimeSpan.FromSeconds(1)
                };

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,mail1.test1.com,1000,MX"));
                client.Register(new DynDnsHostEntry("test2.com,mail1.test2.com,2000,MX"));
                client.Register(new DynDnsHostEntry("test2.com,mail2.test2.com,3000,MX"));
                client.Open(router, clientSettings);

                // Wait for everything to spin up.

                Thread.Sleep(3000);

                // Verify the single result for a MX query on test1.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.MX), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test1.com.", response.QName);
                Assert.AreEqual(DnsQType.MX, response.QType);
                Assert.AreEqual(1, response.Answers.Count);

                record = (MX_RR)response.Answers[0];
                Assert.AreEqual("test1.com.", record.RName);
                Assert.AreEqual("mail1.test1.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(1000, record.TTL);
                Thread.Sleep(2000);

                // Verify the two results for a MX query on test2.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test2.com.", DnsQType.MX), TimeSpan.FromSeconds(2));

                Assert.AreEqual("test2.com.", response.QName);
                Assert.AreEqual(DnsQType.MX, response.QType);
                Assert.AreEqual(2, response.Answers.Count);

                record = (MX_RR)response.Answers.Single(r => ((MX_RR)r).Exchange == "mail1.test2.com.");
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("mail1.test2.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(2000, record.TTL);

                record = (MX_RR)response.Answers.Single(r => ((MX_RR)r).Exchange == "mail2.test2.com.");
                Assert.AreEqual("test2.com.", record.RName);
                Assert.AreEqual("mail2.test2.com.", record.Exchange);
                Assert.AreEqual(0, record.Preference);
                Assert.AreEqual(3000, record.TTL);
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_Udp_Unregister()
        {
            // Start a Dynamic DNS service handler and a client, register two ADDRESS 
            // records for TEST1.COM and confirm that A queries load balance across the two entries.
            // I'm also going to add an ADDRESS record for TEST2.COM and verify that this address
            // is never returned.
            //
            // Then unregister all of the client entries, wait for a brief moment and then
            // verify that the hosts are no longer returned by the DNS server.

            DynDnsClientSettings clientSettings;
            DynDnsHandler handler = null;
            DynDnsClient client = null;
            DnsResponse response;
            A_RR record;
            bool found1;
            bool found2;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, null, null, null);

                clientSettings = new DynDnsClientSettings()
                {

                    Mode = DynDnsMode.Udp,
                    NameServers = new NetworkBinding[] { new NetworkBinding("127.0.0.1:DYNAMIC-DNS") },
                    UdpRegisterInterval = TimeSpan.FromSeconds(1)
                };

                client = new DynDnsClient();
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.1,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test1.com,10.0.0.2,1000,ADDRESS"));
                client.Register(new DynDnsHostEntry("test2.com,10.0.0.3,1000,ADDRESS"));
                client.Open(router, clientSettings);

                // Wait for everything to spin up.

                Thread.Sleep(3000);

                // Loop 1000 times or until we got responses for both entry IP addresses.  Note
                // that there's a 1000:1 chance that this could be working properly and the test
                // still fail.

                found1 = false;
                found2 = false;

                for (int i = 0; i < 1000; i++)
                {
                    response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));

                    Assert.AreEqual("test1.com.", response.QName);
                    Assert.AreEqual(DnsQType.A, response.QType);
                    Assert.AreEqual(1, response.Answers.Count);

                    record = (A_RR)response.Answers[0];
                    Assert.AreNotEqual(IPAddress.Parse("10.0.0.3"), record.Address);

                    if (IPAddress.Parse("10.0.0.1").Equals(record.Address))
                        found1 = true;
                    else if (IPAddress.Parse("10.0.0.2").Equals(record.Address))
                        found2 = true;
                    else
                        Assert.Fail();

                    if (found1 && found2)
                        break;
                }

                Assert.IsTrue(found1);
                Assert.IsTrue(found2);

                // Unregister all of the entries, wait for a bit, and then verify that the
                // DNS server no longer returns answers for these hosts.

                client.UnregisterAll();
                Thread.Sleep(3000);

                try
                {
                    response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.com.", DnsQType.A), TimeSpan.FromSeconds(2));
                    Assert.Fail("Expected a DnsException(\"Name does not exist.\")");
                }
                catch (DnsException)
                {
                    // Expected
                }
            }
            finally
            {
                if (client != null)
                    client.Close();

                if (handler != null)
                    handler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void DynDnsHandler_AddressCache()
        {
            // Verify that the server properly maintains a cache of specified hosts. 

            DynDnsHandler handler = null;

            try
            {
                handler = new DynDnsHandler();
                handler.Start(router, "LillTek.Datacenter.DynDNS.AddressCache", null, null);

                // Wait for everything to spin up.

                Thread.Sleep(10000);

                // Verify that we have cached addresses for www.lilltek.com and www.google.com.

                var addressCache = handler.GetCachedAddresses();
                bool foundLillTek = false;
                bool foundGoogle = false;

                foreach (var entry in addressCache)
                {
                    switch (entry.HostName.ToLower())
                    {
                        case "www.lilltek.com.":

                            foundLillTek = entry.Addresses != null && entry.Addresses.Length > 0;
                            break;

                        case "www.google.com.":

                            foundGoogle = entry.Addresses != null && entry.Addresses.Length > 0;
                            break;
                    }
                }

                Assert.IsTrue(foundLillTek);
                Assert.IsTrue(foundGoogle);

                // Now verify that CNAME lookups that alias to these hosts include A records.

                DnsResponse response;
                bool hasARecords;

                // test1.lilltek.com is an alias for www.lilltek.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test1.lilltek.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                hasARecords = false;
                foreach (var record in response.Additional)
                {
                    if (record.RRType == DnsRRType.A)
                    {
                        hasARecords = true;
                        break;
                    }
                }

                Assert.IsTrue(hasARecords);

                // test1.lilltek.com is an alias for www.google.com

                response = DnsResolver.Query(IPAddress.Loopback, new DnsRequest(DnsFlag.NONE, "test2.lilltek.com.", DnsQType.CNAME), TimeSpan.FromSeconds(2));

                hasARecords = false;
                foreach (var record in response.Additional)
                {
                    if (record.RRType == DnsRRType.A)
                    {
                        hasARecords = true;
                        break;
                    }
                }

                Assert.IsTrue(hasARecords);
            }
            finally
            {
                if (handler != null)
                    handler.Stop();
            }
        }
    }
}

