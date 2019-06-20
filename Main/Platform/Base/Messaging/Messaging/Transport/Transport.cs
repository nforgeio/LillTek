//-----------------------------------------------------------------------------
// FILE:        MsgTransport.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the types of message transports.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the types of message transports.
    /// </summary>
    public enum Transport
    {
        /// <summary>
        /// Transport via a UDP multicast to all listening stations.
        /// </summary>
        Multicast,

        /// <summary>
        /// Point-to-point transport via UDP.
        /// </summary>
        Udp,

        /// <summary>
        /// Point-to-point transport via TCP.
        /// </summary>
        Tcp
    }
}
