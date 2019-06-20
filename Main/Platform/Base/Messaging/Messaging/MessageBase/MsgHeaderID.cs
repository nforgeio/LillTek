//-----------------------------------------------------------------------------
// FILE:        MsgHeaderID.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible extended message header types.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the possible extended message header types.
    /// </summary>
    public enum MsgHeaderID : byte
    {
        /// <summary>
        /// This extended header is ignored by the messaging layer and 
        /// is currently used to unit testing extended headers.
        /// </summary>
        Comment = 0,

        /// <summary>
        /// <see cref="DuplexSession" /> related information encoded
        /// using <see cref="DuplexSessionHeader" />.
        /// </summary>
        DuplexSession = 1,
    }
}
