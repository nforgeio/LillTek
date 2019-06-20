//-----------------------------------------------------------------------------
// FILE:        RegexExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Regular expression extension methods.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LillTek.Common
{
    /// <summary>
    /// Regular expression extension methods.
    /// </summary>
    public static class RegexExtensions
    {
        /// <summary>
        /// Returns the results of a regular expression pattern matching operation as
        /// a list of <see cref="Match" /> instances rather than an enumerable set.
        /// </summary>
        /// <param name="regex">The current <see cref="Regex" /> instance.</param>
        /// <param name="input">The text to be scanned.</param>
        /// <returns>The list of matches.</returns>
        /// <remarks>
        /// This method is useful for situations where knowning the number of matches 
        /// before enumerating them has value.
        /// </remarks>
        public static List<Match> GetMatches(this Regex regex, string input)
        {
            var matches = new List<Match>();

            foreach (Match match in regex.Matches(input))
                matches.Add(match);

            return matches;
        }

        /// <summary>
        /// Returns the results of a regular expression pattern matching operation as
        /// a list of <see cref="Match" /> instances rather than an enumerable set.
        /// </summary>
        /// <param name="regex">The current <see cref="Regex" /> instance.</param>
        /// <param name="input">The text to be scanned.</param>
        /// <param name="startAt">The starting position of the scan.</param>
        /// <returns>The list of matches.</returns>
        /// <remarks>
        /// This method is useful for situations where knowning the number of matches 
        /// before enumerating them has value.
        /// </remarks>
        public static List<Match> GetMatches(this Regex regex, string input, int startAt)
        {
            var matches = new List<Match>();

            foreach (Match match in regex.Matches(input, startAt))
                matches.Add(match);

            return matches;
        }

        /// <summary>
        /// Returns the value of a named group item if one exists, the <paramref name="defValue" />
        /// parameter value otherwise.
        /// </summary>
        /// <param name="groups">The current group collection.</param>
        /// <param name="name">The group name.</param>
        /// <param name="defValue">The default value to be returned if the group doesn't exist.</param>
        public static string GetValue(this GroupCollection groups, string name, string defValue)
        {
            var group = groups[name];

            if (group == null)
                return defValue;

            return group.Value;
        }

        /// <summary>
        /// Returns the value of a named group item if one exists, <c>null</c> otherwise.
        /// </summary>
        /// <param name="groups">The current group collection.</param>
        /// <param name="name">The group name.</param>
        public static string GetValue(this GroupCollection groups, string name)
        {
            return groups.GetValue(name, null);
        }
    }
}
