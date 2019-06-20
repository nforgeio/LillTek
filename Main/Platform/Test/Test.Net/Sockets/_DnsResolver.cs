//-----------------------------------------------------------------------------
// FILE:        _DnsResolver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _DnsResolver
    {
        private const string NoNetMsg       = "Network must be connected with a DNS server configured.";
        private const string NoRecursionMsg = "The DNS server must be able to process recursive requests.";

        private TimeSpan timeout = TimeSpan.FromSeconds(10.0);

        // These tests assume that the computer is connected to a network, that
        // a DNS server is configured for the connection (either manually or
        // via DHCP) and that the DNS server is configured to handle recursive 
        // requests.

        private IPAddress GetDns()
        {
            IPAddress dns = NetHelper.GetDnsServer();

            if (dns == null)
                Assert.Inconclusive(NoNetMsg);

            return dns;
        }

        [TestInitialize]
        public void Initalize()
        {
            NetTrace.Start();
            NetTrace.Enable(DnsResolver.TraceSubSystem, 0);

            DnsResolver.Bind();
        }

        [TestCleanup]
        public void Cleanup()
        {
            DnsResolver.Bind();
            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_Single_Lookup()
        {
            IPAddress dnsEP = GetDns();
            DnsRequest request;
            DnsResponse response;
            IPAddress[] stdAddrs;
            List<IPAddress> addrs;

            // This test assumes that www.lilltek.com maps to
            // a single IP address.  I'm going to compare the
            // address I get back from the standard Dns class
            // with what I get back from DnsResolver.

            stdAddrs = Dns.GetHostAddresses("www.lilltek.com.");
            if (stdAddrs.Length != 1)
                Assert.Inconclusive("www.lilltek.com is configured with multiple A records.");

            request = new DnsRequest(DnsFlag.RD, "www.lilltek.com.", DnsQType.A);
            response = DnsResolver.Query(dnsEP, request, timeout);

            if ((response.Flags & DnsFlag.RA) == 0)
                Assert.Inconclusive(NoRecursionMsg);

            Assert.AreEqual(DnsFlag.RCODE_OK, response.RCode);

            addrs = new List<IPAddress>();
            for (int i = 0; i < response.Answers.Count; i++)
            {

                if (response.Answers[i].RRType == DnsRRType.A)
                    addrs.Add(((A_RR)response.Answers[i]).Address);
            }

            Assert.AreEqual(1, addrs.Count);
            Assert.AreEqual(stdAddrs[0], addrs[0]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_Multiple_Lookup()
        {
            // This test issues simultanious A queries for the 13 
            // global name servers and then verifies that the DnsResolver
            // was able to correctly correlate the responses to the
            // requests.

            IPAddress dns = GetDns();
            string[] gtld = new string[] {

                "a.gtld-servers.net.",
                "b.gtld-servers.net.",
                "c.gtld-servers.net.",
                "d.gtld-servers.net.",
                "e.gtld-servers.net.",
                "f.gtld-servers.net.",
                "g.gtld-servers.net.",
                "h.gtld-servers.net.",
                "i.gtld-servers.net.",
                "j.gtld-servers.net.",
                "k.gtld-servers.net.",
                "l.gtld-servers.net.",
                "m.gtld-servers.net.",
            };

            DnsRequest[] requests = new DnsRequest[gtld.Length];
            DnsResponse[] responses = new DnsResponse[gtld.Length];
            IAsyncResult[] rgAR = new IAsyncResult[gtld.Length];

            for (int j = 0; j < 10; j++)
            {    
                // Repeat the test 10 times just for fun

                for (int i = 0; i < gtld.Length; i++)
                    requests[i] = new DnsRequest(DnsFlag.RD, gtld[i], DnsQType.A);

                for (int i = 0; i < gtld.Length; i++)
                    rgAR[i] = DnsResolver.BeginQuery(dns, requests[i], timeout, null, null);

                for (int i = 0; i < gtld.Length; i++)
                    responses[i] = DnsResolver.EndQuery(rgAR[i]);

                for (int i = 0; i < gtld.Length; i++)
                {

                    Assert.AreEqual(requests[i].QID, responses[i].QID);
                    Assert.AreEqual(requests[i].QName, responses[i].QName);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_Multiple_Lookup_Bind()
        {
            // This test repeats the A_Multiple test, but this time
            // after binding the DnsResolver to 5 client side sockets.

            IPAddress dns = GetDns();
            string[] gtld = new string[] {

                "a.gtld-servers.net.",
                "b.gtld-servers.net.",
                "c.gtld-servers.net.",
                "d.gtld-servers.net.",
                "e.gtld-servers.net.",
                "f.gtld-servers.net.",
                "g.gtld-servers.net.",
                "h.gtld-servers.net.",
                "i.gtld-servers.net.",
                "j.gtld-servers.net.",
                "k.gtld-servers.net.",
                "l.gtld-servers.net.",
                "m.gtld-servers.net.",
            };

            DnsRequest[] requests = new DnsRequest[gtld.Length];
            DnsResponse[] responses = new DnsResponse[gtld.Length];
            IAsyncResult[] rgAR = new IAsyncResult[gtld.Length];
            IPEndPoint[] clientEPs;

            clientEPs = new IPEndPoint[10];
            for (int i = 0; i < clientEPs.Length; i++)
                clientEPs[i] = new IPEndPoint(IPAddress.Any, 0);

            DnsResolver.Bind(clientEPs, 128 * 1024, 128 * 1024);

            try
            {
                for (int j = 0; j < 10; j++)
                {    
                    // Repeat the test 10 times just for fun

                    for (int i = 0; i < gtld.Length; i++)
                        requests[i] = new DnsRequest(DnsFlag.RD, gtld[i], DnsQType.A);

                    for (int i = 0; i < gtld.Length; i++)
                        rgAR[i] = DnsResolver.BeginQuery(dns, requests[i], timeout, null, null);

                    for (int i = 0; i < gtld.Length; i++)
                        responses[i] = DnsResolver.EndQuery(rgAR[i]);

                    for (int i = 0; i < gtld.Length; i++)
                    {
                        Assert.AreEqual(requests[i].QID, responses[i].QID);
                        Assert.AreEqual(requests[i].QName, responses[i].QName);
                    }
                }
            }
            finally
            {
                DnsResolver.Bind();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_Bind_CancelException()
        {
            IPAddress dns = IPAddress.Loopback;
            DnsRequest request;
            DnsResponse response;
            IAsyncResult ar;

            // This test sends a DNS request to the loopback address
            // assuming that there is no DNS server running locally
            // and then calls DnsResolver.Bind() which should terminate
            // the query with a CancelException.

            request = new DnsRequest(DnsFlag.RD, "www.lilltek.com.", DnsQType.A);
            ar = DnsResolver.BeginQuery(dns, request, timeout, null, null);

            DnsResolver.Bind();
            try
            {
                response = DnsResolver.EndQuery(ar);
                Assert.Fail("Expected a CancelException");
            }
            catch (CancelException)
            {
            }
            catch
            {
                Assert.Fail("Expected a CancelException");
            }
            finally
            {
                Thread.Sleep(1000);     // $todo(jeff.lill): 
                //
                // This test causes some problems for some other
                // tests unless I add this (I'm not sure why).
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_CancelAll()
        {
            IPAddress dns = IPAddress.Loopback;
            DnsRequest request;
            DnsResponse response;
            IAsyncResult ar;

            // This test sends a DNS request to the loopback address
            // assuming that there is no DNS server running locally
            // and then calls DnsResolver.CancelAll() which should 
            // terminate the query with a CancelException.

            request = new DnsRequest(DnsFlag.RD, "www.lilltek.com.", DnsQType.A);
            ar = DnsResolver.BeginQuery(dns, request, timeout, null, null);

            DnsResolver.CancelAll();
            try
            {
                response = DnsResolver.EndQuery(ar);
                Assert.Fail("Expected a CancelException");
            }
            catch (CancelException)
            {
            }
            catch
            {
                Assert.Fail("Expected a CancelException");
            }
            finally
            {
                Thread.Sleep(1000);     // $todo(jeff.lill): 

                // This test causes some problems for some other
                //tests unless I add this (I'm not sure why).
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_Timeout()
        {
            IPAddress dns = IPAddress.Loopback;
            DnsRequest request;
            DnsResponse response;
            DateTime start;
            TimeSpan wait;

            // This test sends a DNS request to the loopback address
            // assuming that there is no DNS server running locally.
            // The call should time out after the appropriate wait.

            request = new DnsRequest(DnsFlag.RD, "www.lilltek.com.", DnsQType.A);
            start = SysTime.Now;

            try
            {
                response = DnsResolver.Query(dns, request, TimeSpan.FromSeconds(2.0));
                Assert.Fail("Expected a TimeoutException");
            }
            catch (TimeoutException)
            {
                wait = SysTime.Now - start;
                Assert.IsTrue(wait >= TimeSpan.FromSeconds(2.0));
            }
            catch
            {
                Assert.Fail("Expected a TimeoutException");
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsResolver_QueryWithRetry()
        {
            IPAddress[] nameServers = new IPAddress[] { GetDns(), IPAddress.Loopback };
            DnsRequest request = new DnsRequest(DnsFlag.RD, "www.lilltek.com.", DnsQType.A);

            // Verify that the thing works without any errors.

            for (int i = 0; i < 10; i++)
                DnsResolver.QueryWithRetry(new IPAddress[] { GetDns() }, request, TimeSpan.FromSeconds(2), 1);

            // Verify that a request guaranteed to fail with no retries throws an exception

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    DnsResolver.QueryWithRetry(new IPAddress[] { IPAddress.Loopback }, request, TimeSpan.FromSeconds(2), 1);
                    Assert.Fail("Expected an exception.");
                }
                catch
                {
                }
            }

            // Verify that a request guaranteed to fail with one retry throws an exception

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    DnsResolver.QueryWithRetry(new IPAddress[] { IPAddress.Loopback }, request, TimeSpan.FromSeconds(2), 2);
                    Assert.Fail("Expected an exception.");
                }
                catch
                {
                }
            }

            // This test sends a series of DNS requests to the loopback address
            // and the locally configured DNS server using the retry enabled 
            // query.  The query should be successful in all cases.

            for (int i = 0; i < 10; i++)
                DnsResolver.QueryWithRetry(nameServers, request, TimeSpan.FromSeconds(2), 2);
        }
    }
}

