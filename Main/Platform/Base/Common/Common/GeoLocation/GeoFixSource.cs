//-----------------------------------------------------------------------------
// FILE:        GeoFixSource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Generates periodic geographic position updates for the location
//              of the current device.

using System;

// $todo(jeff.lill):
//
// Implement this using an IP to geolocation service.

namespace LillTek.Common
{
    /// <summary>
    /// Generates periodic geographic position updates for the location
    /// of the current system.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class is not implemented at this time and will not generate any
    /// location updates.
    /// </note>
    /// </remarks>
    public class GeoFixSource : IGeoFixSource
    {
        /// <summary>
        /// Raised when a new position update has been obtained.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This event will be raised on a background thread.
        /// </note>
        /// </remarks>
        public event Action<object, GeoFixChangedArgs> FixChanged;

        /// <summary>
        /// Returns the last known position or a position with no geographical coordinate 
        /// information if no position has been computed.
        /// </summary>
        public GeoFix Fix { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoFixSource()
        {
            this.Fix = new GeoFix();
        }

        /// <summary>
        /// Starts the tracker, specifying the rough interval at which the the current position
        /// will be determined and the <see cref="FixChanged" /> event raised.
        /// </summary>
        /// <param name="updateInterval"></param>
        public void Start(TimeSpan updateInterval)
        {
        }

        /// <summary>
        /// Stops the tracker if it is running.  This is equivalent to calling <see cref="Dispose" />.
        /// </summary>
        public void Stop()
        {
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Raises the <see cref="FixChanged" /> event.
        /// </summary>
        private void RaiseFixChanged()
        {
            if (FixChanged != null)
                FixChanged(this, new GeoFixChangedArgs() { Fix = this.Fix });
        }
    }
}
