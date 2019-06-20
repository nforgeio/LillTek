//-----------------------------------------------------------------------------
// FILE:        _SipHeaderCollection.cs
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
    public class _SipHeaderCollection
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeaderCollection_Basic()
        {
            SipHeaderCollection headers;
            int p1, p2;
            string s;

            headers = new SipHeaderCollection();
            headers.Add(SipHeader.Via, "SIP/2.0/UDP pc33.atlanta.com;branch=z9hG4bK-0000");
            headers.Add(SipHeader.Via, "SIP/2.0/UDP www.lilltek.com;branch=z9hG4bK-1234");
            headers.Add(SipHeader.MaxForwards, SipHelper.MaxForwards);
            headers.Add(SipHeader.To, "Bob <sip:bob@biloxi.com>");
            headers.Add(SipHeader.ContentLength, "142");

            Assert.AreEqual("SIP/2.0/UDP pc33.atlanta.com;branch=z9hG4bK-0000, SIP/2.0/UDP www.lilltek.com;branch=z9hG4bK-1234",
                            headers[SipHeader.Via].FullText);

            s = headers.ToString();
            Assert.IsTrue(s.EndsWith("\r\n\r\n"));

            // Verify the priority rendering order.

            p1 = s.IndexOf("Via:");
            p2 = s.IndexOf(SipHeader.MaxForwards);
            Assert.IsTrue(p1 < p2);

            p1 = s.IndexOf("Max-Forwards:");
            p2 = s.IndexOf(SipHeader.To);
            Assert.IsTrue(p1 < p2);

            Assert.AreEqual(SipHelper.MaxForwards, headers[SipHeader.MaxForwards].Text);
            Assert.IsNull(headers["XXX"]);
        }

        private string Serialize(SipHeaderCollection headers)
        {
            StringBuilder sb = new StringBuilder();

            headers.Serialize(sb);
            return sb.ToString();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeaderCollection_CompactNames()
        {
            SipHeaderCollection headers;

            headers = new SipHeaderCollection();
            headers.Add(SipHeader.CallID, "10");
            Assert.IsFalse(headers.HasCompactHeaders);
            Assert.AreEqual("Call-ID: 10\r\n\r\n", Serialize(headers));

            headers = new SipHeaderCollection();
            headers.Add("i", "10");
            Assert.IsTrue(headers.HasCompactHeaders);
            Assert.AreEqual("i: 10\r\n\r\n", Serialize(headers));
            Assert.AreEqual("10", headers["i"].Text);
            Assert.AreEqual("10", headers[SipHeader.CallID].Text);

            headers = new SipHeaderCollection();
            headers[SipHeader.CallID] = new SipHeader(SipHeader.CallID, "10");
            Assert.IsFalse(headers.HasCompactHeaders);
            Assert.AreEqual("Call-ID: 10\r\n\r\n", Serialize(headers));

            headers = new SipHeaderCollection();
            headers["i"] = new SipHeader("i", "10");
            Assert.IsTrue(headers.HasCompactHeaders);
            Assert.AreEqual("i: 10\r\n\r\n", Serialize(headers));
            Assert.AreEqual("10", headers["i"].Text);
            Assert.AreEqual("10", headers[SipHeader.CallID].Text);

            headers = new SipHeaderCollection();
            headers.Add(SipHeader.CallID, new SipHeader(SipHeader.CallID, "10"));
            Assert.IsFalse(headers.HasCompactHeaders);
            Assert.AreEqual("Call-ID: 10\r\n\r\n", Serialize(headers));

            headers = new SipHeaderCollection();
            headers.Add("i", new SipHeader("i", "10"));
            Assert.IsTrue(headers.HasCompactHeaders);
            Assert.AreEqual("i: 10\r\n\r\n", Serialize(headers));
            Assert.AreEqual("10", headers["i"].Text);
            Assert.AreEqual("10", headers[SipHeader.CallID].Text);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeaderCollection_Append()
        {
            SipHeaderCollection headers;

            headers = new SipHeaderCollection();
            headers.Append("Test", "1");
            Assert.AreEqual("Test: 1\r\n\r\n", headers.ToString());

            headers.Append("Test", "2");
            Assert.AreEqual("Test: 1, 2\r\n\r\n", headers.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeaderCollection_Prepend()
        {
            SipHeaderCollection headers;

            headers = new SipHeaderCollection();
            headers.Prepend("Test", "1");
            Assert.AreEqual("Test: 1\r\n\r\n", headers.ToString());

            headers.Prepend("Test", "2");
            Assert.AreEqual("Test: 2, 1\r\n\r\n", headers.ToString());
        }
    }
}

