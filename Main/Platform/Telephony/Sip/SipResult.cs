//-----------------------------------------------------------------------------
// FILE:        SipResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the results of a SIP request transaction.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Holds the results of a SIP request transaction.
    /// </summary>
    public sealed class SipResult
    {
        /// <summary>
        /// The original request message.
        /// </summary>
        public readonly SipRequest Request;

        /// <summary>
        /// The final response message returned or <c>null</c> if the operation failed.
        /// </summary>
        public SipResponse Response;

        /// <summary>
        /// The status code indicating the success or failure of the operation.
        /// </summary>
        public SipStatus Status;

        /// <summary>
        /// The new <see cref="SipDialog" /> if one was created as the result
        /// of the transaction.
        /// </summary>
        public SipDialog Dialog;

        /// <summary>
        /// The <see cref="ISipAgent" /> that sent the request and received the response.
        /// </summary>
        public ISipAgent Agent;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="request">The initiating <see cref="SipRequest" />.</param>
        /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
        /// <param name="agent">The <see cref="ISipAgent" /> that sent the request and received the response.</param>
        /// <param name="status">The final operation status.</param>
        public SipResult(SipRequest request, SipDialog dialog, ISipAgent agent, SipStatus status)
        {
            this.Request  = request;
            this.Response = null;
            this.Status   = status;
            this.Dialog   = dialog;
            this.Agent    = agent;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="request">The initiating <see cref="SipRequest" />.</param>
        /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
        /// <param name="agent">The <see cref="ISipAgent" /> that sent the request and received the response.</param>
        /// <param name="response">The final <see cref="SipResponse" />.</param>
        public SipResult(SipRequest request, SipDialog dialog, ISipAgent agent, SipResponse response)
        {
            this.Request  = request;
            this.Response = response;
            this.Status   = response.Status;
            this.Dialog   = dialog;
            this.Agent    = agent;
        }
    }
}
