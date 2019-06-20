//-----------------------------------------------------------------------------
// FILE:        _SipHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipHelper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_GetCompactHeader()
        {
            Assert.AreEqual(SipHeader.CallID, SipHelper.GetLongHeader("i"));
            Assert.AreEqual(SipHeader.Contact, SipHelper.GetLongHeader("m"));
            Assert.AreEqual(SipHeader.ContentEncoding, SipHelper.GetLongHeader("e"));
            Assert.AreEqual(SipHeader.ContentLength, SipHelper.GetLongHeader("l"));
            Assert.AreEqual(SipHeader.ContentType, SipHelper.GetLongHeader("c"));
            Assert.AreEqual(SipHeader.From, SipHelper.GetLongHeader("f"));
            Assert.AreEqual(SipHeader.Subject, SipHelper.GetLongHeader("s"));
            Assert.AreEqual(SipHeader.Supported, SipHelper.GetLongHeader("k"));
            Assert.AreEqual(SipHeader.To, SipHelper.GetLongHeader("t"));
            Assert.AreEqual(SipHeader.Via, SipHelper.GetLongHeader("v"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_GetLongHeader()
        {
            Assert.AreEqual("i", SipHelper.GetCompactHeader(SipHeader.CallID));
            Assert.AreEqual("m", SipHelper.GetCompactHeader(SipHeader.Contact));
            Assert.AreEqual("e", SipHelper.GetCompactHeader(SipHeader.ContentEncoding));
            Assert.AreEqual("l", SipHelper.GetCompactHeader(SipHeader.ContentLength));
            Assert.AreEqual("c", SipHelper.GetCompactHeader(SipHeader.ContentType));
            Assert.AreEqual("f", SipHelper.GetCompactHeader(SipHeader.From));
            Assert.AreEqual("s", SipHelper.GetCompactHeader(SipHeader.Subject));
            Assert.AreEqual("k", SipHelper.GetCompactHeader(SipHeader.Supported));
            Assert.AreEqual("t", SipHelper.GetCompactHeader(SipHeader.To));
            Assert.AreEqual("v", SipHelper.GetCompactHeader(SipHeader.Via));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_TryGetBinding()
        {
            NetworkBinding binding;
            SipTransportType transport;
            IPAddress ipLillTek = Dns.GetHostEntry("www.lilltek.com").AddressList.IPv4Only()[0];

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("127.0.0.1", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("<127.0.0.1>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("\"Jeff\" <127.0.0.1>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("Jeff <127.0.0.1>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("127.0.0.1:1234", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sip:127.0.0.1", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sips:127.0.0.1", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(NetworkPort.SIPS, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sip:127.0.0.1:1234", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sips:127.0.0.1:1234", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("<sip:127.0.0.1:1234;transport=tcp>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.TCP, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("<sip:127.0.0.1:1234;transport=udp>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.UDP, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("<sip:127.0.0.1:1234;transport=tls>", out binding, out transport));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.TLS, transport);

            // Host lookups

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("www.lilltek.com", out binding, out transport));
            Assert.AreEqual(ipLillTek, binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("www.lilltek.com:1234", out binding, out transport));
            Assert.AreEqual(ipLillTek, binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sip:www.lilltek.com", out binding, out transport));
            Assert.AreEqual(ipLillTek, binding.Address);
            Assert.AreEqual(NetworkPort.SIP, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sips:www.lilltek.com", out binding, out transport));
            Assert.AreEqual(ipLillTek, binding.Address);
            Assert.AreEqual(NetworkPort.SIPS, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);

            Assert.IsTrue(SipHelper.TryGetRemoteBinding("sip:www.lilltek.com:1234", out binding, out transport));
            Assert.AreEqual(ipLillTek, binding.Address);
            Assert.AreEqual(1234, binding.Port);
            Assert.AreEqual(SipTransportType.Unspecified, transport);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_GenerateCallID()
        {
            string id;

            id = SipHelper.GenerateCallID();
            Assert.IsNotNull(id);
            Assert.IsTrue(id.Length > 0);
            Assert.AreNotEqual(id, SipHelper.GenerateCallID());

            // Make sure that we never see these characters: '/' and '+'

            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(-1, SipHelper.GenerateCallID().IndexOfAny(new char[] { '/', '+' }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_GenerateTagID()
        {
            string id;

            id = SipHelper.GenerateTagID();
            Assert.IsNotNull(id);
            Assert.IsTrue(id.Length > 0);
            Assert.AreNotEqual(id, SipHelper.GenerateTagID());

            // Make sure that we never see these characters: '/' and '+'

            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(-1, SipHelper.GenerateTagID().IndexOfAny(new char[] { '/', '+' }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHelper_GenerateBranchID()
        {
            string id;

            id = SipHelper.GenerateBranchID();
            Assert.IsNotNull(id);
            Assert.IsTrue(id.Length > 0);
            Assert.IsTrue(id.StartsWith(SipHelper.BranchPrefix));
            Assert.AreNotEqual(id, SipHelper.GenerateBranchID());

            // Make sure that we never see these characters: '/' and '+'

            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(-1, SipHelper.GenerateBranchID().IndexOfAny(new char[] { '/', '+' }));
        }
    }
}

