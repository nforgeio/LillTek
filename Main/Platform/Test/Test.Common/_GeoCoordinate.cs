//-----------------------------------------------------------------------------
// FILE:        _GeoCoordinate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

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
    public class _GeoCoordinate
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCoordinate_Parse()
        {
            GeoCoordinate c;

            c = new GeoCoordinate("47", "122");
            Assert.AreEqual(47.0, c.Latitude);
            Assert.AreEqual(122.0, c.Longitude);

            c = new GeoCoordinate("N47", "W122");
            Assert.AreEqual(47.0, c.Latitude);
            Assert.AreEqual(-122.0, c.Longitude);

            c = new GeoCoordinate("S47", "E122");
            Assert.AreEqual(-47.0, c.Latitude);
            Assert.AreEqual(122.0, c.Longitude);

            c = new GeoCoordinate("-47", "-122");
            Assert.AreEqual(-47.0, c.Latitude);
            Assert.AreEqual(-122.0, c.Longitude);

            c = new GeoCoordinate("47.543056", "122.106944");
            Assert.AreEqual("47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.106944", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("47° 32' 35.001\"", "122° 6' 24.9978\"");
            Assert.AreEqual("47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.106944", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("47  32 ", "122  6 ");
            Assert.AreEqual("47.533333", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.100000", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("S47  32 ", "E122  6 ");
            Assert.AreEqual("-47.533333", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.100000", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("-47  32 ", "-122  6 ");
            Assert.AreEqual("-47.533333", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("-122.100000", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("N 47° 32' 35.001\"", "W 122° 6' 24.9978\"");
            Assert.AreEqual("47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("-122.106944", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("S 47° 32' 35.001\"", "E 122° 6' 24.9978\"");
            Assert.AreEqual("-47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.106944", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("47° 32' 35.001\"S", "122° 6' 24.9978\"E");
            Assert.AreEqual("-47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("122.106944", string.Format("{0:#.#####0}", c.Longitude));

            c = new GeoCoordinate("47° 32' 35.001\"N", "122° 6' 24.9978\"W");
            Assert.AreEqual("47.543056", string.Format("{0:#.#####0}", c.Latitude));
            Assert.AreEqual("-122.106944", string.Format("{0:#.#####0}", c.Longitude));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCoordinate_Valiate()
        {
            ExtendedAssert.Throws<ArgumentException>(() => new GeoCoordinate(91.0, 0));
            ExtendedAssert.Throws<ArgumentException>(() => new GeoCoordinate(-91.0, 0));
            ExtendedAssert.Throws<ArgumentException>(() => new GeoCoordinate(0, 181.0));
            ExtendedAssert.Throws<ArgumentException>(() => new GeoCoordinate(0, -181.0));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCoordinate_Compare()
        {
            var z = new GeoCoordinate(0, 0);
            var p1 = new GeoCoordinate(1, 5);
            var p2 = new GeoCoordinate(2, 6);
            var p3 = new GeoCoordinate(1, 5);

            Assert.IsTrue(z == GeoCoordinate.Origin);
            Assert.IsTrue(z != p1);
            Assert.IsTrue(p1 == p3);
            Assert.IsTrue(p1 != p2);

            Assert.IsFalse(z != GeoCoordinate.Origin);
            Assert.IsFalse(z == p1);
            Assert.IsFalse(p1 != p3);
            Assert.IsFalse(p1 == p2);
        }
    }
}

