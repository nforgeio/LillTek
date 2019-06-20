//-----------------------------------------------------------------------------
// FILE:        TopologyCapability.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the special capabilities of an ITopologyProvider implementation.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the special capabilities of an <see cref="ITopologyProvider" /> implementation
    /// as individual flag bits.
    /// </summary>
    [Flags]
    public enum TopologyCapability
    {
        /// <summary>
        /// The topology has no special capabilities.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        /// The topology localizes traffic to specific servers in the cluster based
        /// on the topology key parameter.
        /// </summary>
        Locality = 0x00000001,

        /// <summary>
        /// The number of servers in the cluster may vary over time for reasons other
        /// than failure conditions.
        /// </summary>
        Dynamic = 0x00000002,
    }
}
