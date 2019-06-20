//-----------------------------------------------------------------------------
// FILE:        GenericSwitchEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The generic base class for all .NET events that surface the
//              reception of a NeonSwitch related event.

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
    /// The generic base class for all .NET events that surface the
    /// reception of a NeonSwitch related event.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of the NeonSwitch event (must be derived from 
    /// <see cref="SwitchEvent" />).
    /// </typeparam>
    public class GenericSwitchEventArgs<TEvent> : EventArgs
        where TEvent : SwitchEvent
    {
        /// <summary>
        /// Returns the received event.
        /// </summary>
        public TEvent SwitchEvent { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="switchEvent">The received event.</param>
        protected GenericSwitchEventArgs(TEvent switchEvent)
        {
            this.SwitchEvent = switchEvent;
        }
    }
}
