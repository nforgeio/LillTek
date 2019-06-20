//-----------------------------------------------------------------------------
// FILE:        SwitchEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the state for all switch received by the 
//              Switch.EventReceived event.

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
    /// Holds the state for all switch received by the 
    /// <see cref="Switch" />.<see cref="Switch.EventReceived" /> event.
    /// </summary>
    public class SwitchEventArgs : GenericSwitchEventArgs<SwitchEvent>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="switchEvent"></param>
        internal SwitchEventArgs(SwitchEvent switchEvent)
            : base(switchEvent)
        {
        }
    }
}
