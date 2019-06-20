//-----------------------------------------------------------------------------
// FILE:        SipDialog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides a persistent context for bidirectional transactions
//              between two SIP peers.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// Implement some kind of TouchTime property that can be used
// by the core to purge orphaned dialogs

// $todo(jeff.lill): Need to implement route sets and routing behaviors.

// $todo(jeff.lill): Need to complete the class documentation

// $todo(jeff.lill): 
//
// I'm currently implementing the request(offer)/response(accept) invite pattern but
// I need to come back and implement the request/response(offer)/ack(accept)
// pattern described in the RFC.

// $todo(jeff.lill): Need to implement re-INVITE

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Provides a persistent context for bidirectional transactions between 
    /// two SIP peers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SipDialog" />s are designed to be easily derived from so that application
    /// specific dialog state can be maintained locally within the derived class.  The generic
    /// <see cref="CreateAcceptingDialog" /> and <see cref="CreateInitiatingDialog" /> static 
    /// methods are used for this purpose.  These methods accept the type of the derived class as 
    /// a parameter and then creates, initializes, and returns the new dialog instance.
    /// </para>
    /// <note>
    /// It is possible to pass <see cref="SipDialog" /> itself as the type parameter.
    /// </note>
    /// <para>
    /// These methods work by first creating the derived dialog type using the 
    /// default constructor and the calling <see cref="Initialize" /> with the
    /// parameters.  The derived class will typically override the <see cref="Initialize" /> 
    /// method so that it can perform its own initialization.  The <i>state</i> parameter 
    /// is provided so that application specific state can be passed to the derived class.
    /// </para>
    /// <note>
    /// The derived class must call the base class <see cref="Initialize" /> method
    /// before returning from the overridden version.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SipDialog : ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates an accepting <see cref="SipDialog" /> from a received INVITE <see cref="SipRequest" />.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that owns this dialog.</param>
        /// <param name="transaction">The <see cref="SipServerTransaction"/> the INVITE was received on.</param>
        /// <param name="inviteRequest">The INVITE request received from the initiating dialog.</param>
        /// <param name="localContact">The <see cref="SipContactValue" /> for the local side of the dialog.</param>
        /// <param name="state">State to be passed to the derived class <see cref="Initialize" /> method (or <c>null</c>).</param>
        /// <typeparam name="TDerivedDialog">
        /// Specifies the type of dialog to create.  This type must be derived from <see cref="SipDialog" />
        /// or can actually be <see cref="SipDialog" />.  Note that this type must implement a public
        /// default constructor.
        /// </typeparam>
        /// <returns>The <see cref="SipDialog" /> created.</returns>
        /// <remarks>
        /// <note>
        /// This method generates the dialog tag for the accepting dialog
        /// adds it to the request's <b>To</b> header.
        /// </note>
        /// </remarks>
        public static TDerivedDialog CreateAcceptingDialog<TDerivedDialog>(SipCore core, SipServerTransaction transaction, SipRequest inviteRequest, SipContactValue localContact, object state)
            where TDerivedDialog : SipDialog, new()
        {
            TDerivedDialog dialog;

            dialog = new TDerivedDialog();
            dialog.Initialize(core, inviteRequest, localContact, transaction, state);
            return dialog;
        }

        /// <summary>
        /// Creates a initiating <see cref="SipDialog" /> from the INVITE <see cref="SipRequest" /> to
        /// be sent to the accepting dialog.
        /// </summary>
        /// <param name="core">The <see cref="SipCore" /> that owns this dialog.</param>
        /// <param name="inviteRequest">The INVITE request to be issued by this dialog.</param>
        /// <param name="localContact">The <see cref="SipContactValue" /> for the local side of the dialog.</param>
        /// <param name="state">State to be passed to the derived class <see cref="Initialize" /> method (or <c>null</c>).</param>
        /// <typeparam name="TDerivedDialog">
        /// Specifies the type of dialog to create.  This type must be derived from <see cref="SipDialog" />
        /// or can actually be <see cref="SipDialog" />.  Note that this type must implement a public
        /// default constructor.
        /// </typeparam>
        /// <returns>The <see cref="SipDialog" /> created.</returns>
        /// <remarks>
        /// <note>
        /// This method generates a semi-random sequence number and sets the <b>CSeq</b> header
        /// in the request with this value.
        /// </note>
        /// <note>
        /// This method generates the dialog tag for the initiating side of the dialog and
        /// adds it to the request's <b>From</b> header.
        /// </note>
        /// <note>
        /// This method <b>does not generate the dialog ID</b>.  The <see cref="SipClientAgent" />
        /// will compute and set the dialog ID once it sees the first accepting dialog response with a 
        /// tag on the <b>To</b> header.
        /// </note>
        /// </remarks>
        public static TDerivedDialog CreateInitiatingDialog<TDerivedDialog>(SipCore core, SipRequest inviteRequest, SipContactValue localContact, object state)
            where TDerivedDialog : SipDialog, new()
        {
            TDerivedDialog dialog;

            dialog = new TDerivedDialog();
            dialog.Initialize(core, inviteRequest, localContact, null, state);
            return dialog;
        }

        /// <summary>
        /// Synthesizes a dialog ID from a <see cref="SipRequest" />'s <b>To</b>/<b>From</b> header <b>tag</b>
        /// parameters and the message's <b>Call-ID</b> header value.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <returns>The globally unique dialog ID string or <c>null</c> if the message does not belong to a dialog.</returns>
        public static string GetDialogID(SipRequest request)
        {
            SipValue            vCallID = request.GetHeader<SipValue>(SipHeader.CallID);
            SipContactValue     vTo     = request.GetHeader<SipContactValue>(SipHeader.To);
            SipContactValue     vFrom   = request.GetHeader<SipContactValue>(SipHeader.From);
            string              toTag;
            string              fromTag;

            if (vCallID == null || vTo == null || vFrom == null)
                return null;

            toTag = vTo["tag"];
            if (toTag == null)
                return null;

            fromTag = vFrom["tag"];

            // Note that the order of the to/from tags is reversed from
            // that generated by GetDialogID(SipRequest).

            return vCallID.Text + ":" + toTag + ":" + Helper.Normalize(fromTag);
        }

        /// <summary>
        /// Synthesizes a dialog ID from a <see cref="SipResponse" />'s <b>To</b>/<b>From</b> header <b>tag</b>
        /// parameters and the message's <b>Call-ID</b> header value.
        /// </summary>
        /// <param name="response">The response message.</param>
        /// <returns>The globally unique dialog ID string or <c>null</c> if the message does not belong to a dialog.</returns>
        public static string GetDialogID(SipResponse response)
        {
            SipValue            vCallID = response.GetHeader<SipValue>(SipHeader.CallID);
            SipContactValue     vTo     = response.GetHeader<SipContactValue>(SipHeader.To);
            SipContactValue     vFrom   = response.GetHeader<SipContactValue>(SipHeader.From);
            string              toTag;
            string              fromTag;

            if (vCallID == null || vTo == null || vFrom == null)
                return null;

            toTag = vTo["tag"];
            fromTag = vFrom["tag"];

            if (fromTag == null)
                return null;

            // Note that the order of the to/from tags is reversed from
            // that generated by GetDialogID(SipRequest).

            return vCallID.Text + ":" + fromTag + ":" + Helper.Normalize(toTag);
        }

        /// <summary>
        /// Synthesizes an accepting side early dialog ID for an unconfirmed dialog from a
        /// <see cref="SipRequest" /> received from the initiating side.  The ID returned is a
        /// combination of the <b>Call-ID</b> and the initiating dialog's <b>From</b> header's <b>tag</b> parameter.
        /// </summary>
        /// <param name="request">The request received.</param>
        /// <returns>The dialog's early ID or <c>null</c> if the message does not belong to a dialog.</returns>
        /// <remarks>
        /// <note>
        /// The ID returned will be prefixed by <b>"a:"</b> indicating that the ID refers
        /// to an <b>accepting</b> side early dialog.  Initiating side early dialog IDs will
        /// be prefixed by <b>i:</b>.  These prefixes are necesssary so that we won't have
        /// conflicts when when an attempt is made by a core to establish a dialog with itself.
        /// </note>
        /// </remarks>
        public static string GetEarlyDialogID(SipRequest request)
        {
            SipValue            vCallID = request.GetHeader<SipValue>(SipHeader.CallID);
            SipContactValue     vFrom   = request.GetHeader<SipContactValue>(SipHeader.From);
            string              fromTag;

            if (vCallID == null || vFrom == null)
                return null;

            fromTag = vFrom["tag"];
            if (fromTag == null)
                return null;

            return "a:" + vCallID.Text + ":" + fromTag;
        }

        /// <summary>
        /// Synthesizes an initiating side early dialog ID for an unconfirmed dialog from a
        /// <see cref="SipResponse" /> received from the accepting side.  The ID returned is a
        /// combination of the <b>Call-ID</b> and the initiating dialog's <b>From</b> header's <b>tag</b> parameter.
        /// </summary>
        /// <param name="response">The request message received.</param>
        /// <returns>The dialog's early ID or <c>null</c> if the message does not belong to a dialog.</returns>
        /// <remarks>
        /// <note>
        /// The ID returned will be prefixed by <b>"i:"</b> indicating that the ID refers
        /// to an <b>initiating</b> side early dialog.  Accepting side early dialog IDs will
        /// be prefixed by <b>a:</b>.  These prefixes are necesssary so that we won't have
        /// conflicts when when an attempt is made by a core to establish a dialog with itself.
        /// </note>
        /// </remarks>
        public static string GetEarlyDialogID(SipResponse response)
        {
            SipValue            vCallID = response.GetHeader<SipValue>(SipHeader.CallID);
            SipContactValue     vFrom   = response.GetHeader<SipContactValue>(SipHeader.From);
            string              fromTag;

            if (vCallID == null || vFrom == null)
                return null;

            fromTag = vFrom["tag"];
            if (fromTag == null)
                return null;

            return "i:" + vCallID.Text + ":" + fromTag;
        }

        //---------------------------------------------------------------------
        // Instance members

        private SipCore                 core;                   // The core managing this dialog (null if not initialized)
        private DateTime                earlyTTD;               // Scheduled time-to-die (SYS) for the dialog if it's not confirmed
        private SipRequest              inviteRequest;          // The original INVITE request
        private SipResponse             inviteResponse;         // The final INVITE response (or null)
        private SipStatus               inviteStatus;           // The final INVITE response status
        private SipRequest              inviteAck;              // The ACK request (or null)
        private string                  id;                     // The dialog's globally unique ID
        private string                  earlyID;                // A cached early ID
        private SipDialogState          state;                  // The dialog state
        private bool                    isSecure;               // True if the dialog is over TLS
        private SipContactValue         localContact;           // The Contact header info for this end of the dialog
        private SipContactValue         remoteContact;          // The Contact header info for this end of the dialog (or null)
        private SipHeader               recordRoute;            // The route set from the INVITE or response establishing the dialog (or null)
        private SipContactValue         requestTo;              // The "To" header to use when addressing request to the remote peer
        private SipContactValue         requestFrom;            // The "From" header to use when addressing request to the remote peer
        private int                     localCSeq;              // Local sequence number (or -1 for "empty")
        private int                     remoteCSeq;             // Remote sequence number (or -1 for "empty")
        private int                     ackCSeq;                // Sequence number to be used in the ACK transmission
        private string                  callID;                 // The dialog's call ID
        private string                  localTag;               // Local tag component of the dialog ID
        private string                  remoteTag;              // Remote tage component of the dialog ID
        private string                  localUri;               // The local SIP URI
        private string                  remoteUri;              // The remote SIP URI
        private SdpPayload              localSdp;               // SDP for the local media
        private SdpPayload              remoteSdp;              // SDP for the remote media
        private SipAuthorizationValue   authHeader;             // "Authorization" header value (or null)
        private SipAuthorizationValue   proxyAuthHeader;        // "Proxy-Authentication" header value (or null)
        private SipClientTransaction    initiatingTransaction;  // The initial client transaction for initiating dialogs
        private SipServerTransaction    acceptingTransaction;   // The initial server transaction for accepting dialogs
        private bool                    coreDialogClosedRaised; // True if the core has already fired its DialogClosed event
                                                                // for this dialog

        /// <summary>
        /// Raised when a valid <see cref="SipRequest" /> is received by the dialog.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications will typically enlist in this event to handle requests
        /// from the remote peer.  The event handler can obtain the <see cref="SipDialog" /> 
        /// associated with the request by examining the <see cref="SipRequestEventArgs.Dialog" />
        /// property of the <see cref="SipRequestEventArgs" /> argument.
        /// </para>
        /// <para>
        /// The event handler can choose to process the request immediately and
        /// send one or more responses immediately to the request by referencing the
        /// transaction from the <see cref="SipRequestEventArgs" /> and calling its
        /// <see cref="SipServerTransaction.SendResponse" /> method, before the handler
        /// returns.
        /// </para>
        /// <para>
        /// Alternatively, the event handler may choose to save a reference to the transaction,
        /// begin an asynchronous operation to handle the request, setting the event argument's
        /// <see cref="SipRequestEventArgs.WillRespondAsynchronously" /> property to <c>true</c> returning, 
        /// and then ultimately passing the response to the transaction's <see cref="SipServerTransaction.SendResponse" />
        /// method.
        /// </para>
        /// <para>
        /// Finally, the event handler may construct an appropriate <see cref="SipResponse" />
        /// and assign this to the <b>args</b> parameter's <see cref="SipRequestEventArgs.Response" />
        /// property to have the <see cref="SipCore" /> handle the transmission of the response.
        /// </para>
        /// <note>
        /// The dialog will automatically send a <b>501 (Not Implemented)</b> response if 
        /// no event handler is set or the handler returns without setting the argument's 
        /// <see cref="SipRequestEventArgs.Response" /> or <see cref="SipRequestEventArgs.WillRespondAsynchronously" /> 
        /// properties.
        /// </note>
        /// <note>
        /// The <see cref="SipDialog" /> automatically handles BYE requests by closing
        /// the dialog.  CANCEL requests are handled internally by the <see cref="SipCore" />
        /// This event will not be raised for BYE or CANCEL requests.
        /// </note>
        /// <note>
        /// The <see cref="SipDialog" /> will not fire this event for invalid request
        /// messages.  For example, the class will automatically send a <b>500 (Server Error)</b>
        /// response back to the client dialog if the sequence number for the request is less than
        /// the remote sequence number maintained by the dialog.
        /// </note>
        /// </remarks>
        public event SipRequestDelegate RequestReceived;

        /// <summary>
        /// Raised when a <see cref="SipResponse" /> is received by the dialog.
        /// </summary>
        public event SipResponseDelegate ResponseReceived;

        /// <summary>
        /// Raised when the dialog has been confirmed.
        /// </summary>
        public event MethodDelegate Confirmed;

        /// <summary>
        /// Raised when the dialog has been closed.
        /// </summary>
        public event MethodDelegate Closed;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipDialog()
        {
            this.core                   = null;
            this.initiatingTransaction  = null;
            this.acceptingTransaction   = null;
            this.ackCSeq                = -1;
            this.coreDialogClosedRaised = false;
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
        /// dialogs (note that the <see cref="SipClientAgent" /> will eventually set the <see cref="InitiatingTransaction" />
        /// property to the initial <see cref="SipClientTransaction" /> just before the transaction
        /// is started).
        /// </para>
        /// <note>
        /// The derived class MUST call the base <see cref="Initialize" /> method passing the parameters
        /// before returning.
        /// </note>
        /// </remarks>
        public virtual void Initialize(SipCore core, SipRequest inviteRequest, SipContactValue localContact, SipServerTransaction acceptingTransaction, object state)
        {
            SipValue            vCallID;
            SipContactValue     vTo;
            SipContactValue     vFrom;
            SipContactValue     vContact;
            SipCSeqValue        vCSeq;
            SipException        e;

            if (this.core != null)
                return;     // Ignore multiple initialization attempts

            this.core            = core;
            this.inviteRequest   = inviteRequest;
            this.inviteResponse  = null;
            this.inviteStatus    = SipStatus.Stack_Unknown;
            this.inviteAck       = null;
            this.authHeader      = null;
            this.proxyAuthHeader = null;

            vTo = inviteRequest.GetHeader<SipContactValue>(SipHeader.To);
            if (vTo == null)
            {
                e            = new SipException("Invalid INVITE: Missing [To] header.");
                e.BadMessage = inviteRequest;
                throw e;
            }

            vFrom = inviteRequest.GetHeader<SipContactValue>(SipHeader.From);
            if (vFrom == null)
            {
                e            = new SipException("Invalid INVITE: Missing [From] header.");
                e.BadMessage = inviteRequest;
                throw e;
            }

            vContact = inviteRequest.GetHeader<SipContactValue>(SipHeader.Contact);
            if (vContact == null)
            {
                e            = new SipException("Invalid INVITE: Missing [Contact] header.");
                e.BadMessage = inviteRequest;
                throw e;
            }

            this.isSecure     = new SipUri(inviteRequest.Uri).IsSecure;
            this.localContact = localContact;
            this.localCSeq    = -1;
            this.earlyID      = null;

            if (acceptingTransaction != null)
            {
                // Accepting transaction

                vCallID = inviteRequest.GetHeader<SipValue>(SipHeader.CallID);
                if (vCallID == null)
                {
                    e = new SipException("Invalid INVITE: Missing [Call-ID] header.");
                    e.BadMessage = inviteRequest;
                    throw e;
                }

                vCSeq = inviteRequest.GetHeader<SipCSeqValue>(SipHeader.CSeq);
                if (vCSeq == null)
                {
                    e = new SipException("Invalid INVITE: Missing [CSeq] header.");
                    e.BadMessage = inviteRequest;
                    throw e;
                }

                this.callID               = vCallID.Text;
                this.acceptingTransaction = acceptingTransaction;
                this.id                   = GetDialogID(inviteRequest);
                this.state                = SipDialogState.Early;
                this.remoteCSeq           = vCSeq.Number;
                this.remoteContact        = inviteRequest.GetHeader<SipContactValue>(SipHeader.Contact);
                this.recordRoute          = inviteRequest[SipHeader.RecordRoute];

                this.localTag = vTo["tag"];
                if (this.localTag == null)
                {
                    this.localTag = SipHelper.GenerateTagID();
                    vTo["tag"]     = localTag;
                    inviteRequest.SetHeader(SipHeader.To, vTo);
                }

                this.remoteTag   = Helper.Normalize(vFrom["tag"]);
                this.remoteUri   = vContact.Uri;
                this.localUri    = localContact;
                this.requestTo   = vFrom;
                this.requestFrom = vTo;
            }
            else
            {
                // Initiating transaction

                vCallID = inviteRequest.GetHeader<SipValue>(SipHeader.CallID);
                if (vCallID != null)
                    this.callID = vCallID.Text;
                else
                {
                    this.callID = SipHelper.GenerateCallID();
                    inviteRequest.SetHeader(SipHeader.CallID, this.callID);
                }

                this.initiatingTransaction = null;
                this.id                    = null;
                this.state                 = SipDialogState.Waiting;

                inviteRequest.SetHeader(SipHeader.CSeq, new SipCSeqValue(this.GetNextCSeq(), inviteRequest.MethodText));

                this.remoteContact = null;      // Won't know these values until
                this.recordRoute   = null;      // the accepting dialog responds
                this.remoteCSeq    = -1;
                this.remoteTag     = null;

                this.localTag      = SipHelper.GenerateTagID();
                vFrom["tag"]       = localTag;
                inviteRequest.SetHeader(SipHeader.From, vFrom);

                this.remoteUri   = vTo.Uri;
                this.localUri    = localContact;
                this.requestTo   = vTo;
                this.requestFrom = vFrom;
            }
        }

        /// <summary>
        /// Returns the <see cref="SipCore" /> that manages this dialog.
        /// </summary>
        public SipCore Core
        {
            get { return core; }
        }

        /// <summary>
        /// The scheduled time-to-die (SYS) for the dialog if it is not confirmed.
        /// </summary>
        internal DateTime EarlyTTD
        {
            get { return earlyTTD; }
            set { earlyTTD = value; }
        }

        /// <summary>
        /// Returns the <see cref="SipClientTransaction" /> used to initiate the dialog.
        /// </summary>
        public SipClientTransaction InitiatingTransaction
        {
            get { return initiatingTransaction; }
            internal set { initiatingTransaction = value; }
        }

        /// <summary>
        /// Returns the <see cref="SipServerTransaction" /> used to accept the
        /// initial dialog INVITE request.
        /// </summary>
        public SipServerTransaction AcceptingTransaction
        {
            get { return acceptingTransaction; }
        }

        /// <summary>
        /// Returns the original INVITE request either sent or
        /// received when the dialog was first created.
        /// </summary>
        public SipRequest InviteRequest
        {
            get { return inviteRequest; }
        }

        /// <summary>
        /// Returns the final INVITE response either sent or
        /// received when the dialog was first created (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property will be set on both the initiating and
        /// accepting sides of the INVITE transaction after the response has
        /// been sent on the accepting dialog or received by the initiating 
        /// dialog.
        /// </para>
        /// <para>
        /// Applications will typically use this to examine the
        /// SDP payload necessary for establising the media session
        /// with the remote peer.
        /// </para>
        /// </remarks>
        public SipResponse InviteResponse
        {
            get { return inviteResponse; }
        }

        /// <summary>
        /// Returns the final <see cref="SipStatus" /> for the INVITE
        /// transaction from the initiating dialog's perspective.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is set from the final response received from the
        /// accepting dialog or from any transport or transaction related error
        /// (such as a timeout).  Applications will use this to determine 
        /// the final disposition of the INVITE transaction.
        /// </para>
        /// </remarks>
        public SipStatus InviteStatus
        {
            get { return inviteStatus; }
        }

        /// <summary>
        /// Used by the <see cref="SipCore" /> to assign the final disposition of the 
        /// original INVITE request.  This will be called on both the initiating and
        /// accepting sides of the transaction.
        /// </summary>
        /// <param name="inviteStatus">The final <see cref="SipStatus" />.</param>
        /// <param name="inviteResponse">
        /// The final <see cref="SipResponse" /> or <c>null</c> if the transaction
        /// completed without a response (e.g. due to a transport error or timeout).
        /// </param>
        public virtual void SetDisposition(SipStatus inviteStatus, SipResponse inviteResponse)
        {
            this.inviteStatus   = inviteStatus;
            this.inviteResponse = inviteResponse;
        }

        /// <summary>
        /// Returns <c>true</c> if the dialog was created to initiate a dialog invitation.
        /// </summary>
        public bool IsInitiating
        {
            get
            {
                if (initiatingTransaction != null)
                    return true;
                else if (acceptingTransaction != null)
                    return false;
                else
                    throw new InvalidOperationException("Dialog has not been fully initialized.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the dialog was created to accept a dialog invitation.
        /// </summary>
        public bool IsAccepting
        {
            get { return !IsInitiating; }
        }

        /// <summary>
        /// The ACK request used to confirm the dialog (or <c>null</c>).
        /// </summary>
        public SipRequest InviteAck
        {
            get { return inviteAck; }
        }

        /// <summary>
        /// Used internally by the <see cref="SipCore" /> to determine whether its
        /// <see cref="SipCore.DialogClosed" /> event has already been raised for
        /// this dialog.
        /// </summary>
        internal bool CoreDialogClosedRaised
        {
            get { return coreDialogClosedRaised; }
            set { coreDialogClosedRaised = value; }
        }

        /// <summary>
        /// Returns the dialog's globally unique ID or <c>null</c> if the 
        /// dialog ID has not yet been determined.
        /// </summary>
        /// <remarks>
        /// A dialog ID is composed of three components: the initiating request's
        /// <b>Call-ID</b>, its <b>From</b> tag parameter and the <b>To</b> tag
        /// parameter created by the accepting dialog.  A dialog's ID cannot be constructed
        /// until the accepting dialog responds to the initiating request with the
        /// accepting side's tag.
        /// </remarks>
        public string ID
        {
            get { return id; }

            internal set
            {
                id = value;
                earlyID = null;
            }
        }

        /// <summary>
        /// Returns the dialog's early ID.
        /// </summary>
        /// <remarks>
        /// This is useful for managing dialogs that have been created by the accepting
        /// dialog but have not yet been confirmed.  The ID returned is a combination of
        /// the <b>Call-ID</b> and the <b>From</b> header's <b>tag</b> parameter.
        /// </remarks>
        public string EarlyID
        {
            get
            {
                if (earlyID != null)
                    return earlyID;

                if (IsInitiating)
                    earlyID = "i:" + callID + ":" + localTag;
                else
                    earlyID = "a:" + callID + ":" + remoteTag;

                return earlyID;
            }
        }

        /// <summary>
        /// The current dialog state.
        /// </summary>
        public SipDialogState State
        {
            get { return state; }
            set { state = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the dialog is secured via TLS.
        /// </summary>
        public bool IsSecure
        {
            get { return IsSecure; }
        }

        /// <summary>
        /// Returns the local <b>Contact</b> header information.
        /// </summary>
        public SipContactValue LocalContact
        {
            get { return localContact; }
        }

        /// <summary>
        /// Returns the remote <b>Contact</b> header information.
        /// </summary>
        public SipContactValue RemoteContact
        {
            get { return remoteContact; }
        }

        /// <summary>
        /// Returns the route set to be used for routing messages to the remote side (or <c>null</c>).
        /// </summary>
        public SipHeader Route
        {
            get { return recordRoute; }
        }

        /// <summary>
        /// Returns the next available local message sequence number for this dialog.
        /// </summary>
        /// <returns>The sequence number.</returns>
        public int GetNextCSeq()
        {
            using (TimedLock.Lock(this))
            {
                if (localCSeq == -1)
                {
                    // "empty"

                    localCSeq = SipHelper.GenCSeq();
                    return localCSeq;
                }

                return ++localCSeq;
            }
        }

        /// <summary>
        /// Returns the local message sequence number.
        /// </summary>
        public int LocalCSeq
        {
            get
            {
                if (localCSeq != -1)
                    return LocalCSeq;

                // "empty"

                localCSeq = SipHelper.GenCSeq();
                return localCSeq;
            }
        }

        /// <summary>
        /// Returns the remote message sequence number.
        /// </summary>
        public int RemoteCSeq
        {
            get { return remoteCSeq; }
        }

        /// <summary>
        /// The sequence number used when submitting the dialog creating INVITE request 
        /// from a initiating dialog.  This must be set when the INVITE is send so
        /// that we'll know what sequence number to use in the ACK request.
        /// </summary>
        internal int AckCSeq
        {
            get
            {
                if (ackCSeq == -1)
                    throw new InvalidOperationException("AckCSeq property not initialized.");

                return ackCSeq;
            }

            set { ackCSeq = value; }
        }

        /// <summary>
        /// Returns the dialog's <b>Call-ID</b>.
        /// </summary>
        public string CallID
        {
            get { return callID; }
        }

        /// <summary>
        /// Returns the local tag component of the dialog's globally unique ID.
        /// </summary>
        public string LocalTag
        {
            get { return localTag; }
        }

        /// <summary>
        /// Returns the remote tag component of the dialog's globally unique ID.
        /// </summary>
        public string RemoteTag
        {
            get { return remoteTag; }
        }

        /// <summary>
        /// Returns the local URI.
        /// </summary>
        public string LocalUri
        {
            get { return localUri; }
        }

        /// <summary>
        /// Returns the remote URI.
        /// </summary>
        public string RemoteUri
        {
            get { return remoteUri; }
        }

        /// <summary>
        /// The <see cref="SdpPayload" /> describing the local media (or <c>null</c>).
        /// </summary>
        public SdpPayload LocalSdp
        {
            get { return LocalSdp; }
            set { localSdp = value; }
        }

        /// <summary>
        /// The <see cref="SdpPayload" /> describing the remote media (or <c>null</c>).
        /// </summary>
        public SdpPayload RemoteSdp
        {
            get { return remoteSdp; }
            set { remoteSdp = value; }
        }

        /// <summary>
        /// Called when an accepting dialog receives the confirming ACK from the 
        /// initiating dialog.
        /// </summary>
        public virtual void OnConfirmed()
        {
            state = SipDialogState.Confirmed;
        }

        /// <summary>
        /// Called by <see cref="SipCore" /> when an INVITE transaction
        /// is confirmed by the accepting dialog.
        /// </summary>
        /// <param name="args">The <see cref="SipResponseEventArgs" /> event arguments.</param>
        public virtual void OnConfirmed(SipResponseEventArgs args)
        {
            var response = args.Response;

            using (TimedLock.Lock(this))
            {
                state = SipDialogState.Confirmed;

                // Extract and save the contact URI and the remote tag from responses received
                // by the initiating dialog as necessary.

                if (IsInitiating)
                {
                    if (remoteTag == null)
                    {
                        var vTo = response.GetHeader<SipContactValue>(SipHeader.To);

                        if (vTo != null)
                        {
                            remoteTag = vTo["tag"];
                            requestTo["tag"] = remoteTag;
                        }
                    }

                    var vContact = response.GetHeader<SipContactValue>(SipHeader.Contact);

                    if (vContact != null)
                    {
                        remoteUri = vContact.Uri;
                        remoteContact = vContact;
                    }
                }
            }
        }

        /// <summary>
        /// Called when a <see cref="SipRequest" /> is received for this dialog.
        /// Raises the <see cref="RequestReceived" /> event.
        /// </summary>
        /// <param name="sender">The sender (typically a <see cref="SipCore" />.</param>
        /// <param name="args">A <see cref="SipResponseEventArgs" /> with the event information.</param>
        public virtual void OnRequestReceived(object sender, SipRequestEventArgs args)
        {
            var request = args.Request;

            if (!IsValid(request))
            {
                args.Response = request.CreateResponse(SipStatus.ServerError, null);
                return;
            }

            // Handle pending close situations

            bool    raiseClosed = false;
            bool    remove      = false;

            try
            {
                using (TimedLock.Lock(this))
                {
                    if (state == SipDialogState.ClosePendingAck)
                    {
                        if (request.Method == SipMethod.Ack)
                        {

                            state       = SipDialogState.Closed;
                            raiseClosed = true;
                            remove      = true;

                            return;
                        }
                    }
                }
            }
            finally
            {
                // Do this outside of the lock.

                if (remove)
                    core.RemoveDialog(this);

                if (raiseClosed)
                    OnClose();
            }

            // Handle BYE requests

            if (request.Method == SipMethod.Bye)
            {
                args.Response = request.CreateResponse(SipStatus.OK, null);
                this.state    = SipDialogState.Closed;
                Close();
                return;
            }

            // Handle ACK requests internally

            if (request.Method == SipMethod.Ack)
            {
                if (inviteAck == null)
                {
                    inviteAck = request;

                    if (Confirmed != null)
                        Confirmed();
                }

                return;
            }

            // Raise the RequestReceived event

            if (RequestReceived == null)
            {
                args.Transaction.SendResponse(request.CreateResponse(SipStatus.NotImplemented, null));
                return;
            }

            RequestReceived(sender, args);
            if (args.Response != null)
            {
#if DEBUG
                // To a quick check to verify that the response headers
                // are appropriate for the dialog.

                var response = args.Response;
                var vTo      = response.GetHeader<SipContactValue>(SipHeader.To);
                var vFrom    = response.GetHeader<SipContactValue>(SipHeader.From);
                var callID   = response.GetHeaderText(SipHeader.CallID);

                Assertion.Test(vTo == null || vTo["tag"] != this.localTag ||
                               vFrom != null || vFrom["tag"] != this.remoteTag ||
                               callID != null || callID != this.callID);
#endif
                // Send the response

                args.Transaction.SendResponse(args.Response);
            }
            else if (!args.WillRespondAsynchronously && !args.ResponseSent)
                args.Transaction.SendResponse(request.CreateResponse(SipStatus.NotImplemented, null));
        }

        /// <summary>
        /// Delivers a <see cref="SipResponse" /> for a request received by a <see cref="RequestReceived" />
        /// event handler.
        /// </summary>
        /// <param name="args">The <see cref="SipRequestEventArgs" /> instance passed when the event was raised.</param>
        /// <param name="response">The <see cref="SipResponse" /> to be delivered.</param>
        /// <remarks>
        /// <para>
        /// Applications enlisting in the <see cref="SipDialog" />.<see cref="SipDialog.RequestReceived" />
        /// event can respond to the request in two ways.  If the operation can be completed
        /// immediately, the easiest way is to set the <see cref="SipRequestEventArgs" />.<see cref="SipRequestEventArgs.Response" />
        /// property to the <see cref="SipResponse" />  to have the <see cref="SipDialog" />
        /// class handle the response delivery after the event handler returns.
        /// </para>
        /// <para>
        /// For operations that will take longer, the application will passed the 
        /// response to the <see cref="SipServerTransaction"/>.<see cref="SipServerTransaction.SendResponse" /> method.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if a <see cref="SipResponse" /> has already been sent for this request.</exception>
        public virtual void Reply(SipRequestEventArgs args, SipResponse response)
        {
            if (args.ResponseSent)
                throw new InvalidOperationException("Response has already already been sent for this request.");

            args.ResponseSent = true;
            ((SipServerTransaction)args.Transaction).SendResponse(response);
        }

        /// <summary>
        /// Delivers a <see cref="SipResponse" /> for an INVITE request received by an accepting dialog.
        /// </summary>
        /// <param name="response">The <see cref="SipResponse" /> to be delivered.</param>
        /// <exception cref="InvalidOperationException">Thrown if this is not an accepting dialog.</exception>
        /// <remarks>
        /// <note>
        /// This method works only for dialogs on the accepting side of an INVITE transaction.
        /// </note>
        /// </remarks>
        public virtual void SendInviteResponse(SipResponse response)
        {
            if (acceptingTransaction == null)
                throw new InvalidOperationException("Not the accepting dialog.");

            acceptingTransaction.SendResponse(response);
        }

        /// <summary>
        /// Called when a <see cref="SipResponse" /> is received for this dialog.
        /// Raises the <see cref="ResponseReceived" /> event.
        /// </summary>
        /// <param name="sender">The sender (typically a <see cref="SipCore" />).</param>
        /// <param name="args">A <see cref="SipResponseEventArgs" /> with the event information.</param>
        /// <remarks>
        /// <para>
        /// The base implementation handles the pending close states by generating
        /// CANCEL and BYE requests as necessary.
        /// </para>
        /// </remarks>
        public virtual void OnResponseReceived(object sender, SipResponseEventArgs args)
        {
            var         response    = args.Response;
            bool        raiseClosed = false;
            bool        remove      = false;
            SipRequest  request     = null;

            try
            {
                using (TimedLock.Lock(this))
                {
                    // Handle pending close situations

                    switch (state)
                    {
                        case SipDialogState.ClosePendingProvisional:

                            if (response.IsProvisional)
                            {
                                state = SipDialogState.ClosePendingFinal;
                                request = inviteRequest.CreateCancelRequest();
                                return;
                            }

                            if (response.IsNonSuccessFinal)
                            {
                                state = SipDialogState.Closed;
                                raiseClosed = true;
                                remove = true;
                                return;
                            }

                            // Final success response

                            state = SipDialogState.Closed;
                            request = new SipRequest(SipMethod.Bye, this.remoteUri, null);
                            remove = true;
                            raiseClosed = true;
                            break;

                        case SipDialogState.ClosePendingFinal:

                            return;
                    }
                }
            }
            finally
            {
                // Do this outside of the lock.

                if (remove)
                    core.RemoveDialog(this);

                if (request != null)
                    FireAndForget(request);

                if (raiseClosed)
                    OnClose();
            }

            // Raise the response event

            if (ResponseReceived != null)
                ResponseReceived(sender, args);
        }

        /// <summary>
        /// Initializes a <see cref="SipRequest" />'s headers as appropriate for
        /// transactions within this dialog.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        private void SetHeaders(SipRequest request)
        {
            SipContactValue     toValue;
            SipContactValue     fromValue;

            toValue = request.GetHeader<SipContactValue>(SipHeader.To);
            if (toValue == null)
                toValue = requestTo;

            toValue["tag"] = remoteTag;
            request.SetHeader(SipHeader.To, toValue);

            fromValue = request.GetHeader<SipContactValue>(SipHeader.From);
            if (fromValue == null)
                fromValue = requestFrom;

            fromValue["tag"] = localTag;
            request.SetHeader(SipHeader.From, fromValue);

            if (!request.ContainsHeader(SipHeader.Contact))
                request.AddHeader(SipHeader.Contact, localContact);

            request.SetHeader(SipHeader.CallID, callID);
            request.SetCSeq(GetNextCSeq());
        }

        /// <summary>
        /// Constructs a <see cref="SipResponse" /> for the dialog by setting the 
        /// appropriate headers required by the dialog and then adding any non-dialog
        /// related headers from a <see cref="SipResponse" />.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> being responded to.</param>
        /// <param name="response">The <see cref="SipResponse" /> whose non-dialog related information are to be returned</param>
        /// <returns>A proper <see cref="SipResponse" /> that can be transmitted in response to the original request.</returns>
        /// <remarks>
        /// <para>
        /// This method is useful in situations like a back-to-back user agent (B2BUA) where 
        /// a response received by the B2BUA's UAC needs to be transmitted back to the 
        /// originating transaction's UAC with the approriate dialog related headers.
        /// </para>
        /// </remarks>
        public SipResponse CreateResponse(SipRequest request, SipResponse response)
        {
            var dialogResponse = request.CreateResponse(response.Status, response.ReasonPhrase);

            dialogResponse.Contents = request.Contents;

            // Copy all non-dialog related headers from "response" to "dialogResponse"

            foreach (SipHeader header in response.Headers.Values)
            {
                switch (header.Name.ToUpper())
                {
                    case "VIA":
                    case "TO":
                    case "FROM":
                    case "CONTACT":
                    case "CALL-ID":
                    case "CSEQ":

                        // Ignore dialog related headers

                        break;

                    default:

                        // Set non-dialog headers

                        dialogResponse.SetHeader(header);
                        break;
                }
            }

            return dialogResponse;
        }

        /// <summary>
        /// Submits a <see cref="SipRequest" /> transaction for processing without providing
        /// any completion notifications.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" />.</param>
        /// <remarks>
        /// This method is useful for situations like submitting a BYE or CANCEL
        /// transaction, where the outcome isn't really that important to track.
        /// </remarks>
        public void FireAndForget(SipRequest request)
        {
            SetHeaders(request);
            core.FireAndForget(request);
        }

        /// <summary>
        /// Initiates an asynchronous request transaction with the remote peer.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <param name="callback">The delegate to be called when the transaction completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation,s progress.</returns>
        /// <remarks>
        /// <para>
        /// This method initializes the request's <b>To</b>, <b>From</b>, and <b>Contact</b>
        /// headers if they are not already present and also adds the dialog's tags to the To/From
        /// headers.  The method sets the request's <b>Uri</b> property and <b>CSeq</b> and <b>Call-ID</b> 
        /// headers, overwriting any existing values.
        /// </para>
        /// <para>
        /// Most applications will simply allow this method to initialize all of these
        /// headers.
        /// </para>
        /// </remarks>
        public virtual IAsyncResult BeginRequest(SipRequest request, AsyncCallback callback, object state)
        {
            request.Uri = remoteUri;
            SetHeaders(request);
            return core.BeginRequest(request, callback, state);
        }

        /// <summary>
        /// Completes a pending asynchronous <see cref="BeginRequest" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> returned by <see cref="BeginRequest" />.</param>
        /// <returns>The transaction's <see cref="SipResult" />.</returns>
        public virtual SipResult EndRequest(IAsyncResult ar)
        {
            SipResult       sipResult;
            SipResponse     response;

            sipResult = core.EndRequest(ar);
            response  = sipResult.Response;

            return sipResult;
        }

        /// <summary>
        /// Performs a synchronous request transaction against the remote peer.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <returns>The transaction's <see cref="SipResult" />.</returns>
        /// <remarks>
        /// <para>
        /// This method initializes the request's <b>To</b>, <b>From</b>, and <b>Contact</b>
        /// headers if they are not already present and also add the dialog's tags to the To/From
        /// headers.  The method sets the <b>CSeq</b> and <b>Call-ID</b> headers, overwriting
        /// any existing values.
        /// </para>
        /// <para>
        /// Most applications will simply allow this method to initialize all of these
        /// headers.
        /// </para>
        /// </remarks>
        public virtual SipResult Request(SipRequest request)
        {
            var ar = BeginRequest(request, null, null);

            return EndRequest(ar);
        }

        /// <summary>
        /// Sets the <b>Authorization</b> header to be used when generating the
        /// ACK request to completing an INVITE transaction.
        /// </summary>
        /// <param name="authHeader">The <see cref="SipAuthorizationValue" />.</param>
        /// <remarks>
        /// The <see cref="SipCore" /> will need to call this method if 
        /// authorization was required by the SIP endpoint while submitting the
        /// original INVITE.
        /// </remarks>
        internal void SetAckAuthHeader(SipAuthorizationValue authHeader)
        {
            this.authHeader = authHeader;
        }

        /// <summary>
        /// Sets the <b>Proxy-Authorization</b> header to be used when generating the
        /// ACK request to completing an INVITE transaction.
        /// </summary>
        /// <param name="proxyAuthHeader">The <see cref="SipAuthorizationValue" />.</param>
        /// <remarks>
        /// The <see cref="SipCore" /> will need to call this method if 
        /// authorization was required by a proxy while submitting the
        /// original INVITE.
        /// </remarks>
        internal void SetAckProxyAuthHeader(SipAuthorizationValue proxyAuthHeader)
        {
            this.proxyAuthHeader = proxyAuthHeader;
        }

        /// <summary>
        /// Creates the proper ACK request for the dialog.
        /// </summary>
        /// <returns>The created ACK <see cref="SipRequest" />.</returns>
        public SipRequest CreateAckRequest()
        {
            var ackRequest = new SipRequest(SipMethod.Ack, remoteUri, null);
            var vCSeq      = inviteRequest[SipHeader.CSeq];

            ackRequest.SetHeader(SipHeader.Via, inviteRequest.GetHeader(SipHeader.Via).Text);
            ackRequest.SetHeader(SipHeader.To, requestTo);
            ackRequest.SetHeader(SipHeader.From, requestFrom);
            ackRequest.SetHeader(SipHeader.CallID, callID);
            ackRequest.SetHeader(SipHeader.Contact, localContact);
            ackRequest.SetHeader(SipHeader.MaxForwards, SipHelper.MaxForwards);
            ackRequest.SetHeader(SipHeader.UserAgent, core.Settings.UserAgent);
            ackRequest.SetHeader(SipHeader.CSeq, new SipCSeqValue(this.AckCSeq, "ACK"));    // CSeq has the same number as the
                                                                                            // original request but with the
                                                                                            // "ACK" method
            if (authHeader != null)
                ackRequest.AddHeader(SipHeader.Authorization, authHeader);

            if (proxyAuthHeader != null)
                ackRequest.AddHeader(SipHeader.ProxyAuthorization, proxyAuthHeader);

            return ackRequest;
        }

        /// <summary>
        /// Transmits an ACK request to the accepting dialog.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This works only for the dialogs initiated the dialog, whose <see cref="IsInitiating" />
        /// property returns <c>true</c>.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if request is not an ACK request.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this is not the initiating dialog.</exception>
        public void SendAckRequest(SipRequest ackRequest)
        {
            if (!IsInitiating)
                throw new InvalidOperationException("SendAckRequest() may only be called for initiating dialogs.");

            inviteAck = CreateAckRequest();
            initiatingTransaction.SendAckRequest(inviteAck);
        }

        /// <summary>
        /// Called when the dialog has been closed.
        /// </summary>
        public virtual void OnClose()
        {
            using (TimedLock.Lock(this))
            {
                if (state == SipDialogState.Closed)
                    return;

                state = SipDialogState.Closed;
            }

            if (Closed != null)
                Closed();
        }

        /// <summary>
        /// Determines whether a <see cref="SipRequest" /> is out of
        /// sequence and should be discarded.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>true</c> if the request is valid and should be processed.</returns>
        /// <remarks>
        /// <para>
        /// Examines the <see cref="SipRequest" />'s sequence number and compares
        /// it to the <see cref="RemoteCSeq" /> property maintained by the dialog.  The method
        /// will return <c>false</c> if it appears that the request is out of
        /// sequence.  If this is the case, the caller MUST send a 500 (Server Error)
        /// response for the request.
        /// </para>
        /// <para>
        /// This method updates the <see cref="RemoteCSeq" /> property as necessary
        /// for valid requests.
        /// </para>
        /// </remarks>
        public bool IsValid(SipRequest request)
        {
            var vCSeq = request.GetHeader<SipCSeqValue>(SipHeader.CSeq);

            if (vCSeq == null)
                return false;

            using (TimedLock.Lock(this))
            {
                if (remoteCSeq == -1)
                {
                    // remoteCSeq == "empty"

                    remoteCSeq = vCSeq.Number;
                    return true;
                }

                if (vCSeq.Number < remoteCSeq)
                    return false;

                remoteCSeq = vCSeq.Number;
                return true;
            }
        }

        /// <summary>
        /// Closes the dialog if it's open, sending the appropriate message to the
        /// remote peer and then firing the <see cref="Closed" /> event.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method does nothing if the dialog is already closed. 
        /// </note>
        /// <para>
        /// If the dialog is fully confirmed, then this method will send a BYE
        /// to the remote peer and remove itself from the core's dialog collection
        /// before firing the <see cref="Closed" /> event.  If the dialog is not
        /// confirmed, then the action taken depends on whether this dialog
        /// initiated the INVITE transaction.
        /// </para>
        /// <para><b><u>Unconfirmed INVITE Initiating Dialog</u></b></para>
        /// <para>
        /// If one or more provisional responses have been received from
        /// the accepting dialog, then a CANCEL request is sent to the accepting dialog and 
        /// the dialog transitions to the <see cref="SipDialogState.ClosePendingFinal" />
        /// state.  The dialog will remain in this state until the
        /// INVITE transaction with the accepting dialog completes.  At this
        /// point, if the accepting dialog returns a 2xx success response, the
        /// dialog will submit a BYE request to the accepting dialog and
        /// transition to the <b>SipDialogState.Closed</b> state,
        /// removing itself from the core's dialog collection.
        /// </para>
        /// <para>
        /// If no responses have been received from the accepting dialog yet,
        /// the initiating dialog will transition to the <see cref="SipDialogState.ClosePendingProvisional" />
        /// state to wait for the first response before deciding whether
        /// a CANCEL or BYE request needs to be submitted.
        /// </para>
        /// <para><b><u>Unconfirmed INVITE Accepting Dialog</u></b></para>
        /// <para>
        /// If a final response has not already been sent to the initiating dialog,
        /// the dialog will send a 410 (Gone) response, transition to the
        /// <see cref="SipDialogState.Closed" /> state and remove itself
        /// from the core's dialog collection.
        /// </para>
        /// <para>
        /// If a final response has been sent to the initiating dialog but the
        /// confirming ACK has not yet been received, then the dialog
        /// will submit a BYE request,  transition to the
        /// <see cref="SipDialogState.Closed" /> state and remove itself
        /// from the core's dialog collection.
        /// </para>
        /// </remarks>
        public void Close()
        {
            bool        remove      = false;
            SipRequest  request     = null;
            SipResponse response    = null;
            bool        raiseClosed = false;

            try
            {
                using (TimedLock.Lock(this))
                {
                    switch (state)
                    {
                        case SipDialogState.CloseEventPending:

                            raiseClosed = true;
                            break;

                        case SipDialogState.ClosePendingProvisional:
                        case SipDialogState.ClosePendingFinal:
                        case SipDialogState.ClosePendingAck:
                        case SipDialogState.Closed:

                            break;

                        case SipDialogState.Waiting:

                            if (IsInitiating)
                                state = SipDialogState.ClosePendingProvisional;
                            else
                            {
                                SysLog.LogErrorStackDump("Unexpected dialog state.");
                                state = SipDialogState.Closed;
                            }
                            break;

                        case SipDialogState.Early:

                            if (IsInitiating)
                            {
                                state = SipDialogState.ClosePendingFinal;
                                request = inviteRequest.CreateCancelRequest();
                            }
                            else
                            {
                                SysLog.LogErrorStackDump("Unexpected dialog state.");
                                state = SipDialogState.Closed;
                            }
                            break;

                        case SipDialogState.Confirmed:

                            state       = SipDialogState.Closed;
                            request     = new SipRequest(SipMethod.Bye, this.remoteUri, null);
                            raiseClosed = true;
                            remove      = true;
                            break;
                    }
                }
            }
            finally
            {
                // Handle this outside of the lock.

                if (request != null)
                    FireAndForget(request);

                if (response != null)
                    ((SipServerTransaction)inviteRequest.SourceTransaction).SendResponse(response);

                if (remove)
                    core.RemoveDialog(this);

                if (raiseClosed)
                {
                    core.OnDialogClosed(this);
                    if (Closed != null)
                        Closed();
                }
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better locking diagnostics.
        /// </summary>
        /// <returns>A lock key to be used to identify this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
