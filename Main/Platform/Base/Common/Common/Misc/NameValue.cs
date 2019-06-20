//-----------------------------------------------------------------------------
// FILE:        NameValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds a string based name/value pair.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Holds a string based name/value pair.
    /// </summary>
    public class NameValue
    {
        /// <summary>
        /// The name string.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The value string.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The name string.</param>
        /// <param name="value">The value string.</param>
        public NameValue(string name, string value)
        {
            this.Name  = name;
            this.Value = value;
        }
    }
}
