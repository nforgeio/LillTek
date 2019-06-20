//-----------------------------------------------------------------------------
// FILE:        SwitchPriority.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the priority of a NeonSwitch event.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Identifies the priority of a NeonSwitch event.
    /// </summary>
    /// <remarks>
    /// These map directly to the underlying FreeSWITCH priority values.
    /// </remarks>
    public enum SwitchPriority
    {
        /// <summary>
        /// Normal priority.
        /// </summary>
        Normal = (int)switch_priority_t.SWITCH_PRIORITY_NORMAL,

        /// <summary>
        /// Low priority.
        /// </summary>
        Low = (int)switch_priority_t.SWITCH_PRIORITY_LOW,

        /// <summary>
        /// High priority.
        /// </summary>
        High = (int)switch_priority_t.SWITCH_PRIORITY_HIGH
    }
}
