﻿//-----------------------------------------------------------------------------
// FILE:        GeoCircle.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a circular area on the surface of a sphere.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Describes a circular area on the surface of a sphere.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The area described will clip to the edges of Mercator map projection.
    /// </note>
    /// </remarks>
    public sealed class GeoCircle : GeoRegion
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Determines whether two <see cref="GeoCircle" />s have the same values.
        /// </summary>
        /// <param name="circle1">The first circle.</param>
        /// <param name="circle2">The second circle.</param>
        /// <returns><c>true</c> if the circles are equal.</returns>
        public static bool operator ==(GeoCircle circle1, GeoCircle circle2)
        {
            if ((object)circle1 == null && (object)circle2 == null)
                return true;
            else if ((object)circle1 == null || (object)circle2 == null)
                return false;

            return circle1.center == circle2.center &&
                   circle1.radius == circle2.radius &&
                   circle1.planetRadius == circle2.planetRadius;
        }

        /// <summary>
        /// Determines whether two <see cref="GeoCircle" />s do not have the same values.
        /// </summary>
        /// <param name="circle1">The first circle.</param>
        /// <param name="circle2">The second circle.</param>
        /// <returns><c>true</c> if the circles are not equal.</returns>
        public static bool operator !=(GeoCircle circle1, GeoCircle circle2)
        {
            if ((object)circle1 == null && (object)circle2 == null)
                return false;
            else if ((object)circle1 == null || (object)circle2 == null)
                return true;

            return circle1.center != circle2.center ||
                   circle1.radius != circle2.radius ||
                   circle1.planetRadius != circle2.planetRadius;
        }

        //---------------------------------------------------------------------
        // Instance members

        private GeoCoordinate center;
        private double radius;
        private double planetRadius;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="center">Coordinates of the center of the cricle.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="planetRadius">Radius of the planet expressed in the same units as <paramref name="radius" />.</param>
        public GeoCircle(GeoCoordinate center, double radius, double planetRadius)
        {
            this.center       = center;
            this.radius       = radius;
            this.planetRadius = planetRadius;

            ComputeBounds();
        }

        /// <summary>
        /// Constructor,
        /// </summary>
        /// <param name="lat">Latitude of the center point.</param>
        /// <param name="lon">Longitude of the center point.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="planetRadius">
        /// Radius of the planet expressed in the same units as <paramref name="radius" />.
        /// <note>
        /// This cannot be larger than 1/4 of the planet circumference.
        /// </note>
        /// </param>
        /// <exception cref="ArgumentException">Thrown if any of the coordinates not valid.</exception>
        public GeoCircle(double lat, double lon, double radius, double planetRadius)
        {
            if (radius < 0)
                throw new ArgumentException("GeoCircle: [radius] cannot be less than zero.", "radius");

            if (planetRadius <= 0)
                throw new ArgumentException("GeoCircle: [planetRadius] cannot be less than or equal to zero.", "planetRadius");

            if (radius > 2 * (planetRadius * Math.PI) / 4)
                throw new ArgumentException("GeoCircle: [radius] cannot be greater than 1/4 of the planet circumference.");

            this.center       = new GeoCoordinate(lat, lon);
            this.radius       = radius;
            this.planetRadius = planetRadius;

            ComputeBounds();
        }

        /// <summary>
        /// Deserializes the instance from a string generated by a previous call to <see cref="ToString" />.
        /// </summary>
        /// <param name="input">The serialized region.</param>
        /// <exception cref="ArgumentException">Thrown if the input is not valid.</exception>
        internal GeoCircle(string input)
        {
            string      key;
            int         pos;
            string[]    fields;
            string[]    latLon;

            if (input == null)
                throw new ArgumentNullException("input");

            try
            {
                pos = input.IndexOf(':');
                if (pos == -1)
                    throw new ArgumentException();

                key = input.Substring(0, pos);
                if (String.Compare(key, GeoRegion.CircleRegionKey, StringComparison.OrdinalIgnoreCase) != 0)
                    throw new ArgumentException();

                fields = input.Substring(pos + 1).Split(',');
                if (fields.Length != 3)
                    throw new ArgumentException();

                latLon = fields[0].Split(':');
                if (latLon.Length != 2)
                    throw new ArgumentException();

                center       = new GeoCoordinate(latLon[0], latLon[1]);
                radius       = double.Parse(fields[1]);
                planetRadius = double.Parse(fields[2]);

                ComputeBounds();
            }
            catch (Exception e)
            {
                throw new ArgumentException(string.Format("GeoCircle: Cannot deserialize [{0}].", input), e);
            }
        }

        /// <summary>
        /// Computes the bounding rectangle for the circle.
        /// </summary>
        private void ComputeBounds()
        {
            // This is a bit tricky.  The approach is to plot points for the edge of
            // the circle on the horizonal and vertical axes, clip these to the edges
            // of the map (since we don't implement coordinate wraparound) and then
            // form the rectangle.
            // 
            // Note that this is a bit of hack and may result in inaccuracies for some
            // combinations of planet radii and circle center points.

            double      left;
            double      top;
            double      right;
            double      bottom;

            left = GeoHelper.Plot(center, 270, radius, planetRadius).Longitude;
            if (left > center.Longitude)
                left = -180;    // clip

            right = GeoHelper.Plot(center, 90, radius, planetRadius).Longitude;
            if (right < center.Longitude)
                right = 180;    // clip

            top = GeoHelper.Plot(center, 0, radius, planetRadius).Latitude;
            if (top < center.Latitude)
                top = 90;

            bottom = GeoHelper.Plot(center, 180, radius, planetRadius).Latitude;
            if (bottom > center.Latitude)
                bottom = -90;

            base.Bounds = new GeoRectangle(top, right, bottom, left);
        }

        /// <summary>
        /// Returns the coordinates of the center of the cricle.
        /// </summary>
        public GeoCoordinate Center
        {
            get { return center; }
        }

        /// <summary>
        /// Returns the circle radius.
        /// </summary>
        public double Radius
        {
            get { return radius; }
        }

        /// <summary>
        /// Returns the planet radius.
        /// </summary>
        public double PlanetRadius
        {
            get { return planetRadius; }
        }

        /// <summary>
        /// Determines whether a point is within the circle.
        /// </summary>
        /// <param name="point">The point being tested.</param>
        /// <returns><c>true</c> if the point is within the circle.</returns>
        public override bool Contains(GeoCoordinate point)
        {
            if (!Bounds.Contains(point))
                return false;   // Handles clipping cases

            return GeoHelper.Distance(point, center, planetRadius) <= radius;
        }

        /// <summary>
        /// Serializes the region into a form suitable for transmission or writing
        /// to persistant storage.
        /// </summary>
        /// <returns>The serialized region.</returns>
        public override string ToString()
        {
            return string.Format("{0}:{1}:{2},{3},{4}", GeoRegion.CircleRegionKey, center.Latitude, center.Longitude, radius, planetRadius);
        }

        /// <summary>
        /// Determines whether this instance has the same value as another object.
        /// </summary>
        /// <param name="obj">The object being tested.</param>
        /// <returns><c>true</c> if the instances have the same value.</returns>
        public override bool Equals(object obj)
        {
            var test = obj as GeoCircle;

            if (object.ReferenceEquals(test, null))
                return false;

            return this == test;
        }

        /// <summary>
        /// Computes the hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return center.GetHashCode() ^ radius.GetHashCode() ^ planetRadius.GetHashCode();
        }
    }
}
