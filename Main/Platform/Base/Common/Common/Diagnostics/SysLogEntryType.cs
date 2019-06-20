//-----------------------------------------------------------------------------
// FILE:        SysLogEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible SysLog entry types.

using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace LillTek.Common
{
    /// <summary>
    /// Enumerates the possible <see cref="SysLog" /> entry types.
    /// </summary>
    public enum SysLogEntryType
    {
        // WARNING: These values may be persisted to application databases so
        //          you must not change the ordinal values.

        /// <summary>
        /// The log entry is informational.
        /// </summary>
        Information = 0,

        /// <summary>
        /// The log entry is a warning.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// The log entry indicates a serious error condition.
        /// </summary>
        Error = 2,

        /// <summary>
        /// The log entry describes a successful security change or access attempt.
        /// </summary>
        SecuritySuccess = 3,

        /// <summary>
        /// The log entry describes a failed security change or access attempt.
        /// </summary>
        SecurityFailure = 4,

        /// <summary>
        /// The log entry describes an application exception.
        /// </summary>
        Exception = 5,

        /// <summary>
        /// The log entry holds debugging trace information.
        /// </summary>
        Trace = 6
    }
}
