//-----------------------------------------------------------------------------
// FILE:        _GeoCompositeRegion.cs
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
    public class _GeoCompositeRegion
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_Empty()
        {
            // Verify that empty regions don't barf.

            var comp = new GeoCompositeRegion(new GeoRegion[0]);

            Assert.IsFalse(comp.Contains(0, 0));
            Assert.IsFalse(comp.Contains(10, 10));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_Additive()
        {
            GeoCompositeRegion comp;

            // Create a composite region of two disjoint rectangles.

            comp = new GeoCompositeRegion(
                new GeoRegion[] {
           
                    new GeoRectangle(30,30,0,0),
                    new GeoRectangle(30,-30,0,-60)
                });

            Assert.IsTrue(comp.Contains(15, -45));
            Assert.IsTrue(comp.Contains(15, 15));

            Assert.IsFalse(comp.Contains(15, -15));
            Assert.IsFalse(comp.Contains(45, 15));
            Assert.IsFalse(comp.Contains(-15, -45));

            // Create a composite region of two overlapping rectangles.

            comp = new GeoCompositeRegion(
                new GeoRegion[] {
           
                    new GeoRectangle(30,30,0,0),
                    new GeoRectangle(15,15,-15,-15)
                });

            Assert.IsTrue(comp.Contains(15, 15));
            Assert.IsTrue(comp.Contains(0, 0));

            Assert.IsFalse(comp.Contains(20, -5));
            Assert.IsFalse(comp.Contains(-5, 20));
            Assert.IsFalse(comp.Contains(0, -20));

            // Create a composite region with an additive subregion that's also composite.

            comp = new GeoCompositeRegion(
                new GeoRegion[] {

                    new GeoCircle(80,80,10,GeoHelper.EarthRadiusMiles),
                    new GeoCompositeRegion(
                        new GeoRegion[] {
           
                            new GeoRectangle(30,30,0,0),
                            new GeoRectangle(30,-30,0,-60)
                        })
                });

            Assert.IsTrue(comp.Contains(80, 80));
            Assert.IsTrue(comp.Contains(15, -45));
            Assert.IsTrue(comp.Contains(15, 15));

            Assert.IsFalse(comp.Contains(15, -15));
            Assert.IsFalse(comp.Contains(45, 15));
            Assert.IsFalse(comp.Contains(-15, -45));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_Subtractive()
        {
            GeoCompositeRegion comp;

            // Create a composite region by making a rectangular hole in
            // the middle of a rectangle.

            comp = new GeoCompositeRegion(
                new GeoRegion[] {
           
                    new GeoRectangle(30,30,-30,-30)
                },
                new GeoRegion[] {

                    new GeoRectangle(15,15,-15,-15)
                });

            Assert.IsFalse(comp.Contains(0, 0));
            Assert.IsTrue(comp.Contains(20, 0));
            Assert.IsTrue(comp.Contains(0, 20));
            Assert.IsTrue(comp.Contains(-20, 0));
            Assert.IsTrue(comp.Contains(0, -20));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_VerifyNotComposible()
        {
            // Verify that we get an exception when trying to compose a composite region
            // as a subtractive region.

            ExtendedAssert.Throws<ArgumentException>(
                () =>
                {

                    new GeoCompositeRegion(
                        new GeoRegion[] {

                            new GeoRectangle(30,0,0,30)
                        },
                        new GeoRegion[] {

                            new GeoCompositeRegion(new GeoRegion[0])
                        });
                });
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_Serialize()
        {
            GeoCompositeRegion comp;
            GeoPolygon poly;

            // Test additive regions only

            comp = new GeoCompositeRegion(
                new GeoRegion[] {

                    new GeoRectangle(30,30,-30,-30),
                    new GeoCircle(0,0,20,GeoHelper.EarthRadiusMiles),
                    new GeoPolygon(
                        new GeoCoordinate[] {
                            
                            new GeoCoordinate(0,0),
                            new GeoCoordinate(20,20),
                            new GeoCoordinate(0,30)
                        })
                });

            comp = (GeoCompositeRegion)GeoRegion.Parse(comp.ToString());

            Assert.AreEqual(3, comp.AdditiveRegions.Count);
            Assert.IsTrue(new GeoRectangle(30, 30, -30, -30) == (GeoRectangle)comp.AdditiveRegions[0]);
            Assert.IsTrue(new GeoCircle(0, 0, 20, GeoHelper.EarthRadiusMiles) == (GeoCircle)comp.AdditiveRegions[1]);

            poly = (GeoPolygon)comp.AdditiveRegions[2];
            Assert.AreEqual(3, poly.Vertices.Count);
            Assert.AreEqual(new GeoCoordinate(0, 0), poly.Vertices[0]);
            Assert.AreEqual(new GeoCoordinate(20, 20), poly.Vertices[1]);
            Assert.AreEqual(new GeoCoordinate(0, 30), poly.Vertices[2]);

            Assert.IsNull(comp.SubtractiveRegions);

            // Test additive and subtractive regions

            comp = new GeoCompositeRegion(
                new GeoRegion[] {

                    new GeoRectangle(10,10,0,0)
                },
                new GeoRegion[] {

                    new GeoRectangle(30,30,-30,-30),
                    new GeoCircle(0,0,20,GeoHelper.EarthRadiusMiles),
                    new GeoPolygon(
                        new GeoCoordinate[] {
                            
                            new GeoCoordinate(0,0),
                            new GeoCoordinate(20,20),
                            new GeoCoordinate(0,30)
                        })
                });

            comp = (GeoCompositeRegion)GeoRegion.Parse(comp.ToString());

            Assert.AreEqual(1, comp.AdditiveRegions.Count);
            Assert.IsTrue(new GeoRectangle(10, 10, 0, 0) == (GeoRectangle)comp.AdditiveRegions[0]);

            Assert.AreEqual(3, comp.SubtractiveRegions.Count);
            Assert.IsTrue(new GeoRectangle(30, 30, -30, -30) == (GeoRectangle)comp.SubtractiveRegions[0]);
            Assert.IsTrue(new GeoCircle(0, 0, 20, GeoHelper.EarthRadiusMiles) == (GeoCircle)comp.SubtractiveRegions[1]);

            poly = (GeoPolygon)comp.SubtractiveRegions[2];
            Assert.AreEqual(3, poly.Vertices.Count);
            Assert.AreEqual(new GeoCoordinate(0, 0), poly.Vertices[0]);
            Assert.AreEqual(new GeoCoordinate(20, 20), poly.Vertices[1]);
            Assert.AreEqual(new GeoCoordinate(0, 30), poly.Vertices[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoCompositeRegion_Bounds()
        {
            var comp = new GeoCompositeRegion(
                new GeoRegion[] {
           
                    new GeoRectangle(30,30,0,0),
                    new GeoRectangle(15,15,-15,-15)
                });

            Assert.AreEqual(new GeoRectangle(30, 30, -15, -15), comp.Bounds);

            // Verify that the bounds are restored after serialization.

            comp = new GeoCompositeRegion(comp.ToString());
            Assert.AreEqual(new GeoRectangle(30, 30, -15, -15), comp.Bounds);
        }
    }
}

