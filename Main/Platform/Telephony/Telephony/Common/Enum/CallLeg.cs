//-----------------------------------------------------------------------------
// FILE:        CallLeg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible legs of a call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Enumerates the possible legs of a call.
    /// </summary>
    [Flags]
    public enum CallLeg
    {
        /// <summary>
        /// The originating leg of the call.
        /// </summary>
        A = 1,

        /// <summary>
        /// The receiving leg of the call.
        /// </summary>
        B = 2,

        /// <summary>
        /// Specifies both the A and B legs.
        /// </summary>
        Both = A | B
    }
}
