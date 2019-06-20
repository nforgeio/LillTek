//-----------------------------------------------------------------------------
// FILE:        SipDialogState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible states of a SIP dialog or
//              server transaction.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Enumerates the possible states of a <see cref="SipDialog" />.
    /// </summary>
    public enum SipDialogState
    {
        /// <summary>
        /// Indicates that a client side dialog has sent a dialog
        /// creation request to the server but has not yet received
        /// a response.
        /// </summary>
        Waiting,

        /// <summary>
        /// Indicates that a client side dialog has been partially
        /// established after receiving a provisional response from
        /// the server.
        /// </summary>
        Early,

        /// <summary>
        /// Indicates that the dialog has been confirmed by both peers.
        /// </summary>
        Confirmed,

        /// <summary>
        /// The application has closed the dialog before it was
        /// fully confirmed on the client side.  The dialog is waiting
        /// for the first provisional or final response from the 
        /// server before submitting a CANCEL or BYE transaction,
        /// as necessary.
        /// </summary>
        ClosePendingProvisional,

        /// <summary>
        /// The application has closed the dialog before it was
        /// fully confirmed on the client side.  The dialog is
        /// waiting for the final response from the server before
        /// submitting a BYE transaction (if necessary).
        /// </summary>
        ClosePendingFinal,

        /// <summary>
        /// The application has closed the dialog before it was 
        /// fully confirmed on the server side.  The dialog is
        /// waiting for the reception of the ACK from the client
        /// before submitting a BYE transaction (if necessary).
        /// </summary>
        ClosePendingAck,

        /// <summary>
        /// The dialog has been closed by the <see cref="SipDialog.Closed" />
        /// event has not been yet been raised.
        /// </summary>
        CloseEventPending,

        /// <summary>
        /// The dialog is closed.
        /// </summary>
        Closed
    }
}