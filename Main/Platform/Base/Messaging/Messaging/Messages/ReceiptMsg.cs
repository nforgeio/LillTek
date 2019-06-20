//-----------------------------------------------------------------------------
// FILE:        ReceiptMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sent upon request when a router processes a message targeted
//              at one of its application message handlers.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Sent upon request when a router processes a message targeted at one of 
    /// its application message handlers.
    /// </summary>
    public sealed class ReceiptMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".Receipt";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public ReceiptMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="receiptID">
        /// The ID of the message whose delivery is being confirmed.
        /// </param>
        public ReceiptMsg(Guid receiptID)
        {
            this._Flags    = MsgFlag.Priority;
            this.ReceiptID = receiptID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private ReceiptMsg(Stub param)
            : base(param)
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            ReceiptMsg clone;

            clone = new ReceiptMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The ID of the message whose delivery is being confirmed.
        /// </summary>
        public Guid ReceiptID
        {
            get { return base._Get("receipt-id", Guid.Empty); }
            set { base._Set("receipt-id", value); }
        }
    }
}
