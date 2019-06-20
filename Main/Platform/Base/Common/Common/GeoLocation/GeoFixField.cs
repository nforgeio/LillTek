//-----------------------------------------------------------------------------
// FILE:        GeoFixField.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An enumeration with flag definitions for each serializable field
//              of a GeoFix.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// An enumeration with flag definitions for each serializable field
    /// of a <see cref="GeoFix" />.  Used by the <see cref="GeoFix" />
    /// class' <see cref="GeoFix.ToString(GeoFixField)" /> method to 
    /// identify which fields to serialize.
    /// </summary>
    [Flags]
    public enum GeoFixField : int
    {
        // WARNING:
        //
        // These values are transmitted between clients and servers in the
        // GeoTracker cluster and should not be modified unless all components
        // are being rebuilt and redeployed en masse.

        /// <summary>
        /// Generate the <see cref="GeoFix.TimeUtc" /> field.
        /// </summary>
        TimeUtc = 0x00000001,

        /// <summary>
        /// Generate the <see cref="GeoFix.Latitude" /> field.
        /// </summary>
        Latitude = 0x00000002,

        /// <summary>
        /// Generate the <see cref="GeoFix.Longitude" /> field.
        /// </summary>
        Longitude = 0x00000004,

        /// <summary>
        /// Generate the <see cref="GeoFix.Altitude" /> field.
        /// </summary>
        Altitude = 0x00000008,

        /// <summary>
        /// Generate the <see cref="GeoFix.Course" /> field.
        /// </summary>
        Course = 0x00000010,

        /// <summary>
        /// Generate the <see cref="GeoFix.Speed" /> field.
        /// </summary>
        Speed = 0x00000020,

        /// <summary>
        /// Generate the <see cref="GeoFix.HorizontalAccuracy" /> field.
        /// </summary>
        HorizontalAccuracy = 0x00000040,

        /// <summary>
        /// Generate the <see cref="GeoFix.VerticalAccurancy" /> field.
        /// </summary>
        VerticalAccurancy = 0x00000080,

        /// <summary>
        /// Generate the <see cref="GeoFix.Technology" /> field.
        /// </summary>
        Technology = 0x00000100,

        /// <summary>
        /// Generate the <see cref="GeoFix.NetworkStatus" /> field.
        /// </summary>
        NetworkStatus = 0x00000200,

        /// <summary>
        /// Generate all fields.
        /// </summary>
        All = 0x7FFFFFFF
    }
}
