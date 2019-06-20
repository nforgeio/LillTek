//-----------------------------------------------------------------------------
// FILE:        DynDnsMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the possible Dynamic DNS operating modes.

using System;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Identifies the possible Dynamic DNS server operating modes.
    /// </summary>
    public enum DynDnsMode
    {
        /// <summary>
        /// The server receives host entry updates from clients via UDP messages.
        /// </summary>
        Udp,

        /// <summary>
        /// The server receives host entry updates from clients via LillTek clustering.
        /// </summary>
        Cluster,

        /// <summary>
        /// The server receives host entry updates via UDP and LillTek messages.
        /// </summary>
        Both
    }
}
