//-----------------------------------------------------------------------------
// FILE:        _UdpBroadcastMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;
using LillTek.Testing;

namespace LillTek.Net.Broadcast
{
    [TestClass]
    public class _UdpBroadcastMessage
    {
        SymmetricKey sharedKey = new SymmetricKey(UdpBroadcastHelper.DefaultSharedKey);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcastMessage_Serialize()
        {
            // Verify that we can serialize and deserialize a message.

            UdpBroadcastMessage msg;
            byte[] buffer;
            IPAddress address = Helper.ParseIPAddress("10.1.2.3");

            msg = new UdpBroadcastMessage(UdpBroadcastMessageType.ClientRegister, address, 5, new byte[] { 0, 1, 2, 3, 4 });
            msg.TimeStampUtc = new DateTime(2010, 3, 19);
            Assert.AreEqual(UdpBroadcastMessageType.ClientRegister, msg.MessageType);
            Assert.AreEqual(5, msg.BroadcastGroup);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, msg.Payload);
            Assert.AreEqual(address, msg.SourceAddress);

            buffer = msg.ToArray(sharedKey);
            msg = new UdpBroadcastMessage(buffer, sharedKey);

            Assert.AreEqual(UdpBroadcastMessageType.ClientRegister, msg.MessageType);
            Assert.AreEqual(new DateTime(2010, 3, 19), msg.TimeStampUtc);
            Assert.AreEqual(5, msg.BroadcastGroup);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, msg.Payload);
            Assert.AreEqual(address, msg.SourceAddress);

            // Verify that the message envelope size constant is correct.

            Assert.IsTrue(UdpBroadcastMessage.EnvelopeSize >= buffer.Length - msg.Payload.Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_MessageNulls()
        {
            UdpBroadcastMessage msg;

            msg = new UdpBroadcastMessage(UdpBroadcastMessageType.ServerRegister, 10);

            Assert.AreEqual(10, msg.BroadcastGroup);
            CollectionAssert.AreEqual(new byte[0], msg.Payload);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcastMessage_BadPacket()
        {
            try
            {
                new UdpBroadcastMessage(new byte[] { 0, 1, 2, 3, 4 }, sharedKey);
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(FormatException));
            }
        }
    }
}

