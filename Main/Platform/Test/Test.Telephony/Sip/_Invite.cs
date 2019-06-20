//-----------------------------------------------------------------------------
// FILE:        _Invite.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _Invite
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Invite_Call_Vitelity()
        {
            Assert.Inconclusive("Manual Test: Comment this out to perform this test.");

            // Attempt an outbound call against the Vitelity.com service

            SipBasicCore core = null;
            SipDialog dialog = null;
            SipCoreSettings settings;
            SipRequest request;
            string sdp;

            try
            {
                // These settings are hardcoded for my Vitelity.com account
                // and my home network.  This test also assumes that the router
                // is forwarding UDP port 5060 packets to the test computer.

                settings = new SipCoreSettings();
                settings.LocalContact = "sip:jslill@" + Dns.GetHostEntry("www.lilltek.com").AddressList.IPv4Only()[0].ToString() + ":5060";
                settings.UserName = "jslill";
                settings.Password = "q0jsrd7y";

                core = new SipBasicCore(settings);
                core.Start();

                //core.StartAutoRegistration("sip:sip4.vitelity.net","sip:jslill@sip4.vitelity.net");
                //Assert.IsTrue(core.AutoRegistration);
                //Assert.IsTrue(core.IsRegistered);

                // Make a call to my cellphone

                request = new SipRequest(SipMethod.Invite, "sip:2063561304@sip4.vitelity.net", null);
                request.SetHeader(SipHeader.To, new SipContactValue("sip:2063561304@sip4.vitelity.net"));
                request.SetHeader(SipHeader.From, new SipContactValue("sip:jslill@sip4.vitelity.net"));
                request.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);

                sdp =
@"v=0
o=- 0 2 IN IP4 192.168.1.200
s=LillTek SIP
c=IN IP4 192.168.1.200
t=0 0
m=audio 29318 RTP/AVP 107 119 100 106 0 105 98 8 101
a=alt:1 1 : AEnD+akt rmmTsDRh 192.168.1.200 29318
a=fmtp:101 0-15
a=rtpmap:107 BV32/16000
a=rtpmap:119 BV32-FEC/16000
a=rtpmap:100 SPEEX/16000
a=rtpmap:106 SPEEX-FEC/16000
a=rtpmap:105 SPEEX-FEC/8000
a=rtpmap:98 iLBC/8000
a=rtpmap:101 telephone-event/8000
a=sendrecv
";
                request.Contents = Helper.ToUTF8(sdp);

                dialog = core.CreateDialog(request, (SipContactValue)settings.LocalContact, null);

                Thread.Sleep(30000);    // Wait 30 seconds to monitor the packets via NetMon

                //core.StopAutoRegistration();
            }
            finally
            {
                if (core != null)
                    core.Stop();
            }
        }
    }
}

