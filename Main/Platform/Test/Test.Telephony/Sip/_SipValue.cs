//-----------------------------------------------------------------------------
// FILE:        _SipValue.cs
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
    public class _SipValue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipValue_Basic()
        {
            SipValue v = new SipValue();
            string s;
            StringBuilder sb;

            Assert.AreEqual("", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);
            Assert.IsNull(v["test"]);

            v.Text = "Hello";
            Assert.AreEqual("Hello", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v["arg0"] = "hello0";
            Assert.AreEqual("hello0", v["arg0"]);

            v["arg1"] = "hello1";
            Assert.AreEqual("hello0", v["arg0"]);
            Assert.AreEqual("hello1", v["arg1"]);
            Assert.AreEqual("hello1", v["ARG1"]);

            Assert.IsNull(v["test"]);

            s = v.ToString();
            Assert.IsTrue(s.StartsWith("Hello;"));
            Assert.IsTrue(s.IndexOf(";arg0=hello0") != -1);
            Assert.IsTrue(s.IndexOf(";arg1=hello1") != -1);

            sb = new StringBuilder();
            v.Serialize(sb);
            s = sb.ToString();
            Assert.IsTrue(s.StartsWith("Hello;"));
            Assert.IsTrue(s.IndexOf(";arg0=hello0") != -1);
            Assert.IsTrue(s.IndexOf(";arg1=hello1") != -1);

            v = new SipValue(s);
            v["arg1"] = "hello1";
            Assert.AreEqual(2, v.Parameters.Count);
            Assert.AreEqual("hello0", v["arg0"]);
            Assert.AreEqual("hello1", v["arg1"]);
            Assert.IsNull(v["XXX"]);

            v = new SipValue("10");
            Assert.AreEqual(10, v.IntValue);
            v.IntValue = 20;
            Assert.AreEqual("20", v.Text);

            v = new SipValue("Test;received=127.0.0.1;rport");
            Assert.AreEqual("Test", v.Text);
            Assert.AreEqual("127.0.0.1", v["received"]);
            Assert.AreEqual("", v["rport"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipValue_Complex()
        {
            SipValue v;

            v = new SipValue("\"Jeff Lill\" <jeff@lilltek.com>");
            Assert.AreEqual("\"Jeff Lill\" <jeff@lilltek.com>", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff Lill\" <jeff@lilltek.com>;hello=world");
            Assert.AreEqual("\"Jeff Lill\" <jeff@lilltek.com>", v.Text);
            Assert.AreEqual(1, v.Parameters.Count);
            Assert.AreEqual("world", v.Parameters["hello"]);

            v = new SipValue("\"Jeff Lill <jeff@lilltek.com>");
            Assert.AreEqual("\"Jeff Lill <jeff@lilltek.com>", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff Lill\" <jeff@lilltek.com");
            Assert.AreEqual("\"Jeff Lill\" <jeff@lilltek.com", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff \\\"The Lill\\\"\" <jeff@lilltek.com>");
            Assert.AreEqual("\"Jeff \\\"The Lill\\\"\" <jeff@lilltek.com>", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>");
            Assert.AreEqual("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>");
            Assert.AreEqual("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>", v.Text);
            Assert.AreEqual(0, v.Parameters.Count);

            v = new SipValue("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>;hello=world");
            Assert.AreEqual("\"Jeff;Lill\" <jeff@lilltek.com;transport=tcp>", v.Text);
            Assert.AreEqual(1, v.Parameters.Count);
            Assert.AreEqual("world", v.Parameters["hello"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipValue_Message()
        {
            SipRequest message = new SipRequest(SipMethod.Invite, "sip:jeff@lilltek.com", null);
            SipValue v;

            Assert.IsNull(message.GetHeader<SipValue>("Test"));

            message.AddHeader("Test", new SipValue("jeff;hello=world"));
            v = message.GetHeader<SipValue>("Test");
            Assert.IsNotNull(v);
            Assert.AreEqual("jeff", v.Text);
            Assert.AreEqual("world", v["hello"]);
        }
    }
}

