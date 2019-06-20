//-----------------------------------------------------------------------------
// FILE:        MsgQueueFlag.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible queued message flags.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Enumerates the possible queued message flags.
    /// </summary>
    [Flags]
    public enum MsgQueueFlag
    {
        /// <summary>
        /// Indicates that no flag bits are set.
        /// </summary>
        None = 0x00000000,
    }
}
