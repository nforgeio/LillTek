﻿//-----------------------------------------------------------------------------
// FILE:        RecurringTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to manage tasks that need to be performed on a
//              periodic basis.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;

// $todo(jeff.lill):
//
// I'm only implementing hourly and daily intervals for now.  I need to eventually
// add weekly, bi-weekly, and monthly.

namespace LillTek.Common
{
    /// <summary>
    /// Used to manage tasks that need to be performed on a periodic basis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This timer is designed to be polled periodically from an application's background
    /// thread by calling the <see cref="HasFired()" /> or <see cref="HasFired(DateTime)" />
    /// methods.  These methods will return <c>true</c> if the action associated with the timer
    /// is to be performed.
    /// </para>
    /// <para>
    /// This class works by watching for the transition between a call to <see cref="HasFired()" />
    /// made at a time before the scheduled event and then a subsequent call made when the 
    /// current time is at or after the scheduled event time. <see cref="HasFired()" /> will
    /// return <c>true</c> on the subsequent call if the time is right.
    /// </para>
    /// <para>
    /// This behavior ensures that scheduled tasks will only be executed once for any recurring
    /// schedule, even if the application is restarted.
    /// </para>
    /// <para>
    /// The <see cref="HasFired()" /> method uses the current UTC time to perform the
    /// time comparison.  The <see cref="HasFired(DateTime)" /> will use the time passed
    /// (which may be local time, etc.) to do this.
    /// </para>
    /// <note>
    /// <para>
    /// This timer auto resets after <see cref="HasFired()" /> returns <c>true</c>.  Note also
    /// that <see cref="HasFired()" /> must be called fairly frequently (on the order of a few minutes or less)
    /// to obtain reasonable accuracy.
    /// </para>
    /// <para>
    /// The <see cref="Reset()"/> and <see cref="Reset(DateTime)"/> methods may be used to explictly
    /// reset the timer to fire a the next scheduled time.  This may be useful for ensuring that
    /// short duration timers are properly reset after an operation that may take longer to
    /// complete than the timer interval.
    /// </para>
    /// </note>
    /// <para>
    /// Recurring timers are represented as strings with the format of the string
    /// depending on the type of timer.  The table below describes these formats:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="RecurringTimerType.Disabled" /></term>
    ///         <description>
    ///         <para>
    ///         Disabled timers never fire.  Simply place the word <b>Disabled</b> at the
    ///         beginning of the timer string.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RecurringTimerType.Minute" /></term>
    ///         <description>
    ///         <para>
    ///         Minute timers fire at the top of every minute.  There is no offset.  Minute
    ///         timers are formatted as:
    ///         </para>
    ///         <example>
    ///         Minute
    ///         </example>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RecurringTimerType.QuarterHour" /></term>
    ///         <description>
    ///         <para>
    ///         Quarter hour timers are fired four times an hour at the offset from the 15 minute time.  
    ///         Quarter hour timers formatted as:
    ///         </para>
    ///         <example>
    ///         QuarterHour,MM
    ///         QuarterHour,MM:SS
    ///         </example>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RecurringTimerType.Hourly" /></term>
    ///         <description>
    ///         <para>
    ///         Hourly timers are fired once per hour at the offset from the top of the hour.  Hourly timers
    ///         are formatted as:
    ///         </para>
    ///         <example>
    ///         Hourly,MM
    ///         Hourly,MM:SS
    ///         </example>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RecurringTimerType.Daily" /></term>
    ///         <description>
    ///         <para>
    ///         Daily timers are fired once per day at the specified time of day.  Daily timers
    ///         are formatted as:
    ///         </para>
    ///         <example>
    ///         Daily,HH:MM
    ///         Daily,HH:MM:SS
    ///         </example>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RecurringTimerType.Interval" /></term>
    ///         <description>
    ///         <para>
    ///         Interval timers are fired on a regular interval that is not not
    ///         tied to a specific period.  Interval timers are formatted as:
    ///         </para>
    ///         <example>
    ///         Interval,1s
    ///         Interval,HH:MM:SS
    ///         </example>
    ///         <para>
    ///         Where the value may be a standard .NET timespan string or a LillTek
    ///         interval value.
    ///         </para>
    ///         </description>
    ///     </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class RecurringTimer : IParseable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a disabled timer.
        /// </summary>
        public static RecurringTimer Disabled { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static RecurringTimer()
        {
            RecurringTimer.Disabled = new RecurringTimer();
        }

        //---------------------------------------------------------------------
        // Instance members

        private DateTime            lastPollTime = DateTime.MaxValue;
        private DateTime            nextFireTime = DateTime.MaxValue;
        private RecurringTimerType  type;
        private TimeSpan            timeOffset;

        /// <summary>
        /// Default constructor that creates a <see cref="RecurringTimerType.Disabled" /> timer.
        /// </summary>
        public RecurringTimer()
        {
            this.type = RecurringTimerType.Disabled;
        }

        /// <summary>
        /// Constructs a timer by parsing a string value.
        /// </summary>
        /// <param name="value">The string representation.</param>
        /// <exception cref="ArgumentException">Thrown if the string passed is not valid.</exception>
        public RecurringTimer(string value)
        {
            if (!TryParse(value))
                throw new ArgumentException("Invalid [RecurringTimer] string.");
        }

        /// <summary>
        /// Constructs a recurring timer of the specified type and time offset from
        /// the beginning of the implied period.
        /// </summary>
        /// <param name="type">Describes the timer type which implies the period.</param>
        /// <param name="timeOffset">The time offset from the beginning of the implied timer period.</param>
        public RecurringTimer(RecurringTimerType type, TimeSpan timeOffset)
        {
            this.type       = type;
            this.timeOffset = TimeSpan.Zero; ;

            // Make sure the offset makes sense.

            switch (type)
            {
                case RecurringTimerType.Interval:

                    this.timeOffset = timeOffset;
                    break;

                case RecurringTimerType.QuarterHour:

                    if (timeOffset < TimeSpan.FromMinutes(15))
                        this.timeOffset = timeOffset;

                    break;

                case RecurringTimerType.Hourly:

                    if (timeOffset < TimeSpan.FromHours(1))
                        this.timeOffset = timeOffset;

                    break;

                case RecurringTimerType.Daily:

                    if (timeOffset < TimeSpan.FromDays(1))
                        this.timeOffset = timeOffset;

                    break;
            }
        }

        /// <summary>
        /// Constructs a recurring timer that will fire once a day at the specified time offset.
        /// </summary>
        /// <param name="timeOfDay">The time of day offset.</param>
        public RecurringTimer(TimeOfDay timeOfDay)
        {
            this.type       = RecurringTimerType.Daily;
            this.timeOffset = timeOfDay.TimeSpan;
        }

        /// <summary>
        /// Determines whether the timer has fired by comparing the current UTC time with
        /// the scheduled event time.
        /// </summary>
        public bool HasFired()
        {
            return HasFired(DateTime.UtcNow);
        }

        /// <summary>
        /// Determines if the timer has fired by comparing the current time passed with
        /// the next scheduled firing time.
        /// </summary>
        /// <param name="now">The current time.</param>
        public bool HasFired(DateTime now)
        {
            try
            {
                if (lastPollTime == DateTime.MaxValue)
                {
                    // This is the first time the timer has been called so we're just going
                    // to compute the next firing time and don't fire the timer.

                    Start(now);
                    return false;
                }

                if (now >= nextFireTime)
                {
                    // Timer has fired.

                    Start(now);
                    return true;
                }

                return false;
            }
            finally
            {
                lastPollTime = now;
            }
        }

        /// <summary>
        /// Resets the timer to fire at the next scheduled interval after the current UTC time.
        /// </summary>
        public void Reset()
        {
            Reset(DateTime.UtcNow);
        }

        /// <summary>
        /// Resets the timer to fire at the next scheduled interval after the time passed.
        /// </summary>
        /// <param name="now">The current time.</param>
        public void Reset(DateTime now)
        {
            Start(now);
        }

        /// <summary>
        /// Starts the timer by computing the next firing time after the current time (UTC).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications may use this method to initalize the timer.  This is useful in situations where
        /// some time may pass between the time the timer was constructed and the first time
        /// <see cref="HasFired()" /> has been called.  In these situations, the timer will
        /// not fire for a scheduled event that occurs during this interval.
        /// </para>
        /// </remarks>
        public void Start()
        {
            Start(DateTime.UtcNow);
        }

        /// <summary>
        /// Starts the timer by computing the next firing time after the time passed.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <remarks>
        /// <para>
        /// Applications may use this method to initalize the timer.  This is useful in situations where
        /// some time may pass between the time the timer was constructed and the first time
        /// <see cref="HasFired()" /> has been called.  In these situations, the timer will
        /// not fire for a scheduled event that occurs during this interval.
        /// </para>
        /// </remarks>
        public void Start(DateTime now)
        {
            switch (type)
            {
                case RecurringTimerType.Disabled:

                    nextFireTime = DateTime.MaxValue;
                    break;

                case RecurringTimerType.Minute:

                    nextFireTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                    nextFireTime += TimeSpan.FromMinutes(1);
                    break;

                case RecurringTimerType.QuarterHour:

                    nextFireTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 15 * (now.Minute / 15), 0) + timeOffset;
                    if (nextFireTime <= now)
                        nextFireTime += TimeSpan.FromMinutes(15);

                    break;

                case RecurringTimerType.Hourly:

                    nextFireTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0) + timeOffset;
                    if (nextFireTime <= now)
                        nextFireTime += TimeSpan.FromHours(1);

                    break;

                case RecurringTimerType.Daily:

                    nextFireTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0) + timeOffset;
                    if (nextFireTime <= now)
                        nextFireTime += TimeSpan.FromDays(1);

                    break;

                case RecurringTimerType.Interval:

                    nextFireTime = now + timeOffset;
                    break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Sets the firing time for the timer.
        /// </summary>
        /// <param name="time">The scheduled time.</param>
        /// <remarks>
        /// This is useful in situations where it is necessary to special-case a
        /// specific firing time.
        /// </remarks>
        public void SetFireTime(DateTime time)
        {
            nextFireTime = time;
            lastPollTime = DateTime.MinValue;
        }

        /// <summary>
        /// Returns the timer type.
        /// </summary>
        public RecurringTimerType Type
        {
            get { return type; }
        }

        /// <summary>
        /// Returns the <see cref="TimeSpan" /> offet from the beginning of the 
        /// period when the timer is scheduled to fire.
        /// </summary>
        public TimeSpan TimeOffset
        {
            get { return timeOffset; }
        }

        /// <summary>
        /// Renders the timer into a string.
        /// </summary>
        /// <returns>The timer string.</returns>
        public override string ToString()
        {
            string s;

            switch (type)
            {
                case RecurringTimerType.Disabled:

                    return "Disabled";

                case RecurringTimerType.Minute:

                    return "Minute";

                case RecurringTimerType.QuarterHour:

                    s = timeOffset.ToString();

                    return string.Format("QuarterHour,{0}", s.Substring(s.IndexOf(':') + 1));

                case RecurringTimerType.Hourly:

                    s = timeOffset.ToString();

                    return string.Format("Hourly,{0}", s.Substring(s.IndexOf(':') + 1));

                case RecurringTimerType.Daily:

                    return string.Format("Daily,{0}", timeOffset.ToString());

                case RecurringTimerType.Interval:

                    return string.Format("Interval,{0}", Serialize.ToString(timeOffset));

                default:

                    throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // IParsable implementation

        /// <summary>
        /// Attempts to parse the configuration value.
        /// </summary>
        /// <param name="value">The configuration value.</param>
        /// <returns><c>true</c> if the value could be parsed, <b></b> if the value is not valid for the type.</returns>
        public bool TryParse(string value)
        {
            string[]    fields;
            double      minutes = 0;
            double      seconds = 0;

            if (value == null)
                return false;

            if (value.ToLower().StartsWith("disabled"))
            {
                this.type = RecurringTimerType.Disabled;
                return true;
            }

            fields = value.Split(',');

            switch (fields[0].Trim().ToLower())
            {
                case "minute":

                    this.type = RecurringTimerType.Minute;
                    this.timeOffset = TimeSpan.Zero;
                    return true;

                case "quarterhour":

                    if (fields.Length != 2)
                        return false;

                    fields = fields[1].Split(':');
                    if (fields.Length > 2)
                        return false;

                    this.type = RecurringTimerType.QuarterHour;

                    try
                    {
                        minutes = double.Parse(fields[0]);

                        if (fields.Length > 1)
                            seconds = double.Parse(fields[1]);

                        timeOffset = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

                        if (timeOffset >= TimeSpan.FromMinutes(15))
                            timeOffset = TimeSpan.Zero;
                    }
                    catch
                    {
                        return false;
                    }

                    return true;

                case "hourly":

                    if (fields.Length != 2)
                        return false;

                    fields = fields[1].Split(':');
                    if (fields.Length > 2)
                        return false;

                    this.type = RecurringTimerType.Hourly;

                    try
                    {
                        minutes = double.Parse(fields[0]);

                        if (fields.Length > 1)
                            seconds = double.Parse(fields[1]);

                        timeOffset = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

                        if (timeOffset >= TimeSpan.FromHours(1))
                            timeOffset = TimeSpan.Zero;
                    }
                    catch
                    {
                        return false;
                    }

                    return true;

                case "daily":

                    TimeOfDay timeOfDay;

                    if (fields.Length != 2)
                        return false;

                    if (!TimeOfDay.TryParse(fields[1], out timeOfDay))
                        return false;

                    this.type = RecurringTimerType.Daily;
                    this.timeOffset = timeOfDay.TimeSpan;

                    if (timeOffset >= TimeSpan.FromDays(1))
                        timeOffset = TimeSpan.Zero;

                    return true;

                case "interval":

                    if (fields.Length != 2)
                        return false;

                    timeOffset = Serialize.Parse(fields[1], TimeSpan.FromSeconds(-1));
                    if (timeOffset < TimeSpan.Zero)
                        return false;

                    this.type = RecurringTimerType.Interval;
                    return true;

                default:

                    return false;
            }
        }
    }
}
