//-----------------------------------------------------------------------------
// FILE:        _GeoFix.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests for the Config class.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _GeoFix
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoFix_Defaults()
        {
            var fix = new GeoFix();

            Assert.IsNull(fix.TimeUtc);
            Assert.AreEqual(double.NaN, fix.Latitude);
            Assert.AreEqual(double.NaN, fix.Longitude);
            Assert.AreEqual(double.NaN, fix.Altitude);
            Assert.AreEqual(double.NaN, fix.Course);
            Assert.AreEqual(double.NaN, fix.Speed);
            Assert.AreEqual(double.NaN, fix.HorizontalAccuracy);
            Assert.AreEqual(double.NaN, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.Unknown, fix.Technology);
            Assert.AreEqual(NetworkStatus.Unknown, fix.NetworkStatus);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoFix_Serialize()
        {
            GeoFix fix;
            string[] fields;

            // Default values

            fix = new GeoFix();
            fields = fix.ToString().Split(',');

            Assert.AreEqual(10, fields.Length);
            Assert.AreEqual("", fields[0]);      // Null TimeUtc is rendered as empty string
            Assert.AreEqual("", fields[1]);      // NaN coordinates are rendered as empty strings
            Assert.AreEqual("", fields[2]);
            Assert.AreEqual("", fields[3]);
            Assert.AreEqual("", fields[4]);
            Assert.AreEqual("", fields[5]);
            Assert.AreEqual("", fields[6]);
            Assert.AreEqual("", fields[7]);
            Assert.AreEqual("", fields[8]);      // GetFixTechnology.Unknown is rendered as empty
            Assert.AreEqual("", fields[9]);      // NetworkStatus.Unknown is rendered as empty

            // Set values

            fix = new GeoFix()
            {
                TimeUtc = new DateTime(2011, 3, 3, 10, 55, 15),
                Latitude = 1.1,
                Longitude = 2.2,
                Altitude = 3.3,
                Course = 4.4,
                Speed = 5.5,
                HorizontalAccuracy = 6.6,
                VerticalAccurancy = 7.7,
                Technology = GeoFixTechnology.Tower,
                NetworkStatus = NetworkStatus.Gsm
            };

            fields = fix.ToString().Split(',');

            Assert.AreEqual(10, fields.Length);
            Assert.AreEqual(fix.TimeUtc.Value.Ticks.ToString(), fields[0]);
            Assert.AreEqual("1.1", fields[1]);
            Assert.AreEqual("2.2", fields[2]);
            Assert.AreEqual("3.3", fields[3]);
            Assert.AreEqual("4.4", fields[4]);
            Assert.AreEqual("5.5", fields[5]);
            Assert.AreEqual("6.6", fields[6]);
            Assert.AreEqual("7.7", fields[7]);
            Assert.AreEqual("Tower", fields[8]);
            Assert.AreEqual("Gsm", fields[9]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoFix_Serialize_Fields()
        {
            // Test serialization of specific fields

            GeoFix fix;

            fix = new GeoFix();
            Assert.AreEqual(",,,,,,,,,", fix.ToString(GeoFixField.All));

            fix = new GeoFix()
            {

                TimeUtc = new DateTime(2011, 3, 3, 10, 55, 15),
                Latitude = 1.1,
                Longitude = 2.2,
                Altitude = 3.3,
                Course = 4.4,
                Speed = 5.5,
                HorizontalAccuracy = 6.6,
                VerticalAccurancy = 7.7,
                Technology = GeoFixTechnology.Tower,
                NetworkStatus = NetworkStatus.Gsm
            };

            Assert.AreEqual(string.Format("{0},,,,,,,,,", fix.TimeUtc.Value.Ticks), fix.ToString(GeoFixField.TimeUtc));
            Assert.AreEqual(",1.1,,,,,,,,", fix.ToString(GeoFixField.Latitude));
            Assert.AreEqual(",,2.2,,,,,,,", fix.ToString(GeoFixField.Longitude));
            Assert.AreEqual(",,,3.3,,,,,,", fix.ToString(GeoFixField.Altitude));
            Assert.AreEqual(",,,,4.4,,,,,", fix.ToString(GeoFixField.Course));
            Assert.AreEqual(",,,,,5.5,,,,", fix.ToString(GeoFixField.Speed));
            Assert.AreEqual(",,,,,,6.6,,,", fix.ToString(GeoFixField.HorizontalAccuracy));
            Assert.AreEqual(",,,,,,,7.7,,", fix.ToString(GeoFixField.VerticalAccurancy));
            Assert.AreEqual(",,,,,,,,Tower,", fix.ToString(GeoFixField.Technology));
            Assert.AreEqual(",,,,,,,,,Gsm", fix.ToString(GeoFixField.NetworkStatus));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoFix_Deserialize()
        {
            GeoFix fix;
            string v;

            // Default values

            fix = new GeoFix();
            v = fix.ToString();
            fix = new GeoFix(v);

            Assert.IsNull(fix.TimeUtc);
            Assert.AreEqual(double.NaN, fix.Latitude);
            Assert.AreEqual(double.NaN, fix.Longitude);
            Assert.AreEqual(double.NaN, fix.Altitude);
            Assert.AreEqual(double.NaN, fix.Course);
            Assert.AreEqual(double.NaN, fix.Speed);
            Assert.AreEqual(double.NaN, fix.HorizontalAccuracy);
            Assert.AreEqual(double.NaN, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.Unknown, fix.Technology);
            Assert.AreEqual(NetworkStatus.Unknown, fix.NetworkStatus);

            // Set values

            fix = new GeoFix()
            {

                TimeUtc = new DateTime(2011, 3, 3, 10, 55, 15),
                Latitude = 1.1,
                Longitude = 2.2,
                Altitude = 3.3,
                Course = 4.4,
                Speed = 5.5,
                HorizontalAccuracy = 6.6,
                VerticalAccurancy = 7.7,
                Technology = GeoFixTechnology.GPS,
                NetworkStatus = NetworkStatus.Cdma
            };

            Assert.AreEqual(new DateTime(2011, 3, 3, 10, 55, 15), fix.TimeUtc);
            Assert.AreEqual(1.1, fix.Latitude);
            Assert.AreEqual(2.2, fix.Longitude);
            Assert.AreEqual(3.3, fix.Altitude);
            Assert.AreEqual(4.4, fix.Course);
            Assert.AreEqual(5.5, fix.Speed);
            Assert.AreEqual(6.6, fix.HorizontalAccuracy);
            Assert.AreEqual(7.7, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.GPS, fix.Technology);
            Assert.AreEqual(NetworkStatus.Cdma, fix.NetworkStatus);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoFix_Parse()
        {
            GeoFix fix;

            // Test null or empty input strings.

            Assert.IsNull(GeoFix.Parse(null));
            Assert.IsNull(GeoFix.Parse(""));
            Assert.IsNull(GeoFix.Parse("    "));

            // Test actual parsing.

            fix = new GeoFix()
            {

                TimeUtc = new DateTime(2011, 3, 3, 10, 55, 15),
                Latitude = 1.1,
                Longitude = 2.2,
                Altitude = 3.3,
                Course = 4.4,
                Speed = 5.5,
                HorizontalAccuracy = 6.6,
                VerticalAccurancy = 7.7,
                Technology = GeoFixTechnology.GPS,
                NetworkStatus = NetworkStatus.Cdma
            };

            fix = GeoFix.Parse(fix.ToString());

            Assert.AreEqual(new DateTime(2011, 3, 3, 10, 55, 15), fix.TimeUtc);
            Assert.AreEqual(1.1, fix.Latitude);
            Assert.AreEqual(2.2, fix.Longitude);
            Assert.AreEqual(3.3, fix.Altitude);
            Assert.AreEqual(4.4, fix.Course);
            Assert.AreEqual(5.5, fix.Speed);
            Assert.AreEqual(6.6, fix.HorizontalAccuracy);
            Assert.AreEqual(7.7, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.GPS, fix.Technology);
            Assert.AreEqual(NetworkStatus.Cdma, fix.NetworkStatus);
        }
    }
}

