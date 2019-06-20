//-----------------------------------------------------------------------------
// FILE:        StringSysLogEntryExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simple ISysLogEntryExtension implementation that holds extended
//              log information as a formatted string.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// A simple <see cref="ISysLogEntryExtension" /> implementation that holds extended
    /// log information as a formatted string.
    /// </summary>
    public class StringSysLogEntryExtension : ISysLogEntryExtension
    {
        private string extendedInfo;

        /// <summary>
        /// Constructs an instance holding the specified extended text.
        /// </summary>
        /// <param name="extendedInfo">The extended information.</param>
        public StringSysLogEntryExtension(string extendedInfo)
        {
            if (extendedInfo == null)
                throw new ArgumentNullException("extendedInfo");

            this.extendedInfo = extendedInfo;
        }

        /// <summary>
        /// Constructs an instance holding a formatted string.
        /// </summary>
        /// <param name="format">The string template including standard .NET formatting escapes.</param>
        /// <param name="args">The arguments to be inserted into the format template.</param>
        public StringSysLogEntryExtension(string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            this.extendedInfo = string.Format(format, args);
        }

        //---------------------------------------------------------------------
        // ISysLogEntryExtension implementation

        /// <summary>
        /// Renders the extended information into a form suitable for
        /// including in a logged event.
        /// </summary>
        /// <returns>The rendered information.</returns>
        /// <remarks>
        /// The string returned should be formatted a zero or more lines of text
        /// with each line terminated with a CRLF.
        /// </remarks>
        public string Format()
        {
            return extendedInfo;
        }
    }
}
