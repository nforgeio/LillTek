//-----------------------------------------------------------------------------
// FILE:        DnsQClass.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible query class values.

// Note that the values defined in these enums map directly to the values
// defined by the DNS protocol.

using System;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines the possible query class values.
    /// </summary>
    public enum DnsQClass : uint
    {
        /// <summary>
        /// The Internet
        /// </summary>
        IN = 1,

        /// <summary>
        /// CNET (obsolete)
        /// </summary>
        CS = 2,

        /// <summary>
        /// CNET (obsolete)
        /// </summary>
        CH = 3,

        /// <summary>
        /// Hesoid
        /// </summary>
        HS = 4,
    }
}
