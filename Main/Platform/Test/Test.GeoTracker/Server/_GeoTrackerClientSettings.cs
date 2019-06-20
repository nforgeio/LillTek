//-----------------------------------------------------------------------------
// FILE:        _GeoTrackerClientSettings.cs
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
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _GeoTrackerClientSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerClientSettings_Default()
        {
            var settings = new GeoTrackerClientSettings();

            Assert.AreEqual("logical://LillTek/GeoTracker/Server", settings.ServerEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerClientSettings_ConfigDefault()
        {
            try
            {
                Config.SetConfig(string.Empty);

                var settings = GeoTrackerClientSettings.LoadConfig("Test");

                Assert.AreEqual("logical://LillTek/GeoTracker/Server", settings.ServerEP);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerClientSettings_ConfigSettings()
        {
            string cfg = @"
&section Test

    ServerEP = logical://foo/bar

&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                var settings = GeoTrackerClientSettings.LoadConfig("Test");

                Assert.AreEqual("logical://foo/bar", settings.ServerEP);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

