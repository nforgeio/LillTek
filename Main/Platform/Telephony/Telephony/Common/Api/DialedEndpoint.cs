//-----------------------------------------------------------------------------
// FILE:        DialedEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Describes an endpoint to be targeted when originating or 
//              bridging a call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes an endpoint to be targeted when originating or 
    /// bridging a call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to help manage the generation of FreeSWITCH 
    /// dialstring, including the specification of channel variables.
    /// Pass (a single) desired endpoint to the constructor and then
    /// add any required channel variables to the <see cref="Variables" />
    /// collection.  Then call <see cref="ToString" /> to render the
    /// endpoint into a FreeSWITCH compatible dialstring.
    /// </para>
    /// <note>
    /// <see cref="ToString" /> handles the escaping of any embedded
    /// commas within variable values.
    /// </note>
    /// </remarks>
    public class DialedEndpoint
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Characters not allowed in a channel variable name.
        /// </summary>
        internal static char[] BadNameChars = new char[] { '{', '}', '[', ']', '=', '^', ',' };

        /// <summary>
        /// Characters not allowed in a channel variable value.
        /// </summary>
        internal static char[] BadValueChars = new char[] { '^', '[', ']' };

        /// <summary>
        /// Alternate chararacters to try when escaping commas within a 
        /// channel variable value.
        /// </summary>
        private static char[] alternates = new char[] { ':', '*', '~' };

        //---------------------------------------------------------------------
        // Instance members

        private string endpoint;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="endpoint">The endpoint string (without any channel variables).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="endpoint" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="endpoint" /> empty.</exception>
        /// <remarks>
        /// <note>
        /// The string passed <b>must not</b> include the specification of any channel variables 
        /// (global or local) but the string <b>may</b> include references to FreeSWITCH variables
        /// using the <b>${...}</b> and <b>$${...}</b> syntaxes.
        /// </note>
        /// </remarks>
        public DialedEndpoint(string endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Dialed endpoint cannot be empty.", "endpoint");

            this.endpoint = endpoint;
            this.Variables = new ChannelVariableCollection();
        }

        /// <summary>
        /// The collection of local variables to be assigned to the channel created to
        /// establish communication with this endpoint.
        /// </summary>
        public ChannelVariableCollection Variables { get; private set; }

        /// <summary>
        /// Renders the endpoint into a FreeSWITCH compatible dialstring.
        /// </summary>
        /// <returns>The dial string.</returns>
        public override string ToString()
        {
            if (Variables.Count == 0)
                return endpoint;

            var sb = new StringBuilder();

            sb.Append('[');

            foreach (var variable in Variables)
                sb.AppendFormat("{0}={1},", variable.Key, EscapeValue(variable.Key, variable.Value));

            if (sb.Length > 0)
                sb.Length--;

            sb.Append(']');
            sb.Append(endpoint);

            return sb.ToString();
        }

        /// <summary>
        /// Escapes any commas found within the value passed, using the FreeSWITCH <b>^^</b> escaping
        /// mechanism.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The value to be escaped.</param>
        /// <returns>The escaped value.</returns>
        /// <exception cref="NotSupportedException">Thrown if an alternate character could not be found to escape the comma.</exception>
        private string EscapeValue(string name, string value)
        {
            char escapeChar;

            if (value.IndexOf(',') == -1)
                return value;   // Nothing to escape.

            escapeChar = (char)0;
            foreach (var ch in alternates)
                if (value.IndexOf(ch) == -1)
                {
                    escapeChar = ch;
                    break;
                }

            if (escapeChar == (char)0)
                throw new NotSupportedException(string.Format("The channel variable [{0}] has value [{1}] which contains a comma that cannot be escaped using the available alternate characters.", name, value));

            return string.Format("^^{0}{1}", escapeChar, value.Replace(',', escapeChar));
        }
    }
}
