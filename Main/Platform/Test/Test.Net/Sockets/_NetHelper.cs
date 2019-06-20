//-----------------------------------------------------------------------------
// FILE:        _NetHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the NetHelper class

using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Diagnostics;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _NetHelper
    {

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_SerializeIPv6()
        {
            IPAddress ipAddr;
            byte[] buf;

            ipAddr = IPAddress.Parse("1.2.3.4");
            buf = NetHelper.SerializeIPv6(ipAddr);

            Assert.AreEqual(16, buf.Length);
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(0, buf[i]);

            for (int i = 4; i < 8; i++)
                Assert.AreEqual(0xFF, buf[i]);

            Assert.AreEqual(0, buf[8]);
            Assert.AreEqual(1, buf[9]);
            Assert.AreEqual(0, buf[10]);
            Assert.AreEqual(2, buf[11]);
            Assert.AreEqual(0, buf[12]);
            Assert.AreEqual(3, buf[13]);
            Assert.AreEqual(0, buf[14]);
            Assert.AreEqual(4, buf[15]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_IsIPAddress()
        {
            Assert.IsTrue(NetHelper.IsIPAddress("0.0.0.0"));
            Assert.IsTrue(NetHelper.IsIPAddress("1.2.3.4"));
            Assert.IsTrue(NetHelper.IsIPAddress("255.255.255.255"));

            Assert.IsFalse(NetHelper.IsIPAddress(null));
            Assert.IsFalse(NetHelper.IsIPAddress("..."));
            Assert.IsFalse(NetHelper.IsIPAddress("1"));
            Assert.IsFalse(NetHelper.IsIPAddress("1."));
            Assert.IsFalse(NetHelper.IsIPAddress("1.2"));
            Assert.IsFalse(NetHelper.IsIPAddress("1.2."));
            Assert.IsFalse(NetHelper.IsIPAddress("1.2.3"));
            Assert.IsFalse(NetHelper.IsIPAddress("1.2.3."));
            Assert.IsFalse(NetHelper.IsIPAddress(" 1.2.3.4"));
            Assert.IsFalse(NetHelper.IsIPAddress("1.2.3.4 "));
            Assert.IsFalse(NetHelper.IsIPAddress("1.a.3."));
            Assert.IsFalse(NetHelper.IsIPAddress("256.2.3.4"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_IPEndPoint_Hash()
        {
            // Verify that the hash related methods in IPEndPoint
            // actually work.

            IPEndPoint ep1, ep2;

            ep1 = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            ep2 = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            Assert.AreEqual(ep1.GetHashCode(), ep2.GetHashCode());
            Assert.IsTrue(ep1.Equals(ep2));

            ep1 = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            ep2 = new IPEndPoint(IPAddress.Parse("10.0.0.2"), 10);
            Assert.AreNotEqual(ep1.GetHashCode(), ep2.GetHashCode());
            Assert.IsFalse(ep1.Equals(ep2));

            ep1 = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            ep2 = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 11);
            Assert.AreNotEqual(ep1.GetHashCode(), ep2.GetHashCode());
            Assert.IsFalse(ep1.Equals(ep2));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_GetInterfaceIndex()
        {
            IPAddress address = NetHelper.GetActiveAdapter();
            int index = NetHelper.GetNetworkAdapterIndex(address);
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            bool found;

            found = false;
            foreach (UnicastIPAddressInformation uIPInfo in adapters[index].GetIPProperties().UnicastAddresses)
                if (uIPInfo.Address.Equals(address))
                {
                    found = true;
                    break;
                }

            Assert.IsTrue(found);

            try
            {
                NetHelper.GetNetworkAdapterIndex(IPAddress.Parse("1.0.0.1"));
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(SocketException));
                Assert.AreEqual((int)SocketError.AddressNotAvailable, ((SocketException)e).ErrorCode);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_GetMacAddress()
        {
            byte[] macAddress;

            macAddress = NetHelper.GetMacAddress();
            Assert.IsNotNull(macAddress);
            Assert.IsTrue(macAddress.Length > 0);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_GetCanonicalHost()
        {
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalHost("lilltek.com"));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalHost("lilltek.com."));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void NetHelper_GetCanonicalSecondLevelHost()
        {
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("lilltek.com"));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("lilltek.com."));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("www.lilltek.com"));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("www.lilltek.com."));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("xxx.www.lilltek.com"));
            Assert.AreEqual("lilltek.com.", NetHelper.GetCanonicalSecondLevelHost("xxx.www.lilltek.com."));
        }
    }
}

