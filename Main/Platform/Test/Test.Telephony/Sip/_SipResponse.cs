//-----------------------------------------------------------------------------
// FILE:        _SipResponse.cs
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
    public class _SipResponse
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipResponse_Serialize()
        {
            SipResponse response;
            string input;
            string output;

            input =
@"SIP/2.0 200 OK
Via: SIP/2.0/UDP 192.168.1.200
Content-Length: 0

";
            response = (SipResponse)SipMessage.Parse(Helper.ToUTF8(input), true);
            Assert.AreEqual("SIP/2.0", response.SipVersion);
            Assert.AreEqual(SipStatus.OK, response.Status);
            Assert.AreEqual("OK", response.ReasonPhrase);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", response[SipHeader.Via].Text);
            Assert.AreEqual("0", response[SipHeader.ContentLength].Text);

            output = response.ToString();
            Assert.AreEqual(input, output);
            CollectionAssert.AreEqual(Helper.ToUTF8(input), response.ToArray());

            // Header continuation

            input =
@"SIP/2.0 200 OK
Via: SIP/2.0/UDP
 192.168.1.200
Content-Length: 0

";
            response = (SipResponse)SipMessage.Parse(Helper.ToUTF8(input), true);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", response[SipHeader.Via].Text);
            Assert.AreEqual("0", response[SipHeader.ContentLength].Text);

            // Data with Content-Length header

            input =
@"SIP/2.0 200 OK
Via: SIP/2.0/UDP
 192.168.1.200
Content-Length: 4

";
            response = (SipResponse)SipMessage.Parse(Helper.Concat(Helper.ToUTF8(input), new byte[] { 0, 1, 2, 3 }), true);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", response[SipHeader.Via].Text);
            Assert.AreEqual("4", response[SipHeader.ContentLength].Text);
            Assert.AreEqual(4, response.ContentLength);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, response.Contents);

            // Data with no Content-Length header

            input =
@"SIP/2.0 200 OK
Via: 
 SIP/2.0/UDP
 192.168.1.200

";
            response = (SipResponse)SipMessage.Parse(Helper.Concat(Helper.ToUTF8(input), new byte[] { 0, 1, 2, 3 }), true);
            Assert.AreEqual("SIP/2.0/UDP 192.168.1.200", response[SipHeader.Via].Text);
            Assert.IsNull(response[SipHeader.ContentLength]);
            Assert.AreEqual(4, response.ContentLength);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, response.Contents);
        }
    }
}

