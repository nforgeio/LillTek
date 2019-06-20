//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UDP broadcast cluster related constants and utility methods.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Net.Broadcast
{
    /// <summary>
    /// UDP broadcast cluster related constants and utility methods.
    /// </summary>
    internal static class UdpBroadcastHelper
    {
        /// <summary>
        /// The default shared key.
        /// </summary>
        internal const string DefaultSharedKey = "aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==";
    }
}
