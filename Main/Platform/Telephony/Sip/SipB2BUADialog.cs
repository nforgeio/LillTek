//-----------------------------------------------------------------------------
// FILE:        SipB2BUADialog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the internal SipDialog used by SipB2BUserAgent to
//              relate the client and server side dialogs to the SipB2BUASession.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines the internal <see cref="SipDialog" /> used by <see cref="SipB2BUserAgent{TState}" /> to
    /// relate the client and server side dialogs to the <see cref="SipB2BUASession{TState}" />.
    /// </summary>
    /// <typeparam name="TState">The application session state type.</typeparam>
    public sealed class SipB2BUADialog<TState> : SipDialog
    {
        private SipB2BUASession<TState> session;

        /// <summary>
        /// Returns the <see cref="SipB2BUASession{TState}" /> associated with the dialog.
        /// </summary>
        public SipB2BUASession<TState> Session
        {
            get { return session; }
            internal set { session = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is the downstream dialog to the server.
        /// </summary>
        public bool ToServer
        {
            get { return base.IsInitiating; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is the upstream dialog to the client.
        /// </summary>
        public bool ToClient
        {
            get { return base.IsAccepting; }
        }

        /// <summary>
        /// Initializes a <see cref="SipDialog" /> instance.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that owns this dialog.</param>
        /// <param name="inviteRequest">The INVITE <see cref="SipRequest" /> received by the accepting dialog or being sent by the initiating dialog.</param>
        /// <param name="localContact">The <see cref="SipContactValue" /> for the local side of the dialog.</param>
        /// <param name="acceptingTransaction">
        /// The initial transaction for INVITE requests received by accepting dialogs or <c>null</c> for 
        /// initiating dialogs.
        /// </param>
        /// <param name="state">Derived class specific state (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Derived classes can override this method to intialize any internal state using information
        /// from the derived class specific <paramref name="state" /> parameter.
        /// </para>
        /// <para>
        /// For accepting dialog requests, this method expects the <paramref name="inviteRequest" /> to already
        /// have reasonable <b>Call-ID</b>, <b>To</b>, <b>From</b>, and <b>CSeq</b> headers and the
        /// <b>From</b> header must have a <b>tag</b> parameter.
        /// </para>
        /// <para>
        /// For initiating dialog requests,this method expects the <paramref name="inviteRequest" /> to already
        /// have reasonable <b>To</b>, <b>From</b> and headers but the method will generate and assign
        /// the <b>From</b> header's <b>tag</b> parameter.  The method will set a <b>Call-ID</b> header
        /// if necessary and will always set a new <b>CSeq</b> header.
        /// </para>
        /// <para>
        /// <paramref name="acceptingTransaction"/> must be passed as the server transaction the INVITE
        /// was received on for accepting dialogs.  This will be passed as <c>null</c> for initiating
        /// dialogs (note that the <see cref="SipClientAgent" /> will eventually set the <see cref="SipDialog.InitiatingTransaction" />
        /// property to the initial <see cref="SipClientTransaction" /> just before the transaction
        /// is started).
        /// </para>
        /// <note>
        /// The derived class MUST call the base <see cref="Initialize" /> method passing the parameters
        /// before returning.
        /// </note>
        /// </remarks>
        public override void Initialize(SipCore core, SipRequest inviteRequest, SipContactValue localContact, SipServerTransaction acceptingTransaction, object state)
        {
            base.Initialize(core, inviteRequest, localContact, acceptingTransaction, state);

            // $hack(jeff.lill): 
            //
            // This is a bit of a hack that initializes the downstream dialog to the
            // server's Session property and also initializes the session's ServerDialog
            // property.  I'm relying on the fact that state is passed as non-null
            // only when the B2BUA creates the downstream dialog.

            SipB2BUASession<TState> session = state as SipB2BUASession<TState>;

            if (session != null)
            {
                this.session = session;
                session.ServerDialog = this;
            }
        }
    }
}
