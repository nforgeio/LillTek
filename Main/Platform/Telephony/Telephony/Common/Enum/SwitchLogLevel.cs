//-----------------------------------------------------------------------------
// FILE:        SwitchLogLevel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the possible NeonSwitch logging levels.

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
    /// Identifies the possible NeonSwitch logging levels.
    /// </summary>
    public enum SwitchLogLevel
    {
        // WARNING: The ordinal values assigned below map directly to the underlying
        //          FreeSWITCH values.  Do not modify.

        /// <summary>
        /// Disables event logging.
        /// </summary>
        None = -1,

        /// <summary>
        /// Logs console text.
        /// </summary>
        Console = 0,

        /// <summary>
        /// Logs serious error.
        /// </summary>
        Alert = 1,

        /// <summary>
        /// Logs a critical error that indicates that NeonSwitch probably cannot function.
        /// </summary>
        Critical = 2,

        /// <summary>
        /// Logs a recoverable error.
        /// </summary>
        Error = 3,

        /// <summary>
        /// Logs a warning condition.
        /// </summary>
        Warning = 4,

        /// <summary>
        /// Logs a notification.
        /// </summary>
        Notice = 5,

        /// <summary>
        /// Logs information.
        /// </summary>
        Info = 6,

        /// <summary>
        /// Logs debugging information.
        /// </summary>
        Debug = 7
    }
}
