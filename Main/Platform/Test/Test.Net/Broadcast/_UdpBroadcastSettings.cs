//-----------------------------------------------------------------------------
// FILE:        _UdpBroadcastSettings.cs
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
    public class _UdpBroadcastSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcastSettings_Client()
        {
            try
            {

                UdpBroadcastClientSettings settings;

                // Verify that we can read reasonable settings.

                var cfg1 = @"
&section Settings
    NetworkBinding        = 3.3.3.3:30
    SocketBufferSize      = 128K
    Server[0]             = 1.1.1.1:10
    Server[1]             = 2.2.2.2:20
    SharedKey             = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL            = 15m
    BroadcastGroup        = 99
    BkTaskInterval        = 15s
    KeepAliveInterval     = 10s
    ServerResolveInterval = 20s
&endsection
";
                Config.SetConfig(cfg1.Replace('&', '#'));

                settings = new UdpBroadcastClientSettings("Settings");
                Assert.AreEqual(new NetworkBinding("3.3.3.3:30"), settings.NetworkBinding);
                Assert.AreEqual(128 * 1024, settings.SocketBufferSize);
                CollectionAssert.AreEqual(new NetworkBinding[] { new NetworkBinding("1.1.1.1:10"), new NetworkBinding("2.2.2.2:20") }, settings.Servers);
                Assert.AreEqual(99, settings.BroadcastGroup);
                Assert.AreEqual(TimeSpan.FromSeconds(15), settings.BkTaskInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(10), settings.KeepAliveInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(20), settings.ServerResolveInterval);

                // Verify that an exception is thrown if no servers are specified.

                Config.SetConfig(string.Empty);

                try
                {
                    settings = new UdpBroadcastClientSettings("Settings");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(FormatException));
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcastSettings_Server()
        {
            try
            {
                UdpBroadcastServerSettings settings;

                // Verify that we can read reasonable settings.

                var cfg1 = @"
&section Settings
    NetworkBinding           = 1.1.1.1:10
    SocketBufferSize         = 128K
    Server[0]                = 1.1.1.1:10
    Server[1]                = 2.2.2.2:20
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 7s
    ClusterKeepAliveInterval = 10s
    ServerTTL                = 15s
    ClientTTL                = 20s
&endsection
";
                Config.SetConfig(cfg1.Replace('&', '#'));

                settings = new UdpBroadcastServerSettings("Settings");
                Assert.AreEqual(new NetworkBinding("1.1.1.1:10"), settings.NetworkBinding);
                Assert.AreEqual(128 * 1024, settings.SocketBufferSize);
                CollectionAssert.AreEqual(new NetworkBinding[] { new NetworkBinding("1.1.1.1:10"), new NetworkBinding("2.2.2.2:20") }, settings.Servers);
                Assert.AreEqual(TimeSpan.FromSeconds(7), settings.BkTaskInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(10), settings.ClusterKeepAliveInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(15), settings.ServerTTL);
                Assert.AreEqual(TimeSpan.FromSeconds(20), settings.ClientTTL);

                // Verify that an exception is thrown if no servers are specified.

                Config.SetConfig(string.Empty);

                try
                {
                    settings = new UdpBroadcastServerSettings("Settings");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(FormatException));
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

