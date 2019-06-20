//-----------------------------------------------------------------------------
// FILE:        RadiusCode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: RADIUS protocol packet codes.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// RADIUS protocol packet codes.
    /// </summary>
    public enum RadiusCode
    {
        /// <summary>
        /// Packet requests network access.
        /// </summary>
        AccessRequest = 1,

        /// <summary>
        /// Packet indicates that network access is granted.
        /// </summary>
        AccessAccept = 2,

        /// <summary>
        /// Packet indicates that network access is denied.
        /// </summary>
        AccessReject = 3,

        /// <summary>
        /// Packet requests account information.
        /// </summary>
        AccountingRequest = 4,

        /// <summary>
        /// Packet contains accounting information.
        /// </summary>
        AccountingResponse = 5,

        /// <summary>
        /// Packet challenges client requesting network access to 
        /// encrypt a number with a shared secret.
        /// </summary>
        AccessChallenge = 11,

        /// <summary>
        /// Server status request (experimental).
        /// </summary>
        StatusServer = 12,

        /// <summary>
        /// Client status request (experimental).
        /// </summary>
        StatusClient = 13
    }
}
