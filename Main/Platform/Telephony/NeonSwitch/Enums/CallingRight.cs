//-----------------------------------------------------------------------------
// FILE:        CallingRight.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the toll call access rights.

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
    /// Defines the toll call access rights.  Note that these rights may be combined
    /// using bitwise-OR operations.
    /// </summary>
    [Flags]
    public enum CallingRight
    {
        /// <summary>
        /// Not allowed to make calls.
        /// </summary>
        None = 0,

        /// <summary>
        /// Local calls allowed.
        /// </summary>
        Local = 0x00000001,

        /// <summary>
        /// Domestic calls allowed.
        /// </summary>
        Domestic = 0x00000002,

        /// <summary>
        /// International calls allowed.
        /// </summary>
        International = 0x00000004,

        /// <summary>
        /// Unrestricted calling.
        /// </summary>
        Unrestricted = Local | Domestic | International
    }
}
