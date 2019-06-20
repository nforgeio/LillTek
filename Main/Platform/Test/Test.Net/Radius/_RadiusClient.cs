//-----------------------------------------------------------------------------
// FILE:        _RadiusClient.cs
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
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Testing;

namespace LillTek.Net.Radius.Test
{
    [TestClass]
    public class _RadiusClient
    {
        private NetworkBinding Local_RADIUS = new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.RADIUS);
        private NetworkBinding Local_AAA = new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.AAA);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_Interop()
        {
            if (EnvironmentVars.Get("LT_TESTBIN") == null)
                Assert.Inconclusive("[LT_TESTBIN] environment variable does not exist.");

            // Verify that my RADIUS client code can work against a server from
            // another vendor.

            RadiusTestServer server = new RadiusTestServer();
            Dictionary<string, string> users;
            Dictionary<IPAddress, string> devices;
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_AAA, "secret");

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;

            users = new Dictionary<string, string>();
            users.Add("jeff", "password1");
            users.Add("joe", "password2");

            devices = new Dictionary<IPAddress, string>();
            devices.Add(IPAddress.Loopback, "secret");
            devices.Add(NetHelper.GetActiveAdapter(), "secret");

            try
            {
                server.Start(users, devices);
                client.Open(clientSettings);

                Assert.IsTrue(client.Authenticate("", "jeff", "password1"));
                Assert.IsTrue(client.Authenticate("", "joe", "password2"));

                Assert.IsFalse(client.Authenticate("", "jeff", "passwordX"));
                Assert.IsFalse(client.Authenticate("", "billy", "x"));
            }
            finally
            {
                client.Close();
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_ID_WrapAround()
        {
            // Verify that a single port client instance will wrap request IDs
            // properly after ID=255

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 1;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.Normal);

                for (int i = 0; i < 555; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));

                // We should have 555 packets in the deelie with ordered IDs.

                Assert.AreEqual(555, deelie.Packets.Count);
                for (int i = 0; i < 555; i++)
                    Assert.AreEqual((byte)i, deelie.Packets[i].Identifier);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_ID_Exhaustion_SinglePort()
        {
            // Verify that the client throws an exception when it is asked to
            // manage more than 256 parallel authentication requests.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;
            IAsyncResult[] ar;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 1;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthLongDelay);

                ar = new IAsyncResult[257];

                try
                {
                    for (int i = 0; i < ar.Length; i++)
                        ar[i] = client.BeginAuthenticate("r1", "jeff", "password123", null, null);

                    for (int i = 0; i < ar.Length; i++)
                        if (ar[i] != null)
                            client.EndAuthenticate(ar[i]);

                    Assert.Fail("Expected a RadiusException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(RadiusException));
                }
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_ID_Exhaustion_MultiPort()
        {
            // Verify that the client throws an exception when it is asked to
            // manage more than 256 parallel authentication requests.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;
            IAsyncResult[] ar;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 2;
            clientSettings.MaxTransmissions = 1;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthLongDelay);

                ar = new IAsyncResult[clientSettings.PortCount * 256 + 1];

                try
                {
                    for (int i = 0; i < ar.Length; i++)
                        ar[i] = client.BeginAuthenticate("r1", "jeff", "password123", null, null);

                    for (int i = 0; i < ar.Length; i++)
                        if (ar[i] != null)
                            client.EndAuthenticate(ar[i]);

                    Assert.Fail("Expected a RadiusException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(RadiusException));
                }
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_MultiPort()
        {
            // Verify that a multiport enable client actually works by running a bunch
            // of authentications throught the client and then counting the number of
            // source UDP ports we received packets from and verifying that this equals
            // the number of client ports requested.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 5;
            clientSettings.MaxTransmissions = 1;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.Normal);

                for (int i = 0; i < 555; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));

                Dictionary<int, RadiusPacket> packetsByPort = new Dictionary<int, RadiusPacket>();

                foreach (RadiusPacket packet in deelie.Packets)
                    if (!packetsByPort.ContainsKey(packet.SourceEP.Port))
                        packetsByPort.Add(packet.SourceEP.Port, packet);

                Assert.AreEqual(5, packetsByPort.Count);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_LoadConfig()
        {
            RadiusClientSettings settings;

            try
            {
                // Verify that an exception is thrown if no servers or
                // secret is present.

                Config.SetConfig(@"
                
Prefix.Secret = mysecret
                ");

                try
                {
                    settings = RadiusClientSettings.LoadConfig("Prefix");
                    Assert.Fail("Expected an exception");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(RadiusException));
                }

                Config.SetConfig(@"
                
Prefix.Server[0] = localhost:1812
                ");

                try
                {
                    settings = RadiusClientSettings.LoadConfig("Prefix");
                    Assert.Fail("Expected an exception");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(RadiusException));
                }

                // Verify defaults for the other settings

                Config.SetConfig(@"

Prefix.Server[0] = localhost:10
Prefix.Server[1] = 127.0.0.1:11
Prefix.Secret    = mysecret
");
                settings = RadiusClientSettings.LoadConfig("Prefix");
                CollectionAssert.AreEqual(new NetworkBinding[] { NetworkBinding.Parse("localhost:10"), NetworkBinding.Parse("127.0.0.1:11") }, settings.Servers);
                Assert.AreEqual("mysecret", settings.Secret);
                Assert.AreEqual(32768, settings.SocketBuffer);
                Assert.AreEqual(NetworkBinding.Any, settings.NetworkBinding);
                Assert.AreEqual(TimeSpan.FromSeconds(10), settings.RetryInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkTaskInterval);
                Assert.AreEqual(4, settings.MaxTransmissions);
                Assert.AreEqual(4, settings.PortCount);
                Assert.AreEqual(RealmFormat.Email, settings.RealmFormat);

                // Now load some non-default settings

                Config.SetConfig(@"

Prefix.Server[0]        = localhost:10
Prefix.Server[1]        = 127.0.0.1:11
Prefix.Secret           = mysecret
Prefix.NetworkBinding   = 127.0.0.1:0
Prefix.SocketBuffer     = 10000
Prefix.RetryInterval    = 5s
Prefix.BkTaskInterval   = 3s
Prefix.MaxTransmissions = 5
Prefix.PortCount        = 7
Prefix.RealmFormat      = SLASH
");

                settings = RadiusClientSettings.LoadConfig("Prefix");
                CollectionAssert.AreEqual(new NetworkBinding[] { NetworkBinding.Parse("localhost:10"), NetworkBinding.Parse("127.0.0.1:11") }, settings.Servers);
                Assert.AreEqual("mysecret", settings.Secret);
                Assert.AreEqual(10000, settings.SocketBuffer);
                Assert.AreEqual(new NetworkBinding(IPAddress.Loopback, 0), settings.NetworkBinding);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.RetryInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(3), settings.BkTaskInterval);
                Assert.AreEqual(5, settings.MaxTransmissions);
                Assert.AreEqual(7, settings.PortCount);
                Assert.AreEqual(RealmFormat.Slash, settings.RealmFormat);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_Timeout()
        {
            // Verify that the client detects timeouts.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 1;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.IgnoreAllPackets);

                try
                {
                    client.Authenticate("r1", "jeff", "password123");
                    Assert.Fail("Expected a timeout");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_Retry()
        {
            // Verify that the client actually retries sending request packets and 
            // that it used the same ID for both.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 2;

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                client.Open(clientSettings);
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.IgnoreFirstPacket);

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.AreEqual(2, deelie.Packets.Count);
                Assert.AreEqual(deelie.Packets[0].Identifier, deelie.Packets[1].Identifier);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_LoadBalance_SinglePort()
        {
            // Verify that the client actually distributes packets across multiple
            // RADIUS servers with a single port client.

            RadiusServer server1 = new RadiusServer();
            RadiusServer server2 = new RadiusServer();
            RadiusServerSettings server1Settings = new RadiusServerSettings();
            RadiusServerSettings server2Settings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(new NetworkBinding[] { Local_RADIUS, Local_AAA }, "hello");
            RadiusServerDeelie deelie1;
            RadiusServerDeelie deelie2;

            server1Settings.RealmFormat = RealmFormat.Email;
            server1Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server1Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server1Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.RADIUS);

            server2Settings.RealmFormat = RealmFormat.Email;
            server2Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server2Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server2Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.AAA);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 1;

            try
            {
                string accountInfo = @"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ";
                server1.Start(server1Settings);
                server1.LoadAccountsFromString(accountInfo);
                deelie1 = new RadiusServerDeelie(server1, RadiusServerDeelie.Mode.Normal);

                server2.Start(server2Settings);
                server2.LoadAccountsFromString(accountInfo);
                deelie2 = new RadiusServerDeelie(server2, RadiusServerDeelie.Mode.Normal);

                client.Open(clientSettings);

                for (int i = 0; i < 20; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));

                Assert.IsTrue(deelie1.Packets.Count > 0);
                Assert.IsTrue(deelie2.Packets.Count > 0);
            }
            finally
            {
                server1.Stop();
                server2.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_LoadBalance_MultiPort()
        {
            // Verify that the client actually distributes packets across multiple
            // RADIUS servers with a multi port client.

            RadiusServer server1 = new RadiusServer();
            RadiusServer server2 = new RadiusServer();
            RadiusServerSettings server1Settings = new RadiusServerSettings();
            RadiusServerSettings server2Settings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(new NetworkBinding[] { Local_RADIUS, Local_AAA }, "hello");
            RadiusServerDeelie deelie1;
            RadiusServerDeelie deelie2;

            server1Settings.RealmFormat = RealmFormat.Email;
            server1Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server1Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server1Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.RADIUS);

            server2Settings.RealmFormat = RealmFormat.Email;
            server2Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server2Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server2Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.AAA);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 4;
            clientSettings.MaxTransmissions = 1;

            try
            {
                string accountInfo = @"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ";
                server1.Start(server1Settings);
                server1.LoadAccountsFromString(accountInfo);
                deelie1 = new RadiusServerDeelie(server1, RadiusServerDeelie.Mode.Normal);

                server2.Start(server2Settings);
                server2.LoadAccountsFromString(accountInfo);
                deelie2 = new RadiusServerDeelie(server2, RadiusServerDeelie.Mode.Normal);

                client.Open(clientSettings);

                for (int i = 0; i < 20; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));

                Assert.IsTrue(deelie1.Packets.Count > 0);
                Assert.IsTrue(deelie2.Packets.Count > 0);
            }
            finally
            {
                server1.Stop();
                server2.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_FailOver_SinglePort()
        {
            // Verify that the client actually fails over to alternate 
            // RADIUS servers with a single port client.

            RadiusServer server1 = new RadiusServer();
            RadiusServer server2 = new RadiusServer();
            RadiusServerSettings server1Settings = new RadiusServerSettings();
            RadiusServerSettings server2Settings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(new NetworkBinding[] { NetworkBinding.Parse("192.168.255.1:1645"), Local_AAA }, "hello");
            RadiusServerDeelie deelie1;
            RadiusServerDeelie deelie2;

            server1Settings.RealmFormat = RealmFormat.Email;
            server1Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server1Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server1Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.RADIUS);

            server2Settings.RealmFormat = RealmFormat.Email;
            server2Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server2Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server2Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.AAA);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 10;
            clientSettings.RetryInterval = TimeSpan.FromSeconds(0.5);

            try
            {
                string accountInfo = @"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ";
                server1.Start(server1Settings);
                server1.LoadAccountsFromString(accountInfo);
                deelie1 = new RadiusServerDeelie(server1, RadiusServerDeelie.Mode.IgnoreAlternatePackets);

                server2.Start(server2Settings);
                server2.LoadAccountsFromString(accountInfo);
                deelie2 = new RadiusServerDeelie(server2, RadiusServerDeelie.Mode.IgnoreAlternatePackets);

                client.Open(clientSettings);

                for (int i = 0; i < 10; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
            }
            finally
            {
                server1.Stop();
                server2.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_FailOver_MultiPort()
        {
            // Verify that the client actually fails over to alternate 
            // RADIUS servers with a multi port client.

            RadiusServer server1 = new RadiusServer();
            RadiusServer server2 = new RadiusServer();
            RadiusServerSettings server1Settings = new RadiusServerSettings();
            RadiusServerSettings server2Settings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(new NetworkBinding[] { Local_AAA, NetworkBinding.Parse("192.168.255.1:1645") }, "hello");
            RadiusServerDeelie deelie1;
            RadiusServerDeelie deelie2;

            server1Settings.RealmFormat = RealmFormat.Email;
            server1Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server1Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server1Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.RADIUS);

            server2Settings.RealmFormat = RealmFormat.Email;
            server2Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server2Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server2Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.AAA);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 4;
            clientSettings.MaxTransmissions = 10;
            clientSettings.RetryInterval = TimeSpan.FromSeconds(0.5);

            try
            {
                string accountInfo = @"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ";
                server1.Start(server1Settings);
                server1.LoadAccountsFromString(accountInfo);
                deelie1 = new RadiusServerDeelie(server1, RadiusServerDeelie.Mode.IgnoreAlternatePackets);

                server2.Start(server2Settings);
                server2.LoadAccountsFromString(accountInfo);
                deelie2 = new RadiusServerDeelie(server2, RadiusServerDeelie.Mode.IgnoreAlternatePackets);

                client.Open(clientSettings);

                for (int i = 0; i < 10; i++)
                    Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
            }
            finally
            {
                server1.Stop();
                server2.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_Blast()
        {
            // Send a bunch of queries to multiple servers from multiple client ports.

            RadiusServer server1 = new RadiusServer();
            RadiusServer server2 = new RadiusServer();
            RadiusServerSettings server1Settings = new RadiusServerSettings();
            RadiusServerSettings server2Settings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(new NetworkBinding[] { Local_RADIUS, Local_AAA }, "hello");
            RadiusServerDeelie deelie1;
            RadiusServerDeelie deelie2;
            IAsyncResult[] ar;

            server1Settings.RealmFormat = RealmFormat.Email;
            server1Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server1Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server1Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.RADIUS);

            server2Settings.RealmFormat = RealmFormat.Email;
            server2Settings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            server2Settings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));
            server2Settings.NetworkBinding = new IPEndPoint(IPAddress.Any, NetworkPort.AAA);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 4;
            clientSettings.MaxTransmissions = 3;

            try
            {
                string accountInfo = @"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ";
                server1.Start(server1Settings);
                server1.LoadAccountsFromString(accountInfo);
                deelie1 = new RadiusServerDeelie(server1, RadiusServerDeelie.Mode.Normal);

                server2.Start(server2Settings);
                server2.LoadAccountsFromString(accountInfo);
                deelie2 = new RadiusServerDeelie(server2, RadiusServerDeelie.Mode.Normal);

                client.Open(clientSettings);

                ar = new IAsyncResult[clientSettings.PortCount * 256];
                for (int i = 0; i < ar.Length; i++)
                    ar[i] = client.BeginAuthenticate("r1", "jeff", "password123", null, null);

                for (int i = 0; i < ar.Length; i++)
                    Assert.IsTrue(client.EndAuthenticate(ar[i]));

                Assert.IsTrue(deelie1.Packets.Count > 0);
                Assert.IsTrue(deelie2.Packets.Count > 0);
            }
            finally
            {
                server1.Stop();
                server2.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusClient_Interop_AD_IAS()
        {
            if (EnvironmentVars.Get("LT_TESTBIN") == null)
                Assert.Inconclusive("[LT_TESTBIN] environment variable does not exist.");

            if (EnvironmentVars.Get("LT_TEST_AD") == null)
                Assert.Inconclusive("[LT_TEST_AD] environment variable does not exist.");

            var ad = new ADTestSettings();

            if (ad.NasSecret == string.Empty)
            {
                Assert.Inconclusive("AD/IAS Testing is disabled");
                return;
            }

            // Verify that RADIUS client works against AD/IAS.  This requires that
            // the LT_TEST_AD environment variable be set properly as described
            // in the LillTek DevInstall.doc document.  The IAS server must also
            // be manually configured with the NAS shared secret for this client.

            RadiusClient client = new RadiusClient();
            NetworkBinding serverEP = new NetworkBinding(EnhancedDns.GetHostByName(ad.Servers[0]).AddressList.IPv4Only()[0], NetworkPort.RADIUS);
            RadiusClientSettings clientSettings = new RadiusClientSettings(serverEP, ad.NasSecret);

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;

            try
            {
                client.Open(clientSettings);

                Assert.IsTrue(client.Authenticate(ad.Domain, ad.Account, ad.Password));

                Assert.IsFalse(client.Authenticate(ad.Domain + "x", ad.Account, ad.Password));
                Assert.IsFalse(client.Authenticate(ad.Domain, ad.Account + "x", ad.Password));
                Assert.IsFalse(client.Authenticate(ad.Domain, ad.Account, ad.Password + "x"));
            }
            finally
            {
                client.Close();
            }
        }
    }
}

