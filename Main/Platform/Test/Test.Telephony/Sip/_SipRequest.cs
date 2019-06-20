//-----------------------------------------------------------------------------
// FILE:        _SipRequest.cs
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
    public class _SipRequest
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipRequest_Serialize()
        {
            SipRequest request;
            string input;
            string output;

            input =
@"INVITE sip:jeff.lill@sipserver.com SIP/2.0
Via: SIP/2.0/UDP 192.168.1.200
Content-Length: 0

";
            request = (SipRequest)SipMessage.Parse(Helper.ToUTF8(input), true);
            Assert.AreEqual("SIP/2.0", request.SipVersion);
            Assert.AreEqual("INVITE", request.MethodText);
            Assert.AreEqual(SipMethod.Invite, request.Method);
            Assert.AreEqual("sip:jeff.lill@sipserver.com", request.Uri);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", request[SipHeader.Via].Text);
            Assert.AreEqual("0", request[SipHeader.ContentLength].Text);

            output = request.ToString();
            Assert.AreEqual(input, output);
            CollectionAssert.AreEqual(Helper.ToUTF8(input), request.ToArray());

            // Header continuation

            input =
@"invite sip:jeff.lill@sipserver.com SIP/2.0
Via: SIP/2.0/UDP
 192.168.1.200
Content-Length: 0

";
            request = (SipRequest)SipMessage.Parse(Helper.ToUTF8(input), true);
            Assert.AreEqual("SIP/2.0", request.SipVersion);
            Assert.AreEqual("INVITE", request.MethodText);
            Assert.AreEqual(SipMethod.Invite, request.Method);
            Assert.AreEqual("sip:jeff.lill@sipserver.com", request.Uri);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", request[SipHeader.Via].Text);
            Assert.AreEqual("0", request[SipHeader.ContentLength].Text);

            // Data with Content-Length header

            input =
@"register sip:jeff.lill@sipserver.com SIP/2.0
Via: SIP/2.0/UDP
 192.168.1.200
Content-Length: 4

";
            request = (SipRequest)SipMessage.Parse(Helper.Concat(Helper.ToUTF8(input), new byte[] { 0, 1, 2, 3 }), true);
            Assert.AreEqual("REGISTER", request.MethodText);
            Assert.AreEqual(SipMethod.Register, request.Method);
            Assert.AreEqual("SIP/2.0", request.SipVersion);
            Assert.AreEqual("sip:jeff.lill@sipserver.com", request.Uri);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", request[SipHeader.Via].Text);
            Assert.AreEqual("4", request[SipHeader.ContentLength].Text);
            Assert.AreEqual(4, request.ContentLength);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, request.Contents);

            // Data with no Content-Length header

            input =
@"INVITE sip:jeff.lill@sipserver.com SIP/2.0
Via: 
 SIP/2.0/UDP
 192.168.1.200

";
            request = (SipRequest)SipMessage.Parse(Helper.Concat(Helper.ToUTF8(input), new byte[] { 0, 1, 2, 3 }), true);
            Assert.AreEqual("INVITE", request.MethodText);
            Assert.AreEqual(SipMethod.Invite, request.Method);
            Assert.AreEqual("SIP/2.0", request.SipVersion);
            Assert.AreEqual("sip:jeff.lill@sipserver.com", request.Uri);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", request[SipHeader.Via].Text);
            Assert.IsNull(request[SipHeader.ContentLength]);
            Assert.AreEqual(4, request.ContentLength);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, request.Contents);
        }
    }
}

