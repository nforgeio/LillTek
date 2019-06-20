//-----------------------------------------------------------------------------
// FILE:        HiResTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Abstracts the Windows high resolution timer

using System;
using System.Runtime.InteropServices;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Implements static methods to access the platform's high 
    /// resolution timer.
    /// </summary>
    /// <remarks>
    /// This class should be used for situations where more resolution
    /// than is possible using <see cref="Environment.TickCount" />.  
    /// If the current platform doesn't support a high resolution timer, 
    /// then this class will fall back to using <see cref="Environment.TickCount" />.
    /// </remarks>
    public sealed class HiResTimer
    {
        private static bool     isHiRes;    // True if using the hi-res timer
        private static long  frequency;  // Timer frequency

        /// <summary>
        /// Initializes the static variables.
        /// </summary>
        static HiResTimer()
        {
            isHiRes = WinApi.QueryPerformanceFrequency(out frequency);
            if (!isHiRes)
                frequency = 1000;   // Simulate a 1ms resolution
        }

        /// <summary>
        /// Returns <c>true</c> if the underlying timer is a high resolution timer.
        /// </summary>
        public static bool IsHiRes
        {
            get { return isHiRes; }
        }

        /// <summary>
        /// Returns the timer's frequency in counts per second.  Note that
        /// the frequency returned will be 1000 counts/second or better.
        /// </summary>
        public static long Frequency
        {
            get { return frequency; }
        }

        /// <summary>
        /// Returns the current timer count.
        /// </summary>
        public static long Count
        {
            get
            {
                if (isHiRes)
                {
                    long count;

                    WinApi.QueryPerformanceCounter(out count);
                    return count;
                }
                else
                    return (long)Environment.TickCount;
            }
        }

        /// <summary>
        /// Returns the difference between the start and ending high resolution
        /// timer counts expressed as a TimeSpan.
        /// </summary>
        /// <param name="startTime">The starting timer count.</param>
        /// <param name="endTime">The ending timer count.</param>
        /// <returns>The time difference as a TimeSpan.</returns>
        public static TimeSpan CalcTimeSpan(long startTime, long endTime)
        {
            return TimeSpan.FromSeconds((double)Diff(startTime, endTime) / (double)Frequency);
        }

        /// <summary>
        /// Returns the difference between the start and the current high resolution
        /// timer counts expressed as a TimeSpan.
        /// </summary>
        /// <param name="startTime">The starting timer count.</param>
        /// <returns>The time difference as a TimeSpan.</returns>
        public static TimeSpan CalcTimeSpan(long startTime)
        {
            return TimeSpan.FromSeconds((double)Diff(startTime, Count) / (double)Frequency);
        }

        /// <summary>
        /// Computes the difference between the start and end times passed.
        /// </summary>
        /// <param name="startTime">The starting timer count.</param>
        /// <param name="endTime">The ending timer count.</param>
        /// <returns>The absolute difference in the times.</returns>
        /// <remarks>
        /// This method should be used rather than simply subtracting the
        /// values, since this method will correctly handle overflow/wrap-around.
        /// </remarks>
        public static long Diff(long startTime, long endTime)
        {
            if (isHiRes)
            {
                // I'm assuming that hi-res timers can wrap around to negative numbers.

                if (startTime <= endTime)
                    return endTime - startTime;
                else
                    return startTime - endTime;
            }
            else
            {
                // TickCount wraps around to 0.

                if (startTime < endTime)
                    return endTime - startTime;
                else
                    return int.MaxValue - startTime + endTime;
            }
        }

        /// <summary>
        /// Computes the difference between the start and end times passed.  This
        /// version uses the state of the isHiRes parameter to determine exactly how
        /// to perform the calaculation rather than relying on the native capabilities
        /// of the current platform.
        /// </summary>
        /// <param name="isHiRes"></param>
        /// <param name="startTime">The start time.</param>
        /// <param name="endTime">The end time.</param>
        /// <returns>The absolute difference in the times.</returns>
        /// <remarks>
        /// This method should be used rather than simply subtracting the
        /// values, since this method will correctly handle overflow/wrap-around.
        /// </remarks>
        public static long Diff(bool isHiRes, long startTime, long endTime)
        {
            if (isHiRes)
            {
                // I'm assuming that hi-res timers wrap around to negative numbers.

                if (startTime <= endTime)
                    return endTime - startTime;
                else
                    return startTime - endTime;
            }
            else
            {
                // TickCount wraps around to 0.

                if (startTime < endTime)
                    return endTime - startTime;
                else
                    return int.MaxValue - startTime + endTime;
            }
        }
    }
}
