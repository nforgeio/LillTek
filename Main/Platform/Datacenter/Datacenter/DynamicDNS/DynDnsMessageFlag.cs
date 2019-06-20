//-----------------------------------------------------------------------------
// FILE:        DynDnsMessageFlag.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal Dynamic DNS Message flags.

using System;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Internal Dynamic DNS Message flags.
    /// </summary>
    public enum DynDnsMessageFlag
    {
        /// <summary>
        /// Bitmask used to extract the operation code from the flags.
        /// </summary>
        OpMask = 0x000000FF,

        /// <summary>
        /// Commands the DNS server to register the host entry embedded in the message.
        /// </summary>
        OpRegister = 0,

        /// <summary>
        /// Commands the DNS server to unregister the host entry embedded in the message.
        /// </summary>
        OpUnregister = 1
    }
}
