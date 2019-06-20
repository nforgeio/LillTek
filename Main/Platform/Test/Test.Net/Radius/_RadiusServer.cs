//-----------------------------------------------------------------------------
// FILE:        _RadiusServer.cs
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
    /// <summary>
    /// Used internally to capture server log entries and to record and potentially
    /// mess with received packets.
    /// </summary>
    internal class RadiusServerDeelie
    {
        /// <summary>
        /// The list of received packets.
        /// </summary>
        public List<RadiusPacket> Packets;

        /// <summary>
        /// Logged entries.
        /// </summary>
        public List<RadiusLogEntry> Log;

        public enum Mode
        {
            Normal,
            IgnoreAllPackets,
            IgnoreAlternatePackets,
            IgnoreFirstPacket,
            AuthSuccess,
            AuthFail,
            AuthShortDelay,
            AuthLongDelay,
        }

        private object syncLock = new object();
        private Mode mode;
        private TimeSpan longDelay;
        private TimeSpan shortDelay;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="server">The RADIUS server.</param>
        public RadiusServerDeelie(RadiusServer server, Mode mode)
        {
            this.mode = mode;
            this.Packets = new List<RadiusPacket>();
            this.Log = new List<RadiusLogEntry>();

            server.LogEvent += new RadiusLogDelegate(OnLogEntry);
            server.DiagnosticHook = new RadiusDiagnosticDelegate(DiagnosticHook);

            switch (mode)
            {
                case Mode.AuthSuccess:
                case Mode.AuthFail:
                case Mode.AuthShortDelay:
                case Mode.AuthLongDelay:

                    server.AuthenticateEvent += new RadiusAuthenticateDelegate(OnAuth);
                    break;
            }

            // Set the long delay to a bit longer than the client retry interval and
            // the short delay to something less than this interval.

            RadiusClientSettings clientSettings = new RadiusClientSettings(NetworkBinding.Any, "");

            shortDelay = TimeSpan.FromMilliseconds(100);
            longDelay = clientSettings.RetryInterval + TimeSpan.FromSeconds(clientSettings.BkTaskInterval.TotalSeconds / 2);
        }

        private bool OnAuth(string realm, string account, string password)
        {
            switch (mode)
            {
                case Mode.AuthSuccess:

                    return true;

                case Mode.AuthFail:

                    return false;

                case Mode.AuthShortDelay:

                    Thread.Sleep(shortDelay);
                    return true;

                case Mode.AuthLongDelay:

                    Thread.Sleep(longDelay);
                    return true;

                default:

                    return true;
            }
        }

        private void OnLogEntry(RadiusLogEntry logEntry)
        {
            lock (syncLock)
                Log.Add(logEntry);
        }

        private bool DiagnosticHook(RadiusServer server, RadiusPacket packet)
        {
            bool ignore = false;

            lock (syncLock)
            {
                Packets.Add(packet);

                switch (mode)
                {
                    case Mode.Normal:

                        ignore = false;
                        break;

                    case Mode.IgnoreAllPackets:

                        ignore = true;
                        break;

                    case Mode.IgnoreFirstPacket:

                        ignore = Packets.Count == 1;
                        break;

                    case Mode.IgnoreAlternatePackets:

                        // Ignore even packets

                        ignore = (Packets.Count & 1) == 0;
                        break;
                }
            }

            return !ignore;
        }
    }

    [TestClass]
    public class _RadiusServer
    {
        private NetworkBinding Local_RADIUS = new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.RADIUS);
        private NetworkBinding Local_AAA = new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.AAA);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_RadiusTestApps()
        {
            Assert.Inconclusive("The trial period for the RADIUS client tool has expired.");

            // Verify that the RadiusServerTest and RadiusClientTest classes
            // work properly against each other.

            RadiusTestServer server = null;
            Dictionary<string, string> users;
            Dictionary<IPAddress, string> devices;

            users = new Dictionary<string, string>();
            users.Add("jeff", "password1");
            users.Add("joe", "password2");

            devices = new Dictionary<IPAddress, string>();
            devices.Add(IPAddress.Loopback, "secret");
            devices.Add(NetHelper.GetActiveAdapter(), "secret");

            server = new RadiusTestServer();
            server.Start(users, devices);

            try
            {
                Assert.IsTrue(RadiusTestClient.Authenticate(server.EndPoint, "secret", "jeff", "password1"));
                Assert.IsTrue(RadiusTestClient.Authenticate(server.EndPoint, "secret", "joe", "password2"));

                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "secret", "jeff", "passwordX"));
                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "secret", "billy", "x"));
                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "xxxx", "jeff", "password1"));
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Interop()
        {
            Assert.Inconclusive("The trial period for the RADIUS client tool has expired.");

            // Verify that my RADIUS server code can work against a client from
            // another vendor.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            try
            {
                server.Start(serverSettings);
                server.LoadAccountsFromString(@"

    // This is a comment line

    r1;jeff;password123
    r2;jeff;passwordXXX
    r1;jane;bigfish
    ");

                Assert.IsTrue(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jeff@r1", "password123"));
                Assert.IsTrue(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jeff@r2", "passwordXXX"));
                Assert.IsTrue(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jane@r1", "bigfish"));

                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jeff@r1", "PASSWORD123"));
                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jeff", "password123"));
                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "hello", "jeff@r3", "password123"));
                Assert.IsFalse(RadiusTestClient.Authenticate(server.EndPoint, "badsecret", "jeff@r1", "password123"));
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_RealmFmt_Email()
        {
            // Test the client against the server using RealmFormat.Email.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

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

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsTrue(client.Authenticate("r2", "jeff", "passwordXXX"));
                Assert.IsTrue(client.Authenticate("r1", "jane", "bigfish"));

                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));
                Assert.IsFalse(client.Authenticate("", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate(null, "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r3", "jeff", "password123"));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_RealmFmt_Slash()
        {
            // Test the client against the server using RealmFormat.Slash.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsTrue(client.Authenticate("r2", "jeff", "passwordXXX"));
                Assert.IsTrue(client.Authenticate("r1", "jane", "bigfish"));

                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));
                Assert.IsFalse(client.Authenticate("", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate(null, "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r3", "jeff", "password123"));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_DefaultSecret()
        {
            // Verify that the default secret will be used if the NAS device
            // is not specified.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.DefaultSecret = "hello";

            clientSettings.RealmFormat = RealmFormat.Slash;
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

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsTrue(client.Authenticate("r2", "jeff", "passwordXXX"));
                Assert.IsTrue(client.Authenticate("r1", "jane", "bigfish"));

                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));
                Assert.IsFalse(client.Authenticate("", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate(null, "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r3", "jeff", "password123"));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Nas_HostName()
        {
            // Verify that the server can handle NAS devices specified by DNS host name.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.Devices.Add(new RadiusNasInfo(Helper.MachineName, "hello"));

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

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsTrue(client.Authenticate("r2", "jeff", "passwordXXX"));
                Assert.IsTrue(client.Authenticate("r1", "jane", "bigfish"));

                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));
                Assert.IsFalse(client.Authenticate("", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate(null, "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r3", "jeff", "password123"));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Nas_HostRefresh()
        {
            // Verify that the server refreshes NAS host name to IP address mappings.
            // I'm going to do this by specifying a NAS host name that does not
            // exist, verify that an authentication fails, then add the host name
            // to the HOSTS file, wait a bit for the server to refresh the mappings
            // and then verify that this worked by making sure that an authentication
            // attempt succeeds.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

            serverSettings.RealmFormat = RealmFormat.Email;
            serverSettings.DnsRefreshInterval = TimeSpan.FromSeconds(10);
            serverSettings.BkTaskInterval = TimeSpan.FromSeconds(2);
            serverSettings.Devices.Add(new RadiusNasInfo("nas.test.lilltek.com", "hello"));

            clientSettings.RealmFormat = RealmFormat.Email;
            clientSettings.PortCount = 1;
            clientSettings.MaxTransmissions = 1;
            clientSettings.RetryInterval = TimeSpan.FromSeconds(2);

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

                try
                {
                    client.Authenticate("r1", "jeff", "password123");
                    Assert.Fail();
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }

                EnhancedDns.AddHost("nas.test.lilltek.com", NetHelper.GetActiveAdapter());
                Thread.Sleep(serverSettings.DnsRefreshInterval + serverSettings.BkTaskInterval);

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
            }
            finally
            {
                EnhancedDns.RemoveHosts();
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Bad_Secret()
        {
            // Verify that the server detects a bad shared secret.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "badsecret"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "badsecret"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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

                Assert.IsFalse(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));
                Assert.IsFalse(client.Authenticate("", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate(null, "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r3", "jeff", "password123"));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Bad_NasDevice()
        {
            // Verify that the server detects an unknown NAS device.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;

            clientSettings.RealmFormat = RealmFormat.Slash;
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

                try
                {
                    client.Authenticate("r1", "jeff", "password123");
                    Assert.Fail("TimeoutException expected");
                }
                catch (TimeoutException)
                {
                    // Expecting a timeout since the server should ignore this packet
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }

                Assert.IsTrue(deelie.Log.Count > 0);
                Assert.AreEqual(RadiusLogEntryType.UnknownNas, deelie.Log[0].EntryType);
                Assert.IsFalse(deelie.Log[0].Success);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Auth_Log()
        {
            // Verify that authentication events are logged

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));
                Assert.IsFalse(client.Authenticate("r1", "jeff", "PASSWORD123"));

                Assert.AreEqual(2, deelie.Log.Count);

                Assert.IsTrue(deelie.Log[0].Success);
                Assert.AreEqual(RadiusLogEntryType.Authentication, deelie.Log[0].EntryType);
                Assert.AreEqual("r1", deelie.Log[0].Realm);
                Assert.AreEqual("jeff", deelie.Log[0].Account);

                Assert.IsFalse(deelie.Log[1].Success);
                Assert.AreEqual(RadiusLogEntryType.Authentication, deelie.Log[1].EntryType);
                Assert.AreEqual("r1", deelie.Log[1].Realm);
                Assert.AreEqual("jeff", deelie.Log[1].Account);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Auth_HookSuccess()
        {
            // Verify successful authentication callbacks

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthSuccess);

                Assert.IsTrue(client.Authenticate("r1", "jeff", "password123"));

                Assert.AreEqual(1, deelie.Log.Count);

                Assert.IsTrue(deelie.Log[0].Success);
                Assert.AreEqual(RadiusLogEntryType.Authentication, deelie.Log[0].EntryType);
                Assert.AreEqual("r1", deelie.Log[0].Realm);
                Assert.AreEqual("jeff", deelie.Log[0].Account);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Auth_HookFail()
        {
            // Verify failed authentication callbacks

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthFail);

                Assert.IsFalse(client.Authenticate("r1", "jeff", "password123"));

                Assert.AreEqual(1, deelie.Log.Count);

                Assert.IsFalse(deelie.Log[0].Success);
                Assert.AreEqual(RadiusLogEntryType.Authentication, deelie.Log[0].EntryType);
                Assert.AreEqual("r1", deelie.Log[0].Realm);
                Assert.AreEqual("jeff", deelie.Log[0].Account);
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Auth_Parallel()
        {
            // Verify that we can perform multiple parallel authentications with 
            // no delay.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            IAsyncResult[] ar = new IAsyncResult[255];
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthSuccess);

                for (int i = 0; i < ar.Length; i++)
                    ar[i] = client.BeginAuthenticate("r1", "jeff", "password123", null, null);

                for (int i = 0; i < ar.Length; i++)
                    Assert.IsTrue(client.EndAuthenticate(ar[i]));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_Auth_Parallel_Delay()
        {
            // Verify that we can perform multiple parallel authentications with 
            // a brief delay.

            RadiusServer server = new RadiusServer();
            RadiusServerSettings serverSettings = new RadiusServerSettings();
            RadiusClient client = new RadiusClient();
            RadiusClientSettings clientSettings = new RadiusClientSettings(Local_RADIUS, "hello");
            IAsyncResult[] ar = new IAsyncResult[255];
            RadiusServerDeelie deelie;

            serverSettings.RealmFormat = RealmFormat.Slash;
            serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "hello"));
            serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "hello"));

            clientSettings.RealmFormat = RealmFormat.Slash;
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
                deelie = new RadiusServerDeelie(server, RadiusServerDeelie.Mode.AuthShortDelay);

                for (int i = 0; i < ar.Length; i++)
                    ar[i] = client.BeginAuthenticate("r1", "jeff", "password123", null, null);

                for (int i = 0; i < ar.Length; i++)
                    Assert.IsTrue(client.EndAuthenticate(ar[i]));
            }
            finally
            {
                server.Stop();
                client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Radius")]
        public void RadiusServer_LoadConfig()
        {
            RadiusServerSettings settings;

            try
            {
                // Verify the defaults

                Config.SetConfig(null);
                settings = RadiusServerSettings.LoadConfig("xxx");

                Assert.AreEqual(new NetworkBinding(IPAddress.Any, 1812), settings.NetworkBinding);
                Assert.AreEqual(131072, settings.SocketBuffer);
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.BkTaskInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(15), settings.DnsRefreshInterval);
                Assert.AreEqual(0, settings.Devices.Count);
                Assert.AreEqual(RealmFormat.Email, settings.RealmFormat);

                // Now try some actual settings

                Config.SetConfig(@"

Prefix.NetworkBinding       = 127.0.0.1:1645
Prefix.SocketBuffer         = 10000
Prefix.BkTaskInterval       = 5s
Prefix.DnsRefreshInterval   = 2m
Prefix.RealmFormat          = slash
Prefix.Devices[0]           = 127.0.0.1;secret1
Prefix.Devices[1]           = localhost;secret2
");

                settings = RadiusServerSettings.LoadConfig("Prefix");

                Assert.AreEqual(new NetworkBinding(IPAddress.Loopback, 1645), settings.NetworkBinding);
                Assert.AreEqual(10000, settings.SocketBuffer);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.BkTaskInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(2), settings.DnsRefreshInterval);

                Assert.AreEqual(2, settings.Devices.Count);
                Assert.AreEqual(IPAddress.Loopback, settings.Devices[0].Address);
                Assert.IsNull(settings.Devices[0].Host);
                Assert.AreEqual("secret1", settings.Devices[0].Secret);

                Assert.AreEqual(IPAddress.Any, settings.Devices[1].Address);
                Assert.AreEqual("localhost", settings.Devices[1].Host);
                Assert.AreEqual("secret2", settings.Devices[1].Secret);

                Assert.AreEqual(RealmFormat.Slash, settings.RealmFormat);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

