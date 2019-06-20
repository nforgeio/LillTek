//-----------------------------------------------------------------------------
// FILE:        GeoFixTechnology.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the technology used to obtain a GeoFix.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Describes the technology used to obtain a <see cref="GeoFix" />.
    /// </summary>
    public enum GeoFixTechnology
    {
        /// <summary>
        /// The technology cannot be idenified.
        /// </summary>
        Unknown,

        /// <summary>
        /// An IP address to geocode table was used.
        /// </summary>
        IP,

        /// <summary>
        /// The fix was triangulated from cellular radio towers.
        /// </summary>
        Tower,

        /// <summary>
        /// The fix was obtained from Global Positioning System satellites.
        /// </summary>
        GPS
    }
}
