//-----------------------------------------------------------------------------
// FILE:        HttpOption.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the HTTP connection options.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Specifies the HTTP connection options.
    /// </summary>
    [Flags]
    public enum HttpOption
    {
        /// <summary>
        /// Enable no special connection functions.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        /// Establish a SSL connection (not implemented).
        /// </summary>
        SSL = 0x00000001,

        /// <summary>
        /// Perform any necessary authentication (not implemented).
        /// </summary>
        Authentication = 0x00000002,

        /// <summary>
        /// Automatically follow any redirects (not implemented).
        /// </summary>
        FollowRedirects = 0x00000004,
    }
}
