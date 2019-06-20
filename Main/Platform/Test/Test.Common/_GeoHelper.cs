//-----------------------------------------------------------------------------
// FILE:        _GeoHelper.cs
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
    public class _GeoHelper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoHelper_Distance()
        {
            // Compute the distance between two points on the earth where
            // we know the distance from the calculator on: 
            //
            // http://www.movable-type.co.uk/scripts/latlong.html

            var millCreek = new GeoCoordinate(47.845, -122.248);
            var rosemont = new GeoCoordinate(47.621, -122.092);
            var distance = GeoHelper.Distance(millCreek, rosemont, GeoHelper.EarthRadiusKilometers);

            Assert.IsTrue(27.45 <= distance && distance < 27.55);

            distance = GeoHelper.Distance(rosemont, millCreek, GeoHelper.EarthRadiusKilometers);
            Assert.IsTrue(27.45 <= distance && distance < 27.55);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoHelper_Plot()
        {
            // Plot a destination from an origin point using a course and
            // distance and compare this to a known result from:
            //
            // http://www.movable-type.co.uk/scripts/latlong.html

            var start = new GeoCoordinate("50 03 59N", "005 42 53W");
            var end = GeoHelper.Plot(start, 45, 15, GeoHelper.EarthRadiusKilometers);
            var reference = new GeoCoordinate("50°09′42″N", "005°33′57″W");

            Assert.IsTrue(start.Latitude < end.Latitude);
            Assert.IsTrue(start.Longitude < end.Longitude);
            Assert.IsTrue(GeoHelper.Distance(end, reference, GeoHelper.EarthRadiusKilometers) < 0.02);    // Verify accuracy to within 2%
        }
    }
}

