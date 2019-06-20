//-----------------------------------------------------------------------------
// FILE:        _GeoQuery.cs
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
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Msgs;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _GeoQuery
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoQuery_GeoEntityQueryOptions()
        {
            GeoEntityQueryOptions options;
            GeoQueryMsg queryMsg;

            // Test default options

            options = new GeoEntityQueryOptions();
            Assert.AreEqual(1, options.FixCount);
            Assert.AreEqual(DateTime.MinValue, options.MinFixTimeUtc);
            Assert.AreEqual(GeoFixField.All, options.FixFields);

            options.FixCount = 2;
            options.MinFixTimeUtc = new DateTime(2011, 5, 2);
            options.FixFields = GeoFixField.Latitude | GeoFixField.Longitude;

            // Test serialization and rehydration from a query message.

            queryMsg = new GeoQueryMsg();
            options.SaveTo(queryMsg, Stub.Param);

            options = new GeoEntityQueryOptions();
            options.LoadFrom(queryMsg, Stub.Param);

            Assert.AreEqual(2, options.FixCount);
            Assert.AreEqual(new DateTime(2011, 5, 2), options.MinFixTimeUtc);
            Assert.AreEqual(GeoFixField.Latitude | GeoFixField.Longitude, options.FixFields);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoQuery_GeoHeatmapQueryOptions()
        {
            GeoHeatmapQueryOptions options;
            GeoQueryMsg queryMsg;

            // Test default options

            options = new GeoHeatmapQueryOptions();
            Assert.IsTrue(options.MapBounds.IsEmpty);
            Assert.IsNull(options.ResolutionMiles);
            Assert.IsNull(options.ResolutionKilometers);

            options.MapBounds = new GeoRectangle(10, 10, -10, -10);
            options.ResolutionMiles = 10;

            Assert.AreEqual(new GeoRectangle(10, 10, -10, -10), options.MapBounds);
            Assert.AreEqual(10.0, options.ResolutionMiles);
            Assert.IsTrue(Math.Abs(10.0 / 0.621371192 - options.ResolutionKilometers.Value) < 0.001);

            // Test serialization and rehydration from a query message.

            queryMsg = new GeoQueryMsg();
            options.SaveTo(queryMsg, Stub.Param);

            options = new GeoHeatmapQueryOptions();
            options.LoadFrom(queryMsg, Stub.Param);

            Assert.AreEqual(new GeoRectangle(10, 10, -10, -10), options.MapBounds);
            Assert.AreEqual(10.0, options.ResolutionMiles);
            Assert.IsTrue(Math.Abs(10.0 / 0.621371192 - options.ResolutionKilometers.Value) < 0.001);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoQuery_GeoQuery()
        {
            Assert.Inconclusive("Not implemented");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoQuery_GeoEntityResults()
        {
            Assert.Inconclusive("Not implemented");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoQuery_GeoHeatmapResults()
        {
            Assert.Inconclusive("Not implemented");
        }
    }
}

