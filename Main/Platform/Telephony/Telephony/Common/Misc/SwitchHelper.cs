//-----------------------------------------------------------------------------
// FILE:        SwitchHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NeonSwitch related utlities.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// NeonSwitch related utlities.
    /// </summary>
    public static partial class SwitchHelper
    {
        private static Dictionary<char, bool> unsafeUrlChars;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SwitchHelper()
        {
            InitEnumMappings();

            // Initialize the dictionary of unsafe URL characters.

            unsafeUrlChars = new Dictionary<char, bool>();

            foreach (var ch in "\r\n \"#%&+:;<=>?@[\\]^`{|}")
                unsafeUrlChars[ch] = true;
        }

        /// <summary>
        /// Process a set of raw event properties by creating <b>properties</b> and <b>variables</b> collections
        /// and separating the properties and variables into their own collections.
        /// </summary>
        /// <param name="rawProperties">The raw event properties.</param>
        /// <param name="properties">Returns as the properties collection.</param>
        /// <param name="variables">Returns as the variables collection.</param>
        internal static void ProcessEventProperties(ArgCollection rawProperties, out ArgCollection properties, out ArgCollection variables)
        {
            properties = new ArgCollection(ArgCollectionType.Unconstrained);
            variables  = new ArgCollection(ArgCollectionType.Unconstrained);

            foreach (var key in rawProperties)
            {
                if (key.ToLower().StartsWith("variable_"))
                    variables[key.Substring(9)] = rawProperties[key];
                else
                    properties[key] = rawProperties[key];
            }

            properties.IsReadOnly = true;
            variables.IsReadOnly = true;
        }

        /// <summary>
        /// Converts a timespan interval into seconds, rounding to the nearest second but returning
        /// 1 second for durations in range of 0 &lt; <paramref name="interval" /> &lt; 1 second.
        /// </summary>
        /// <param name="interval">The timespan interval to be converted.</param>
        /// <returns>The timespan converted to seconds.</returns>
        /// <remarks>
        /// This method is used for scheduling delayed switch actions and commands when the 
        /// scheduling resolution does not support fractional seconds.
        /// </remarks>
        public static int GetScheduleSeconds(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                return 0;

            var seconds = interval.TotalSeconds;

            if (seconds <= 1.0)
                return 1;
            else
                return (int)Math.Round(seconds);
        }

        /// <summary>
        /// URL encodes a string using an encoding algorithm compatible with FreeSWITCH.
        /// </summary>
        /// <param name="value">The string to be encoded.</param>
        /// <returns>The encoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <c>null</c> or empty.</exception>
        /// <remarks>
        /// This is used internally rather than using <see cref="Helper.UrlEncode(string)" /> to ensure
        /// compatibility with the FreeSWITCH implementation.
        /// </remarks>
        public static string UrlEncode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException("value");

            var sb = new StringBuilder();

            foreach (var ch in value)
            {
                if (unsafeUrlChars.ContainsKey(ch) || ch < ' ' || ch > '~')
                    sb.Append("%" + Helper.ToHex((byte)ch));
                else
                    sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}
