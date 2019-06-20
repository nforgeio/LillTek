//-----------------------------------------------------------------------------
// FILE:        SipDialogEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments passed to a SipDialogDelegate event handler.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// The arguments passed to a <see cref="SipDialogDelegate" /> event handler.
    /// </summary>
    public sealed class SipDialogEventArgs
    {
        private SipRequest clientRequest;

        /// <summary>
        /// The <see cref="SipDialog" /> the referred to by the raised event.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Note that some lower level event handlers will be called before
        /// a dialog correlation has been attempted.  This property will be
        /// set to <c>null</c> in this case, even though the response may
        /// actually be associated with a dialog.
        /// </note>
        /// </remarks>
        public readonly SipDialog Dialog;

        /// <summary>
        /// The transaction associated with the event or <c>null</c>.
        /// </summary>
        public readonly SipTransaction Transaction;

        /// <summary>
        /// The received <see cref="SipRequest" /> if the dialog has just been
        /// created on the server side, or <c>null</c> if the event isn't
        /// related to server side dialog creation.
        /// </summary>
        /// <remarks>
        /// Applications that enlist in the <see cref="SipCore" />
        /// <see cref="SipCore.DialogCreated" /> event handler
        /// will use this to determine whether or the event is
        /// on the server side and that a response must generated.
        /// </remarks>
        public SipRequest ClientRequest
        {
            get { return clientRequest; }
            internal set { clientRequest = value; }
        }

        /// <summary>
        /// The <see cref="SipResult" /> returned by the server confirming the
        /// dialog creation, or <c>null</c> if the event isn't related to
        /// client-side dialog creation.
        /// </summary>
        public readonly SipResult ServerResult;

        /// <summary>
        /// The <see cref="SipResponse" /> to be delivered after returning
        /// from a <see cref="SipDialog" />'s <see cref="SipDialog.RequestReceived" />
        /// event (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications enlisting in the <see cref="SipDialog" />.<see cref="SipDialog.RequestReceived" />
        /// event can respond to the request in two ways.  If the operation can be completed
        /// immediately, the easiest way is to set the <see cref="SipDialogEventArgs" />.<see cref="SipDialogEventArgs.Response" />
        /// property to the <see cref="SipResponse" />  to have the <see cref="SipDialog" />
        /// class handle the response delivery after the event handler returns.
        /// </para>
        /// <para>
        /// For operations that will take longer, the application will passed the 
        /// response to the <see cref="SipDialog"/>.<see cref="SipDialog.Reply(SipRequestEventArgs,SipResponse)" /> method.
        /// </para>
        /// </remarks>
        public SipResponse Response;

        /// <summary>
        /// The <see cref="SipCore" /> The <see cref="SipCore" /> that raised the event.
        /// </summary>
        public readonly SipCore Core;

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="dialog">The <see cref="SipDialog" />.</param>
        /// <param name="transaction">The <see cref="SipTransaction" /> associated with the event (or <c>null</c>).</param>
        /// <param name="core">The <see cref="SipCore" /> that raised the event.</param>
        /// <param name="clientRequest">Pass the <see cref="SipRequest" /> received for server side dialog creation (or <c>null</c>).</param>
        /// <param name="serverResult">The <see cref="SipResult" /> returned by the server, completing its side of the dialog creation (or <c>null</c>).</param>
        internal SipDialogEventArgs(SipDialog dialog, SipTransaction transaction, SipCore core, SipRequest clientRequest, SipResult serverResult)
        {
            this.Dialog        = dialog;
            this.Transaction   = transaction;
            this.Core          = core;
            this.ClientRequest = clientRequest;
            this.ServerResult  = serverResult;
            this.Response      = null;
        }
    }
}
