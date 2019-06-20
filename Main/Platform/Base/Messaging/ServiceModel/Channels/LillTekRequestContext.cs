//-----------------------------------------------------------------------------
// FILE:        LillTekRequestContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Concrete implementation of the WCF RequestContext that maps
//              calls back to the originating channel via the IReplyImplementation
//              interface.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Concrete implementation of the WCF <see cref="RequestContext" /> that maps
    /// calls back to the originating channel via the <see cref="IReplyImplementation" />
    /// interface.
    /// </summary>
    internal sealed class LillTekRequestContext : RequestContext
    {
        private object                  syncLock = new object();
        private MsgRequestContext       msgRequestContext;      // Low-level LillTek Messaging request context
        private Message                 request;                // The request message
        private IReplyImplementation    implementation;         // The reply implementation reference
        private bool                    open;                   // True if no reply has been sent yet

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="msgRequestContext">The underlying LillTek Messaging <see cref="MsgRequestContext" />.</param>
        /// <param name="request">The WCF request <see cref="Message" />.</param>
        /// <param name="implementation">The concrete reply <see cref="IReplyImplementation" />.</param>
        public LillTekRequestContext(MsgRequestContext msgRequestContext, Message request, IReplyImplementation implementation)
            : base()
        {
            this.msgRequestContext = msgRequestContext;
            this.request           = request;
            this.implementation    = implementation;
            this.open              = true;
        }

        /// <summary>
        /// Returns the underlying LillTek Messaging <see cref="MsgRequestContext" />.
        /// </summary>
        public MsgRequestContext MsgRequestContext
        {
            get { return msgRequestContext; }
        }

        /// <summary>
        /// Returns the request <see cref="Message" /> associated with this context.
        /// </summary>
        public override Message RequestMessage
        {
            get { return request; }
        }

        /// <summary>
        /// Checks to see if it is still OK to reply to this context, throwing an
        /// exception if not, and then disallowing any further replies.
        /// </summary>
        private void VerifyOpen()
        {
            lock (syncLock)
            {
                if (!open)
                    throw new InvalidOperationException("RequestContext cannot send a reply because the context is closed or a reply has already been sent.");

                open = false;
            }
        }

        /// <summary>
        /// Aborts processing of the context request such that a <see cref="CommunicationCanceledException" />
        /// will be thrown on the client.
        /// </summary>
        public override void Abort()
        {
            open = false;
            implementation.Abort(this);
        }

        /// <summary>
        /// Aborts processing of the context request such that no reply is sent to the
        /// client.  The client will begin seeing timeouts, potentially resubmitting
        /// the query.
        /// </summary>
        public void AbortWithoutReply()
        {
            open = false;
            implementation.AbortWithoutReply(this);
        }

        /// <summary>
        /// Closes the context.
        /// </summary>
        public override void Close()
        {
            open = false;
            implementation.Close(this);
        }

        /// <summary>
        /// Closes the context using a timeout.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        public override void Close(TimeSpan timeout)
        {
            open = false;
            implementation.Close(this);
        }

        /// <summary>
        /// Intitiates an asynchronous operation to send a reply to the context request.
        /// </summary>
        /// <param name="message">The reply <see cref="Message" />.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// Every successful call to <see cref="BeginReply(Message,AsyncCallback,object)" /> must eventually be followed by
        /// a call to <see cref="EndReply" />.
        /// </note>
        /// </remarks>
        public override IAsyncResult BeginReply(Message message, AsyncCallback callback, object state)
        {
            VerifyOpen();
            return implementation.BeginReply(this, message, callback, state);
        }

        /// <summary>
        /// Intitiates an asynchronous operation with a timeout to send a reply to the context request.
        /// </summary>
        /// <param name="message">The reply <see cref="Message" />.</param>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// Every successful call to <see cref="BeginReply(Message,TimeSpan,AsyncCallback,object)" /> must eventually be followed by
        /// a call to <see cref="EndReply" />.
        /// </note>
        /// </remarks>
        public override IAsyncResult BeginReply(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            VerifyOpen();
            return implementation.BeginReply(this, message, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by one of the <b>BeginReply()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <b>BeginReply()</b>.</param>
        public override void EndReply(IAsyncResult result)
        {
            implementation.EndReply(result);
        }

        /// <summary>
        /// Synchronously replies to the context request.
        /// </summary>
        /// <param name="message">The reply <see cref="Message" />.</param>
        public override void Reply(Message message)
        {
            VerifyOpen();
            implementation.Reply(this, message);
        }

        /// <summary>
        /// Synchronously replies to the context request using a timeout.
        /// </summary>
        /// <param name="message">The reply <see cref="Message" />.</param>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        public override void Reply(Message message, TimeSpan timeout)
        {
            VerifyOpen();
            implementation.Reply(this, message);
        }
    }
}
