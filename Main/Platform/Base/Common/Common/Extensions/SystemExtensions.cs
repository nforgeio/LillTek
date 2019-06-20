//-----------------------------------------------------------------------------
// FILE:        SystemExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: System types extension methods.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Misc extension methods.
    /// </summary>
    public static class SystemExtensions
    {
        //---------------------------------------------------------------------
        // System.Type extensions

        /// <summary>
        /// Determines whether a type is or derives from a base type.
        /// </summary>
        /// <param name="type">The type being tested.</param>
        /// <param name="baseType">The base type.</param>
        /// <returns><c>true</c> if <paramref name="type"/> is or inherites from <paramref name="baseType" />.</returns>
        public static bool IsDerivedFrom(this System.Type type, System.Type baseType)
        {
            while (type != null)
            {

                if (type == baseType)
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        //---------------------------------------------------------------------
        // DateTime and TimeSpan extensions

        /// <summary>
        /// Converts a <see cref="DateTime" /> into a <see cref="TimeSpan" /> by
        /// returning the offset of the time portion of the date from 12:00am.
        /// </summary>
        /// <param name="date">The date to be converted.</param>
        /// <returns>The converted <see cref="TimeSpan" />.</returns>
        public static TimeSpan ToTimeSpan(this DateTime date)
        {
            return date - date.Date;
        }

        /// <summary>
        /// Returns the <see cref="DateTime" /> with <see cref="DateTime.Kind" />
        /// set to <see cref="DateTimeKind.Unspecified" />.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        /// <returns>The converted value.</returns>
        public static DateTime ToUnspecifiedTimeZone(this DateTime value)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Converts a <see cref="TimeSpan" /> into a <see cref="DateTime" /> by
        /// adding the offset to the minimum date value.
        /// </summary>
        /// <param name="timeSpan">The time span to be converted.</param>
        /// <returns>The converted <see cref="DateTime" />.</returns>
        public static DateTime ToDateTime(this TimeSpan timeSpan)
        {
            return DateTime.MinValue + timeSpan;
        }

        /// <summary>
        /// Returns the starting date for the current month.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime ThisMonth(this DateTime time)
        {
            return new DateTime(time.Year, time.Month, 1);
        }

        /// <summary>
        /// Returns the starting date for the previous month.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime LastMonth(this DateTime time)
        {
            if (time.Month == 1)
                return new DateTime(time.Year - 1, 12, 1);
            else
                return new DateTime(time.Year, time.Month - 1, 1);
        }

        /// <summary>
        /// Returns the starting date for the next month.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime NextMonth(this DateTime time)
        {
            if (time.Month == 12)
                return new DateTime(time.Year + 1, 1, 1);
            else
                return new DateTime(time.Year, time.Month + 1, 1);
        }

        /// <summary>
        /// Returns the starting date for the current quarter.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime ThisQuarter(this DateTime time)
        {
            switch (time.Month)
            {
                case 1:
                case 2:
                case 3:

                    return new DateTime(time.Year, 1, 1);

                case 4:
                case 5:
                case 6:

                    return new DateTime(time.Year, 4, 1);

                case 7:
                case 8:
                case 9:

                    return new DateTime(time.Year, 7, 1);

                case 10:
                case 11:
                case 12:

                    return new DateTime(time.Year, 10, 1);

                default:

                    throw new ArgumentException("Invalid date");
            }
        }

        /// <summary>
        /// Returns the starting date for the previous quarter.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime LastQuarter(this DateTime time)
        {
            switch (time.Month)
            {
                case 1:
                case 2:
                case 3:

                    return new DateTime(time.Year - 1, 10, 1);

                case 4:
                case 5:
                case 6:

                    return new DateTime(time.Year, 1, 1);

                case 7:
                case 8:
                case 9:

                    return new DateTime(time.Year, 4, 1);

                case 10:
                case 11:
                case 12:

                    return new DateTime(time.Year, 7, 1);

                default:

                    throw new ArgumentException("Invalid date");
            }
        }

        /// <summary>
        /// Returns the starting date for the next quarter.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime NextQuarter(this DateTime time)
        {
            switch (time.Month)
            {
                case 1:
                case 2:
                case 3:

                    return new DateTime(time.Year, 4, 1);

                case 4:
                case 5:
                case 6:

                    return new DateTime(time.Year, 7, 1);

                case 7:
                case 8:
                case 9:

                    return new DateTime(time.Year, 10, 1);

                case 10:
                case 11:
                case 12:

                    return new DateTime(time.Year + 1, 1, 1);

                default:

                    throw new ArgumentException("Invalid date");
            }
        }

        /// <summary>
        /// Returns the starting date for the current year.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime ThisYear(this DateTime time)
        {
            return new DateTime(time.Year, 1, 1);
        }

        /// <summary>
        /// Returns the starting date for the previous year.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime LastYear(this DateTime time)
        {
            return new DateTime(time.Year - 1, 1, 1);
        }

        /// <summary>
        /// Returns the starting date for the next year.
        /// </summary>
        /// <param name="time">The current time.</param>
        public static DateTime NextYear(this DateTime time)
        {
            return new DateTime(time.Year + 1, 1, 1);
        }

        //---------------------------------------------------------------------
        // Guid extensions

        /// <summary>
        /// Converts the <see cref="Guid"/> into a Base-64 encoded string.
        /// </summary>
        /// <param name="guid">The <see cref="Guid"/>.</param>
        /// <returns>The Nase-64 encoded string.</returns>
        public static string ToBase64(this Guid guid)
        {
            return Convert.ToBase64String(guid.ToByteArray());
        }
    }
}
