//-----------------------------------------------------------------------------
// FILE:        SipB2BUAEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the arguments passed when a SipB2BUserAgent raises its events.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines the arguments passed when a <see cref="SipB2BUserAgent{TState}" /> raises its events.
    /// </summary>
    /// <typeparam name="TState">The application session state type.</typeparam>
    public sealed class SipB2BUAEventArgs<TState>
    {
        /// <summary>
        /// Overrides the default maximum number of INVITE redirects if positive.
        /// </summary>
        public int MaxRedirects = 0;

        /// <summary>
        /// Overrides the default <b>Contact</b> header value when transmitting SIP
        /// messages from the B2BUA to the server (when not <c>null</c>).
        /// </summary>
        public SipContactValue ServerLocalContact = null;

        /// <summary>
        /// Overrides the default <b>Contact</b> header value when transmitting SIP
        /// messages from the B2BUA to the client (when not <c>null</c>).
        /// </summary>
        public SipContactValue ClientLocalContact = null;

        /// <summary>
        /// <c>true</c> if the B2BUA session should be closed after the event handler returns.
        /// </summary>
        public bool CloseSession = false;

        /// <summary>
        /// The associated <see cref="SipCore" />.
        /// </summary>
        public readonly SipCore Core;

        /// <summary>
        /// The <see cref="SipB2BUserAgent{TState}" /> that raised the event.
        /// </summary>
        public readonly SipB2BUserAgent<TState> B2BUserAgent;

        /// <summary>
        /// The session associated with the event.  The session includes the application state 
        /// for the session as well as the upstream and downstream dialogs.
        /// </summary>
        public readonly SipB2BUASession<TState> Session;

        /// <summary>
        /// The unmodified received <see cref="SipRequest" /> (or <c>null</c>).
        /// </summary>
        public SipRequest ReceivedRequest;

        /// <summary>
        /// The <see cref="SipRequest" /> to be forwarded by the B2BUA (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is initialized by the B2BUA by copying all of the non-dialog
        /// related request properties and headers from the request received and
        /// initializing the dialog-related fields to reasonable values for establishing
        /// or maintaining a dialog with the remote peer.
        /// </para>
        /// <para>
        /// Applications can modify or replace this request as necessary and may also
        /// set the property to <c>null</c>, indicating that no request should be
        /// forwarded.
        /// </para>
        /// </remarks>
        public SipRequest Request;

        /// <summary>
        /// The unmodified received <see cref="SipResponse" /> (or <c>null</c>).
        /// </summary>
        public SipResponse ReceivedResponse;

        /// <summary>
        /// The <see cref="SipResponse" /> to be forwarded by the B2BUA (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is initialized by the B2BUA by copying all of the non-dialog
        /// related response properties and headers from the request received and
        /// initializing the dialog-related fields to reasonable values for establishing
        /// or maintaining a dialog with the remote peer.
        /// </para>
        /// <para>
        /// Applications can modify or replace this response as necessary and may also
        /// set the property to <c>null</c>, indicating that no response should be
        /// forwarded.
        /// </para>
        /// </remarks>
        public SipResponse Response;

        /// <summary>
        /// Constructs B2BUA event arguments.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        /// <param name="b2bUserAgent">The <see cref="SipB2BUserAgent{TState}" /> that raised the event.</param>
        /// <param name="session">The <see cref="SipB2BUASession{TState}" /> associated with the event.</param>
        internal SipB2BUAEventArgs(SipCore core,
                                   SipB2BUserAgent<TState> b2bUserAgent,
                                   SipB2BUASession<TState> session)
        {
            this.Core         = core;
            this.B2BUserAgent = b2bUserAgent;
            this.Session      = session;
        }

        /// <summary>
        /// Constructs B2BUA event arguments for a received <see cref="SipRequest" />.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        /// <param name="b2bUserAgent">The <see cref="SipB2BUserAgent{TState}" /> that raised the event.</param>
        /// <param name="session">The <see cref="SipB2BUASession{TState}" /> associated with the event.</param>
        /// <param name="receivedRequest">The received <see cref="SipRequest" />.</param>
        /// <param name="request">The proposed <see cref="SipRequest" /> to be forwarded.</param>
        internal SipB2BUAEventArgs(SipCore core,
                                   SipB2BUserAgent<TState> b2bUserAgent,
                                   SipB2BUASession<TState> session,
                                   SipRequest receivedRequest,
                                   SipRequest request)
        {
            Assertion.Test(receivedRequest != null);
            Assertion.Test(request != null);

            this.Core            = core;
            this.B2BUserAgent    = b2bUserAgent;
            this.Session         = session;
            this.ReceivedRequest = receivedRequest;
            this.Request         = request;
        }

        /// <summary>
        /// Constructs B2BUA event arguments for a received <see cref="SipResponse" />.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        /// <param name="b2bUserAgent">The <see cref="SipB2BUserAgent{TState}" /> that raised the event.</param>
        /// <param name="session">The <see cref="SipB2BUASession{TState}" /> associated with the event.</param>
        /// <param name="receivedResponse">The received <see cref="SipResponse" />.</param>
        /// <param name="response">The proposed <see cref="SipResponse" /> to be forwarded.</param>
        internal SipB2BUAEventArgs(SipCore core,
                                   SipB2BUserAgent<TState> b2bUserAgent,
                                   SipB2BUASession<TState> session,
                                   SipResponse receivedResponse,
                                   SipResponse response)
        {
            Assertion.Test(receivedResponse != null);
            Assertion.Test(response != null);

            this.Core             = core;
            this.B2BUserAgent     = b2bUserAgent;
            this.Session          = session;
            this.ReceivedResponse = receivedResponse;
            this.Response         = response;
        }
    }
}
