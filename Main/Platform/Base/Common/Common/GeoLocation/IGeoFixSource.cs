//-----------------------------------------------------------------------------
// FILE:        IGeoFixSource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface for classes that generate intermittent 
//              geographical location update events.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the interface for classes that generate intermittent 
    /// geographical location update events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface's purpose is to define a platform independant interface to
    /// a concrete class that provides for intermittent current geographical position 
    /// events for logging or other purposes.  The concrete class will typically be
    /// named <see cref="GeoFixSource" />.
    /// </para>
    /// <para>
    /// This class is designed to be used for situations where we'd like to track the
    /// user's position over a period of time where we're not displaying the position to
    /// the user in real time (e.g. for navigation).  Instead, the current position will
    /// be determined at fixed relatively long intervals (probably measured in minutes).
    /// Concrete implementations on mobile devices will attempt to save battery power
    /// by powering up the underlying sensors only when necessary.
    /// </para>
    /// </remarks>
    public interface IGeoFixSource : IDisposable
    {
        /// <summary>
        /// Raised when a new position fix has been obtained.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This event will be raised on a background thread.
        /// </note>
        /// </remarks>
        event Action<object, GeoFixChangedArgs> FixChanged;

        /// <summary>
        /// Returns the last known position or a position with no geographical coordinate 
        /// information if no position has been computed.
        /// </summary>
        GeoFix Fix { get; }

        /// <summary>
        /// Starts the tracker, specifying the rough interval at which the the current position
        /// will be determined and the <see cref="FixChanged" /> event raised.
        /// </summary>
        /// <param name="updateInterval"></param>
        void Start(TimeSpan updateInterval);

        /// <summary>
        /// Stops the tracker if it is running.  This is equivalent to calling <see cref="IDisposable.Dispose" />.
        /// </summary>
        void Stop();
    }
}
