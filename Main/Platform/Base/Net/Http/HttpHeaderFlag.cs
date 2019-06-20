//-----------------------------------------------------------------------------
// FILE:        HttpHeaderFlag.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Flag bits passed to EnhancedHttpListener.AddResponseHeaders.

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Flag bits passed to <see cref="EnhancedHttpListener.AddResponseHeaders" />.
    /// </summary>
    [Flags]
    public enum HttpHeaderFlag
    {
        /// <summary>
        /// This adds the typical headers required by non-cachable API responses.
        /// </summary>
        ApiTransient = Server | NoCache,

        /// <summary>
        /// Adds the <b>Server</b> header with the name passed to the 
        /// <see cref="EnhancedHttpListener(string)" /> constructor.
        /// </summary>
        Server = 0x00000001,

        /// <summary>
        /// Adds headers to disable client and proxy caching of the response
        /// <b>Pragma: no-cache</b> and <b>Cache-Control: private</b>.
        /// </summary>
        NoCache = 0x00000002,
    }
}
