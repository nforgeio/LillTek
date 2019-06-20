//-----------------------------------------------------------------------------
// FILE:        _DnsSerialization.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    /// <summary>
    /// Test the serialization of the DNS resource record
    /// classes by comparing the results to actual DNS 
    /// requests and responses captured from the network.
    /// </summary>
    [TestClass]
    public class _DnsSerialization
    {
        private byte[] Serialize(DnsMessage message)
        {
            byte[] packet;
            byte[] output;
            int cb;

            output = message.FormatPacket(out cb);
            packet = new byte[cb];
            Array.Copy(output, packet, cb);

            return packet;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_NameCompression()
        {
            DnsMessage message = new DnsMessage();
            Dictionary<string, int> namePtrs = new Dictionary<string, int>();
            int offset;
            byte[] packet;
            string name;

            message.SetNamePtrs(namePtrs);

            // Verify that labels greater than 63 characters throw
            // an exception.

            try
            {
                namePtrs.Clear();
                packet = new byte[512];
                offset = 0;
                message.WriteName(packet, ref offset, new String('a', 64) + ".com.");
                Assert.Fail("Exception expected");
            }
            catch
            {
            }

            // Verify that zero length internal labels throw
            // an exception.

            try
            {
                namePtrs.Clear();
                packet = new byte[512];
                offset = 0;
                message.WriteName(packet, ref offset, "test..com.");
                Assert.Fail("Exception expected");
            }
            catch
            {
            }

            // Verify that relative domain names throw an exception.

            try
            {
                namePtrs.Clear();
                packet = new byte[512];
                offset = 0;
                message.WriteName(packet, ref offset, "test.com");
                Assert.Fail("Exception expected");
            }
            catch
            {
            }

            // Verify writing the root domain by itself.

            namePtrs.Clear();
            packet = new byte[512];
            offset = 0;
            message.WriteName(packet, ref offset, ".");
            Assert.AreEqual(1, offset);
            Assert.AreEqual(0, packet[0]);
            Assert.AreEqual(0, namePtrs.Count);

            // Verify writing a domain with a single label.

            namePtrs.Clear();
            packet = new byte[512];
            offset = 0;
            message.WriteName(packet, ref offset, "a.");
            Assert.AreEqual(3, offset);
            Assert.AreEqual(1, packet[0]);
            Assert.AreEqual('a', (char)packet[1]);
            Assert.AreEqual(0, packet[2]);
            Assert.AreEqual(1, namePtrs.Count);
            Assert.AreEqual(0, namePtrs["a"]);

            // Verify writing a domain with a three labels.

            namePtrs.Clear();
            packet = new byte[512];
            offset = 0;
            message.WriteName(packet, ref offset, "a.b.c.");
            Assert.AreEqual(7, offset);
            Assert.AreEqual(1, packet[0]);
            Assert.AreEqual('a', (char)packet[1]);
            Assert.AreEqual(1, packet[2]);
            Assert.AreEqual('b', (char)packet[3]);
            Assert.AreEqual(1, packet[4]);
            Assert.AreEqual('c', (char)packet[5]);
            Assert.AreEqual(0, packet[6]);

            Assert.AreEqual(3, namePtrs.Count);
            Assert.AreEqual(0, namePtrs["a.b.c"]);
            Assert.AreEqual(2, namePtrs["b.c"]);
            Assert.AreEqual(4, namePtrs["c"]);

            // Write the entire three label domain and verify
            // that only a pointer is written.

            namePtrs.Clear();
            packet = new byte[512];
            offset = 0;
            message.WriteName(packet, ref offset, "a.b.c.");
            message.WriteName(packet, ref offset, "a.b.c.");
            Assert.AreEqual(9, offset);
            Assert.AreEqual(0xC0, packet[7]);
            Assert.AreEqual(0, packet[8]);

            // Verify that after writing "a.b.c." and then
            // writing "z.b.c." that a pointer is written
            // for the "b.c." portion.

            namePtrs.Clear();
            packet = new byte[512];
            offset = 0;
            message.WriteName(packet, ref offset, "a.b.c.");
            message.WriteName(packet, ref offset, "z.b.c.");
            Assert.AreEqual(11, offset);
            Assert.AreEqual(1, packet[7]);
            Assert.AreEqual('z', (char)packet[8]);
            Assert.AreEqual(0xC0, packet[9]);
            Assert.AreEqual(2, packet[10]);

            // Verify that we can read the two domains just written.

            offset = 0;
            message.ReadName(packet, ref offset, out name);
            Assert.AreEqual("a.b.c.", name);
            message.ReadName(packet, ref offset, out name);
            Assert.AreEqual("z.b.c.", name);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_SOA_Request()
        {
            // Request packet captured for: 
            //
            //      nslookup -type=soa lilltek.com.

            const string raw =
@" 
                              00 07 01 00 00 01
00 00 00 00 00 00 07 6C 69 6C 6C 74 65 6B 03 63
6F 6D 00 00 06 00 01   
";

            byte[] packet = Helper.FromHex(raw);
            DnsRequest message;

            // Test parsing

            message = new DnsRequest();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.SOA, message.QType);
            Assert.AreEqual("lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) == 0);

            Assert.AreEqual(0, message.Answers.Count);
            Assert.AreEqual(0, message.Authorities.Count);

            // Test rendering

            CollectionAssert.AreEqual(packet, Serialize(message));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_SOA_Response()
        {
            // Response packet captured for: 
            //
            //      nslookup -type=soa lilltek.com.

            const string raw =
@" 
                              00 07 81 80 00 01
00 00 00 01 00 00 07 6C 69 6C 6C 74 65 6B 03 63
6F 6D 00 00 06 00 01 C0 0C 00 06 00 01 00 00 2A
30 00 39 06 70 61 72 6B 31 39 0C 73 65 63 75 72
65 73 65 72 76 65 72 03 6E 65 74 00 03 64 6E 73
05 6A 6F 6D 61 78 C0 3D 77 82 0C F4 00 00 70 80
00 00 1C 20 00 09 3A 80 00 01 51 80
 ";

            byte[] packet = Helper.FromHex(raw);
            DnsResponse message;
            SOA_RR soa_rr;

            // Test parsing

            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.SOA, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);

            Assert.AreEqual(0, message.Answers.Count);

            Assert.AreEqual(1, message.Authorities.Count);
            Assert.AreEqual(DnsRRType.SOA, message.Authorities[0].RRType);
            soa_rr = (SOA_RR)message.Authorities[0];
            Assert.AreEqual("lilltek.com.", soa_rr.RName);
            Assert.AreEqual(DnsRRType.SOA, soa_rr.RRType);
            Assert.AreEqual(DnsQClass.IN, soa_rr.QClass);
            Assert.AreEqual(10800, soa_rr.TTL);
            Assert.AreEqual("park19.secureserver.net.", soa_rr.Primary);
            Assert.AreEqual("dns.jomax.net.", soa_rr.AdminEmail);
            Assert.AreEqual((uint)2005011700, soa_rr.Serial);
            Assert.AreEqual((uint)28800, soa_rr.Refresh);
            Assert.AreEqual((uint)7200, soa_rr.Retry);
            Assert.AreEqual((uint)604800, soa_rr.Expire);
            Assert.AreEqual((uint)86400, soa_rr.Minimum);

            // Test rendering

            CollectionAssert.AreEqual(packet, Serialize(message));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_CNAME_Request()
        {
            // Request packet captured for: 
            //
            //      nslookup -type=cname lilltek.com.

            const string raw =
@" 
                              00 03 01 00 00 01
00 00 00 00 00 00 07 6C 69 6C 6C 74 65 6B 03 63
6F 6D 00 00 05 00 01
";

            byte[] packet = Helper.FromHex(raw);
            DnsRequest message;

            // Test parsing

            message = new DnsRequest();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.CNAME, message.QType);
            Assert.AreEqual("lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) == 0);

            Assert.AreEqual(0, message.Answers.Count);
            Assert.AreEqual(0, message.Authorities.Count);

            // Test rendering

            CollectionAssert.AreEqual(packet, Serialize(message));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_CNAME_Response()
        {
            // Response packet captured for: 
            //
            //      nslookup -type=cname lilltek.com.
            //
            // which returned an SOA record in the authority section.

            const string rawReferral =
@" 
                              00 03 84 00 00 01
00 00 00 01 00 00 07 6C 69 6C 6C 74 65 6B 03 63
6F 6D 00 00 05 00 01 C0 0C 00 06 00 01 00 01 51
80 00 39 06 70 61 72 6B 31 39 0C 73 65 63 75 72
65 73 65 72 76 65 72 03 6E 65 74 00 03 64 6E 73
05 6A 6F 6D 61 78 C0 3D 77 82 0C F4 00 00 70 80
00 00 1C 20 00 09 3A 80 00 01 51 80
";

            // Response packet captured for: 
            //
            //      nslookup -type=a www.lilltek.com. cuba.islandpassport.com. 

            const string rawAnswer =
@" 
                              00 02 81 80 00 01
00 02 00 00 00 00 03 77 77 77 07 6C 69 6C 6C 74
65 6B 03 63 6F 6D 00 00 01 00 01 C0 0C 00 05 00
01 00 00 0C 9F 00 02 C0 10 C0 2D 00 01 00 01 00
00 0C 9F 00 04 45 36 2D F9  
";

            byte[] packet;
            DnsResponse message;
            A_RR a_rr;
            CNAME_RR cname_rr;

            //-----------------------------------------------------------------
            // Test parsing a response containing a referral response

            // Test parsing

            packet = Helper.FromHex(rawReferral);
            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.CNAME, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.AA) != 0);

            Assert.AreEqual(0, message.Answers.Count);
            Assert.AreEqual(1, message.Authorities.Count);
            Assert.AreEqual(DnsRRType.SOA, message.Authorities[0].RRType);
            Assert.AreEqual("park19.secureserver.net.", ((SOA_RR)message.Authorities[0]).Primary);

            // Test rendering

            CollectionAssert.AreEqual(packet, Serialize(message));

            //-----------------------------------------------------------------
            // Test parsing a response containing an answer response

            // Test parsing

            packet = Helper.FromHex(rawAnswer);
            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.A, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("www.lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.AA) == 0);

            Assert.AreEqual(2, message.Answers.Count);

            Assert.AreEqual(DnsRRType.CNAME, message.Answers[0].RRType);
            cname_rr = (CNAME_RR)message.Answers[0];
            Assert.AreEqual("lilltek.com.", cname_rr.CName);

            Assert.AreEqual(DnsRRType.A, message.Answers[1].RRType);
            a_rr = (A_RR)message.Answers[1];
            Assert.AreEqual(IPAddress.Parse("69.54.45.249"), a_rr.Address);

            Assert.AreEqual(0, message.Authorities.Count);

            // Test rendering.  Note that the DNS server implemented
            // a slight different name compression algorithm so I can't
            // compare the output of my code directly to the raw source
            // packet.  My implementation produced the same packet size
            // but it picked different names to point at.

            packet = Serialize(message);
            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.A, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("www.lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.AA) == 0);

            Assert.AreEqual(2, message.Answers.Count);

            Assert.AreEqual(DnsRRType.CNAME, message.Answers[0].RRType);
            cname_rr = (CNAME_RR)message.Answers[0];
            Assert.AreEqual("lilltek.com.", cname_rr.CName);

            Assert.AreEqual(DnsRRType.A, message.Answers[1].RRType);
            a_rr = (A_RR)message.Answers[1];
            Assert.AreEqual(IPAddress.Parse("69.54.45.249"), a_rr.Address);

            Assert.AreEqual(0, message.Authorities.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_NS_Response()
        {
            // Response packet captured for:
            //
            //      nslookup -type=ns www.lilltek.com. cuba.islandpassport.com

            const string raw =
@" 
                              00 02 81 80 00 01
00 03 00 00 00 02 03 77 77 77 07 6C 69 6C 6C 74
65 6B 03 63 6F 6D 00 00 02 00 01 C0 0C 00 05 00
01 00 00 0D FD 00 02 C0 10 C0 2D 00 02 00 01 00
00 0D FD 00 19 06 70 61 72 6B 32 30 0C 73 65 63
75 72 65 73 65 72 76 65 72 03 6E 65 74 00 C0 2D
00 02 00 01 00 00 0D FD 00 09 06 70 61 72 6B 31
39 C0 42 C0 3B 00 01 00 01 00 01 30 0A 00 04 44
B2 D3 72 C0 60 00 01 00 01 00 01 30 0A 00 04 40
CA A5 86
 ";

            byte[] packet = Helper.FromHex(raw);
            DnsResponse message;
            CNAME_RR cname_rr;
            NS_RR ns_rr;

            // Test parsing

            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(2, message.QID);
            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.NS, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("www.lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);

            Assert.AreEqual(3, message.Answers.Count);

            Assert.AreEqual(DnsRRType.CNAME, message.Answers[0].RRType);
            cname_rr = (CNAME_RR)message.Answers[0];
            Assert.AreEqual("lilltek.com.", cname_rr.CName);

            Assert.AreEqual(DnsRRType.NS, message.Answers[1].RRType);
            ns_rr = (NS_RR)message.Answers[1];
            Assert.AreEqual(3581, ns_rr.TTL);
            Assert.AreEqual("park20.secureserver.net.", ns_rr.NameServer);

            Assert.AreEqual(DnsRRType.NS, message.Answers[2].RRType);
            ns_rr = (NS_RR)message.Answers[2];
            Assert.AreEqual(3581, ns_rr.TTL);
            Assert.AreEqual("park19.secureserver.net.", ns_rr.NameServer);

            // Test rendering.  Note that the DNS server implemented
            // a slight different name compression algorithm so I can't
            // compare the output of my code directly to the raw source
            // packet.

            packet = Serialize(message);
            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(2, message.QID);
            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.NS, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("www.lilltek.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);

            Assert.AreEqual(3, message.Answers.Count);

            Assert.AreEqual(DnsRRType.CNAME, message.Answers[0].RRType);
            cname_rr = (CNAME_RR)message.Answers[0];
            Assert.AreEqual("lilltek.com.", cname_rr.CName);

            Assert.AreEqual(DnsRRType.NS, message.Answers[1].RRType);
            ns_rr = (NS_RR)message.Answers[1];
            Assert.AreEqual(3581, ns_rr.TTL);
            Assert.AreEqual("park20.secureserver.net.", ns_rr.NameServer);

            Assert.AreEqual(DnsRRType.NS, message.Answers[2].RRType);
            ns_rr = (NS_RR)message.Answers[2];
            Assert.AreEqual(3581, ns_rr.TTL);
            Assert.AreEqual("park19.secureserver.net.", ns_rr.NameServer);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void DnsSerialization_MX_Response()
        {
            // Response packet captured for:
            //
            //      nslookup -type=mx lill-home.com. cuba.islandpassport.com

            const string raw =
@" 
                              00 02 81 80 00 01
00 03 00 00 00 02 09 6C 69 6C 6C 2D 68 6F 6D 65
03 63 6F 6D 00 00 0F 00 01 C0 0C 00 0F 00 01 00
00 0D F1 00 09 00 00 04 6D 61 69 6C C0 0C C0 0C
00 0F 00 01 00 00 0D F1 00 1F 00 0A 0A 6D 61 69
6C 73 74 6F 72 65 31 0C 73 65 63 75 72 65 73 65
72 76 65 72 03 6E 65 74 00 C0 0C 00 0F 00 01 00
00 0D F1 00 09 00 00 04 73 6D 74 70 C0 4D C0 42
00 01 00 01 00 00 02 DD 00 04 40 CA A6 0B C0 6D
00 01 00 01 00 00 0D 9A 00 04 40 CA A6 0C
 ";

            byte[] packet = Helper.FromHex(raw);
            DnsResponse message;
            MX_RR mx_rr;
            A_RR a_rr;

            // Test parsing

            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(2, message.QID);
            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.MX, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("lill-home.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);

            Assert.AreEqual(3, message.Answers.Count);

            Assert.AreEqual(DnsRRType.MX, message.Answers[0].RRType);
            mx_rr = (MX_RR)message.Answers[0];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("mail.lill-home.com.", mx_rr.Exchange);
            Assert.AreEqual(0, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(DnsRRType.MX, message.Answers[1].RRType);
            mx_rr = (MX_RR)message.Answers[1];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("mailstore1.secureserver.net.", mx_rr.Exchange);
            Assert.AreEqual(10, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(DnsRRType.MX, message.Answers[2].RRType);
            mx_rr = (MX_RR)message.Answers[2];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("smtp.secureserver.net.", mx_rr.Exchange);
            Assert.AreEqual(0, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(2, message.Additional.Count);

            Assert.AreEqual(DnsRRType.A, message.Additional[0].RRType);
            a_rr = (A_RR)message.Additional[0];
            Assert.AreEqual("mailstore1.secureserver.net.", a_rr.RName);
            Assert.AreEqual(IPAddress.Parse("64.202.166.11"), a_rr.Address);

            Assert.AreEqual(DnsRRType.A, message.Additional[1].RRType);
            a_rr = (A_RR)message.Additional[1];
            Assert.AreEqual("smtp.secureserver.net.", a_rr.RName);
            Assert.AreEqual(IPAddress.Parse("64.202.166.12"), a_rr.Address);

            // Test rendering.  Note that the DNS server implemented
            // a slight different name compression algorithm so I can't
            // compare the output of my code directly to the raw source
            // packet.

            packet = Serialize(message);
            message = new DnsResponse();
            Assert.IsTrue(message.ParsePacket(packet, packet.Length));

            Assert.AreEqual(2, message.QID);
            Assert.AreEqual(DnsOpcode.QUERY, message.Opcode);
            Assert.AreEqual(DnsQClass.IN, message.QClass);
            Assert.AreEqual(DnsQType.MX, message.QType);
            Assert.AreEqual(DnsFlag.RCODE_OK, message.RCode);
            Assert.AreEqual("lill-home.com.", message.QName);
            Assert.IsTrue((message.Flags & DnsFlag.QR) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.TC) == 0);
            Assert.IsTrue((message.Flags & DnsFlag.RD) != 0);
            Assert.IsTrue((message.Flags & DnsFlag.RA) != 0);

            Assert.AreEqual(3, message.Answers.Count);

            Assert.AreEqual(DnsRRType.MX, message.Answers[0].RRType);
            mx_rr = (MX_RR)message.Answers[0];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("mail.lill-home.com.", mx_rr.Exchange);
            Assert.AreEqual(0, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(DnsRRType.MX, message.Answers[1].RRType);
            mx_rr = (MX_RR)message.Answers[1];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("mailstore1.secureserver.net.", mx_rr.Exchange);
            Assert.AreEqual(10, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(DnsRRType.MX, message.Answers[2].RRType);
            mx_rr = (MX_RR)message.Answers[2];
            Assert.AreEqual("lill-home.com.", mx_rr.RName);
            Assert.AreEqual("smtp.secureserver.net.", mx_rr.Exchange);
            Assert.AreEqual(0, mx_rr.Preference);
            Assert.AreEqual(3569, mx_rr.TTL);

            Assert.AreEqual(2, message.Additional.Count);

            Assert.AreEqual(DnsRRType.A, message.Additional[0].RRType);
            a_rr = (A_RR)message.Additional[0];
            Assert.AreEqual("mailstore1.secureserver.net.", a_rr.RName);
            Assert.AreEqual(IPAddress.Parse("64.202.166.11"), a_rr.Address);

            Assert.AreEqual(DnsRRType.A, message.Additional[1].RRType);
            a_rr = (A_RR)message.Additional[1];
            Assert.AreEqual("smtp.secureserver.net.", a_rr.RName);
            Assert.AreEqual(IPAddress.Parse("64.202.166.12"), a_rr.Address);
        }
    }
}

