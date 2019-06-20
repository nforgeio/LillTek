//-----------------------------------------------------------------------------
// FILE:        _GeoTrackerServerSettings.cs
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
using LillTek.Messaging;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _GeoTrackerServerSettings
    {
        private const string DefRsaKey = "<RSAKeyValue><Modulus>pCRHtqA872QYibpZif0Xo2xzNhTnXDsIwwTKdM1umBO7Dm+8NBcO23KJNTQQLGzOXtQ8rqMGfAEbXmk4+9pxxu7S5/shuKWV8MjUa1jeMvdfD3f1rh7xDZCoYtGPtMk6vjYM5jckJ4kaNqF7XT4zlEk6qM2am86xMMyThke7xBE=</Modulus><Exponent>AQAB</Exponent><P>3zMihEf+wPLMSonI76TEU3AFAlxFHFW+ZwZ4xmMClLBuQYXKpNbp4YJ6I5Bf2k6ToHtJPqUptZe2Aq93NXpw7Q==</P><Q>vENbvGlu3q/7OhfnScD7LKb+P6aQx1ok/ZLk+pCGkIp1e9dfkNOI278n9y4UQz65JFcuNezmk9J6aUoxPcaPNQ==</Q><DP>IgGHc8IIVVtotr6RZ7mh09iQWtC2EuAZd1bsFcXGAeNzmPYKbtzzm1EmzL5VbExmf5/pA+tkFG+94mDbd8Fk7Q==</DP><DQ>dvsvIA2WR2D7KsTupNs1IwxLRVj0yTj8hdHvqzfqA7Gt/F2qhTJbnV3bWUmi/rjGc+QxTV1ygFwWhzKfmkZCPQ==</DQ><InverseQ>a7E6CztwA2gDf5sSlrUOs95VrmmWISYa6PJOdqefF3+N/odlJ2bJaACjVDlQ7Edsnf2o6QGb0ImRTHW5Qx6kdQ==</InverseQ><D>WGQhKjuIFPI2NJTheumMPTk9obYIESbJRRvjWpr2H3cgmFmbZAG2wn4fXUM4InRFfdOVCgZIi6ac8m5/fUDZW4XUkisQJZaCp4pON25vEt79MXYr3D2sjeVEAVo8f1PFiATNvdSdkbrkWrdkK7alVIX9BfYIH/oZjh53PXoa0kE=</D></RSAKeyValue>";

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerServerSettings_Default()
        {
            var settings = new GeoTrackerServerSettings();

            Assert.AreEqual("logical://LillTek/GeoTracker/Server", settings.ServerEP);
            Assert.AreEqual("logical://LillTek/GeoTracker/Cluster", settings.ClusterEP);
            Assert.AreEqual(typeof(DynamicHashedTopology), settings.ClusterTopology);
            Assert.AreEqual(0, settings.ClusterArgs.Count);
            Assert.AreEqual(typeof(NullGeoFixArchiver), settings.GeoFixArchiver);
            Assert.AreEqual(0, settings.GeoFixArchiverArgs.Count);
            Assert.AreEqual(TimeSpan.FromHours(1), settings.GeoFixRetentionInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(1), settings.GeoFixPurgeInterval);
            Assert.AreEqual(30, settings.MaxEntityGeoFixes);
            Assert.AreEqual(1000, settings.IndexHighWatermarkLimit);
            Assert.AreEqual(750, settings.IndexLowWatermarkLimit);
            Assert.AreEqual(2, settings.IndexMaxGroupTableLevel);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.IndexBalancingInterval);
            Assert.AreEqual(true, settings.IPGeocodeEnabled);
            Assert.AreEqual(new Uri("http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat"), settings.IPGeocodeSourceUri);
            Assert.AreEqual(DefRsaKey, settings.IPGeocodeSourceRsaKey);
            Assert.AreEqual(TimeSpan.FromDays(1), settings.IPGeocodeSourcePollInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.IPGeocodeSourceTimeout);
            Assert.AreEqual(TimeSpan.FromMinutes(2.5), settings.SweepInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkInterval);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerServerSettings_ConfigDefault()
        {
            try
            {
                Config.SetConfig(string.Empty);

                var settings = GeoTrackerServerSettings.LoadConfig("Test");

                Assert.AreEqual("logical://LillTek/GeoTracker/Server", settings.ServerEP);
                Assert.AreEqual("logical://LillTek/GeoTracker/Cluster", settings.ClusterEP);
                Assert.AreEqual(typeof(DynamicHashedTopology), settings.ClusterTopology);
                Assert.AreEqual(0, settings.ClusterArgs.Count);
                Assert.AreEqual(typeof(NullGeoFixArchiver), settings.GeoFixArchiver);
                Assert.AreEqual(0, settings.GeoFixArchiverArgs.Count);
                Assert.AreEqual(TimeSpan.FromHours(1), settings.GeoFixRetentionInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.GeoFixPurgeInterval);
                Assert.AreEqual(30, settings.MaxEntityGeoFixes);
                Assert.AreEqual(1000, settings.IndexHighWatermarkLimit);
                Assert.AreEqual(750, settings.IndexLowWatermarkLimit);
                Assert.AreEqual(2, settings.IndexMaxGroupTableLevel);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.IndexBalancingInterval);
                Assert.AreEqual(true, settings.IPGeocodeEnabled);
                Assert.AreEqual(new Uri("http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat"), settings.IPGeocodeSourceUri);
                Assert.AreEqual(DefRsaKey, settings.IPGeocodeSourceRsaKey);
                Assert.AreEqual(TimeSpan.FromDays(1), settings.IPGeocodeSourcePollInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.IPGeocodeSourceTimeout);
                Assert.AreEqual(TimeSpan.FromMinutes(2.5), settings.SweepInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkInterval);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerServerSettings_ConfigSettings()
        {
            string cfg = @"
&section Test

    ServerEP                    = logical://test/server
    ClusterEP                   = logical://test/cluster
    ClusterTopology             = LillTek.Messaging.Dynamic2DTopology:LillTek.Messaging.dll
    ClusterArgs                 = {{

        test1 = 1
        test2 = 2
    }}

    GeoFixArchiver              = LillTek.GeoTracker.Server.AppLogGeoFixArchiver:LillTek.GeoTracker.Server.dll
    GeoFixArchiverArgs          = {{

        Test  = Foo
        Hello = World
    }}

    GeoFixRetentionInterval     = 2h
    GeoFixPurgeInterval         = 7m
    MaxEntityGeoFixes           = 100
    LongitudeIndexResolution    = 0.25

    IndexHighWatermarkLimit     = 2000
    IndexLowWatermarkLimit      = 1500
    IndexMaxGroupTableLevel     = 3
    IndexBalancingInterval      = 1m

    IPGeocodeEnabled            = no
    IPGeocodeSourceUri          = http://www.google.com
    IPGeocodeSourceRsaKey       = <RSAKeyValue><Modulus>trZSBdVGPcfVlaQrKK7nVAmMqu65eKLKoQuAujN8fd3OCZlkayn1Cil6SInwLS9sHIHG1QXgHT+d/M3bQybaeE0kU5SMQQIXi2Z41EHaVcaXU3Pw81v2ybFkVf8eQPTmuxESyw85BymVkre5rSZiRlk7nlZQN812z36mv6ByNYE=</Modulus><Exponent>AQAB</Exponent><P>2KQogNyfS5KEVh93Fsp/b7lovUfZvxkfBH5cdOhX8S43eLVZV8hV1I54B8FWoiA7RIHq/52WWS8E5TzI5ntZ7Q==</P><Q>1+gk6cHptG/aWfmVsRd7ZvnoZiWHPTijnoRtaO3ynAgb1Vn8VehqQBbunN6EqKV2/Qnt5flE/+aY98MBJFNHZQ==</Q><DP>rgPeTPPqOGfuSMdpfzMU/gcuLKwkKa3iDlf5qCZhTWdUQ29X3n0bBGuT2pbgIcZGFRdOThilBeoQwpn6vbfjWQ==</DP><DQ>DLujGaoW9041aWL/wf7phywr2YJTFHg3pgyXSz3lNfCAe7ef2w0m3vq7PcMdvbhsaQXh4tMtj43w7YOxmIvUxQ==</DQ><InverseQ>V1tegYUSKG89LsK44zaGKWAYAyygNimBkgLgxs92asWb6LVzA/iP1R1WEELb4VOKj7h0KLk7kY0sZxC/gC2Ybg==</InverseQ><D>VpQ4c9knKrlZ5UngxatzpKfNx2XN73M8j2mS+yjQkhgbvQK5yeoc2k7jSiJK9C5njW6VmHXrSBDQPW4Su1Ra6x+zDiCqK6qY/BIDetO6MuU/5zTd0+uMmLZg1PUkU4e42MJZDjNET2KFmG0IrB/jp2099lI/E6d2Mv2FzxO0atE=</D></RSAKeyValue>
    IPGeocodeSourcePollInterval = 1h
    IPGeocodeSourceTimeout      = 10m
    SweepInterval               = 5m
    BkInterval                  = 30s

&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                var settings = GeoTrackerServerSettings.LoadConfig("Test");

                Assert.AreEqual("logical://test/server", settings.ServerEP);
                Assert.AreEqual("logical://test/cluster", settings.ClusterEP);
                Assert.AreEqual("LillTek.Messaging.Dynamic2DTopology", settings.ClusterTopology.FullName);
                Assert.AreEqual(2, settings.ClusterArgs.Count);
                Assert.AreEqual("1", settings.ClusterArgs["test1"]);
                Assert.AreEqual("2", settings.ClusterArgs["test2"]);
                Assert.AreEqual("LillTek.GeoTracker.Server.AppLogGeoFixArchiver", settings.GeoFixArchiver.FullName);
                Assert.AreEqual(2, settings.GeoFixArchiverArgs.Count);
                Assert.AreEqual("Foo", settings.GeoFixArchiverArgs["Test"]);
                Assert.AreEqual("World", settings.GeoFixArchiverArgs["Hello"]);
                Assert.AreEqual(TimeSpan.FromHours(2), settings.GeoFixRetentionInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(7), settings.GeoFixPurgeInterval);
                Assert.AreEqual(100, settings.MaxEntityGeoFixes);
                Assert.AreEqual(2000, settings.IndexHighWatermarkLimit);
                Assert.AreEqual(1500, settings.IndexLowWatermarkLimit);
                Assert.AreEqual(3, settings.IndexMaxGroupTableLevel);
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.IndexBalancingInterval);
                Assert.AreEqual(false, settings.IPGeocodeEnabled);
                Assert.AreEqual(new Uri("http://www.google.com"), settings.IPGeocodeSourceUri);
                Assert.AreEqual("<RSAKeyValue><Modulus>trZSBdVGPcfVlaQrKK7nVAmMqu65eKLKoQuAujN8fd3OCZlkayn1Cil6SInwLS9sHIHG1QXgHT+d/M3bQybaeE0kU5SMQQIXi2Z41EHaVcaXU3Pw81v2ybFkVf8eQPTmuxESyw85BymVkre5rSZiRlk7nlZQN812z36mv6ByNYE=</Modulus><Exponent>AQAB</Exponent><P>2KQogNyfS5KEVh93Fsp/b7lovUfZvxkfBH5cdOhX8S43eLVZV8hV1I54B8FWoiA7RIHq/52WWS8E5TzI5ntZ7Q==</P><Q>1+gk6cHptG/aWfmVsRd7ZvnoZiWHPTijnoRtaO3ynAgb1Vn8VehqQBbunN6EqKV2/Qnt5flE/+aY98MBJFNHZQ==</Q><DP>rgPeTPPqOGfuSMdpfzMU/gcuLKwkKa3iDlf5qCZhTWdUQ29X3n0bBGuT2pbgIcZGFRdOThilBeoQwpn6vbfjWQ==</DP><DQ>DLujGaoW9041aWL/wf7phywr2YJTFHg3pgyXSz3lNfCAe7ef2w0m3vq7PcMdvbhsaQXh4tMtj43w7YOxmIvUxQ==</DQ><InverseQ>V1tegYUSKG89LsK44zaGKWAYAyygNimBkgLgxs92asWb6LVzA/iP1R1WEELb4VOKj7h0KLk7kY0sZxC/gC2Ybg==</InverseQ><D>VpQ4c9knKrlZ5UngxatzpKfNx2XN73M8j2mS+yjQkhgbvQK5yeoc2k7jSiJK9C5njW6VmHXrSBDQPW4Su1Ra6x+zDiCqK6qY/BIDetO6MuU/5zTd0+uMmLZg1PUkU4e42MJZDjNET2KFmG0IrB/jp2099lI/E6d2Mv2FzxO0atE=</D></RSAKeyValue>", settings.IPGeocodeSourceRsaKey);
                Assert.AreEqual(TimeSpan.FromHours(1), settings.IPGeocodeSourcePollInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.IPGeocodeSourceTimeout);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.SweepInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(30), settings.BkInterval);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

