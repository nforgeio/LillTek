//-----------------------------------------------------------------------------
// FILE:        _GeoCircle.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _GeoCircle
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCircle_Basic()
        {
            var circle = new GeoCircle(new GeoCoordinate(10, 20), 10.5, GeoHelper.EarthRadiusKilometers);

            Assert.AreEqual(new GeoCoordinate(10, 20), circle.Center);
            Assert.AreEqual(10.5, circle.Radius);
            Assert.AreEqual(GeoHelper.EarthRadiusKilometers, circle.PlanetRadius);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCircle_Contains()
        {
            var circle = new GeoCircle(GeoCoordinate.Origin, 10, GeoHelper.EarthRadiusMiles);

            Assert.IsTrue(circle.Contains(circle.Center));
            Assert.IsTrue(circle.Contains(GeoHelper.Plot(circle.Center, 0.45, 5, GeoHelper.EarthRadiusMiles)));
            Assert.IsFalse(circle.Contains(GeoHelper.Plot(circle.Center, 0.45, 15, GeoHelper.EarthRadiusMiles)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCircle_Serialize()
        {
            var circle1 = new GeoCircle(GeoCoordinate.Origin, 10, GeoHelper.EarthRadiusMiles);
            var circle2 = (GeoCircle)GeoRegion.Parse(circle1.ToString());

            Assert.AreEqual(circle1, circle2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCircle_Bounds()
        {
            var circle = new GeoCircle(GeoCoordinate.Origin, 10, GeoHelper.EarthRadiusMiles);
            var r = circle.Bounds;

            Assert.IsTrue(Math.Abs(r.Southwest.Longitude - GeoHelper.Plot(circle.Center, 270, circle.Radius, GeoHelper.EarthRadiusMiles).Longitude) < 0.01);
            Assert.IsTrue(Math.Abs(r.Northeast.Longitude - GeoHelper.Plot(circle.Center, 90, circle.Radius, GeoHelper.EarthRadiusMiles).Longitude) < 0.01);
            Assert.IsTrue(Math.Abs(r.Southwest.Latitude - GeoHelper.Plot(circle.Center, 180, circle.Radius, GeoHelper.EarthRadiusMiles).Latitude) < 0.01);
            Assert.IsTrue(Math.Abs(r.Northeast.Latitude - GeoHelper.Plot(circle.Center, 0, circle.Radius, GeoHelper.EarthRadiusMiles).Latitude) < 0.01);

            // Make sure that bounds are restored after serialization.

            circle = new GeoCircle(circle.ToString());

            Assert.AreEqual(r, circle.Bounds);
        }
    }
}

