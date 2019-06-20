//-----------------------------------------------------------------------------
// FILE:        CallSessionArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments for a SwitchApp.NewCallSessionEvent.

using System;
using System.Collections.Generic;
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
    /// The arguments for a <see cref="Switch" />.<see cref="Switch.CallSessionEvent" />.
    /// </summary>
    public class CallSessionArgs : EventArgs
    {
        /// <summary>
        /// Returns the new call session.
        /// </summary>
        public CallSession Session { get; private set; }

        /// <summary>
        /// Returns the parameters/data being passed to the application (or <c>null</c>).
        /// </summary>
        public string Arguments { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The new call session.</param>
        /// <param name="args">The parameters/data being passed to the application (or <c>null</c>).</param>
        internal CallSessionArgs(CallSession session, string args)
        {
            this.Session   = session;
            this.Arguments = args;
        }
    }
}
