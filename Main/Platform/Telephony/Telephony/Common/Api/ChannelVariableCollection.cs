//-----------------------------------------------------------------------------
// FILE:        ChannelVariableCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Holds a set of channel variables.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Holds a set of channel variables.
    /// </summary>
    public class ChannelVariableCollection : Dictionary<string, string>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ChannelVariableCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Adds a new variable to the collection.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The variable value.</param>
        /// <exception cref="ArgumentException">Thrown if the variable name or value includes an illegal character or name is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the name or value is <c>null</c>.</exception>
        public new void Add(string name, string value)
        {
            CheckName(name);
            CheckValue(value);

            base.Add(name, value);
        }

        /// <summary>
        /// Indexes into the collection using the variable name.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The assocuated value.</returns>
        /// <exception cref="ArgumentException">Thrown if the variable name or value includes an illegal character or name is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the name or value is <c>null</c>.</exception>
        public new string this[string name]
        {
            get { return base[name]; }

            set
            {
                CheckName(name);
                CheckValue(value);

                base[name] = value;
            }
        }

        /// <summary>
        /// Verifies that a variable name is reasonable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <exception cref="ArgumentException">Thrown if the variable name includes an illegal character.</exception>
        /// /// <exception cref="ArgumentNullException">Thrown if the name is <c>null</c>.</exception>
        private void CheckName(string name)
        {
            int pos;

            if (name == null)
                throw new ArgumentNullException("name");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel variable name cannot be empty.");

            pos = name.IndexOfAny(DialedEndpoint.BadNameChars);
            if (pos != -1)
                throw new ArgumentException(string.Format("Invalid channel variable name [{0}].  The [{1}] character is not supported.", name, name[pos]), "name");
        }

        /// <summary>
        /// Verifies that a variable value is reasonable.
        /// </summary>
        /// <param name="value">The variable value.</param>
        /// <exception cref="ArgumentException">Thrown if the variable value includes an illegal character.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the value is <c>null</c>.</exception>
        private void CheckValue(string value)
        {
            int pos;

            if (value == null)
                throw new ArgumentNullException("value");

            pos = value.IndexOfAny(DialedEndpoint.BadValueChars);
            if (pos != -1)
                throw new ArgumentException(string.Format("Invalid channel variable value [{0}].  The [{1}] character is not supported.", value, value[pos]), "value");
        }
    }
}
