//-----------------------------------------------------------------------------
// FILE:        IHttpModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the behavior an HTTP server plug-in to the
//              HttpServer.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Describes the behavior an HTTP server plug-in to a HttpServer.
    /// </summary>
    public interface IHttpModule
    {
        /// <summary>
        /// Handles a HTTP request received by the server.
        /// </summary>
        /// <param name="server">The calling server.</param>
        /// <param name="request">The request.</param>
        /// <param name="firstRequest"><c>true</c> if this is the first request on a connection.</param>
        /// <param name="close">Returns as <c>true</c> if the client connection is to be closed.</param>
        /// <returns>The reply to be returned to the client (or <c>null</c>).</returns>
        /// <remarks>
        /// The <see cref="HttpServer" /> class supports the installation of multiple <see cref="IHttpModule" />
        /// instances.  Each request received will be passed to each module in
        /// turn to be handled stopping at the first module whose <see cref="IHttpModule.OnRequest" />
        /// method returns a non-<c>null</c> result.  The server will close the 
        /// client connection if any of the <see cref="IHttpModule.OnRequest" /> methods called set
        /// close=true.
        /// </remarks>
        HttpResponse OnRequest(HttpServer server, HttpRequest request, bool firstRequest, out bool close);
    }
}
