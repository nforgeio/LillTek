//-----------------------------------------------------------------------------
// FILE:        HangupEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a hangup event raised by a CallSession.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Describes a hangup event raised by a <see cref="CallSession" />.
    /// </summary>
    public class HangupEventArgs : EventArgs
    {
        /// <summary>
        /// Returns the hangup reason code.
        /// </summary>
        public SwitchHangupReason Reason { get; private set; }

        /// <summary>
        /// Returns the call details record (CDR).
        /// </summary>
        public string CallDetails { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="reason">The hangup reason code.</param>
        /// <param name="callDetails">The call details record (CDR).</param>
        internal HangupEventArgs(SwitchHangupReason reason, string callDetails)
        {
            this.Reason      = reason;
            this.CallDetails = callDetails;
        }
    }
}
