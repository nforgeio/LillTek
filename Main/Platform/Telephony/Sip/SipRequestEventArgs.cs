//-----------------------------------------------------------------------------
// FILE:        SipRequestEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments passed to a SipRequestDelegate event handler.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// The arguments passed to a <see cref="SipRequestDelegate" /> event handler.
    /// </summary>
    public sealed class SipRequestEventArgs
    {
        private SipRequest request;

        /// <summary>
        /// The received <see cref="SipRequest" />.
        /// </summary>
        /// <remarks>
        /// <note>
        /// If this is request is and ACK for an INVITE received earlier,
        /// then the <see cref="InviteRequest" /> property will be set
        /// to that INVITE request.
        /// </note>
        /// <note>
        /// This will be set to <c>null</c> when a <see cref="SipCore" />
        /// raises its <see cref="SipCore.DialogClosed" /> event.
        /// </note>
        /// </remarks>
        public SipRequest Request
        {
            get { return request; }
            internal set { request = value; }
        }

        /// <summary>
        /// The <see cref="SipServerTransaction" /> handling the request.
        /// </summary>
        public readonly SipServerTransaction Transaction;

        /// <summary>
        /// The <see cref="SipDialog" /> this request is <b>known</b> to be associated 
        /// with, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Note that some lower level event handlers will be called before
        /// a dialog correlation has been attempted.  This property will be
        /// set to <c>null</c> in this case, even though the request may
        /// actually be associated with a dialog.
        /// </note>
        /// </remarks>
        public SipDialog Dialog;

        /// <summary>
        /// The <see cref="SipServerAgent" /> managing the transaction.
        /// </summary>
        public readonly SipServerAgent Agent;

        /// <summary>
        /// The <see cref="SipCore" /> that raised the event.
        /// </summary>
        public readonly SipCore Core;

        /// <summary>
        /// This will be set to the original INVITE request when the ACK
        /// is received from the UAC.
        /// </summary>
        /// <remarks>
        /// <note>
        /// If this property is not <c>null</c> then <see cref="Request" />
        /// will be set to the ACK request.
        /// </note>
        /// </remarks>
        public readonly SipRequest InviteRequest;

        /// <summary>
        /// This property is initially set to <c>null</c>.  <see cref="SipRequestDelegate" />
        /// event handlers may set this to the <see cref="SipResponse" /> to be returned to 
        /// the UAC that submitted the request.
        /// </summary>
        public SipResponse Response = null;

        /// <summary>
        /// Indicates that the event handler will process the received <see cref="SipRequest" />
        /// and send the <see cref="SipResponse" /> asynchronously.  Set to <c>false</c> by default.
        /// </summary>
        public bool WillRespondAsynchronously = false;

        /// <summary>
        /// Set internally by <see cref="SipCore" />'s <see cref="SipCore.Reply(SipRequestEventArgs,SipStatus,string )" />
        /// and <see cref="SipCore.Reply(SipRequestEventArgs ,SipResponse )" /> methods as well as
        /// <see cref="SipDialog" />'s <see cref="SipDialog.Reply(SipRequestEventArgs,SipResponse)" /> 
        /// method to indicate that a response has already been sent for this event.
        /// </summary>
        internal bool ResponseSent = false;

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="request">The received <see cref="SipRequest" />.</param>
        /// <param name="transaction">The associated <see cref="SipServerTransaction" />.</param>
        /// <param name="dialog">The associated <see cref="SipDialog" /> (or <c>null</c>).</param>
        /// <param name="agent">The <see cref="SipServerAgent" /> that processed the request.</param>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        /// <param name="inviteRequest">The original INVITE <see cref="SipRequest" />.</param>
        internal SipRequestEventArgs(SipRequest request, SipServerTransaction transaction, SipDialog dialog,
                                     SipServerAgent agent, SipCore core, SipRequest inviteRequest)
        {
            this.Request       = request;
            this.Transaction   = transaction;
            this.Dialog        = dialog;
            this.Agent         = agent;
            this.Core          = core;
            this.InviteRequest = inviteRequest;
        }
    }
}
