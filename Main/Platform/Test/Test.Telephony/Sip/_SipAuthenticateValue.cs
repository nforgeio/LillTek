//-----------------------------------------------------------------------------
// FILE:        _SipAuthenticateValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipAuthenticateValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipAuthenticateValue_Basic()
        {
            SipAuthenticateValue v;
            string s;

            v = new SipAuthenticateValue(" Digest algorithm=MD5, realm=\"asterisk\", nonce=\"5c9dda7a\"");
            Assert.AreEqual("MD5", v["algorithm"]);
            Assert.AreEqual("asterisk", v["realm"]);
            Assert.IsNull(v["domain"]);
            Assert.AreEqual("5c9dda7a", v["nonce"]);
            Assert.IsNull(v["opaque"]);
            Assert.IsNull(v["stale"]);

            s = v.ToString();
            Assert.IsTrue(s.StartsWith("Digest"));
            Assert.IsTrue(s.IndexOf("algorithm=MD5") != -1);
            Assert.IsTrue(s.IndexOf("realm=\"asterisk\"") != -1);
            Assert.IsTrue(s.IndexOf("domain=") == -1);
            Assert.IsTrue(s.IndexOf("nonce=\"5c9dda7a\"") != -1);
            Assert.IsTrue(s.IndexOf("opaque=") == -1);
            Assert.IsTrue(s.IndexOf("stale=") == -1);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipAuthenticateValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipAuthenticateValue v;

            Assert.IsNull(message.GetHeader<SipAuthenticateValue>(SipHeader.WWWAuthenticate));

            message.AddHeader(SipHeader.WWWAuthenticate, new SipAuthenticateValue("Digest algorithm=MD5, realm=\"asterisk\", nonce=\"5c9dda7a\""));
            v = message.GetHeader<SipAuthenticateValue>(SipHeader.WWWAuthenticate);
            Assert.IsNotNull(v);
            Assert.AreEqual("MD5", v.Algorithm);
            Assert.AreEqual("asterisk", v.Realm);
        }
    }
}


