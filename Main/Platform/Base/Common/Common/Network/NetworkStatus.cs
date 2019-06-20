//-----------------------------------------------------------------------------
// FILE:        NetworkStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the current state of the network connection.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Describes the current state of the network connection.
    /// </summary>
    public enum NetworkStatus
    {
        /// <summary>
        /// The network connection status is not known.
        /// </summary>
        Unknown,

        /// <summary>
        /// The network is not connected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The system is connected via ethernet.
        /// </summary>
        Ethernet,

        /// <summary>
        /// The system is connected via a 802.11 wi-fi hotspot.
        /// </summary>
        Wifi,

        /// <summary>
        /// The system is connected to a GSM cellular network.
        /// </summary>
        Gsm,

        /// <summary>
        /// The system is connected to a CDMA cellular network.
        /// </summary>
        Cdma
    }
}
