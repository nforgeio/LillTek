//-----------------------------------------------------------------------------
// FILE:        _SipAuthorizationValue.cs
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
    public class _SipAuthorizationValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipAuthorizationValue_Basic()
        {
            SipAuthorizationValue v;
            string s;

            v = new SipAuthorizationValue("Digest username=\"jslill\",realm=\"asterisk\",nonce=\"5c9dda7a\",uri=\"sip:sip4.vitelity.net\",response=\"394487a182712a1c348c3861ee6465f8\",algorithm=MD5");
            Assert.AreEqual("MD5", v["algorithm"]);
            Assert.AreEqual("asterisk", v["realm"]);
            Assert.AreEqual("5c9dda7a", v["nonce"]);
            Assert.AreEqual("sip:sip4.vitelity.net", v["uri"]);
            Assert.AreEqual("394487a182712a1c348c3861ee6465f8", v["response"]);
            Assert.AreEqual("MD5", v["algorithm"]);

            s = v.ToString();
            Assert.IsTrue(s.StartsWith("Digest"));
            Assert.IsTrue(s.IndexOf("algorithm=MD5") != -1);
            Assert.IsTrue(s.IndexOf("realm=\"asterisk\"") != -1);
            Assert.IsTrue(s.IndexOf("nonce=\"5c9dda7a\"") != -1);
            Assert.IsTrue(s.IndexOf("uri=\"sip:sip4.vitelity.net\"") != -1);
            Assert.IsTrue(s.IndexOf("response=\"394487a182712a1c348c3861ee6465f8\"") != -1);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipAuthorizationValue_Compute()
        {
            SipAuthenticateValue vChallenge;
            SipAuthorizationValue vResponse;

            vChallenge = new SipAuthenticateValue("Digest realm=\"asterisk\", nonce=\"5c9dda7a\"");
            vResponse = new SipAuthorizationValue(vChallenge, "jslill", "q0jsrd7y", "REGISTER", "sip:sip4.vitelity.net");

            Assert.AreEqual("394487a182712a1c348c3861ee6465f8", vResponse.Response);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipAuthorizationValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipAuthenticateValue vChallenge;
            SipAuthorizationValue vResponse;
            SipAuthorizationValue v;

            vChallenge = new SipAuthenticateValue("Digest realm=\"asterisk\", nonce=\"5c9dda7a\"");
            vResponse = new SipAuthorizationValue(vChallenge, "jslill", "q0jsrd7y", "REGISTER", "sip:sip4.vitelity.net");

            Assert.IsNull(message.GetHeader<SipAuthorizationValue>(SipHeader.Authorization));

            message.AddHeader(SipHeader.Authorization, vResponse);
            v = message.GetHeader<SipAuthorizationValue>(SipHeader.Authorization);
            Assert.IsNotNull(v);
            Assert.AreEqual(vResponse.Response, v.Response);
        }
    }
}



