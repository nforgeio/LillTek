//-----------------------------------------------------------------------------
// FILE:        GeoCoordinate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Coordinates for a location on Earth.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// GPS coordinates for a location on Earth.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b><font color="red">Warning:</font></b> LillTek geographical classes support
    /// Euclidean geometry and do not currently support planar wraparound at the 180th
    /// meridian (opposite of the Prime Merdian) or at the poles.
    /// </note>
    /// </remarks>
    public struct GeoCoordinate
    {

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="p1">First coordinate.</param>
        /// <param name="p2">Second coordinate.</param>
        /// <returns><c>true</c> if the coordinates are equal.</returns>
        public static bool operator ==(GeoCoordinate p1, GeoCoordinate p2)
        {
            return p1.Latitude == p2.Latitude && p1.Longitude == p2.Longitude;
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        /// <param name="p1">First coordinate.</param>
        /// <param name="p2">Second coordinate.</param>
        /// <returns><c>true</c> if the coordinates are not equal.</returns>
        public static bool operator !=(GeoCoordinate p1, GeoCoordinate p2)
        {
            return p1.Latitude != p2.Latitude || p1.Longitude != p2.Longitude;
        }

        /// <summary>
        /// Returns the (0,0) coordinate.
        /// </summary>
        public static readonly GeoCoordinate Origin = new GeoCoordinate(0, 0);

        //---------------------------------------------------------------------
        // Instance members

        private double latitude;
        private double longitude;

        /// <summary>
        /// Constructs a coordinate from a latitude/longitude pair expressed
        /// as floating point numbers.
        /// </summary>
        /// <param name="latitude">The latitude in degrees.</param>
        /// <param name="longitude">The longitude in degrees.</param>
        /// <exception cref="ArgumentException">Thrown if an input value is out of range.</exception>
        public GeoCoordinate(double latitude, double longitude)
        {
            ValidateLatitude(latitude);
            ValidateLongitude(longitude);

            this.latitude = latitude;
            this.longitude = longitude;
        }

        /// <summary>
        /// Constructs a coordinate by parsing the latitude and longitude coordinates expressed
        /// as degrees minutes and seconds.
        /// </summary>
        /// <param name="latitude">The latitude.</param>
        /// <param name="longitude">The longitude.</param>
        /// <remarks>
        /// <para>
        /// This method attempts to be reasonably flexible about the format of the input strings.
        /// Here are some examples of valid input.
        /// </para>
        /// <list type="bullet">
        ///     <item>lat: 47.543056</item>
        ///     <item>lat: 47 32 35.001</item>
        ///     <item>lat: N47 32 35.001</item>
        ///     <item>lat: S47 32 35.001</item>
        ///     <item>lat: -47 32</item>
        ///     <item>lon: 122.106944</item>
        ///     <item>lon: 122 6 24.9978</item>
        ///     <item>lon: W122 6 24.9978</item>
        ///     <item>lon: E122 6 24.9978</item>
        ///     <item>lon: -122 6 24.9978</item>
        ///     <item>lon: 122 6 24.9978W</item>
        ///     <item>lon: 122 6 24.9978E</item>
        /// </list>
        /// </remarks>
        public GeoCoordinate(string latitude, string longitude)
        {
            double coordinate;

            coordinate = Parse(latitude, true);
            ValidateLatitude(coordinate);
            this.latitude = coordinate;

            coordinate = Parse(longitude, false);
            ValidateLongitude(coordinate);
            this.longitude = coordinate;
        }

        /// <summary>
        /// Handles the parsing of a coordinate element.
        /// </summary>
        /// <param name="input">The coordinate element string.</param>
        /// <param name="isLat">Pass <c>true</c> for latitude elements, <c>false</c> for longitude coordinates.</param>
        /// <returns>The parsed value.</returns>
        private static double Parse(string input, bool isLat)
        {
            string          errorMsg = isLat ? "Invalid latitude [{0}]." : "Invalid longitude [{0}].";
            StringBuilder   sb;
            string[]        fields;
            bool            isNegative;
            double          degrees;
            double          minutes;
            double          seconds;
            double          coordinate;
            int             pos;
            char            ch;
            bool            inWhitespace;

            // Parse the latitude

            if (input == null)
                throw new ArgumentNullException("input");

            input = input.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException(string.Format(errorMsg, input));

            // Sometimes the N/S or E/W characters are at the end of the string.
            // If this is the case then normalize the input by moving it to the
            // front.

            switch (input[input.Length - 1])
            {
                case 'n':
                case 's':
                case 'e':
                case 'w':

                    input = input.Substring(input.Length - 1) + input.Substring(0, input.Length - 1);
                    break;
            }

            // Parse the N/S or E/W or minus sign.

            isNegative = false;

            pos = 0;
            switch (input[pos])
            {
                case '-':

                    isNegative = true;
                    pos++;
                    break;

                case 's':

                    if (!isLat)
                        throw new ArgumentException(string.Format(errorMsg, input));

                    isNegative = true;
                    pos++;

                    for (; pos < input.Length && Char.IsWhiteSpace(input[pos]); pos++) ;    // Skip whitespace
                    break;

                case 'n':

                    if (!isLat)
                        throw new ArgumentException(string.Format(errorMsg, input));

                    pos++;

                    for (; pos < input.Length && Char.IsWhiteSpace(input[pos]); pos++) ;    // Skip whitespace
                    break;

                case 'e':

                    if (isLat)
                        throw new ArgumentException(string.Format(errorMsg, input));

                    pos++;

                    for (; pos < input.Length && Char.IsWhiteSpace(input[pos]); pos++) ;    // Skip whitespace
                    break;

                case 'w':

                    if (isLat)
                        throw new ArgumentException(string.Format(errorMsg, input));

                    isNegative = true;
                    pos++;

                    for (; pos < input.Length && Char.IsWhiteSpace(input[pos]); pos++) ;    // Skip whitespace
                    break;
            }

            // Normalize the input by removing any characters other than digits and
            // the decimal point and making sure that values are separated with a
            // single space.

            sb = new StringBuilder();
            inWhitespace = false;

            for (; pos < input.Length; pos++)
            {
                ch = input[pos];

                if (Char.IsDigit(ch) || ch == '.')
                {
                    sb.Append(ch);
                    inWhitespace = false;
                }
                else
                {
                    if (!inWhitespace)
                    {
                        sb.Append(' ');
                        inWhitespace = true;
                    }
                }
            }

            // Split the fields and parse each component.

            fields  = sb.ToString().Split(' ');
            degrees = 0;
            minutes = 0;
            seconds = 0;

            if (fields.Length == 0)
                throw new ArgumentException(string.Format(errorMsg, input));

            if (!double.TryParse(fields[0], out degrees))
                throw new ArgumentException(string.Format(errorMsg, input));

            if (fields.Length > 1)
            {
                if (!double.TryParse(fields[1], out minutes))
                    throw new ArgumentException(string.Format(errorMsg, input));

                if (fields.Length > 2)
                {
                    if (!double.TryParse(fields[2], out seconds))
                        throw new ArgumentException(string.Format(errorMsg, input));
                }
            }

            coordinate = degrees + minutes * (1.0 / 60.0) + seconds * (1.0 / 3600.0);

            return isNegative ? -coordinate : coordinate;
        }

        /// <summary>
        /// Verifies that the latitude passed is valid.
        /// </summary>
        /// <param name="latitude">The latitude.</param>
        /// <exception cref="ArgumentException">Thrown if the input value is out of range.</exception>
        private static void ValidateLatitude(double latitude)
        {
            if (latitude < -90 || latitude > +90)
                throw new ArgumentException(string.Format("Latitude coordinate [{0}] is out of the valid range of -90...+90", latitude));
        }

        /// <summary>
        /// Verifies that the longitude passed is valid.
        /// </summary>
        /// <param name="longitude">The longitude.</param>
        /// <exception cref="ArgumentException">Thrown if the input value is  out of range.</exception>
        private static void ValidateLongitude(double longitude)
        {
            if (longitude < -180 || longitude > +180)
                throw new ArgumentException(string.Format("Longitude coordinate [{0}] is out of the valid range of -180...+180", longitude));
        }

        /// <summary>
        /// The latitude.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the input value is out of range.</exception>
        public double Latitude
        {
            get { return latitude; }

            set
            {
                ValidateLatitude(value);
                latitude = value;
            }
        }

        /// <summary>
        /// The longitude.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the input value is out of range.</exception>
        public double Longitude
        {
            get { return longitude; }

            set
            {
                ValidateLongitude(value);
                longitude = value;
            }
        }

        /// <summary>
        /// Computes the coordinate hash code.
        /// </summary>
        /// <returns>The computed hash.</returns>
        public override int GetHashCode()
        {
            return Latitude.GetHashCode() ^ Longitude.GetHashCode();
        }

        /// <summary>
        /// Determines whether this instance equals another.
        /// </summary>
        /// <param name="obj">The test instance.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        public override bool Equals(object obj)
        {
            GeoCoordinate coord;

            if (!(obj is GeoCoordinate))
                return false;

            coord = (GeoCoordinate)obj;
            return this.Latitude == coord.Latitude && this.Longitude == coord.Longitude;
        }
    }
}
