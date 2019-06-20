//-----------------------------------------------------------------------------
// FILE:        _ClusterMemberSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ClusterMemberSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberSettings_Default()
        {
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://test");

            Assert.AreEqual((MsgEP)"logical://test", settings.ClusterBaseEP);
            Assert.AreEqual(ClusterMemberMode.Normal, settings.Mode);
            Assert.AreEqual(TimeSpan.FromSeconds(30), settings.MasterBroadcastInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(30), settings.SlaveUpdateInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(10), settings.ElectionInterval);
            Assert.AreEqual(2, settings.MissingMasterCount);
            Assert.AreEqual(2, settings.MissingSlaveCount);
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.MasterBkInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.SlaveBkInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkInterval);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberSettings_LoadConfig()
        {
            ClusterMemberSettings settings;
            string cfg;

            try
            {
                // Verify that we actually load existing settings

                cfg = @"
&section Settings

    ClusterBaseEP           = logical://test
    Mode                    = PreferMaster
    MasterBroadcastInterval = 10m
    SlaveUpdateInterval     = 11m
    MissingMasterCount      = 13
    MissingSlaveCount       = 20
    MasterBkInterval        = 14m
    SlaveBkInterval         = 15m
    BkInterval              = 16m
    ElectionInterval        = 17m

&endsection
";
                Config.SetConfig(cfg.Replace('&', '#'));
                settings = ClusterMemberSettings.LoadConfig("Settings");

                Assert.AreEqual((MsgEP)"logical://test", settings.ClusterBaseEP);
                Assert.AreEqual(ClusterMemberMode.PreferMaster, settings.Mode);
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.MasterBroadcastInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(11), settings.SlaveUpdateInterval);
                Assert.AreEqual(13, settings.MissingMasterCount);
                Assert.AreEqual(20, settings.MissingSlaveCount);
                Assert.AreEqual(TimeSpan.FromMinutes(14), settings.MasterBkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(15), settings.SlaveBkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(16), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(17), settings.ElectionInterval);

                // Verify that settings not in the config are initialized
                // with the proper defaults

                cfg = @"
&section Settings

    ClusterBaseEP = logical://test

&endsection
";
                Config.SetConfig(cfg.Replace('&', '#'));
                settings = ClusterMemberSettings.LoadConfig("Settings");

                Assert.AreEqual((MsgEP)"logical://test", settings.ClusterBaseEP);
                Assert.AreEqual(ClusterMemberMode.Normal, settings.Mode);
                Assert.AreEqual(TimeSpan.FromSeconds(30), settings.MasterBroadcastInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(30), settings.SlaveUpdateInterval);
                Assert.AreEqual(2, settings.MissingMasterCount);
                Assert.AreEqual(2, settings.MissingSlaveCount);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.MasterBkInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.SlaveBkInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(10), settings.ElectionInterval);

                // Make sure we see an exception if the ClusterBaseEP setting is
                // not present.

                cfg = @"
&section Settings

&endsection
";
                Config.SetConfig(cfg.Replace('&', '#'));

                try
                {
                    settings = ClusterMemberSettings.LoadConfig("Settings");
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(ArgumentException));
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberSettings_LoadFromClusterMemberStatus()
        {
            ClusterMemberStatus status;
            ClusterMemberSettings settings;
            string cfg;

            try
            {
                cfg = @"
&section Settings

    ClusterBaseEP           = logical://test
    Mode                    = PreferSlave
    MasterBroadcastInterval = 10m
    SlaveUpdateInterval     = 11m
    MissingMasterCount      = 13
    MissingSlaveCount       = 20
    MasterBkInterval        = 14m
    SlaveBkInterval         = 15m
    BkInterval              = 16m
    ElectionInterval        = 17m

&endsection
";
                Config.SetConfig(cfg.Replace('&', '#'));

                settings = ClusterMemberSettings.LoadConfig("Settings");
                status = new ClusterMemberStatus("logical://test/foo", ClusterMemberState.Slave, settings);
                settings = new ClusterMemberSettings(status);

                Assert.AreEqual((MsgEP)"logical://test", settings.ClusterBaseEP);
                Assert.AreEqual(ClusterMemberMode.PreferSlave, settings.Mode);
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.MasterBroadcastInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(11), settings.SlaveUpdateInterval);
                Assert.AreEqual(13, settings.MissingMasterCount);
                Assert.AreEqual(20, settings.MissingSlaveCount);
                Assert.AreEqual(TimeSpan.FromMinutes(14), settings.MasterBkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(15), settings.SlaveBkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(16), settings.BkInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(17), settings.ElectionInterval);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberSettings_Equals()
        {
            ClusterMemberSettings s1, s2;

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            Assert.AreEqual(s1, s2);

            s1.ClusterBaseEP = "logical://foo";
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.MasterBroadcastInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.SlaveUpdateInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.ElectionInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.MissingMasterCount = 99;
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.MissingSlaveCount = 99;
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.MasterBkInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.SlaveBkInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);

            s1 = new ClusterMemberSettings((MsgEP)"logical://test");
            s2 = new ClusterMemberSettings((MsgEP)"logical://test");
            s1.BkInterval = TimeSpan.FromMinutes(99);
            Assert.AreNotEqual(s1, s2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberSettings_ComputeIntervals()
        {
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://foo");

            settings.MasterBroadcastInterval = TimeSpan.FromSeconds(2);
            settings.MissingMasterCount = 3;
            settings.SlaveUpdateInterval = TimeSpan.FromSeconds(4);
            settings.MissingSlaveCount = 5;

            Assert.AreEqual(TimeSpan.FromSeconds(2 * 3), settings.MissingMasterInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(4 * 5), settings.MissingSlaveInterval);
        }
    }
}

