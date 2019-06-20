//-----------------------------------------------------------------------------
// FILE:        _ChannelEP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for ChannelEP class

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ChannelEP
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ChannelEP_Tcp()
        {
            ChannelEP ep;

            ep = new ChannelEP("tcp://127.0.0.1:55");
            Assert.AreEqual(Transport.Tcp, ep.Transport);
            Assert.AreEqual(IPAddress.Loopback, ep.NetEP.Address);
            Assert.AreEqual(55, ep.NetEP.Port);
            Assert.AreEqual("tcp://127.0.0.1:55", ep.ToString());

            ep = new ChannelEP("TCP://127.0.0.1:98");
            Assert.AreEqual(Transport.Tcp, ep.Transport);
            Assert.AreEqual(IPAddress.Loopback, ep.NetEP.Address);
            Assert.AreEqual(98, ep.NetEP.Port);
            Assert.AreEqual("tcp://127.0.0.1:98", ep.ToString());

            ep = "TCP://1.2.3.4:1001";
            Assert.AreEqual(Transport.Tcp, ep.Transport);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), ep.NetEP.Address);
            Assert.AreEqual(1001, ep.NetEP.Port);
            Assert.AreEqual("tcp://1.2.3.4:1001", (string)ep);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ChannelEP_Udp()
        {
            ChannelEP ep;

            ep = new ChannelEP("udp://127.0.77.1:55");
            Assert.AreEqual(Transport.Udp, ep.Transport);
            Assert.AreEqual(IPAddress.Parse("127.0.77.1"), ep.NetEP.Address);
            Assert.AreEqual(55, ep.NetEP.Port);
            Assert.AreEqual("udp://127.0.77.1:55", ep.ToString());

            ep = new ChannelEP("UDP://127.0.77.1:104");
            Assert.AreEqual(Transport.Udp, ep.Transport);
            Assert.AreEqual(IPAddress.Parse("127.0.77.1"), ep.NetEP.Address);
            Assert.AreEqual(104, ep.NetEP.Port);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ChannelEP_Multicast()
        {
            ChannelEP ep;

            ep = new ChannelEP("mcast://255.255.255.255:78");
            Assert.AreEqual(Transport.Multicast, ep.Transport);
            Assert.AreEqual(IPAddress.Broadcast, ep.NetEP.Address);
            Assert.AreEqual(78, ep.NetEP.Port);
            Assert.AreEqual("mcast://*:78", ep.ToString());

            ep = new ChannelEP("mcast://*:39101");
            Assert.AreEqual(Transport.Multicast, ep.Transport);
            Assert.AreEqual(IPAddress.Broadcast, ep.NetEP.Address);
            Assert.AreEqual(39101, ep.NetEP.Port);

            ep = new ChannelEP("Mcast://*:39101");
            Assert.AreEqual(Transport.Multicast, ep.Transport);
            Assert.AreEqual(IPAddress.Broadcast, ep.NetEP.Address);
            Assert.AreEqual(39101, ep.NetEP.Port);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ChannelEP_Errors()
        {
            try
            {
                new ChannelEP("");
                Assert.Fail("FormatException expected.");
            }
            catch (FormatException)
            {
            }

            try
            {
                new ChannelEP("foo://127.0.0.1:10");
                Assert.Fail("FormatException expected.");
            }
            catch (FormatException)
            {
            }

            try
            {
                new ChannelEP("ep://127.0.0.1");
                Assert.Fail("FormatException expected.");
            }
            catch (FormatException)
            {
            }

            try
            {
                new ChannelEP("ep://127.0.0.1:");
                Assert.Fail("FormatException expected.");
            }
            catch (FormatException)
            {
            }

            try
            {
                new ChannelEP("ep://127.0.0.1:xx");
                Assert.Fail("FormatException expected.");
            }
            catch (FormatException)
            {
            }
        }
    }
}

