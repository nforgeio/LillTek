//-----------------------------------------------------------------------------
// FILE:        BridgeMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible NeonSwitch call bridging behaviors.

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
    /// Enumerates the possible NeonSwitch call bridging behaviors.
    /// </summary>
    public enum BridgeMode
    {
        /// <summary>
        /// Directs the switch to attempt to bridge to the specified extensions in order,
        /// one at a time, until the one of the extensions answer.
        /// </summary>
        LinearHunt,

        /// <summary>
        /// Directs the switch the attempt to bridge to the sepecfied extensions in
        /// a random order, one at a time, until one of the extensions answer.
        /// </summary>
        RandomHunt,

        /// <summary>
        /// Directs the switch to ring all of the extensions in parallel and bridge to
        /// the first extension that answers.
        /// </summary>
        RingAll
    }
}
