//-----------------------------------------------------------------------------
// FILE:        WatchDogTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a global watchdog timer factory as well as a 
//              simulated watchdog timer.

using System;
using System.Diagnostics;
using System.IO;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Implements a global watchdog timer factory as well as a simulated watchdog timer.
    /// </summary>
    public sealed class WatchDogTimer : IWatchDogTimer
    {
        //---------------------------------------------------------------------
        // Static members

        private static IWatchDogTimer   sysTimer       = null;          // The global timer
        private static object           globalSyncRoot = new object();

        /// <summary>
        /// Returns the system's watchdog timer instance.  If the current hardware
        /// doesn't implement a timer then this property will return a simulated 
        /// timer.
        /// </summary>
        /// <remarks>
        /// <note>
        /// At time, physical timers are available only on the Windows CE
        /// running on the Micro/sys SBC1625 microcontoller with the appropriate
        /// drivers added to the operating system image.  A simulated timer is returned
        /// for all other environments.
        /// </note>
        /// </remarks>
        public static IWatchDogTimer SystemTimer
        {
            get
            {
                lock (globalSyncRoot)
                {
                    if (sysTimer != null)
                        return sysTimer;
#if WINCE
                    // I'm going to poll the timer implementations we know about
                    // to see if they can be instantiated.

                    sysTimer = SBC1625WatchDogTimer.Create();
                    if (sysTimer != null)
                        return sysTimer;
#endif
                    // None of the actual timer implementations were able to
                    // implemnent a timer on the current hardware, so return
                    // a simulated timer.

                    sysTimer = new WatchDogTimer();
                    return sysTimer;
                }
            }
        }

        //---------------------------------------------------------------------
        // Instance members simulating a watchdog timer.

        private object      syncLock = new object();
        private bool        enabled;
        private uint        rate;
        private TimeSpan    timeToReset;
        private uint        countToReset;
        private DateTime    resetTime;

        /// <summary>
        /// Constructor.
        /// </summary>
        private WatchDogTimer()
        {
            enabled      = false;
            rate         = 66666666;        // 66,666,666 MHz (the rate implemented by Intel xScale processors)
            countToReset = 0xFFFFFFFF;
            timeToReset  = TimeSpan.FromSeconds(countToReset / rate);
        }

        /// <summary>
        /// Returns <c>true</c> if the last device reset was caused by a watchdog
        /// timer expiration.
        /// </summary>
        /// <remarks>
        /// This may not be supported by all devices.  In these cases, the
        /// property will always return false.
        /// </remarks>
        public bool Expired
        {
            get { return false; }
        }

        /// <summary>
        /// Determines the enable state of the timer.
        /// </summary>
        /// <remarks>
        /// This defaults to false for new timers.  When initially enabled, the timer
        /// count will be set to its default interval as determined by the TimeToReset
        /// property.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                lock (syncLock)
                    return enabled;
            }

            set
            {
                lock (syncLock)
                {
                    if (enabled == value)
                        return;

                    enabled = value;

                    if (enabled)
                    {
                        resetTime = DateTime.MaxValue;
                        Set();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the rate at which the timer is decremented in counts per second.
        /// </summary>
        public uint Rate
        {
            get
            {
                lock (syncLock)
                    return rate;
            }
        }

        /// <summary>
        /// The default countdown interval for the timer.  This will not have
        /// an impact until the next time <see cref="Set()" /> is called.
        /// </summary>
        /// <remarks>
        /// This defaults to the maximum time allowed by the timer hardware.
        /// </remarks>
        public TimeSpan TimeToReset
        {
            get
            {
                lock (syncLock)
                    return timeToReset;
            }

            set
            {
                lock (syncLock)
                    timeToReset = value;
            }
        }

        /// <summary>
        /// Sets the timer's time-to-system reset to the timespan passed.
        /// </summary>
        /// <param name="interval">The countdown time.</param>
        /// <remarks>
        /// <note>
        /// This method will throw an exception if the interval passed
        /// is too large for the resolution of the timer.
        /// </note>
        /// <note>
        /// Simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </note>
        /// </remarks>
        public void Set(TimeSpan interval)
        {
            lock (syncLock)
            {
                if (SysTime.Now >= resetTime)
                    throw new InvalidOperationException("Watchdog timer expired.");

                resetTime = SysTime.Now + interval;
            }
        }

        /// <summary>
        /// Resets the timer's time-to-system reset to the TimeToReset interval.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </note>
        /// </remarks>
        public void Set()
        {
            Set(timeToReset);
        }
    }
}