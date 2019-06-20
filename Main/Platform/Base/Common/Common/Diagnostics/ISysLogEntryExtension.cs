//-----------------------------------------------------------------------------
// FILE:        ISysLogEntryExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface to be implemented by custom SysLogEntry 
//              extensions.

using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the interface to be implemented by custom <see cref="SysLogEntry" />
    /// extensions.
    /// </summary>
    public interface ISysLogEntryExtension
    {
        /// <summary>
        /// Renders the extended information into a form suitable for
        /// including in a logged event.
        /// </summary>
        /// <returns>The rendered information.</returns>
        /// <remarks>
        /// The string returned should be formatted a zero or more lines of text
        /// with each line terminated with a CRLF.
        /// </remarks>
        string Format();
    }
}
