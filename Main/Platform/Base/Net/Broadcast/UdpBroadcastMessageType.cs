//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastMessageType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the type of a UdpBroadcastMessage.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Net.Broadcast
{
    /// <summary>
    /// Identifies the type of a <see cref="UdpBroadcastMessage" />.
    /// </summary>
    internal enum UdpBroadcastMessageType
    {
        // WARNING: Do not change these ordinal values unless you are prepared
        //          to rebuild and deploy all systems interacting with the a
        //          UDP broadcast system.

        /// <summary>
        /// Sent by UDP broadcast servers to all of the servers in the cluster
        /// to register and renew the presence of the server instance.
        /// </summary>
        ServerRegister = 0,

        /// <summary>
        /// Sent by UDP broadcast servers to all of the servers in the cluster
        /// deregistering the presence of the server instance.
        /// </summary>
        ServerUnregister = 1,

        /// <summary>
        /// Sent by UDP broadcast clients to all of the servers in the cluster
        /// to register and renew the presence of the client instance.
        /// </summary>
        ClientRegister = 2,

        /// <summary>
        /// Sent by UDP broadcast clients to all of the servers in the cluster
        /// to deregister client instance.
        /// </summary>
        ClientUnregister = 3,

        /// <summary>
        /// Sent by UDP broadcast clients to all servers in the cluster with a
        /// payload to be delivered to all clients in the broadcast group.
        /// The cluster's master server will transmit the payload to the
        /// clients within broadcast messages.
        /// </summary>
        Broadcast = 4
    }
}
