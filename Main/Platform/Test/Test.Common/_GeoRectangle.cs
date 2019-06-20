//-----------------------------------------------------------------------------
// FILE:        _GeoRectangle.cs
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
    public class _GeoRectangle
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_Basic()
        {
            GeoRectangle r1;

            r1 = new GeoRectangle(new GeoCoordinate(45, 60), new GeoCoordinate(1, 2));
            Assert.AreEqual(new GeoCoordinate(45, 60), r1.Northeast);
            Assert.AreEqual(new GeoCoordinate(1, 2), r1.Southwest);

            r1 = new GeoRectangle(45, 60, 1, 2);
            Assert.AreEqual(new GeoCoordinate(45, 60), r1.Northeast);
            Assert.AreEqual(new GeoCoordinate(1, 2), r1.Southwest);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_Compare()
        {
            GeoRectangle r1, r2;

            r1 = new GeoRectangle(new GeoCoordinate(45, 60), new GeoCoordinate(1, 2));

            r2 = r1;
            Assert.IsTrue(r1.Equals(r2));
            Assert.IsFalse(r1.Equals(null));
            Assert.IsFalse(r1.Equals(10));
            Assert.IsTrue(r1 == r2);
            Assert.IsFalse(r1 != r2);

            r2 = new GeoRectangle(new GeoCoordinate(44, 61), new GeoCoordinate(0, 1));
            Assert.IsFalse(r1.Equals(r2));
            Assert.IsFalse(r1 == r2);
            Assert.IsTrue(r1 != r2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_ContainsPoint()
        {
            var r = new GeoRectangle(new GeoCoordinate(45, 60), new GeoCoordinate(0, 0));

            // Test points on the boundaries.

            Assert.IsTrue(r.Contains(new GeoCoordinate(45, 30)));
            Assert.IsTrue(r.Contains(new GeoCoordinate(22.5, 60)));
            Assert.IsTrue(r.Contains(new GeoCoordinate(0, 30)));
            Assert.IsTrue(r.Contains(new GeoCoordinate(22.5, 0)));

            // Test a point within

            Assert.IsTrue(r.Contains(new GeoCoordinate(22.5, 30)));

            // Test points outside

            Assert.IsFalse(r.Contains(new GeoCoordinate(22.5, 61)));
            Assert.IsFalse(r.Contains(new GeoCoordinate(-1, 61)));
            Assert.IsFalse(r.Contains(new GeoCoordinate(-1, 30)));
            Assert.IsFalse(r.Contains(new GeoCoordinate(0, -1)));
            Assert.IsFalse(r.Contains(new GeoCoordinate(22.5, -1)));
            Assert.IsFalse(r.Contains(new GeoCoordinate(46, -1)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_Wraparound()
        {
            // Verify that we detect coordinate wraparound.

            ExtendedAssert.Throws<ArgumentException>(() => new GeoRectangle(new GeoCoordinate(45, 5), new GeoCoordinate(60, 0)));
            ExtendedAssert.Throws<ArgumentException>(() => new GeoRectangle(new GeoCoordinate(45, -10), new GeoCoordinate(0, 0)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_Serialize()
        {
            var r1 = new GeoRectangle(new GeoCoordinate(30, 20), new GeoCoordinate(-30, 5));
            var r2 = (GeoRectangle)GeoRegion.Parse(r1.ToString());

            Assert.AreEqual(r1, r2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_Bounds()
        {
            var r1 = new GeoRectangle(new GeoCoordinate(30, 20), new GeoCoordinate(-30, 5));

            Assert.AreEqual(r1, r1.Bounds);

            // Verify that bounds are restored after serialization

            var r2 = new GeoRectangle(r1.ToString());

            Assert.AreEqual(r1, r2.Bounds);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoRectangle_IntersectsWith()
        {
            GeoRectangle r, rTest;

            // $todo(jeff.lill): Geo related operations need some work.

            Assert.Inconclusive("Geo related operations need some work.");

            //-----------------------------------------------------------------
            // Test cases where the rectangles don't intersect at all.

            r = new GeoRectangle(10, 10, -10, -10);

            // Verify that a rectangle the same size as the test rectangle
            // that appears in the 8 areas around the test and does not touch
            // are considered to be non-intersecting.

            rTest = new GeoRectangle(10, 10, -10, -10);
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, -30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, -30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, -30)));

            // Verify that a rectangle the same size as the test rectangle
            // that appears in the 8 areas around the test and does not touch
            // are considered to be non-intersecting.

            rTest = new GeoRectangle(5, 5, -5, -5);
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, +30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-30, -30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, -30)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+30, -30)));

            // Verify that a rectangle the same size as the test rectangle
            // that appears in the 8 areas around the test and does touch
            // are considered to be non-intersecting.

            rTest = new GeoRectangle(10, 10, -10, -10);
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, +20)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+20, +20)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+20, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-20, +20)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-20, 0)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(-20, -20)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(0, -20)));
            Assert.IsFalse(r.IntersectsWith(rTest.Translate(+20, -20)));

            // Verify that rectangles that span the areas above, below,
            // and on either side of the test rectangle but do not touch
            // are considered to be non-intersecting.

            Assert.IsFalse(r.IntersectsWith(new GeoRectangle(20, 20, 15, -20)));       // Above
            Assert.IsFalse(r.IntersectsWith(new GeoRectangle(-15, 20, -20, -20)));     // Below
            Assert.IsFalse(r.IntersectsWith(new GeoRectangle(20, -15, -20, -20)));     // Left
            Assert.IsFalse(r.IntersectsWith(new GeoRectangle(20, 20, 15, -20)));       // Right

            //-----------------------------------------------------------------
            // Verify intersection

            // A rectangle the entirely surrounds the test rectangle.

            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(20, 20, -20, -20)));

            // A rectangle that is entirely within the test.

            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(5, 5, -5, -5)));

            // The identical rectangle.

            Assert.IsTrue(r.IntersectsWith(r));

            // Rectangles with two corners within the test rectangle.

            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(0, 0, -5, -20)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(0, 0, -20, -5)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(5, 20, 0, 0)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(20, 5, 0, 0)));

            // Rectangles with one corner within the test rectangle.

            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(0, 0, -20, -20)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(0, 0, -20, 20)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(20, 20, 0, 0)));
            Assert.IsTrue(r.IntersectsWith(new GeoRectangle(20, -20, 0, 0)));
        }
    }
}

