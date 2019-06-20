//-----------------------------------------------------------------------------
// FILE:        SysLogEntryFormat.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: SysLogEntry formatting options.

using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace LillTek.Common
{
    /// <summary>
    /// <see cref="SysLogEntry" /> formatting options.
    /// </summary>
    [Flags]
    public enum SysLogEntryFormat
    {
        /// <summary>
        /// Specifies special formatting.
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// Indicates that the current time (UTC) should be included
        /// in the log entry.
        /// </summary>
        ShowTime = 0x0001,

        /// <summary>
        /// Include a horizontal bar to visually separate log entries.
        /// </summary>
        ShowBar = 0x0002,

        /// <summary>
        /// Show extended log entry information if present.
        /// </summary>
        ShowExtended = 0x0004,

        /// <summary>
        /// Show the event type.
        /// </summary>
        ShowType = 0x0008,

        /// <summary>
        /// Include all special formatting.
        /// </summary>
        ShowAll = 0xFFFF,

        /// <summary>
        /// Include all special formatting except for including the time.
        /// </summary>
        AllButTime = ShowAll & ~ShowTime,

        /// <summary>
        /// Include all special formatting except for including the bar.
        /// </summary>
        AllButBar = ShowAll & ~ShowBar,
    }
}
