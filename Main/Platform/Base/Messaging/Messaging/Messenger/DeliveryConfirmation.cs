//-----------------------------------------------------------------------------
// FILE:        DeliveryConfirmation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds information about a successful or failed reliable messaging 
//              delivery attempt.

using System;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds information about a successful or failed reliable messaging delivery attempt.
    /// </summary>
    public sealed class DeliveryConfirmation
    {
        /// <summary>
        /// The time (UTC) when the message was delivered successfully or 
        /// or the delivery attempts has been aborted.
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        /// The original target endpoint or cluster endpoint.
        /// </summary>
        public MsgEP TargetEP;

        /// <summary>
        /// The original query message.  Note that if the message type is not 
        /// currently registered as with the LillTek.Messaging library then an 
        /// <see cref="EnvelopeMsg" /> instance will be returned instead.
        /// </summary>
        public Msg Query;

        /// <summary>
        /// The cluster's globally unique topology provider instance ID if the 
        /// message was targeted at at a cluster, <see cref="Guid.Empty" /> otherwise.
        /// </summary>
        public Guid TopologyID;

        /// <summary>
        /// The serialized topology provider client state if the message was targeted
        /// at a cluster, null otherwise.
        /// </summary>
        public string TopologyInfo;

        /// <summary>
        /// The serialized topology parameter or <c>null</c>.
        /// </summary>
        public string TopologyParam;

        /// <summary>
        /// This is <c>null</c> if the message was delivered successfully otherwise this
        /// will be the exception returned by the target or a <see cref="TimeoutException" />
        /// if the messenger aborted the delivery.
        /// </summary>
        public Exception Exception;

        /// <summary>
        /// The response then target or <c>null</c> if the message delivery failed.  Note
        /// that if the message type is not currently registered as with the 
        /// LillTek.Messaging library then an <see cref="EnvelopeMsg" /> instance
        /// will be returned instead.
        /// </summary>
        public Msg Response;

        /// <summary>
        /// This property is available for use by internal <see cref="IReliableMessenger" />
        /// implementations to hold implementation specific state.  This is initialized
        /// to null by the constructor. 
        /// </summary>
        internal object State;

        /// <summary>
        /// Constructs an instance with default property values.
        /// </summary>
        public DeliveryConfirmation()
        {
            this.Timestamp     = DateTime.MinValue;
            this.TargetEP      = null;
            this.Query         = null;
            this.TopologyID    = Guid.Empty;
            this.TopologyInfo  = null;
            this.TopologyParam = null;
            this.Exception     = null;
            this.Response      = null;
            this.State         = null;
        }

        /// <summary>
        /// Constructs an instance by copying the appropriate fields from
        /// the delivery confirmation message passed.
        /// </summary>
        public DeliveryConfirmation(DeliveryMsg msg)
        {
            Assertion.Test(msg.Operation == DeliveryOperation.Confirmation);

            this.Timestamp     = msg.Timestamp;
            this.TargetEP      = msg.TargetEP;
            this.Query         = msg.Query;
            this.TopologyID    = msg.TopologyID;
            this.TopologyInfo  = msg.TopologyInfo;
            this.TopologyParam = msg.TopologyParam;
            this.Exception     = msg.Exception;
            this.Response      = msg.Response;
            this.State         = null;
        }
    }
}
