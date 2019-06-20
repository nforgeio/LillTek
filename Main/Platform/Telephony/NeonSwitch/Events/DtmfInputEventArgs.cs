//-----------------------------------------------------------------------------
// FILE:        DtmfInputEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes DTMF received by a CallSession. 

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
    /// Describes DTMF received by a <see cref="CallSession" />. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// These arguments are passed when the <see cref="CallSession" />.<see cref="CallSession.DtmfReceived"/>
    /// event is raised when the switch detects that a DTMF key has been pressed.  The arguments include
    /// the <see cref="Digit" /> property which will be set to the specific key pressed and <see cref="Duration" />
    /// which will be set to the length of time that the user pressed the key.
    /// </para>
    /// <para>
    /// The <see cref="Break" /> property can be used by applications to implement <b>barge</b> functionality
    /// by cancelling any running command, such as the playing of a prompt.  Simply set <see cref="Break" />=<c>true</c>
    /// to accomplish this before returning from the event handler.
    /// </para>
    /// </remarks>
    public class DtmfInputEventArgs : EventArgs
    {
        /// <summary>
        /// The received DTMF digit.
        /// </summary>
        public char Digit { get; private set; }

        /// <summary>
        /// The length of time that the digit was pressed.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Set to <c>true</c> by applications that wish to stop any currently
        /// executing commands such as the playing of a prompt.
        /// </summary>
        public bool Break { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="digit">The received DTMF digit.</param>
        /// <param name="duration">The length of time that the digit was pressed.</param>
        internal DtmfInputEventArgs(char digit, TimeSpan duration)
        {
            this.Digit    = digit;
            this.Duration = duration;
            this.Break    = false;
        }
    }
}
