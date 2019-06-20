//-----------------------------------------------------------------------------
// FILE:        HttpAsyncResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An IAsyncResult implementation used by HttpClient.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// An IAsyncResult implementation used by HttpClient.
    /// </summary>
    internal sealed class HttpAsyncResult : AsyncResult
    {
        /// <summary>
        /// The query response received from the server.
        /// </summary>
        public HttpResponse Response;

        /// <summary>
        /// The response receive buffer.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Initialize the instance.
        /// </summary>
        /// <param name="owner">The object that "owns" this operation (or <c>null</c>).</param>
        /// <param name="callback">The delegate to call when the operation completes.</param>
        /// <param name="state">The application defined state.</param>
        /// <remarks>
        /// The owner parameter is optionally used to identify the object that "owns"
        /// this operation.  This parameter may be null or any object type.  Additional
        /// information will be tracked by AsyncTracker if the object implements the
        /// IAsyncResultOwner interface.
        /// </remarks>
        public HttpAsyncResult(object owner, AsyncCallback callback, object state)
            : base(owner, callback, state)
        {
        }
    }
}
