//-----------------------------------------------------------------------------
// FILE:        IWatchDogTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the implementation to a system watchdog timer.

using System;
using System.Diagnostics;
using System.IO;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Defines the implentation to a system watchdog timer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Watchdog timers are used to implement a failsafe mechanism for rebooting
    /// locked up hardware and applications.
    /// </para>
    /// <para>
    /// A watchdog timer is a hardware counter that is automatically decremented
    /// while the system is running and the counter is enabled.  If the counter
    /// ever reaches zero, the system will reset and restart.
    /// </para>
    /// <para>
    /// Applications making use of a watchdog timer will enable the timer and then
    /// periodically reset the count to a non-zero value, ensuring that the count
    /// will never reach zero when the program is running properly.
    /// </para>
    /// <para>
    /// Watchdog timers are not implemented on all platforms but this class will
    /// simulate a watchdog timer on those systems that don't provide one.
    /// </para>
    /// </remarks>
    public interface IWatchDogTimer
    {
        /// <summary>
        /// Returns <c>true</c> if the last device reset was caused by a watchdog
        /// timer expiration.
        /// </summary>
        /// <remarks>
        /// This may not be supported by all devices.  In these cases, the
        /// property will always return false.
        /// </remarks>
        bool Expired { get; }

        /// <summary>
        /// Determines the enable state of the timer.
        /// </summary>
        /// <remarks>
        /// This defaults to false for new timers.  When initially enabled, the timer
        /// count will be set to its default interval as determined by the TimeToReset
        /// property.
        /// </remarks>
        bool Enabled { get; set; }

        /// <summary>
        /// Returns the rate at which the timer is decremented in counts per second.
        /// </summary>
        uint Rate { get; }

        /// <summary>
        /// The default countdown interval for the timer.  This will not have
        /// an impact until the next time <see cref="Set()" /> is called.
        /// </summary>
        /// <remarks>
        /// This defaults to the maximum time allowed by the timer hardware.
        /// </remarks>
        TimeSpan TimeToReset { get; set; }

        /// <summary>
        /// Sets the timer's time-to-system reset to the timespan passed.
        /// </summary>
        /// <param name="interval">The countdown time.</param>
        /// <remarks>
        /// <para>
        /// Note that this method will throw an exception if the interval passed
        /// is too large for the resolution of the timer.
        /// </para>
        /// <para>
        /// Note that simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </para>
        /// </remarks>
        void Set(TimeSpan interval);

        /// <summary>
        /// Resets the timer's time-to-system reset to the TimeToReset interval.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </note>
        /// </remarks>
        void Set();
    }
}
