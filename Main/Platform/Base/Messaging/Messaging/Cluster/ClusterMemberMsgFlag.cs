//-----------------------------------------------------------------------------
// FILE:        ClusterMemberMsgFlag.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines optional flags passed with ClusterMember messages.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines optional flags passed with <see cref="ClusterMemberMsg" />s.
    /// </summary>
    [Flags]
    public enum ClusterMemberMsgFlag
    {
        /// <summary>
        /// No flags are set.
        /// </summary>
        None = 0x0000000,

        /// <summary>
        /// Indicates that a member-status update sent to a master
        /// should be replicated to the cluster ASAP.
        /// </summary>
        Priority = 0x00000001
    }
}
