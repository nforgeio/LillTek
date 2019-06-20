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
    /// Defines various TCP related constants.
    /// </summary>
    public static class TcpConst
    {
        /// <summary>
        /// UDP Maximum Transmission Unit (MTU).
        /// </summary>
        public const int MTU = 1460;
    }
}
