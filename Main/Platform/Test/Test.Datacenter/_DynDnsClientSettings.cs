//-----------------------------------------------------------------------------
// FILE:        _DynDnsClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Reflection;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _DynDnsClientSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_Defaults()
        {
            DynDnsClientSettings def = new DynDnsClientSettings();

            Assert.IsTrue(def.Enabled);
            Assert.IsTrue(def.NetworkBinding.IsAny);
            Assert.AreEqual(DynDnsMode.Cluster, def.Mode);
            Assert.IsNotNull(def.SharedKey);
            Assert.IsTrue(def.Domain.IsAny);
            ExtendedAssert.IsEmpty(def.NameServers);
            ExtendedAssert.IsEmpty(def.Hosts);
            Assert.AreEqual(TimeSpan.FromSeconds(1), def.BkInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(15), def.DomainRefreshInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(1), def.UdpRegisterInterval);
            Assert.IsNull(def.Cluster);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_LoadCluster()
        {
            var cfg = @"

&section DynDnsClient

    Enabled               = true
    NetworkBinding        = ANY
    Mode                  = CLUSTER
    SharedKey             = aes:ac2qGMV/VZXXdwdjFvaOBpLjOJgOuG6SbM86w3xk0NM=:B4s0wIHjn+PdRHsIcBgJPQ==
    // Domain             = ANY
    // NameServer[0]      =
    BkInterval            = 3s
    DomainRefreshInterval = 4m
    UdpRegisterInterval   = 5m

    &section Cluster

        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection
 
&endsection
";
            try
            {
                DynDnsClientSettings settings;

                Config.SetConfig(cfg.Replace('&', '#'));

                settings = new DynDnsClientSettings("DynDnsClient");

                Assert.IsTrue(settings.Enabled);
                Assert.IsTrue(settings.NetworkBinding.IsAny);
                Assert.AreEqual(DynDnsMode.Cluster, settings.Mode);
                Assert.AreNotEqual(new DynDnsClientSettings().SharedKey.ToString(), settings.SharedKey.ToString());
                Assert.IsTrue(settings.Domain.IsAny);
                ExtendedAssert.IsEmpty(settings.NameServers);
                Assert.AreEqual(TimeSpan.FromSeconds(3), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(4), settings.DomainRefreshInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.UdpRegisterInterval);
                Assert.IsNotNull(settings.Cluster);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_LoadUdpDomain()
        {
            var cfg = @"

&section DynDnsClient

    Enabled               = true
    NetworkBinding        = ANY
    Mode                  = UDP
    SharedKey             = aes:ac2qGMV/VZXXdwdjFvaOBpLjOJgOuG6SbM86w3xk0NM=:B4s0wIHjn+PdRHsIcBgJPQ==
    Domain                = lilltek.net:DYNAMIC-DNS
    // NameServer[0]      =
    BkInterval            = 3s
    DomainRefreshInterval = 4m
    UdpRegisterInterval   = 5m
 
&endsection
";
            try
            {
                DynDnsClientSettings settings;

                Config.SetConfig(cfg.Replace('&', '#'));

                settings = new DynDnsClientSettings("DynDnsClient");

                Assert.IsTrue(settings.Enabled);
                Assert.IsTrue(settings.NetworkBinding.IsAny);
                Assert.AreEqual(DynDnsMode.Udp, settings.Mode);
                Assert.AreNotEqual(new DynDnsClientSettings().SharedKey.ToString(), settings.SharedKey.ToString());
                Assert.AreEqual(new NetworkBinding("lilltek.net", NetworkPort.DynamicDns), settings.Domain);
                ExtendedAssert.IsEmpty(settings.NameServers);
                Assert.AreEqual(TimeSpan.FromSeconds(3), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(4), settings.DomainRefreshInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.UdpRegisterInterval);
                Assert.IsNull(settings.Cluster);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_LoadUdpNameServers()
        {
            var cfg = @"

&section DynDnsClient

    Enabled               = true
    NetworkBinding        = ANY
    Mode                  = UDP
    SharedKey             = aes:ac2qGMV/VZXXdwdjFvaOBpLjOJgOuG6SbM86w3xk0NM=:B4s0wIHjn+PdRHsIcBgJPQ==
    // Domain             = 
    NameServer[0]         = 10.0.0.1:10
    NameServer[1]         = 10.0.0.1:20
    BkInterval            = 3s
    DomainRefreshInterval = 4m
    UdpRegisterInterval   = 5m
 
&endsection
";
            try
            {
                DynDnsClientSettings settings;

                Config.SetConfig(cfg.Replace('&', '#'));

                settings = new DynDnsClientSettings("DynDnsClient");

                Assert.IsTrue(settings.Enabled);
                Assert.IsTrue(settings.NetworkBinding.IsAny);
                Assert.AreEqual(DynDnsMode.Udp, settings.Mode);
                Assert.AreNotEqual(new DynDnsClientSettings().SharedKey.ToString(), settings.SharedKey.ToString());
                Assert.IsTrue(settings.Domain.IsAny);
                CollectionAssert.AreEqual(new NetworkBinding[] { new NetworkBinding("10.0.0.1:10"), new NetworkBinding("10.0.0.1:20") }, settings.NameServers);
                Assert.AreEqual(TimeSpan.FromSeconds(3), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(4), settings.DomainRefreshInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.UdpRegisterInterval);
                Assert.IsNull(settings.Cluster);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_LoadUdpBadMode()
        {
            var cfg = @"

&section DynDnsClient

    Enabled               = true
    NetworkBinding        = ANY
    Mode                  = BOTH
    SharedKey             = aes:ac2qGMV/VZXXdwdjFvaOBpLjOJgOuG6SbM86w3xk0NM=:B4s0wIHjn+PdRHsIcBgJPQ==
    Domain                = lilltek.com:DYNAMIC-DNS
    // NameServer[0]      =
    BkInterval            = 3s
    DomainRefreshInterval = 4m
    UdpRegisterInterval   = 5m

    &section Cluster

        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection
 
&endsection
";
            try
            {
                DynDnsClientSettings settings;

                Config.SetConfig(cfg.Replace('&', '#'));

                try
                {
                    settings = new DynDnsClientSettings("DynDnsClient");
                    Assert.Fail("Expected an exception for [Mode=Both]");
                }
                catch
                {
                    // Expected
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsClientSettings_LoadUdpBadServer()
        {
            var cfg = @"

&section DynDnsClient

    Enabled               = true
    NetworkBinding        = ANY
    Mode                  = BOTH
    SharedKey             = aes:ac2qGMV/VZXXdwdjFvaOBpLjOJgOuG6SbM86w3xk0NM=:B4s0wIHjn+PdRHsIcBgJPQ==
    // Domain             = lilltek.com:DYNAMIC-DNS
    // NameServer[0]      =
    BkInterval            = 3s
    DomainRefreshInterval = 4m
    UdpRegisterInterval   = 5m

    &section Cluster

        ClusterBaseEP           = abstract://LillTek/DataCenter/DynDNS
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 3s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection
 
&endsection
";
            try
            {
                DynDnsClientSettings settings;

                Config.SetConfig(cfg.Replace('&', '#'));

                try
                {
                    settings = new DynDnsClientSettings("DynDnsClient");
                    Assert.Fail("Expected an exception because neither Domain or NameServer[#] were specified.");
                }
                catch
                {
                    // Expected
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

