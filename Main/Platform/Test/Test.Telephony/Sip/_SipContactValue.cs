//-----------------------------------------------------------------------------
// FILE:        _SipContactValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LillTek.Common;
using LillTek.Telephony.Sip;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipContactValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_Uri()
        {
            SipContactValue v;
            SipUri uri;

            v = new SipContactValue("sip:jeff@lilltek.com");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.IsNull(v.DisplayName);
            Assert.AreEqual("<sip:jeff@lilltek.com>", v.ToString());

            uri = new SipUri("sip:jeff@lilltek.com;transport=tcp");
            v = uri;
            Assert.AreEqual("<sip:jeff@lilltek.com;transport=tcp>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_QuotedUri()
        {
            SipContactValue v;

            v = new SipContactValue("<sip:jeff@lilltek.com>");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.IsNull(v.DisplayName);
            Assert.AreEqual("<sip:jeff@lilltek.com>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_DisplayAndUri()
        {
            SipContactValue v;

            v = new SipContactValue("Jeff sip:jeff@lilltek.com");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.AreEqual("Jeff", v.DisplayName);
            Assert.AreEqual("\"Jeff\"<sip:jeff@lilltek.com>", v.ToString());

            v = new SipContactValue("Internal<sip:1234@192.168.1.200:8899>");
            Assert.AreEqual("sip:1234@192.168.1.200:8899", v.Uri);
            Assert.AreEqual("Internal", v.DisplayName);
            Assert.AreEqual("\"Internal\"<sip:1234@192.168.1.200:8899>", v.ToString());

            // Microsoft Speech Server sometimes does not quote the display
            // name in a SIP URI properly.  I've seen situations where it
            // sends a 180 (Ringing) response with an unquoted display name
            // with a space and an angle bracket quoted URI.  The LillTek
            // SIP stack will handle this by looking for URIs without double
            // quotes but with angle brackets and use the first angle bracket
            // as the display name termination.

            v = new SipContactValue("MSS Gateway<sip:1234@192.168.1.200:8899>");
            Assert.AreEqual("sip:1234@192.168.1.200:8899", v.Uri);
            Assert.AreEqual("MSS Gateway", v.DisplayName);
            Assert.AreEqual("\"MSS Gateway\"<sip:1234@192.168.1.200:8899>", v.ToString());

            v = new SipContactValue("MSS Gateway \t<sip:1234@192.168.1.200:8899>");
            Assert.AreEqual("sip:1234@192.168.1.200:8899", v.Uri);
            Assert.AreEqual("MSS Gateway", v.DisplayName);
            Assert.AreEqual("\"MSS Gateway\"<sip:1234@192.168.1.200:8899>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_QuotedDisplayAndUri()
        {
            SipContactValue v;

            v = new SipContactValue("\"Jeff Lill\" sip:jeff@lilltek.com");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.AreEqual("Jeff Lill", v.DisplayName);
            Assert.AreEqual("\"Jeff Lill\"<sip:jeff@lilltek.com>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_QuotedDisplayAndQuotedUri()
        {
            SipContactValue v;

            v = new SipContactValue("\"Jeff Lill\" <sip:jeff@lilltek.com>");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.AreEqual("Jeff Lill", v.DisplayName);
            Assert.AreEqual("\"Jeff Lill\"<sip:jeff@lilltek.com>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_DisplayEscapeChars()
        {
            SipContactValue v;

            v = new SipContactValue("\"Jeff \\\"The\\\\Lill\\\"\" <sip:jeff@lilltek.com>");
            Assert.AreEqual("sip:jeff@lilltek.com", v.Uri);
            Assert.AreEqual("Jeff \"The\\Lill\"", v.DisplayName);
            Assert.AreEqual("\"Jeff \\\"The\\\\Lill\\\"\"<sip:jeff@lilltek.com>", v.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_SetProperties()
        {
            SipContactValue v;

            v = new SipContactValue(null, "sip:jeff@lilltek.com");
            Assert.AreEqual("<sip:jeff@lilltek.com>", v.ToString());
            Assert.AreEqual("<sip:jeff@lilltek.com>", v.Text);

            v.DisplayName = "Jeff";
            Assert.AreEqual("\"Jeff\"<sip:jeff@lilltek.com>", v.ToString());
            Assert.AreEqual("\"Jeff\"<sip:jeff@lilltek.com>", v.Text);

            v.Uri = "sip:test@lilltek.com";
            Assert.AreEqual("\"Jeff\"<sip:test@lilltek.com>", v.ToString());
            Assert.AreEqual("\"Jeff\"<sip:test@lilltek.com>", v.Text);

            v.Uri = "sip:jeff@lilltek.com";
            v.DisplayName = "Jeff \"The\\Lill\"";
            Assert.AreEqual("\"Jeff \\\"The\\\\Lill\\\"\"<sip:jeff@lilltek.com>", v.ToString());
            Assert.AreEqual("\"Jeff \\\"The\\\\Lill\\\"\"<sip:jeff@lilltek.com>", v.Text);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipContactValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipContactValue v;

            Assert.IsNull(message.GetHeader<SipContactValue>(SipHeader.Contact));

            message.AddHeader(SipHeader.Contact, new SipContactValue("\"Jeff Lill\" <jeff@lilltek.com>;hello=world"));
            v = message.GetHeader<SipContactValue>(SipHeader.Contact);
            Assert.IsNotNull(v);
            Assert.AreEqual("Jeff Lill", v.DisplayName);
            Assert.AreEqual("jeff@lilltek.com", v.Uri);
            Assert.AreEqual("world", v["hello"]);
        }
    }
}

