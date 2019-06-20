//-----------------------------------------------------------------------------
// FILE:        GeoFixChangedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The location fix arguments for an IGeoFixSource.FixChanged event.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// The location arguments for an <see cref="IGeoFixSource" />.<see cref="IGeoFixSource.FixChanged" /> event.
    /// </summary>
    public class GeoFixChangedArgs : EventArgs
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoFixChangedArgs()
        {
        }

        /// <summary>
        /// Information about the current location and movement.
        /// </summary>
        public GeoFix Fix { get; internal set; }
    }
}
