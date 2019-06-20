//-----------------------------------------------------------------------------
// FILE:        DeliveryStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumeration describing the status of a queued message delivery.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Enumeration describing the status of a queued message delivery.
    /// </summary>
    public enum DeliveryStatus
    {
        /// <summary>
        /// The message is still in transit.
        /// </summary>
        InTransit = 0,

        /// <summary>
        /// Message was delivered successfully.
        /// </summary>
        Delivered = 1,

        /// <summary>
        /// Message lifetime expired before being delivered.
        /// </summary>
        Expired = 2,

        /// <summary>
        /// Message delivery could not be confirmed by the consumer.
        /// </summary>
        Poison = 3,

        /// <summary>
        /// Message was purged while in transit either manually
        /// or via an automatic purge of messages that have been
        /// queued for longer than a set criteria.
        /// </summary>
        TransitPurge = 4
    }
}
