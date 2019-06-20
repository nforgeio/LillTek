//-----------------------------------------------------------------------------
// FILE:        CallDirection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the direction of a call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes the direction of a call.
    /// </summary>
    public enum CallDirection
    {
        /// <summary>
        /// The call direction is not known.
        /// </summary>
        Unknown,

        /// <summary>
        /// The call was made to the switch.
        /// </summary>
        Inbound,

        /// <summary>
        /// The call was made from the switch.
        /// </summary>
        Outbound
    }
}
