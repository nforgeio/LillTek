//-----------------------------------------------------------------------------
// FILE:        SwitchGlobal.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the names of the NeonSwitch specific global variables.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Defines the names of the NeonSwitch specific global variables.
    /// </summary>
    public static class SwitchGlobal
    {
        /// <summary>
        /// Indicates that the this is a NeonSwitch enhanced deployment of FreeSWITCH.
        /// This variable is added as <c>true</c> manually to the FreeSWITCH build.
        /// </summary>
        public const string NeonSwitch = "NeonSwitch";

        /// <summary>
        /// Set to the FreeSWITCH build version when the core NeonSwitch application starts.
        /// </summary>
        public const string FreeSwitchVersion = "freeswitch_version";

        /// <summary>
        /// Set to the NeonSwitch build version when the core NeonSwitch application starts.
        /// </summary>
        public const string NeonSwitchVersion = "NeonSwitch_version";

        /// <summary>
        /// Set to <c>true</c> once the NeonSwitch core service has finished loading.
        /// </summary>
        public const string NeonSwitchReady = "NeonSwitch_ready";
    }
}
