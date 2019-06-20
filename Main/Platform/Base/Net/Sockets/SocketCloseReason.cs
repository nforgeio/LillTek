//-----------------------------------------------------------------------------
// FILE:        SocketCloseReason.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used by SocketClosedException to detail how a socket was closed.

using System;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Used by <see cref="SocketClosedException" /> to detail how a socket was closed.
    /// </summary>
    public enum SocketCloseReason
    {
        /// <summary>
        /// The socket was closed locally and this exception is probably the result
        /// of a pending asynchronous operation failing due to the socket now being
        /// closed.
        /// </summary>
        LocalClose,

        /// <summary>
        /// The socket was closed gracefully on the remote side.
        /// </summary>
        RemoteClose,

        /// <summary>
        /// The socket was closed or reset from the remote side without performing
        /// the shut down handshake.
        /// </summary>
        RemoteReset,
    }
}
