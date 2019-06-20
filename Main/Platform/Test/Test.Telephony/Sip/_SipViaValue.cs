//-----------------------------------------------------------------------------
// FILE:        _SipViaValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipViaValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipViaValue_Basic()
        {
            SipViaValue v;

            v = new SipViaValue(SipTransportType.UDP, "127.0.0.1");
            Assert.AreEqual("SIP/2.0", v.Version);
            Assert.AreEqual(SipTransportType.UDP, v.TransportType);
            Assert.AreEqual("127.0.0.1", v.SentBy);
            Assert.AreEqual("SIP/2.0/UDP 127.0.0.1", v.Text);
            Assert.AreEqual("SIP/2.0/UDP 127.0.0.1", v.ToString());

            v = new SipViaValue("SIP/2.0/TCP 127.0.0.1;received=1.2.3.4;rport=10;maddr=2.3.4.5;branch=xyz");
            Assert.AreEqual("SIP/2.0", v.Version);
            Assert.AreEqual(SipTransportType.TCP, v.TransportType);
            Assert.AreEqual("127.0.0.1", v.SentBy);
            Assert.AreEqual("1.2.3.4", v.Received);
            Assert.AreEqual("10", v.RPort);
            Assert.AreEqual("2.3.4.5", v.MAddr);
            Assert.AreEqual("xyz", v.Branch);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipViaValue_SendByBinding()
        {
            SipViaValue v;

            v = new SipViaValue("SIP/2.0/TCP 127.0.0.1");
            Assert.AreEqual("127.0.0.1:5060", v.SentByBinding.ToString());
            Assert.IsFalse(v.SentByBinding.IsHost);

            v = new SipViaValue("SIP/2.0/TCP 127.0.0.1:1234");
            Assert.AreEqual("127.0.0.1:1234", v.SentByBinding.ToString());
            Assert.IsFalse(v.SentByBinding.IsHost);

            v = new SipViaValue("SIP/2.0/UDP 127.0.0.1");
            Assert.AreEqual("127.0.0.1:5060", v.SentByBinding.ToString());
            Assert.IsFalse(v.SentByBinding.IsHost);

            v = new SipViaValue("SIP/2.0/TLS 127.0.0.1");
            Assert.AreEqual("127.0.0.1:5061", v.SentByBinding.ToString());
            Assert.IsFalse(v.SentByBinding.IsHost);

            v = new SipViaValue("SIP/2.0/TLS www.lilltek.com");
            Assert.AreEqual("www.lilltek.com:5061", v.SentByBinding.ToString());
            Assert.IsTrue(v.SentByBinding.IsHost);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipViaValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipViaValue v;

            Assert.IsNull(message.GetHeader<SipViaValue>("Test"));

            message.AddHeader(SipHeader.Via, new SipViaValue("SIP/2.0/UDP 1.2.3.4;received=5.6.7.8"));
            v = message.GetHeader<SipViaValue>(SipHeader.Via);
            Assert.IsNotNull(v);
            Assert.AreEqual(SipTransportType.UDP, v.TransportType);
            Assert.AreEqual("1.2.3.4", v.SentBy);
            Assert.AreEqual("5.6.7.8", v.Received);
        }
    }
}

