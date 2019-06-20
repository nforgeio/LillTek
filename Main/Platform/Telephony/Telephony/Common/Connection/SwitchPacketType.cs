//-----------------------------------------------------------------------------
// FILE:        SwitchPacketType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the type of a switch packet.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes the type of a switch packet.
    /// </summary>
    public enum SwitchPacketType
    {
        /// <summary>
        /// The packet type is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// The packet acknowledges that the switch has received a command.
        /// </summary>
        ExecuteAck,

        /// <summary>
        /// The packet holds the response to a command execution.
        /// </summary>
        ExecuteResponse,

        /// <summary>
        /// The packet holds a switch event.
        /// </summary>
        Event,

        /// <summary>
        /// The packet holds a switch log entry.
        /// </summary>
        Log,

        /// <summary>
        /// The packet holds a switch command.
        /// </summary>
        Command,
    }
}
