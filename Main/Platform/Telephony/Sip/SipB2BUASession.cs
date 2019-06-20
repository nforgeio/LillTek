//-----------------------------------------------------------------------------
// FILE:        SipB2BUASession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the dialogs and application state for a SIP Back-to-Back 
//              User Agent session.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Holds the dialogs and application state for a SIP Back-to-Back 
    /// User Agent session.
    /// </summary>
    /// <typeparam name="TState">The application session state type.</typeparam>
    /// <remarks>
    /// <para>
    /// <see cref="SipB2BUASession{TState}" /> generic class instances are used by the <see cref="SipB2BUserAgent{TState}" /> to
    /// maintain the state of a back-to-back session.  A back-to-back session is the combination of a
    /// client <see cref="SipDialog" /> established with the agent and a server  <see cref="SipDialog" /> established 
    /// by the agent on behalf of the client.  The client dialog is referred to as the <i>upstream dialog</i>
    /// and the server dialog as the <i>downstream dialog</i>.
    /// </para>
    /// <para>
    /// Instances of this class are created internally by the SIP stack.  The upstream dialog with the
    /// client will be initialized when the session is established by the client and can be retreived
    /// via the <see cref="ClientDialog" /> property.  The downstream dialog with the server will
    /// be established later, and the <see cref="ServerDialog" /> property returns a reference
    /// to it (or <c>null</c>).
    /// </para>
    /// <para>
    /// Applications can use the <see cref="State" /> property to access any application specific
    /// state defined by the <typeparamref name="TState"/> type.
    /// </para>
    /// <para>
    /// Each B2BUA session is assigned process local unique 64-bit ID.  This ID is used to
    /// reference the session in the <see cref="SipB2BUserAgent{TState}.Sessions" /> collection 
    /// and is available as the <see cref="ID" /> property.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class SipB2BUASession<TState>
    {
        private TState                  state;                  // Application stae
        private SipB2BUADialog<TState>  clientDialog;           // Client side dialog
        private SipB2BUADialog<TState>  serverDialog;           // Server side dialog (or null)
        private long                    id;                     // Process unique session ID
        private SipContactValue         serverLocalContact;     // Contact header for messages to the server
        private SipContactValue         clientLocalContact;     // Contact header for messages to the client

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="id">The process local unique session ID.</param>
        /// <param name="clientDialog">The upstream client <see cref="SipDialog" />.</param>
        /// <param name="defLocalContact">The default local contact for the session.</param>
        public SipB2BUASession(long id, SipB2BUADialog<TState> clientDialog, SipContactValue defLocalContact)
        {

            this.id                 = id;
            this.clientDialog       = clientDialog;
            this.serverLocalContact = defLocalContact;
            this.clientLocalContact = defLocalContact;
        }

        /// <summary>
        /// The application defined session state.
        /// </summary>
        public TState State
        {
            get { return state; }
            set { state = value; }
        }

        /// <summary>
        /// The session's globally unique ID.
        /// </summary>
        public long ID
        {
            get { return id; }
        }

        /// <summary>
        /// Returns the session's upstream dialog to the client.
        /// </summary>
        public SipB2BUADialog<TState> ClientDialog
        {
            get { return clientDialog; }
            internal set { clientDialog = value; }
        }

        /// <summary>
        /// Returns the session's downstream dialog to the session's server or <c>null</c> if no dialog exists.
        /// </summary>
        public SipB2BUADialog<TState> ServerDialog
        {
            get { return serverDialog; }
            internal set { serverDialog = value; }
        }

        /// <summary>
        /// Contact header for messages to the server.
        /// </summary>
        internal SipContactValue ServerLocalContact
        {
            get { return serverLocalContact; }
            set { serverLocalContact = value; }
        }

        /// <summary>
        /// Contact header for messages to the client.
        /// </summary>
        internal SipContactValue ClientLocalContact
        {
            get { return clientLocalContact; }
            set { clientLocalContact = value; }
        }
    }
}
