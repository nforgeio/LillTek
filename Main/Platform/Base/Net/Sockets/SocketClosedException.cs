//-----------------------------------------------------------------------------
// FILE:        SocketClosedException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown when an operation is attempted on a closed socket.

using System;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Thrown when an operation is attempted on a closed socket.
    /// </summary>
    public sealed class SocketClosedException : SocketException
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SocketClosedException()
            : base()
        {
        }

        /// <summary>
        /// Constructs a SocketClosedException instance with a reason code.
        /// </summary>
        /// <param name="reason">Identifies the exception reason.</param>
        public SocketClosedException(SocketCloseReason reason)
            : base((reason == SocketCloseReason.LocalClose || reason == SocketCloseReason.RemoteClose) ? 10057 /* WSAENOTCONN */ : 10054 /* WSAECONNRESET */ )
        {
        }
    }
}
