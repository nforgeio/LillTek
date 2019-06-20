//-----------------------------------------------------------------------------
// FILE:        HttpAsyncState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used internally to track async operations.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Used internally to track async operations.
    /// </summary>
    internal sealed class HttpAsyncState
    {
        /// <summary>
        /// <c>true</c> if processing the first request on a connection.
        /// </summary>
        public bool FirstRequest;

        /// <summary>
        /// The HTTP request being received.
        /// </summary>
        public HttpRequest Request;

        /// <summary>
        /// The request socket.
        /// </summary>
        public EnhancedSocket Socket;

        /// <summary>
        /// The network buffer.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Number of bytes received so far.
        /// </summary>
        public int RecvSize;
    }
}
