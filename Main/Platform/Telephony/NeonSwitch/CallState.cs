//-----------------------------------------------------------------------------
// FILE:        CallState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides call operations and state management for event driven
//              NeonSwitch application.

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
    /// Provides call operations and state management for event driven
    /// NeonSwitch application.
    /// </summary>
    public class CallState
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        public void Execute(SwitchAction action)
        {
            throw new NotImplementedException();
        }
    }
}
