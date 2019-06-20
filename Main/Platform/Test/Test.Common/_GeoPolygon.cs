//-----------------------------------------------------------------------------
// FILE:        _GeoPolygon.cs
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
    public class _GeoPolygon
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Empty()
        {
            // Verify that a polygon created from an empty set of vertices is initalized to (0,0).

            var poly = new GeoPolygon(new GeoCoordinate[0]);

            Assert.IsTrue(poly.IsPoint);
            Assert.AreEqual(1, poly.Vertices.Count);
            Assert.AreEqual(GeoCoordinate.Origin, poly.Vertices[0]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Point()
        {
            // Verifies that point polygons are allowed.

            var pt = new GeoCoordinate(10, 20);
            var poly = new GeoPolygon(new GeoCoordinate[] { pt });

            Assert.IsTrue(poly.IsPoint);
            Assert.AreEqual(1, poly.Vertices.Count);
            Assert.AreEqual(pt, poly.Vertices[0]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Closed()
        {
            // Verify that a closed set of polygon vertices works.

            var vertices = new GeoCoordinate[] {

                new GeoCoordinate(0,0),
                new GeoCoordinate(30,0),
                new GeoCoordinate(30,30),
                new GeoCoordinate(0,30),
                new GeoCoordinate(0,0)
            };

            var poly = new GeoPolygon(vertices);

            Assert.IsFalse(poly.IsPoint);
            Assert.AreEqual(vertices.Length - 1, poly.Vertices.Count);

            for (int i = 0; i < poly.Vertices.Count; i++)
                Assert.AreEqual(vertices[i], poly.Vertices[i]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Open()
        {
            // Verify that a an open set of polygon vertices is closed
            // by the constructor.

            var vertices = new GeoCoordinate[] {

                new GeoCoordinate(0,0),
                new GeoCoordinate(30,0),
                new GeoCoordinate(30,30),
                new GeoCoordinate(0,30)
            };

            var poly = new GeoPolygon(vertices);

            Assert.IsFalse(poly.IsPoint);
            Assert.AreEqual(vertices.Length, poly.Vertices.Count);

            for (int i = 0; i < vertices.Length; i++)
                Assert.AreEqual(vertices[i], poly.Vertices[i]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_ContainsPoint()
        {
            GeoPolygon poly;

            //-----------------------------------------------------------------
            // Test a point polygon.

            poly = new GeoPolygon(new GeoCoordinate[] { new GeoCoordinate(10, 10) });
            Assert.IsTrue(poly.Contains(new GeoCoordinate(10, 10)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(10, 0)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(0, 10)));

            //-----------------------------------------------------------------
            // Test a simple rectangle

            poly = new GeoPolygon(
                new GeoCoordinate[] {

                    new GeoCoordinate(45,0),
                    new GeoCoordinate(0,0),
                    new GeoCoordinate(6,60),
                    new GeoCoordinate(45,60)
                });

            // Points inside

            Assert.IsTrue(poly.Contains(new GeoCoordinate(22.5, 30)));

            // Points outside

            Assert.IsFalse(poly.Contains(new GeoCoordinate(22.5, 61)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(-1, 61)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(-1, 30)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(0, -1)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(22.5, -1)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(46, -1)));

            //-----------------------------------------------------------------
            // Test a slanted parallelogram.

            poly = new GeoPolygon(
                new GeoCoordinate[] {

                    new GeoCoordinate(45,0),
                    new GeoCoordinate(0,45),
                    new GeoCoordinate(0,155),
                    new GeoCoordinate(45,65)
                });

            // Points inside the polygon

            Assert.IsTrue(poly.Contains(new GeoCoordinate(22.5, 60)));
            Assert.IsTrue(poly.Contains(new GeoCoordinate(1, 119)));
            Assert.IsTrue(poly.Contains(new GeoCoordinate(1, 46)));

            // Points outside the polygon but within the bounding rectangle

            Assert.IsFalse(poly.Contains(new GeoCoordinate(44, 119)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(1, 1)));

            // Points outside the bounding rectangle

            Assert.IsFalse(poly.Contains(new GeoCoordinate(46, 121)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(46, 60)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(46, -1)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(22.5, -1)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(44, 119)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(-1, -1)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(-1, 60)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(-1, 121)));
            Assert.IsFalse(poly.Contains(new GeoCoordinate(22.5, 121)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Serialize()
        {
            GeoPolygon poly;

            poly = new GeoPolygon(
                new GeoCoordinate[] {

                    new GeoCoordinate(10,20)
                });

            poly = (GeoPolygon)GeoRegion.Parse(poly.ToString());
            Assert.AreEqual(1, poly.Vertices.Count);
            Assert.IsTrue(new GeoCoordinate(10, 20) == poly.Vertices[0]);

            poly = new GeoPolygon(
                new GeoCoordinate[] {

                    new GeoCoordinate(10,20),
                    new GeoCoordinate(0,20),
                    new GeoCoordinate(0,0)
                });

            poly = (GeoPolygon)GeoRegion.Parse(poly.ToString());
            Assert.AreEqual(3, poly.Vertices.Count);
            Assert.IsTrue(new GeoCoordinate(10, 20) == poly.Vertices[0]);
            Assert.IsTrue(new GeoCoordinate(0, 20) == poly.Vertices[1]);
            Assert.IsTrue(new GeoCoordinate(0, 0) == poly.Vertices[2]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void GeoPolygon_Bounds()
        {
            GeoPolygon poly;

            poly = new GeoPolygon(
                new GeoCoordinate[] {

                    new GeoCoordinate(10,20),
                    new GeoCoordinate(0,20),
                    new GeoCoordinate(0,0)
                });

            Assert.AreEqual(new GeoRectangle(10, 20, 0, 0), poly.Bounds);

            // Verify that bounds are restored after serialization

            poly = (GeoPolygon)GeoRegion.Parse(poly.ToString());
            Assert.AreEqual(new GeoRectangle(10, 20, 0, 0), poly.Bounds);
        }
    }
}

