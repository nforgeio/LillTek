//-----------------------------------------------------------------------------
// FILE:        RadiusLogEntryType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible RADIUS server log entry types.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Enumerates the possible RADIUS server log entry types.
    /// </summary>
    public enum RadiusLogEntryType
    {
        /// <summary>
        /// Account authentication event.
        /// </summary>
        Authentication,

        /// <summary>
        /// Requesting NAS is unknown.
        /// </summary>
        UnknownNas
    }
}
