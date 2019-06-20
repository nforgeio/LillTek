//-----------------------------------------------------------------------------
// FILE:        DiscoveryMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible states of a ClusterMember instance.

using System;

using LillTek.Common;
using LillTek.Net.Broadcast;

namespace LillTek.Messaging
{
    /// <summary>
    /// Specifies how <see cref="MsgRouter" />s go about dynamically discovering other
    /// routers on the local network.
    /// </summary>
    public enum DiscoveryMode
    {
        /// <summary>
        /// Use UDP multicast to broadcast and receive presence packets.
        /// </summary>
        Multicast,

        /// <summary>
        /// Use the LillTek UDP broadcast server broadcast and receive precence
        /// packets on networks that don't support multicast (see <see cref="UdpBroadcastServer" />
        /// for more information).
        /// </summary>
        UdpBroadcast
    }
}
