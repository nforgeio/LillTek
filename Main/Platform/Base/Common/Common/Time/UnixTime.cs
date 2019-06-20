//-----------------------------------------------------------------------------
// FILE:        UnixTime.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a time/date class based on Unix time standards.

using System;
using System.Xml;
using System.Diagnostics;
using System.Collections;

namespace LillTek.Common
{
    /// <summary>
    /// Time formats supported by the UnixTime parsing and rendering methods.
    /// </summary>
    public enum UnixTimeFormat
    {
        /// <summary>
        /// Renders time as a 64-bit UNIX time value (seconds since 1/1/1970 12:00am).
        /// </summary>
        Time64,

        /// <summary>
        /// Renders time as 14 digits: YYYYMMDDHHMMSS.
        /// </summary>
        YYYYMMDDHHMMSS
    }

    /// <summary>
    /// Implements a time/date class based on Unix time standards.
    /// </summary>
    public sealed class UnixTime
    {
        /// <summary>
        /// Time units.
        /// </summary>
        public enum Unit
        {
            /// <summary>
            /// Time expressed as milliseconds.
            /// </summary>
            Milliseconds,

            /// <summary>
            /// Time expressed as seconds.
            /// </summary>
            Seconds,

            /// <summary>
            /// Time expressed as minutes.
            /// </summary>
            Minutes,

            /// <summary>
            /// Time expressed as hours.
            /// </summary>
            Hours,

            /// <summary>
            /// Time expressed as days.
            /// </summary>
            Days
        }

        //---------------------------------------------------------------------
        // Static members

        private static DateTime     unixTimeZero = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        private static UnixTime     timeZero     = new UnixTime(unixTimeZero);
        private static DateTime     maxTime31    = unixTimeZero + TimeSpan.FromSeconds(2147483647.0);
        private static DateTime     maxTime32    = unixTimeZero + TimeSpan.FromSeconds(4294967295.0);

        /// <summary>
        /// This static property returns the beginning of Unix time, 1/1/1970 00:00
        /// </summary>
        public static UnixTime TimeZero
        {
            get { return timeZero; }
        }

        /// <summary>
        /// This static property returns the current time.
        /// </summary>
        public static UnixTime Now
        {
            get { return new UnixTime(DateTime.Now); }
        }

        /// <summary>
        /// This static property returns the current time.
        /// </summary>
        public static UnixTime UtcNow
        {
            get { return new UnixTime(DateTime.UtcNow); }
        }

        /// <summary>
        /// Returns the maximum time that can be expressed as Unix time
        /// using a signed 32 bit integer (effectively 31 bits).
        /// </summary>
        public static DateTime MaxTime31
        {
            get { return maxTime31; }
        }

        /// <summary>
        /// Returns the maximum time that can be expressed as Unix time
        /// using an unsigned 32 bit integer.
        /// </summary>
        public static DateTime MaxTime32
        {
            get { return maxTime32; }
        }

        /// <summary>
        /// Constructs a UnixTime instance that is the specified delta from the 
        /// beginning of Unix time.
        /// </summary>
        /// <param name="milliseconds">The time delta in milliseconds.</param>
        /// <returns>The corresponding UnixTime instance.</returns>
        public static UnixTime FromMilliseconds(long milliseconds)
        {
            return new UnixTime(milliseconds, Unit.Milliseconds);
        }

        /// <summary>
        /// Constructs a UnixTime instance that is the specified delta from the 
        /// beginning of Unix time.
        /// </summary>
        /// <param name="seconds">The time delta in seconds.</param>
        /// <returns>The corresponding UnixTime instance.</returns>
        public static UnixTime FromSeconds(long seconds)
        {
            return new UnixTime(seconds, Unit.Seconds);
        }

        /// <summary>
        /// Constructs a UnixTime instance that is the specified delta from the 
        /// beginning of Unix time.
        /// </summary>
        /// <param name="minutes">The time delta in minutes.</param>
        /// <returns>The corresponding UnixTime instance.</returns>
        public static UnixTime FromMinutes(long minutes)
        {
            return new UnixTime(minutes, Unit.Minutes);
        }

        /// <summary>
        /// Constructs a UnixTime instance that is the specified delta from the 
        /// beginning of Unix time.
        /// </summary>
        /// <param name="hours">The time delta in seconds.</param>
        /// <returns>The corresponding UnixTime instance.</returns>
        public static UnixTime FromHours(long hours)
        {
            return new UnixTime(hours, Unit.Hours);
        }

        /// <summary>
        /// Constructs a UnixTime instance that is the specified delta from the 
        /// beginning of Unix time.
        /// </summary>
        /// <param name="days">The time delta in days.</param>
        /// <returns>The corresponding UnixTime instance.</returns>
        public static UnixTime FromDays(long days)
        {
            return new UnixTime(days, Unit.Days);
        }

        /// <summary>
        /// Converts the time passed into Unix time expressed as the number
        /// of seconds from Unix time zero.
        /// </summary>
        /// <param name="time">The time to convert.</param>
        /// <returns>Seconds since 1/1/1970.</returns>
        public static uint ToSeconds(DateTime time)
        {
            if (time <= timeZero)
                return 0;

            if (time > maxTime32)
                return 4294967295;

            return (uint)(time - timeZero).TotalSeconds;
        }

        //---------------------------------------------------------------------
        // Instance members

        private DateTime time;

        /// <summary>
        /// This constructor initializes the time to 1/1/1970 00:00
        /// </summary>
        public UnixTime()
        {
            this.time = unixTimeZero;
        }

        /// <summary>
        /// This constructor initializes the time to the number of seconds
        /// since 1/1/1970 00:00 passed.
        /// </summary>
        /// <param name="seconds">The unix time to set in seconds.</param>
        public UnixTime(long seconds)
        {
            if (seconds == 0)
                this.time = timeZero.time;
            else
                this.time = unixTimeZero + new TimeSpan(seconds * TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// This constructor initializes the time to the specified delta from
        /// 1/1/1970 00:00.
        /// </summary>
        /// <param name="delta">The offset from time zero.</param>
        /// <param name="unit">The delta units.</param>
        public UnixTime(long delta, Unit unit)
        {
            if (delta == 0)
                this.time = timeZero.time;
            else
                switch (unit)
                {
                    case Unit.Milliseconds:

                        this.time = unixTimeZero + new TimeSpan(delta * TimeSpan.TicksPerMillisecond);
                        break;

                    case Unit.Seconds:

                        this.time = unixTimeZero + new TimeSpan(delta * TimeSpan.TicksPerSecond);
                        break;

                    case Unit.Minutes:

                        this.time = unixTimeZero + new TimeSpan(delta * TimeSpan.TicksPerMinute);
                        break;

                    case Unit.Hours:

                        this.time = unixTimeZero + new TimeSpan(delta * TimeSpan.TicksPerHour);
                        break;

                    case Unit.Days:

                        this.time = unixTimeZero + new TimeSpan(delta * TimeSpan.TicksPerDay);
                        break;
                }
        }

        /// <summary>
        /// This constructor initializes the time value to the calendar date passed.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month: 1-12</param>
        /// <param name="day">The day: 1-31</param>
        public UnixTime(int year, int month, int day)
        {
            this.time = new DateTime(year, month, day);
        }

        /// <summary>
        /// This constructor initializes the time value to the calendar date
        /// passed and time passed.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month: 1-12</param>
        /// <param name="day">The day: 1-31</param>
        /// <param name="hour">The hour: 0-23</param>
        /// <param name="min">The minute: 0-59</param>
        /// <param name="sec">The second: 0-59</param>
        public UnixTime(int year, int month, int day, int hour, int min, int sec)
        {
            this.time = new DateTime(year, month, day, hour, min, sec);
        }

        /// <summary>
        /// This constructor initializes the time to the value passed.
        /// </summary>
        /// <param name="time">The framework time to set.</param>
        public UnixTime(DateTime time)
        {
            this.time = time - new TimeSpan(time.Ticks % TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// This method parses the string passed into a time value using
        /// the format specified.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <param name="format">The enumeration specifying the input format.</param>
        /// <returns>The time value or <c>null</c> if the operation failed.</returns>
        public static UnixTime Parse(string str, UnixTimeFormat format)
        {
            switch (format)
            {
                case UnixTimeFormat.Time64:

                    long ticks;

                    try
                    {
                        ticks = long.Parse(str) * TimeSpan.TicksPerSecond;
                        return new UnixTime(unixTimeZero + new TimeSpan(ticks));
                    }
                    catch
                    {
                        return null;
                    }

                case UnixTimeFormat.YYYYMMDDHHMMSS:

                    int year, month, day, hour, min, sec;

                    if (str.Length != 14)
                        return null;

                    try
                    {
                        year  = int.Parse(str.Substring(0, 4));
                        month = int.Parse(str.Substring(4, 2));
                        day   = int.Parse(str.Substring(6, 2));
                        hour  = int.Parse(str.Substring(8, 2));
                        min   = int.Parse(str.Substring(10, 2));
                        sec   = int.Parse(str.Substring(12, 2));

                        return new UnixTime(year, month, day, hour, min, sec);
                    }
                    catch
                    {
                        return null;
                    }

                default:

                    Assertion.Test(false, "Unexpected time format.");
                    return null;
            }
        }

        /// <summary>
        /// This method formats the time into a string using the
        /// format type passed.
        /// </summary>
        /// <param name="format">The enumerator specifying the desired output format.</param>
        public string Format(UnixTimeFormat format)
        {
            switch (format)
            {
                case UnixTimeFormat.Time64:

                    var delta = this.time - unixTimeZero;

                    return (delta.Ticks / TimeSpan.TicksPerSecond).ToString();

                case UnixTimeFormat.YYYYMMDDHHMMSS:

                    return string.Format("{0:D4}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);

                default:

                    Assertion.Test(false, "Unexpected time format.");
                    return "";
            }
        }

        /// <summary>
        /// This property returns <c>true</c> if this time value is set to the beginning of Unix time (1/1/1970 00:00).
        /// </summary>
        public bool IsTimeZero
        {
            get { return this.time == unixTimeZero; }
        }

        /// <summary>
        /// This implicit cast converts unix time back to normal .NET framework time.
        /// </summary>
        public static implicit operator DateTime(UnixTime uxTime)
        {
            if ((object)uxTime == null)
                return new DateTime();

            return uxTime.time;
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        public static bool operator ==(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null && (object)time2 == null)
                return true;
            else if ((object)time1 == null || (object)time2 == null)
                return false;

            return time1.time == time2.time;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        public static bool operator !=(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null && (object)time2 == null)
                return false;
            else if ((object)time1 == null || (object)time2 == null)
                return true;

            return time1.time != time2.time;
        }

        /// <summary>
        /// Less than operator
        /// </summary>
        public static bool operator <(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null || (object)time2 == null)
                throw new ArgumentException("Invalid comparision to null.");

            return time1.time < time2.time;
        }

        /// <summary>
        /// Greater than operator
        /// </summary>
        public static bool operator >(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null || (object)time2 == null)
                throw new ArgumentException("Invalid comparision to null.");

            return time1.time > time2.time;
        }

        /// <summary>
        /// LEQ than operator
        /// </summary>
        public static bool operator <=(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null || (object)time2 == null)
                throw new ArgumentException("Invalid comparision to null.");

            return time1.time <= time2.time;
        }

        /// <summary>
        /// GEQ than operator
        /// </summary>
        public static bool operator >=(UnixTime time1, UnixTime time2)
        {
            if ((object)time1 == null || (object)time2 == null)
                throw new ArgumentException("Invalid comparision to null.");

            return time1.time >= time2.time;
        }

        /// <summary>
        /// This method returns a hash code for the time value.
        /// </summary>
        public override int GetHashCode()
        {
            return (int)(time.Ticks | (time.Ticks >> 32));
        }

        /// <summary>
        /// This method returns <c>true</c> if the object passed equals this object.
        /// </summary>
        public override bool Equals(object o)
        {
            UnixTime time;

            time = o as UnixTime;
            if (time == null)
                return false;

            return this == time;
        }
    }
}
