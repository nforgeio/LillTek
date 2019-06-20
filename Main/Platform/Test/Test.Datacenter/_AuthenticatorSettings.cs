//-----------------------------------------------------------------------------
// FILE:        _AuthenticatorSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter.Msgs.AuthService;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AuthenticationSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthenticationSettings_Defaults()
        {
            AuthenticatorSettings settings;

            settings = new AuthenticatorSettings();
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkTaskInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(1), settings.CacheFlushInterval);
            Assert.AreEqual(10000, settings.MaxCacheSize);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.SuccessTTL);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.FailTTL);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthenticationSettings_LoadConfig()
        {
            AuthenticatorSettings settings;

            try
            {
                Config.SetConfig(@"

Settings.BkTaskInterval     = 2s
Settings.CacheFlushInterval = 3s
Settings.MaxCacheSize       = 4
Settings.SuccessTTL         = 5s
Settings.FailTTL            = 6s
");

                settings = AuthenticatorSettings.LoadConfig("Settings");
                Assert.AreEqual(TimeSpan.FromSeconds(2), settings.BkTaskInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(3), settings.CacheFlushInterval);
                Assert.AreEqual(4, settings.MaxCacheSize);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.SuccessTTL);
                Assert.AreEqual(TimeSpan.FromSeconds(6), settings.FailTTL);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

