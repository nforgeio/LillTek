//-----------------------------------------------------------------------------
// FILE:        _SdpPayload.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SdpPayload
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SdpPayload_Parse()
        {
            // Test parsing and rendering

            const string packet =
@"v=0
o=jdoe 2890844526 2890842807 IN IP4 10.47.16.5
s=SDP Seminar
i=A Seminar on the session description protocol
u=http://www.example.com/seminars/sdp.pdf
e=j.doe@example.com (Jane Doe)
c=IN IP4 224.2.17.12
a=recvonly
m=audio 49170 RTP/AVP 0
m=video 51372 RTP/AVP 99
a=rtpmap:99 h263-1998/90000
";
            SdpPayload sdp;
            SdpMediaDescription media;
            string s;

            sdp = new SdpPayload(packet);

            Assert.AreEqual(0, sdp.Version);
            Assert.AreEqual("jdoe", sdp.UserName);
            Assert.AreEqual("2890844526", sdp.SessionID);
            Assert.AreEqual("2890842807", sdp.SessionVersion);
            Assert.AreEqual("10.47.16.5", sdp.UnicastAddress);
            Assert.AreEqual("SDP Seminar", sdp.SessionName);
            Assert.AreEqual("A Seminar on the session description protocol", sdp.SessionDescription);
            Assert.AreEqual("http://www.example.com/seminars/sdp.pdf", sdp.Uri);
            Assert.AreEqual("j.doe@example.com (Jane Doe)", sdp.EmailAddress);
            Assert.AreEqual("224.2.17.12", sdp.ConnectionAddress.ToString());
            Assert.AreEqual("recvonly", sdp.Attributes[0]);

            media = sdp.Media[0];
            Assert.AreEqual(SdpMediaType.Audio, media.Media);
            Assert.AreEqual(49170, media.Port);
            Assert.AreEqual(0, media.PortCount);
            Assert.AreEqual(MediaProtocol.RtpAvp, media.Protocol);
            Assert.AreEqual("0", media.Format);

            media = sdp.Media[1];
            Assert.AreEqual(SdpMediaType.Video, media.Media);
            Assert.AreEqual(51372, media.Port);
            Assert.AreEqual(0, media.PortCount);
            Assert.AreEqual(MediaProtocol.RtpAvp, media.Protocol);
            Assert.AreEqual("99", media.Format);
            Assert.AreEqual("rtpmap:99 h263-1998/90000", media.Attributes[0]);

            s = sdp.ToString();
            Assert.AreEqual(packet, s);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SdpPayload_Construct()
        {
            // Build a packet from scratch

            SdpPayload sdp;
            string packet;
            string s;

            sdp = new SdpPayload();
            sdp.UnicastAddress = "192.168.1.200";
            sdp.ConnectionAddress = IPAddress.Parse("192.168.1.200");
            sdp.Media.Add(new SdpMediaDescription(SdpMediaType.Audio, 1010, 2, MediaProtocol.RtpAvp, "22"));

            s = sdp.ToString();

            packet =
@"v=0
o=- 0 0 IN IP4 192.168.1.200
s=LillTek SIP
c=IN IP4 192.168.1.200
m=audio 1010/2 RTP/AVP 22
";
            Assert.AreEqual(packet, s);
        }
    }
}

