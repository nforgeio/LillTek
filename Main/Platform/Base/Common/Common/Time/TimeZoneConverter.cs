//-----------------------------------------------------------------------------
// FILE:        TimeZoneConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides an easy to use way to convert DateTime values between
//              two time zones.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Provides an easy to use way to convert <see cref="DateTime" /> values
    /// between two time zones.
    /// </summary>
    public sealed class TimeZoneConverter
    {
        /// <summary>
        /// The current time zone information.
        /// </summary>
        public TimeZoneInfo CurrentTimeZone { get; private set; }

        /// <summary>
        /// The remote time zone information.
        /// </summary>
        public TimeZoneInfo RemoteTimeZone { get; private set; }

        /// <summary>
        /// Constructs a converter between two time zones specified as 
        /// <see cref="TimeZoneInfo" /> values.
        /// </summary>
        /// <param name="currentTimeZone">Information about the current time zone.</param>
        /// <param name="remoteTimeZone">Information about the remote time zone.</param>
        public TimeZoneConverter(TimeZoneInfo currentTimeZone, TimeZoneInfo remoteTimeZone)
        {
            this.CurrentTimeZone = currentTimeZone;
            this.RemoteTimeZone = remoteTimeZone;
        }

        /// <summary>
        /// Constructs a converter between two time zones specified using 
        /// their timezone IDs.
        /// </summary>
        /// <param name="currentTimeZoneID">The ID name of the current time zone.</param>
        /// <param name="remoteTimeZoneID">The ID name of the remote time zone.</param>
        /// <exception cref="ArgumentException">Thrown if either of the time zones could not be found on the current computer.</exception>
        public TimeZoneConverter(string currentTimeZoneID, string remoteTimeZoneID)
        {
            var                                 sysTimeZones = TimeZoneInfo.GetSystemTimeZones();
            Dictionary<string, TimeZoneInfo>    timeZones;
            TimeZoneInfo                        tz;

            timeZones = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var sysTz in sysTimeZones)
                timeZones.Add(sysTz.Id, sysTz);

            if (!timeZones.TryGetValue(currentTimeZoneID, out tz))
                throw new ArgumentException(string.Format("Cannot map [{0}] to a system time zone.", currentTimeZoneID));
            else
                this.CurrentTimeZone = tz;

            if (!timeZones.TryGetValue(remoteTimeZoneID, out tz))
                throw new ArgumentException(string.Format("Cannot map [{0}] to a system time zone.", remoteTimeZoneID));
            else
                this.RemoteTimeZone = tz;
        }

        /// <summary>
        /// Converts a <see cref="DateTime" /> relative to the current time zone
        /// into a <see cref="DateTime" /> relative to the remote time zone.
        /// </summary>
        /// <param name="value">The current time zone time.</param>
        /// <returns>The equivalent remote time zone time.</returns>
        public DateTime ConvertTo(DateTime value)
        {
            value = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(value, this.CurrentTimeZone, this.RemoteTimeZone);
        }

        /// <summary>
        /// Converts a <see cref="DateTime" /> relative to the remote time zone
        /// into a <see cref="DateTime" /> relative to the current time zone.
        /// </summary>
        /// <param name="value">The remote time zone time.</param>
        /// <returns>The equivalent current time zone time.</returns>
        public DateTime ConvertFrom(DateTime value)
        {
            value = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(value, this.RemoteTimeZone, this.CurrentTimeZone);
        }
    }
}
