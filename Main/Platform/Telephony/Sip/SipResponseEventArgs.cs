//-----------------------------------------------------------------------------
// FILE:        SipResponseEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments passed to a SipResponseDelegate event handler.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// The arguments passed to a <see cref="SipResponseDelegate" /> event handler.
    /// </summary>
    public sealed class SipResponseEventArgs
    {
        private SipResponse response;

        /// <summary>
        /// The received <see cref="SipStatus" />.
        /// </summary>
        public readonly SipStatus Status;

        /// <summary>
        /// The received <see cref="SipResponse" /> (or <c>null</c> if an error was detected without receiving a response).
        /// </summary>
        public SipResponse Response
        {
            get { return response; }
            internal set { response = value; }
        }

        /// <summary>
        /// The <see cref="SipClientTransaction" /> handling the request.
        /// </summary>
        public readonly SipClientTransaction Transaction;

        /// <summary>
        /// The <see cref="SipDialog" /> this response is <b>known</b> to be associated 
        /// with, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Note that some lower level event handlers will be called before
        /// a dialog correlation has been attempted.  This property will be
        /// set to <c>null</c> in this case, even though the response may
        /// actually be associated with a dialog.
        /// </note>
        /// </remarks>
        public SipDialog Dialog;

        /// <summary>
        /// The <see cref="SipClientAgent" /> managing the transaction.
        /// </summary>
        public readonly SipClientAgent Agent;

        /// <summary>
        /// The <see cref="SipCore" /> that raised the event.
        /// </summary>
        public readonly SipCore Core;

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" />.</param>
        /// <param name="response">The received <see cref="SipResponse" /> (or <c>null</c>).</param>
        /// <param name="transaction">The <see cref="SipClientTransaction" />.</param>
        /// <param name="dialog">The associated <see cref="SipDialog" /> (or <c>null</c>).</param>
        /// <param name="agent">The <see cref="SipClientAgent" /> that processed the response.</param>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        internal SipResponseEventArgs(SipStatus status, SipResponse response, SipClientTransaction transaction,
                                      SipDialog dialog, SipClientAgent agent, SipCore core)
        {
            this.Status      = status;
            this.Response    = response;
            this.Transaction = transaction;
            this.Dialog      = dialog;
            this.Agent       = agent;
            this.Core        = core;
        }
    }
}
