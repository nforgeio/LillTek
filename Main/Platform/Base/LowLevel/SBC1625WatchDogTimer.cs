//-----------------------------------------------------------------------------
// FILE:        SBC1625WatchDogTimer.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005 by Jeff Lill.  All rights reserved.
// DESCRIPTION: Implements a watchdog timer for the Micro/sys SBC1625 
//              microcontroller.

#if WINCE

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Implements a watchdog timer for the Micro/sys SBC1625 microcontroller.
    /// </summary>
    internal sealed class SBC1625WatchDogTimer : IWatchDogTimer
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Examines the current hardware and operation system configuration to
        /// determine whether a watchdog timer is available that can be operated
        /// by this class.  This method returns a watchdog timer instance if
        /// this is the case, null otherwise.
        /// </summary>
        /// <returns>An IWatchDogTimer instance or null.</returns>
        internal static IWatchDogTimer Create()
        {
            try
            {
                SBC1625IO           board;
                SBC1625IO.Config    config;

                if (!SBC1625IO.Present)
                    return null;

                config = new SBC1625IO.Config();
                config.EnableIO = false;

                board = new SBC1625IO();
                board.Open(config);

                return new SBC1625WatchDogTimer(board);
            }
            catch
            {

                return null;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private SBC1625IO       board;
        private bool            expired;
        private bool            enabled;
        private uint            rate;
        private TimeSpan        timeToReset;
        private uint            countToReset;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="board">The board driver.</param>
        private SBC1625WatchDogTimer(SBC1625IO board)
        {
            this.board        = board;
            this.enabled      = false;
            this.expired      = board.WatchDogExpired();
            this.rate         = board.WatchDogCountRate();
            this.timeToReset  = TimeSpan.FromSeconds(0xFFFFFFFF / rate);
            this.countToReset = 0xFFFFFFFF;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SBC1625WatchDogTimer()
        {
            Close();
        }

        /// <summary>
        /// Releases disables the timer and all system resources associated it.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                this.Enabled = false;
                board.Close();
            }
        }

        /// <summary>
        /// Returns true if the last device reset was caused by a watchdog
        /// timer expiration.
        /// </summary>
        /// <remarks>
        /// This property is supported by the SBC1625 microcontroller.  The only
        /// issue is that the board support package implements IOCTL_HAL_REBOOT
        /// by starting the watchdog timer and letting it expire.  This means
        /// that there is no way to distinguish between an expired watchdog timer
        /// and a call to Helper.Reboot() when the system restarts.
        /// </remarks>
        public bool Expired
        {
            // $todo(jeffl): Implement a mechanism for distinguishing between
            //               a timer expiration and Helper.Reboot().

            get { return expired; }
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
                using (TimedLock.Lock(this))
                    return enabled;
            }

            set
            {
                using (TimedLock.Lock(this))
                {
                    if (enabled == value)
                        return;

                    enabled = value;
                    board.WatchDogEnable(enabled);

                    if (enabled)
                        board.WatchDogSet(countToReset);
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
                using (TimedLock.Lock(this))
                    return rate;
            }
        }

        /// <summary>
        /// The default countdown interval for the timer.  This will not have
        /// an impact until the next time Set() is called.
        /// </summary>
        /// <remarks>
        /// This defaults to the maximum time allowed by the timer hardware.
        /// </remarks>
        public TimeSpan TimeToReset
        {
            get
            {
                using (TimedLock.Lock(this))
                    return timeToReset;
            }

            set
            {
                using (TimedLock.Lock(this))
                {
                    double d;

                    d = value.TotalSeconds * rate;
                    if (d > (uint)0xFFFFFFFF)
                        throw new OverflowException("Countdown time too large for the current hardware.");

                    timeToReset  = value;
                    countToReset = (uint)d;
                }
            }
        }

        /// <summary>
        /// Sets the timer's time-to-system reset to the timespan passed.
        /// </summary>
        /// <param name="interval">The countdown time.</param>
        /// <remarks>
        /// Note that this method will throw an exception if the interval passed
        /// is too large for the resolution of the timer.
        /// 
        /// Note that simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </remarks>
        public void Set(TimeSpan interval)
        {
            using (TimedLock.Lock(this))
            {
                double d;

                d = interval.TotalSeconds * rate;
                if (d > (uint)0xFFFFFFFF)
                    throw new OverflowException("Countdown time too large for the current hardware.");

                board.WatchDogSet((uint)d);
            }
        }

        /// <summary>
        /// Resets the timer's time-to-system reset to the TimeToReset interval.
        /// </summary>
        /// <remarks>
        /// Note that simulated implementations may check here that an actual 
        /// timer would have been reset in time, avoiding a system reset.
        /// </remarks>
        public void Set()
        {
            using (TimedLock.Lock(this))
            {
                board.WatchDogSet(countToReset);
            }
        }
    }
}

#endif // WINCE