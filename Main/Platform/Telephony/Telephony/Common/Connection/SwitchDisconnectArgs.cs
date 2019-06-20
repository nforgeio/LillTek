//-----------------------------------------------------------------------------
// FILE:        SwitchDisconnectArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the SwitchConnection.Disconnected
//              event is raised.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the <see cref="SwitchConnection" />.<see cref="SwitchConnection.Disconnected" />
    /// event is raised.
    /// </summary>
    public class SwitchDisconnectArgs : EventArgs
    {
        /// <summary>
        /// Returns as <c>null</c> if the connection was disconnected via a call to
        /// <see cref="SwitchConnection.Close" /> or <see cref="SwitchConnection.Dispose"/> 
        /// or non-<c>null</c> if it/ was disconnected due to an error. 
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="error">The error exception or <c>null</c>.</param>
        internal SwitchDisconnectArgs(Exception error)
        {
            this.Error = error;
        }
    }
}
