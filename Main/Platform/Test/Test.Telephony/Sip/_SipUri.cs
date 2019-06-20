//-----------------------------------------------------------------------------
// FILE:        _SipUri.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipUri
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Basic()
        {
            SipUri uri;

            uri = new SipUri("jeff", "lilltek.com");
            Assert.AreEqual("sip:jeff@lilltek.com", uri.ToString());

            uri = new SipUri("jeff", "lilltek.com", 5060);
            Assert.AreEqual("sip:jeff@lilltek.com:5060", uri.ToString());

            uri = new SipUri("jeff", "lilltek.com", 5060);
            uri.IsSecure = true;
            Assert.AreEqual("sips:jeff@lilltek.com:5060", uri.ToString());

            uri = new SipUri("sip:jeff@lilltek.com");
            Assert.IsFalse(uri.IsSecure);
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("sip:jeff@lilltek.com", uri.ToString());

            uri = new SipUri("sips:jeff@lilltek.com:1234");
            Assert.IsTrue(uri.IsSecure);
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("sips:jeff@lilltek.com:1234", uri.ToString());

            uri = (SipUri)"sips:jeff@lilltek.com:1234";
            Assert.IsTrue(uri.IsSecure);
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("sips:jeff@lilltek.com:1234", uri.ToString());

            uri = new SipUri("sip:lilltek.com");
            Assert.IsFalse(uri.IsSecure);
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("sip:lilltek.com", uri.ToString());

            uri = new SipUri("sip:lilltek.com:1234");
            Assert.IsFalse(uri.IsSecure);
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("sip:lilltek.com:1234", uri.ToString());

            Assert.IsTrue(SipUri.TryParse("sip:lilltek.com:1234", out uri));
            Assert.AreEqual("sip:lilltek.com:1234", uri.ToString());

            uri = new SipUri(SipTransportType.UDP, null, "test.com", 55);
            Assert.AreEqual("sip:test.com:55", uri.ToString());

            uri = new SipUri(SipTransportType.TCP, "jeff", "test.com", 55);
            Assert.AreEqual("sip:jeff@test.com:55;transport=tcp", uri.ToString());

            uri = new SipUri(SipTransportType.TLS, new NetworkBinding(IPAddress.Parse("127.0.0.1"), 55));
            Assert.AreEqual("sips:127.0.0.1:55", uri.ToString());

            uri = new SipUri(SipTransportType.TCP, new NetworkBinding("lilltek.com", 55));
            Assert.AreEqual("sip:lilltek.com:55;transport=tcp", uri.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Cast()
        {
            SipUri uri;

            Assert.IsNull((SipUri)(string)null);
            Assert.IsNull((string)(SipUri)null);

            uri = new SipUri(SipTransportType.TCP, new NetworkBinding("lilltek.com", 55));
            Assert.AreEqual("sip:lilltek.com:55;transport=tcp", (string)uri);

            uri = (SipUri)"sip:lilltek.com:55;transport=tcp";
            Assert.AreEqual("sip:lilltek.com:55;transport=tcp", uri.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Parameters()
        {
            SipUri uri;

            uri = new SipUri("sip:jeff@lilltek.com;one=two;three=four");
            Assert.AreEqual("sip:jeff@lilltek.com;one=two;three=four", uri.ToString());
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("two", uri.Parameters["ONE"]);
            Assert.AreEqual("four", uri.Parameters["three"]);

            uri = new SipUri("sip:jeff@lilltek.com:1234;one=two;three=four");
            Assert.AreEqual("sip:jeff@lilltek.com:1234;one=two;three=four", uri.ToString());
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("two", uri.Parameters["ONE"]);
            Assert.AreEqual("four", uri.Parameters["three"]);

            uri = new SipUri("sip:lilltek.com;one=two;three=four");
            Assert.IsFalse(uri.IsSecure);
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("sip:lilltek.com;one=two;three=four", uri.ToString());

            uri = new SipUri("sip:lilltek.com:1234;one=two;three=four");
            Assert.IsFalse(uri.IsSecure);
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("sip:lilltek.com:1234;one=two;three=four", uri.ToString());

            uri = new SipUri("sip:lilltek.com:1234;one=two;three=four");
            Assert.IsNull(uri["five"]);
            uri["five"] = "six";
            Assert.AreEqual("six", uri["five"]);
            Assert.AreEqual("two", uri["one"]);
            uri["one"] = "xxx";
            Assert.AreEqual("xxx", uri["one"]);
            uri["three"] = null;
            Assert.IsFalse(uri.Parameters.ContainsKey("three"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Headers()
        {
            SipUri uri;

            uri = new SipUri("sip:jeff@lilltek.com:1234;one=two;three=four?arg1=value1&arg2=value2");
            Assert.AreEqual("sip:jeff@lilltek.com:1234;one=two;three=four?arg1=value1&arg2=value2", uri.ToString());
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("two", uri.Parameters["ONE"]);
            Assert.AreEqual("four", uri.Parameters["three"]);
            Assert.AreEqual("arg1", uri.Headers[0].Name);
            Assert.AreEqual("value1", uri.Headers[0].FullText);
            Assert.AreEqual("arg2", uri.Headers[1].Name);
            Assert.AreEqual("value2", uri.Headers[1].FullText);

            uri = new SipUri("sip:jeff@lilltek.com:1234?arg1=value1&arg2=value2");
            Assert.AreEqual("sip:jeff@lilltek.com:1234?arg1=value1&arg2=value2", uri.ToString());
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("arg1", uri.Headers[0].Name);
            Assert.AreEqual("value1", uri.Headers[0].FullText);
            Assert.AreEqual("arg2", uri.Headers[1].Name);
            Assert.AreEqual("value2", uri.Headers[1].FullText);

            uri = new SipUri("sip:jeff@lilltek.com?arg1=value1&arg2=value2");
            Assert.AreEqual("sip:jeff@lilltek.com?arg1=value1&arg2=value2", uri.ToString());
            Assert.AreEqual("jeff", uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("arg1", uri.Headers[0].Name);
            Assert.AreEqual("value1", uri.Headers[0].FullText);
            Assert.AreEqual("arg2", uri.Headers[1].Name);
            Assert.AreEqual("value2", uri.Headers[1].FullText);

            uri = new SipUri("sip:lilltek.com?arg1=value1&arg2=value2");
            Assert.AreEqual("sip:lilltek.com?arg1=value1&arg2=value2", uri.ToString());
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(NetworkPort.SIP, uri.Port);
            Assert.AreEqual("arg1", uri.Headers[0].Name);
            Assert.AreEqual("value1", uri.Headers[0].FullText);
            Assert.AreEqual("arg2", uri.Headers[1].Name);
            Assert.AreEqual("value2", uri.Headers[1].FullText);

            uri = new SipUri("sip:lilltek.com:1234?arg1=value1&arg2=value2");
            Assert.AreEqual("sip:lilltek.com:1234?arg1=value1&arg2=value2", uri.ToString());
            Assert.IsNull(uri.User);
            Assert.AreEqual("lilltek.com", uri.Host);
            Assert.AreEqual(1234, uri.Port);
            Assert.AreEqual("arg1", uri.Headers[0].Name);
            Assert.AreEqual("value1", uri.Headers[0].FullText);
            Assert.AreEqual("arg2", uri.Headers[1].Name);
            Assert.AreEqual("value2", uri.Headers[1].FullText);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Clone()
        {
            SipUri uri, clone;

            uri = new SipUri("sip:jeff@lilltek.com:1234;p1=one;p2=two?h1=header1&h2=header2");
            clone = uri.Clone();
            Assert.AreEqual(uri.ToString(), clone.ToString());
            Assert.AreEqual(uri.IsSecure, clone.IsSecure);
            Assert.AreEqual(uri.User, clone.User);
            Assert.AreEqual(uri.Host, clone.Host);
            Assert.AreEqual(uri.Port, clone.Port);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUri_Escaping()
        {
            Assert.Inconclusive("Not implemented.");
        }
    }
}

