//-----------------------------------------------------------------------------
// FILE:        _DnsServer.cs
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
    public class _DnsServer
    {
        private void OnDnsRequest(DnsServer server, DnsServerEventArgs args)
        {
            DnsRequest request = args.Request;
            DnsResponse response = new DnsResponse(request);

            if (request.QType == DnsQType.A || request.QType == DnsQType.CNAME)
            {
                response.Flags |= DnsFlag.AA;
                response.RCode = DnsFlag.RCODE_OK;
                response.Answers.Add(new A_RR(request.QName, IPAddress.Parse("1.2.3.4"), 1));
            }
            else
                response.RCode = DnsFlag.RCODE_NOTIMPL;

            args.Response = response;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsServer_Test()
        {
            DnsServer dnsServer = null;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();
            DnsResponse response;
            A_RR aRR;

            try
            {
                dnsServer = new DnsServer();
                dnsServer.RequestEvent += new DnsServerDelegate(OnDnsRequest);
                dnsServer.Start(new DnsServerSettings());

                response = DnsResolver.Query(activeAdapter, new DnsRequest(DnsFlag.NONE, "foo.com.", DnsQType.A), TimeSpan.FromSeconds(10));
                Assert.AreEqual(DnsFlag.RCODE_OK, response.RCode);

                aRR = (A_RR)response.Answers[0];
                Assert.AreEqual(IPAddress.Parse("1.2.3.4"), aRR.Address);
            }
            finally
            {
                dnsServer.Stop();
            }
        }
    }
}

