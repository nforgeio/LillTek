//-----------------------------------------------------------------------------
// FILE:        SipRequestDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegates called when a SipCore raises its RequestReceived
//              event.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegates called when a <see cref="SipCore" /> raises its <see cref="SipCore.RequestReceived" />
    /// event.  Custom cores will enlist in this event to implement these requests.
    /// </summary>
    /// <param name="sender">The event source (the <see cref="SipCore" />).</param>
    /// <param name="args">The <see cref="SipRequestEventArgs" /> event arguments.</param>
    /// <remarks>
    /// <para>
    /// The event handler can choose to process the request immediately and
    /// send one or more responses immediately to the request by referencing the
    /// transaction from the <see cref="SipRequestEventArgs" /> and calling its
    /// <see cref="SipServerTransaction.SendResponse" /> method, before the handler
    /// returns.
    /// </para>
    /// <para>
    /// Alternatively, the event handler may choose to save a reference to the transaction,
    /// begin an asynchronous operation to handle the request and return, passing
    /// the responses to the transaction's <see cref="SipServerTransaction.SendResponse" />
    /// while on a different thread.
    /// </para>
    /// <para>
    /// Finally, the event handler may construct an appropriate <see cref="SipResponse" />
    /// and assign this to the <paramref name="args"/>'s <see cref="SipRequestEventArgs.Response" />
    /// property to have the <see cref="SipCore" /> handle the transmission of the response.
    /// </para>
    /// </remarks>
    public delegate void SipRequestDelegate(object sender, SipRequestEventArgs args);
}
