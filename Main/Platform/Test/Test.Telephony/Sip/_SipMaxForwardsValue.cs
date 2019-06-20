//-----------------------------------------------------------------------------
// FILE:        _SipMaxForwardsValue.cs
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
    public class _SipMaxForwardsValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipMaxForwardsValue_Basic()
        {
            SipMaxForwardsValue v;

            v = new SipMaxForwardsValue(77);
            Assert.AreEqual(77, v.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipMaxForwardsValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipMaxForwardsValue v;

            Assert.IsNull(message.GetHeader<SipMaxForwardsValue>(SipHeader.MaxForwards));

            message.AddHeader(SipHeader.MaxForwards, new SipMaxForwardsValue(77));
            v = message.GetHeader<SipMaxForwardsValue>(SipHeader.MaxForwards);
            Assert.IsNotNull(v);
            Assert.AreEqual(77, v.Count);
        }
    }
}

