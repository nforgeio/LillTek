//-----------------------------------------------------------------------------
// FILE:        _NetworkBinding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests 

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _NetworkBinding
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetworkBinding_Basic()
        {
            NetworkBinding binding;

            binding = new NetworkBinding(IPAddress.Loopback, 55);
            Assert.IsFalse(binding.IsHost);
            Assert.IsNull(binding.Host);
            Assert.AreEqual(IPAddress.Loopback, binding.Address);
            Assert.AreEqual(55, binding.Port);
            Assert.AreEqual("127.0.0.1:55", binding.ToString());

            binding = new NetworkBinding("localhost", 55);
            Assert.IsTrue(binding.IsHost);
            Assert.IsNotNull(binding.Host);
            Assert.AreEqual(IPAddress.Loopback, binding.Address);
            Assert.AreEqual(55, binding.Port);
            Assert.AreEqual("localhost:55", binding.ToString());

            binding = new NetworkBinding("any", 55);
            Assert.AreEqual(IPAddress.Any, binding.Address);
            Assert.AreEqual(55, binding.Port);
            Assert.IsFalse(binding.IsHost);

            Assert.AreEqual(IPAddress.Any, NetworkBinding.Any.Address);
            Assert.AreEqual(0, NetworkBinding.Any.Port);
            Assert.IsNull(NetworkBinding.Any.Host);
            Assert.IsFalse(NetworkBinding.Any.IsHost);
            Assert.AreEqual("0.0.0.0:0", NetworkBinding.Any.ToString());

            Assert.AreNotEqual(NetworkBinding.Parse("1.2.3.4:5").GetHashCode(), NetworkBinding.Parse("2.3.4.5:6").GetHashCode());
            Assert.AreNotEqual(NetworkBinding.Parse("1.2.3.4:5").GetHashCode(), NetworkBinding.Parse("1.2.3.4:6").GetHashCode());
            Assert.AreNotEqual(NetworkBinding.Parse("1.2.3.4:5").GetHashCode(), NetworkBinding.Parse("2.3.4.5:5").GetHashCode());

            Assert.IsTrue(NetworkBinding.Parse("1.2.3.4:5").Equals(NetworkBinding.Parse("1.2.3.4:5")));
            Assert.IsFalse(NetworkBinding.Parse("1.2.3.4:5").Equals(NetworkBinding.Parse("1.2.3.4:6")));
            Assert.IsFalse(NetworkBinding.Parse("1.2.3.5:5").Equals(NetworkBinding.Parse("1.2.3.4:5")));
            Assert.IsFalse(NetworkBinding.Parse("localhost:0").Equals(NetworkBinding.Parse("0.0.0.0:0")));

            binding = new NetworkBinding(IPAddress.Loopback, 55);
            Assert.AreEqual("127.0.0.1", binding.HostOrAddress);
            Assert.IsFalse(binding.IsAny);

            binding = new NetworkBinding("www.lilltek.com", 55);
            Assert.AreEqual("www.lilltek.com", binding.HostOrAddress);
            Assert.IsFalse(binding.IsAny);

            binding = new NetworkBinding("www.lilltek.com", 0);
            Assert.IsTrue(binding.IsAny);

            binding = new NetworkBinding(IPAddress.Any, 55);
            Assert.IsTrue(binding.IsAny);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetworkBinding_Parse()
        {
            NetworkBinding binding;

            binding = NetworkBinding.Parse("0.0.0.0:0");
            Assert.AreEqual(IPAddress.Any, binding.Address);
            Assert.AreEqual(0, binding.Port);

            binding = NetworkBinding.Parse("1.2.3.4:5");
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), binding.Address);
            Assert.AreEqual(5, binding.Port);

            binding = NetworkBinding.Parse("localhost:7890");
            Assert.AreEqual("localhost", binding.Host);
            Assert.AreEqual(7890, binding.Port);
            Assert.AreEqual(IPAddress.Loopback, binding.Address);

            binding = NetworkBinding.Parse("Any:55");
            Assert.AreEqual(IPAddress.Any, binding.Address);
            Assert.AreEqual(55, binding.Port);
            Assert.IsFalse(binding.IsHost);

            binding = NetworkBinding.Parse("ANY");
            Assert.AreEqual(IPAddress.Any, binding.Address);
            Assert.AreEqual(0, binding.Port);
            Assert.IsFalse(binding.IsHost);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetworkBinding_DnsResolve()
        {
            NetworkBinding binding = NetworkBinding.Parse("badhost.UNIT.lilltek.com:55");
            IPAddress address;

            try
            {

                address = binding.Address;
                Assert.Fail("Expected the DNS resolution to fail.  This test will fail if the current DNS server returns answers to a redirect site.  Many cable Internet providers do this.");
            }
            catch (Exception e)
            {

                Assert.IsInstanceOfType(e, typeof(SocketException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetworkBinding_Operators()
        {
            IPEndPoint endPoint;
            NetworkBinding binding;

            endPoint = NetworkBinding.Parse("1.2.3.4:5");
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), endPoint.Address);
            Assert.AreEqual(5, endPoint.Port);

            endPoint = NetworkBinding.Parse("localhost:4001");
            Assert.AreEqual(IPAddress.Loopback, endPoint.Address);
            Assert.AreEqual(4001, endPoint.Port);

            binding = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 88);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), binding.Address);
            Assert.AreEqual(88, binding.Port);
            Assert.IsFalse(binding.IsHost);

            Assert.IsTrue(NetworkBinding.Parse("1.2.3.4:5") == NetworkBinding.Parse("1.2.3.4:5"));
            Assert.IsFalse(NetworkBinding.Parse("1.2.3.4:5") != NetworkBinding.Parse("1.2.3.4:5"));
            Assert.IsTrue(NetworkBinding.Parse("1.2.3.4:5") != NetworkBinding.Parse("1.2.3.4:6"));
            Assert.IsTrue(NetworkBinding.Parse("1.2.3.5:5") != NetworkBinding.Parse("1.2.3.4:5"));
            Assert.IsFalse(NetworkBinding.Parse("1.2.3.4:5") == NetworkBinding.Parse("1.2.3.4:6"));
            Assert.IsFalse(NetworkBinding.Parse("1.2.3.5:5") == NetworkBinding.Parse("1.2.3.4:5"));
            Assert.IsTrue(NetworkBinding.Parse("localhost:55") == NetworkBinding.Parse("localhost:55"));
            Assert.IsFalse(NetworkBinding.Parse("localhost:55") != NetworkBinding.Parse("localhost:55"));
            Assert.IsFalse(NetworkBinding.Parse("localhostx:55") == NetworkBinding.Parse("localhost:55"));
            Assert.IsTrue(NetworkBinding.Parse("localhostx:55") != NetworkBinding.Parse("localhost:55"));
            Assert.IsFalse(NetworkBinding.Parse("localhost:55") == NetworkBinding.Parse("localhost:56"));
            Assert.IsTrue(NetworkBinding.Parse("localhost:55") != NetworkBinding.Parse("localhost:56"));

            Assert.IsTrue((NetworkBinding)null == (NetworkBinding)null);
            Assert.IsFalse((NetworkBinding)null == NetworkBinding.Parse("localhost:55"));
            Assert.IsFalse(NetworkBinding.Parse("localhost:55") == (NetworkBinding)null);

            Assert.IsFalse((NetworkBinding)null != (NetworkBinding)null);
            Assert.IsTrue((NetworkBinding)null != NetworkBinding.Parse("localhost:55"));
            Assert.IsTrue(NetworkBinding.Parse("localhost:55") != (NetworkBinding)null);

            Assert.IsNull((NetworkBinding)((IPEndPoint)null));
            Assert.IsNull((IPEndPoint)((NetworkBinding)null));
        }

        private struct Map
        {
            public string Name;
            public int Port;

            public Map(string name, int Port)
            {
                this.Name = name;
                this.Port = Port;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetworkBinding_KnownPorts()
        {
            Map[] ports = new Map[] {

                new Map("HTTP", 80),
                new Map("SSL", 443),
                new Map("DNS", 53),
                new Map("SMTP", 25),
                new Map("POP3", 110),
                new Map("TELNET", 23),
                new Map("FTP", 21),
                new Map("FTPDATA", 20),
                new Map("SFTP", 22),
                new Map("RADIUS", 1812),
                new Map("AAA", 1645),
                new Map("ECHO", 7),
                new Map("DAYTIME", 13),
                new Map("TFTP", 69),
                new Map("SSH", 22),
                new Map("TIME", 37),
                new Map("NTP", 123),
                new Map("IMAP", 143),
                new Map("SNMP", 161),
                new Map("SNMTRAP", 162),
                new Map("LDAP", 389),
                new Map("LDAPS", 636),
                new Map("HTTP-HEARTBEAT", 9167),
                new Map("NETTRACE", 47743),
                new Map("LILLCOM", 4530)
            };

            foreach (Map map in ports)
                Assert.AreEqual(map.Port, NetworkBinding.Parse("127.0.0.1:" + map.Name).Port);
        }
    }
}

