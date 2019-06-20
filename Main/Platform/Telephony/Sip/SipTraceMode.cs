//-----------------------------------------------------------------------------
// FILE:        SipTraceMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Diagnostic tracing flags.

using System;
using System.Collections.Generic;
using System.Text;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Diagnostic tracing flags.
    /// </summary>
    [Flags]
    public enum SipTraceMode
    {
        /// <summary>
        /// Disable all tracing.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Trace transmitted SIP messages.
        /// </summary>
        Send = 0x01,

        /// <summary>
        /// Trace received SIP messages.
        /// </summary>
        Receive = 0x02,

        /// <summary>
        /// Enable all tracing.
        /// </summary>
        All = 0x03
    }
}
