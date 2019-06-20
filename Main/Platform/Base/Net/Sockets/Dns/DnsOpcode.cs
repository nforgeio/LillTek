//-----------------------------------------------------------------------------
// FILE:        DnsOpcode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible DNS query opcodes.

// Note that the values defined in these enums map directly to the values
// defined by the DNS protocol.

using System;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines the possible DNS query opcodes.
    /// </summary>
    public enum DnsOpcode : uint
    {
        /// <summary>
        /// Query
        /// </summary>
        QUERY = 0,

        /// <summary>
        /// Inverse query
        /// </summary>
        IQUERY = 1,

        /// <summary>
        /// Status request
        /// </summary>
        STATUS = 2,
    }
}
