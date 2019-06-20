//-----------------------------------------------------------------------------
// FILE:        IReplyImplementation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the concrete implementation referenced by the 
//              LillTekRequestContext class.

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
    /// Defines the concrete implementation referenced by the <see cref="LillTekRequestContext" /> class.
    /// </summary>
    internal interface IReplyImplementation
    {
        /// <summary>
        /// Aborts processing of the context request such that a <see cref="CommunicationCanceledException" />
        /// will be thrown on the client.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        void Abort(LillTekRequestContext context);

        /// <summary>
        /// Aborts processing of the context request such that no reply is sent to the
        /// client.  The client will begin seeing timeouts, potentially resubmitting
        /// the query.
        /// </summary>
        void AbortWithoutReply(LillTekRequestContext context);

        /// <summary>
        /// Closes the context.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        void Close(LillTekRequestContext context);

        /// <summary>
        /// Intitiates an asynchronous operation to send a reply to the context request.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        /// <param name="message">The reply <see cref="Message" />.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// Every successful call to <see cref="BeginReply(LillTekRequestContext,Message,AsyncCallback,object)" /> must eventually be followed by
        /// a call to <see cref="EndReply" />.
        /// </note>
        /// </remarks>
        IAsyncResult BeginReply(LillTekRequestContext context, Message message, AsyncCallback callback, object state);

        /// <summary>
        /// Completes an asynchronous operation initiated by one of the <b>BeginReply()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <b>BeginReply()</b>.</param>
        void EndReply(IAsyncResult result);

        /// <summary>
        /// Synchronously replies to the context request.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        /// <param name="message">The reply <see cref="Message" />.</param>
        void Reply(LillTekRequestContext context, Message message);
    }
}
