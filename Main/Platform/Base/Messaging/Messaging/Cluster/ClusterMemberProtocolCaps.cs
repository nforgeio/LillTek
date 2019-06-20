//-----------------------------------------------------------------------------
// FILE:        ClusterMemberProtocolCaps.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the cluster member protocol capability flags.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the cluster member protocol capability flags.
    /// </summary>
    /// <remarks>
    /// Each <see cref="ClusterMemberMsg" /> includes a <see cref="ClusterMemberMsg.ProtocolCaps" />
    /// property that describes the capabilities of the sender.  This is designed to provide for
    /// the possibility of backwards compatibility as the clustering protocol is extended in
    /// the future.  These flags will also included in the cluster status broadcast by the
    /// cluster master instance and will be available <see cref="ClusterStatus" /> and
    /// <see cref="ClusterMemberStatus" /> information maintained by all cluster members.
    /// </remarks>
    [Flags]
    public enum ClusterMemberProtocolCaps
    {
        /// <summary>
        /// Indicates that the cluster member implements the baseline
        /// cluster protocol.
        /// </summary>
        Baseline = 0x00000001,

        /// <summary>
        /// Indicates the current implementation's capabilities.
        /// </summary>
        Current = Baseline
    }
}
