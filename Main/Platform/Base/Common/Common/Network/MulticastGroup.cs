//-----------------------------------------------------------------------------
// FILE:        TcpConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc network related constants

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Defines some common multicast group IP addresses and endpoints.
    /// </summary>
    public static class MulticastGroup
    {
        /// <summary>
        /// Used by <b>NetHelper.GetActiveAdapter()</b>.
        /// </summary>
        public static readonly IPAddress GetActiveAdapter = IPAddress.Parse("231.222.0.3");

        /// <summary>
        /// The default LillTek NetTrace multicast group.
        /// </summary>
        public const string NetTraceGroup = "231.222.0.77";

    }
}
