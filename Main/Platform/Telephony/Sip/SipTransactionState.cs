//-----------------------------------------------------------------------------
// FILE:        SipTransactionState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible states of a SIP client or
//              server transaction.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Enumerates the possible states of a SIP client or server transaction.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Separate states for INVITE vs non-INVITE transactions have been
    /// provided since INVITE is handled quite differently that transactions
    /// involving other messages.
    /// </note>
    /// </remarks>
    public enum SipTransactionState
    {
        /// <summary>
        /// The transaction has not started.
        /// </summary>
        Unknown,

        /// <summary>
        /// INVITE: Calling.
        /// </summary>
        InviteCalling,

        /// <summary>
        /// INVITE: Proceeding.
        /// </summary>
        InviteProceeding,

        /// <summary>
        /// INVITE: Completed.
        /// </summary>
        InviteCompleted,

        /// <summary>
        /// INVITE: Confirmed.
        /// </summary>
        InviteConfirmed,

        /// <summary>
        /// Trying.
        /// </summary>
        Trying,

        /// <summary>
        /// Proceeding.
        /// </summary>
        Proceeding,

        /// <summary>
        /// Completed.
        /// </summary>
        Completed,

        /// <summary>
        /// Terminated (used for INVITE transactions as well).
        /// </summary>
        Terminated,
    }
}
