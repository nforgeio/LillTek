//-----------------------------------------------------------------------------
// FILE:        GeoHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Geographic coordinate related utilities.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Geographic coordinate related utilities.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b><font color="red">Warning:</font></b> LillTek geographical classes support
    /// Euclidean geometry and do not currently support planar wraparound at the 180th
    /// meridian (opposite of the Prime Merdian) or at the poles.
    /// </note>
    /// </remarks>
    public static class GeoHelper
    {
        /// <summary>
        /// Radius of the Earth in miles.
        /// </summary>
        public const double EarthRadiusMiles = 3961.3;

        /// <summary>
        /// Radius of the Earth on kilometers.
        /// </summary>
        public const double EarthRadiusKilometers = 6378.1;

        /// <summary>
        /// Computes the distance between two points on a sphere.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <param name="planetRadius">
        /// The radius of the planet in the desired output measurement units 
        /// (see <see cref="EarthRadiusMiles" /> or <see cref="EarthRadiusKilometers" />).
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="planetRadius" /> is less than or equal to zero.
        /// </exception>
        /// <returns>
        /// The distance along the surface of the planet between the two 
        /// points (in the same units as the radius parameter).
        /// </returns>
        public static double Distance(GeoCoordinate point1, GeoCoordinate point2, double planetRadius)
        {
            if (planetRadius <= 0.0)
                throw new ArgumentException(string.Format("[planetRadius={0}] must be >= 0", planetRadius));

            double dLat = ToRadians(point2.Latitude - point1.Latitude);
            double dLon = ToRadians(point2.Longitude - point1.Longitude);
            double a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                          Math.Cos(ToRadians(point1.Latitude)) * Math.Cos(ToRadians(point2.Latitude)) *
                          Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c    = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
            double d    = planetRadius * c;

            return d;
        }

        /// <summary>
        /// Computes the coordinate of the location the given course and distance from a starting point.
        /// </summary>
        /// <param name="point">The starting point.</param>
        /// <param name="course">The course in degrees from true north.</param>
        /// <param name="distance">The distance.</param>
        /// <param name="planetRadius">
        /// The radius of the planet in the same measurement units as <paramref name="distance" />
        /// (see <see cref="EarthRadiusMiles" /> or <see cref="EarthRadiusKilometers" />).
        /// </param>
        /// <returns>The destination point.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="planetRadius" /> is less than or equal to zero, <paramref name="course" />, is 
        /// out of the range of 0..360 or if <paramref name="distance" /> is less than zero or greater than <paramref name="planetRadius" />.
        /// </exception>
        public static GeoCoordinate Plot(GeoCoordinate point, double course, double distance, double planetRadius)
        {
            if (planetRadius <= 0.0)
                throw new ArgumentException(string.Format("[planetRadius={0}] must be >= 0", planetRadius));

            if (course < 0.0 || course >= 360.0)
                throw new ArgumentException(string.Format("[course={0}] is out of the range of 0 <= course < 360", course));

            if (distance < 0 || distance > planetRadius)
                throw new ArgumentException(string.Format("[distance={0}] is out of range of 0 <= planetRadius", distance));

            if (distance == 0 || distance == planetRadius)
                return point;   // Either didn't move or wrapped all the way across the globe

            // Adapted from javascript code found at: http://www.movable-type.co.uk/scripts/latlong.html

            var d    = distance;
            var brng = ToRadians(course);
            var R    = planetRadius;
            var lat1 = ToRadians(point.Latitude);
            var lon1 = ToRadians(point.Longitude);
            var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d / R) + Math.Cos(lat1) * Math.Sin(d / R) * Math.Cos(brng));
            var lon2 = lon1 + Math.Atan2(Math.Sin(brng) * Math.Sin(d / R) * Math.Cos(lat1), Math.Cos(d / R) - Math.Sin(lat1) * Math.Sin(lat2));

            lon2 = (lon2 + 3 * Math.PI) % (2 * Math.PI) - Math.PI;  // normalise to -180...+180

            return new GeoCoordinate(ToDegrees(lat2), ToDegrees(lon2));
        }

        /// <summary>
        /// Converts an angle from degrees to radians.
        /// </summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>Converted angle in radians.</returns>
        public static double ToRadians(double degrees)
        {
            return Math.PI * degrees / 180.0;
        }

        /// <summary>
        /// Converts and angle from radians to degrees.
        /// </summary>
        /// <param name="radians">Angle in radians.</param>
        /// <returns>Converted angle in degrees.</returns>
        public static double ToDegrees(double radians)
        {
            return 180.0 * radians / Math.PI;
        }

        /// <summary>
        /// Converts kilometers to miles.
        /// </summary>
        /// <param name="kilometers">Distance in kilometers.</param>
        /// <returns>Converted distance in miles.</returns>
        public static double ToMiles(double kilometers)
        {
            return kilometers * 0.621371192;
        }

        /// <summary>
        /// Converts miles to kilometers.
        /// </summary>
        /// <param name="miles">Distance in miles.</param>
        /// <returns>Converted distance in kilometers.</returns>
        public static double ToKilometers(double miles)
        {
            return miles / 0.621371192;
        }
    }
}
