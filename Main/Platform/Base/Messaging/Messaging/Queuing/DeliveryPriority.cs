//-----------------------------------------------------------------------------
// FILE:        DeliveryPriority.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An enumeration describing the possible delivery priority
//              levels for queued messages.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// An enumeration describing the possible delivery priority levels for queued messages.
    /// </summary>
    public enum DeliveryPriority
    {
        /// <summary>
        /// Very low delivery priority.
        /// </summary>
        VeryLow = 0,

        /// <summary>
        /// Low delivery priority.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Normal delivery priority.
        /// </summary>
        Normal = 2,

        /// <summary>
        /// High delivery priority.
        /// </summary>
        High = 3,

        /// <summary>
        /// Very high delivery priority.
        /// </summary>
        VeryHigh = 4
    }
}
