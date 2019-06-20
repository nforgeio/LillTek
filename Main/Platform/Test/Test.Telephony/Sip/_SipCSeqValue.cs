//-----------------------------------------------------------------------------
// FILE:        _SipCSeqValue.cs
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
    public class _SipCSeqValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipCSeqValue_Basic()
        {
            SipCSeqValue v;

            v = new SipCSeqValue(10, "INVITE");
            Assert.AreEqual(10, v.Number);
            Assert.AreEqual("INVITE", v.Method);
            Assert.AreEqual("10 INVITE", v.Text);

            v = new SipCSeqValue("10 INVITE");
            Assert.AreEqual(10, v.Number);
            Assert.AreEqual("INVITE", v.Method);
            Assert.AreEqual("10 INVITE", v.Text);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipCSeqValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipCSeqValue v;

            Assert.IsNull(message.GetHeader<SipCSeqValue>(SipHeader.CSeq));

            message.AddHeader(SipHeader.CSeq, new SipCSeqValue("10 INVITE"));
            v = message.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            Assert.IsNotNull(v);
            Assert.AreEqual("INVITE", v.Method);
            Assert.AreEqual("10 INVITE", v.Text);
        }
    }
}

