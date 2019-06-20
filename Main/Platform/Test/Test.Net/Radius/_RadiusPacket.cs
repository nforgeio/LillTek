//-----------------------------------------------------------------------------
// FILE:        _RadiusPacket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Testing;

namespace LillTek.Net.Radius.Test
{
    [TestClass]
    public class _RadiusPacket
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusPacket_Serialization()
        {
            RadiusPacket packet;
            byte[] packetBytes;
            byte[] authenticator;

            // No attributes.

            authenticator = Crypto.Rand(16);
            packet = new RadiusPacket(RadiusCode.AccessRequest, 55, authenticator);
            packetBytes = packet.ToArray();
            packet = new RadiusPacket(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 77), packetBytes, packetBytes.Length);

            Assert.AreEqual(RadiusCode.AccessRequest, packet.Code);
            Assert.AreEqual(55, packet.Identifier);
            CollectionAssert.AreEqual(authenticator, packet.Authenticator);
            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 77), packet.SourceEP);
            Assert.AreEqual(0, packet.Attributes.Count);

            // Some attributes.

            authenticator = Crypto.Rand(16);
            packet = new RadiusPacket(RadiusCode.AccessRequest, 55, authenticator,
                                             new RadiusAttribute(RadiusAttributeType.ImplementationFirst, new byte[] { 0, 1, 2, 3, 4, 5 }),
                                             new RadiusAttribute(RadiusAttributeType.ImplementationLast, 0x55667788),
                                             new RadiusAttribute(RadiusAttributeType.ExperimentalFirst, IPAddress.Parse("4.3.2.1")),
                                             new RadiusAttribute(RadiusAttributeType.ExperimentalLast, "Hello World!"));
            packetBytes = packet.ToArray();
            packet = new RadiusPacket(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 77), packetBytes, packetBytes.Length);

            Assert.AreEqual(RadiusCode.AccessRequest, packet.Code);
            CollectionAssert.AreEqual(authenticator, packet.Authenticator);
            Assert.AreEqual(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 77), packet.SourceEP);
            Assert.AreEqual(4, packet.Attributes.Count);

            Assert.AreEqual(RadiusAttributeType.ImplementationFirst, packet.Attributes[0].Type);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, packet.Attributes[0].Value);

            Assert.AreEqual(RadiusAttributeType.ImplementationLast, packet.Attributes[1].Type);
            CollectionAssert.AreEqual(new byte[] { 0x55, 0x66, 0x77, 0x88 }, packet.Attributes[1].Value);

            Assert.AreEqual(RadiusAttributeType.ExperimentalFirst, packet.Attributes[2].Type);
            CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1 }, packet.Attributes[2].Value);

            Assert.AreEqual(RadiusAttributeType.ExperimentalLast, packet.Attributes[3].Type);
            CollectionAssert.AreEqual(Helper.ToUTF8("Hello World!"), packet.Attributes[3].Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusPacket_GetAttribute()
        {
            RadiusPacket packet;
            byte[] byteValue;
            int intValue;
            string stringValue;
            IPAddress ipValue;

            packet = new RadiusPacket(RadiusCode.AccessRequest, 55, Crypto.Rand(16),
                                      new RadiusAttribute(RadiusAttributeType.ImplementationFirst, new byte[] { 0, 1, 2, 3, 4, 5 }),
                                      new RadiusAttribute(RadiusAttributeType.ImplementationLast, 0x55667788),
                                      new RadiusAttribute(RadiusAttributeType.ExperimentalFirst, IPAddress.Parse("4.3.2.1")),
                                      new RadiusAttribute(RadiusAttributeType.ExperimentalLast, "Hello World!"));

            Assert.IsFalse(packet.GetAttributeAsBinary(RadiusAttributeType.LoginLatGroup, out byteValue));
            Assert.IsFalse(packet.GetAttributeAsInteger(RadiusAttributeType.LoginLatGroup, out intValue));
            Assert.IsFalse(packet.GetAttributeAsText(RadiusAttributeType.LoginLatGroup, out stringValue));
            Assert.IsFalse(packet.GetAttributeAsAddress(RadiusAttributeType.LoginLatGroup, out ipValue));

            Assert.IsTrue(packet.GetAttributeAsBinary(RadiusAttributeType.ImplementationFirst, out byteValue));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, byteValue);

            Assert.IsTrue(packet.GetAttributeAsInteger(RadiusAttributeType.ImplementationLast, out intValue));
            Assert.AreEqual(0x55667788, intValue);

            Assert.IsTrue(packet.GetAttributeAsAddress(RadiusAttributeType.ExperimentalFirst, out ipValue));
            Assert.AreEqual(IPAddress.Parse("4.3.2.1"), ipValue);

            Assert.IsTrue(packet.GetAttributeAsText(RadiusAttributeType.ExperimentalLast, out stringValue));
            Assert.AreEqual("Hello World!", stringValue);
        }

        private string GetPassword(int count)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < count; i++)
                sb.Append((char)(i + 1));

            return sb.ToString();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusPacket_PasswordEncryption()
        {
            // Test passwords from 1 to 128 characters in length.

            for (int i = 1; i < 128; i++)
            {
                string password = GetPassword(i);
                RadiusPacket packet;

                packet = new RadiusPacket(RadiusCode.AccessRequest, 55, Crypto.Rand(16));
                packet.Attributes.Add(new RadiusAttribute(RadiusAttributeType.UserPassword, packet.EncryptUserPassword(password, "secret")));
                Assert.AreNotEqual(password, Helper.FromAnsi(packet.Attributes[0].Value, 0, password.Length));
                Assert.AreEqual(password, packet.DecryptUserPassword(packet.Attributes[0].Value, "secret"));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusPacket_RFC_Examples()
        {
            const string secret = "xyzzy5461";
            RadiusPacket request;
            RadiusPacket response;
            byte[] raw;
            string sValue;
            byte[] bValue;
            IPAddress aValue;
            int iValue;

            // Verifies packet serialization against some examples in RFC 2865.
            //
            // 7.1.  User Telnet to Specified Host

            raw = Helper.FromHex(@"

01 00 00 38 0f 40 3f 94 73 97 80 57 bd 83 d5 cb
98 f4 22 7a 01 06 6e 65 6d 6f 02 12 0d be 70 8d
93 d4 13 ce 31 96 e4 3f 78 2a 0a ee 04 06 c0 a8
01 10 05 06 00 00 00 03
");
            request = new RadiusPacket(new IPEndPoint(IPAddress.Parse("192.168.1.16"), 4001), raw, raw.Length);
            Assert.AreEqual(RadiusCode.AccessRequest, request.Code);
            Assert.AreEqual(0, request.Identifier);
            Assert.AreEqual(4, request.Attributes.Count);

            Assert.IsTrue(request.GetAttributeAsText(RadiusAttributeType.UserName, out sValue));
            Assert.AreEqual("nemo", sValue);

            Assert.IsTrue(request.GetAttributeAsBinary(RadiusAttributeType.UserPassword, out bValue));
            Assert.AreEqual("arctangent", request.DecryptUserPassword(bValue, secret));

            Assert.IsTrue(request.GetAttributeAsAddress(RadiusAttributeType.NasIpAddress, out aValue));
            Assert.AreEqual(IPAddress.Parse("192.168.1.16"), aValue);

            Assert.IsTrue(request.GetAttributeAsInteger(RadiusAttributeType.NasPort, out iValue));
            Assert.AreEqual(3, iValue);

            // Verify the response

            response = new RadiusPacket(RadiusCode.AccessAccept, 0, null);
            response.Attributes.Add(new RadiusAttribute(RadiusAttributeType.ServiceType, 1));
            response.Attributes.Add(new RadiusAttribute(RadiusAttributeType.LoginService, 0));
            response.Attributes.Add(new RadiusAttribute(RadiusAttributeType.LoginIpHost, IPAddress.Parse("192.168.1.3")));

            response.ComputeResponseAuthenticator(request, secret);

            CollectionAssert.AreEqual(Helper.FromHex(@"

      02 00 00 26 86 fe 22 0e 76 24 ba 2a 10 05 f6 bf
      9b 55 e0 b2 06 06 00 00 00 01 0f 06 00 00 00 00
      0e 06 c0 a8 01 03
"),
                response.ToArray());
        }
    }
}

