//-----------------------------------------------------------------------------
// FILE:        RadiusServiceType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: RADIUS service type codes.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// RADIUS service type codes.
    /// </summary>
    public enum RadiusServiceType
    {
        /// <summary>
        /// The user should be connected to a host.
        /// </summary>
        Login = 1,

        /// <summary>
        /// A Framed Protocol should be started for the
        /// User, such as PPP or SLIP.
        /// </summary>
        Framed = 2,

        /// <summary>
        /// The user should be disconnected and called
        /// back, then connected to a host.
        /// </summary>
        CallbackLogin = 3,

        /// <summary>
        /// The user should be disconnected and called
        /// back, then a Framed Protocol should be started
        /// for the User, such as PPP or SLIP.
        /// </summary>
        CallbackFramed = 4,

        /// <summary>
        /// The user should be granted access to outgoing devices.
        /// </summary>
        Outbound = 5,

        /// <summary>
        /// The user should be granted access to the
        /// administrative interface to the NAS from which
        /// privileged commands can be executed.
        /// </summary>
        Administrative = 6,

        /// <summary>
        /// The user should be provided a command prompt
        /// on the NAS from which non-privileged commands
        /// can be executed.
        /// </summary>
        NasPrompt = 7,

        /// <summary>
        /// Only Authentication is requested, and no
        /// authorization information needs to be returned
        /// in the Access-Accept (typically used by proxy
        /// servers rather than the NAS itself).
        /// </summary>
        AuthenticateOnly = 8,

        /// <summary>
        /// The user should be disconnected and called
        /// back, then provided a command prompt on the
        /// NAS from which non-privileged commands can be
        /// executed.
        /// </summary>
        CallbackNasPrompt = 9,

        /// <summary>
        /// Used by the NAS in an Access-Request packet to
        /// indicate that a call is being received and
        /// that the RADIUS server should send back an
        /// Access-Accept to answer the call, or an
        /// Access-Reject to not accept the call,
        /// typically based on the Called-Station-Id or
        /// Calling-Station-Id attributes.  It is
        /// recommended that such Access-Requests use the
        /// value of Calling-Station-Id as the value of
        /// the User-Name.
        /// </summary>
        CallCheck = 10,

        /// <summary>
        /// The user should be disconnected and called
        /// back, then granted access to the
        /// administrative interface to the NAS from which
        /// privileged commands can be executed.
        /// </summary>
        CallbackAdministrative = 11
    }
}
