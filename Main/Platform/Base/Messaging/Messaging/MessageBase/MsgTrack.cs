//-----------------------------------------------------------------------------
// FILE:        MsgTrack.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds tracking information about a message for which we're expecting a ReceiptMsg
//              to be delivered back to the forwarding router.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds tracking information about a message for which we're expecting a <see cref="ReceiptMsg" /> 
    /// to be delivered back to the forwarding router.
    /// </summary>
    internal sealed class MsgTrack
    {
        /// <summary>
        /// The target router's physical endpoint.
        /// </summary>
        public readonly MsgEP RouterEP;

        /// <summary>
        /// The logical endpoint set ID if the message is being sent at a 
        /// logical endpoint on the targeted router, null otherwise.
        /// </summary>
        public readonly Guid LogicalEndpointSetID;

        /// <summary>
        /// The forwarded message's GUID.
        /// </summary>
        public readonly Guid MsgID;

        /// <summary>
        /// The time (SYS) beyond which we'll consider that we've detected a dead router
        /// if we haven't seen a valid <see cref="ReceiptMsg" />.
        /// </summary>
        public readonly DateTime TTD;

        /// <summary>
        /// Constructs a MsgTrack instance from the parameters passed.
        /// </summary>
        /// <param name="routerEP">The target router's physical endpoint.</param>
        /// <param name="logicalEndpointSetID">
        /// The logical endpoint set ID if the message is being sent at a logical 
        /// endpoint on the targeted router, null otherwise.
        /// </param>
        /// <param name="msgID">The forwarded message's GUID.</param>
        /// <param name="ttd">
        /// The time (SYS) beyond which we'll consider that we've detected a dead 
        /// router if we haven't seen a valid <see cref="ReceiptMsg" />.
        /// </param>
        public MsgTrack(MsgEP routerEP, Guid logicalEndpointSetID, Guid msgID, DateTime ttd)
        {
            this.RouterEP             = routerEP;
            this.LogicalEndpointSetID = logicalEndpointSetID;
            this.MsgID                = msgID;
            this.TTD                  = ttd;
        }
    }
}
