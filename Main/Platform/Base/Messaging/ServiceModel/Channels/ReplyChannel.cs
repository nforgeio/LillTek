//-----------------------------------------------------------------------------
// FILE:        ReplyChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IReplyChannel.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
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
    /// Extends Windows Communication Foundation, adding a custom
    /// transport using LillTek Messaging to implement <see cref="IReplyChannel" />.
    /// </summary>
    internal class ReplyChannel : LillTekChannelBase, IReplyChannel, IReplyImplementation
    {
        private ReplyChannelListener                    listener;               // Listener responsible for this channel
        private EndpointAddress                         localAddress;           // Address on which the channel receives requests
        private LimitedQueue<RequestInfo>               requestQueue;           // Queued received request information
        private int                                     maxReceiveQueueSize;    // Maximum size of the queue
        private Dictionary<Guid, MsgRequestContext>     pendingRequests;        // Requests being processed keyed by low-level session ID
        private MessageEncoder                          encoder;                // The message encoder
        private PayloadSizeEstimator                    payloadEstimator;       // Used to estimate the buffer required to serialize
                                                                                // the next message sent
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="localAddress">The local <see cref="EndpointAddress" /> this channel will use to receive requests.</param>
        /// <param name="encoder">The <see cref="MessageEncoder" /> for serializing messages to the wire format.</param>
        public ReplyChannel(ChannelManagerBase channelManager, EndpointAddress localAddress, MessageEncoder encoder)
            : base(channelManager)
        {
            this.maxReceiveQueueSize = ServiceModelHelper.MaxAcceptedMessages; // $todo(jeff.lill): Hardcoding this

            this.listener         = (ReplyChannelListener)channelManager;
            this.localAddress     = localAddress;
            this.requestQueue     = new LimitedQueue<RequestInfo>(maxReceiveQueueSize);
            this.pendingRequests  = new Dictionary<Guid, MsgRequestContext>();
            this.encoder          = encoder;
            this.payloadEstimator = new PayloadSizeEstimator(ServiceModelHelper.PayloadEstimatorSampleCount);
        }

        /// <summary>
        /// Adds a request's information to the channel's receive queue, completing a
        /// pending receive related operation.
        /// </summary>
        /// <param name="requestInfo">The received request information.</param>
        internal void Enqueue(RequestInfo requestInfo)
        {
            using (TimedLock.Lock(this))
            {
                if (!base.CanAcceptMessages)
                    return;

                // If there's a pending receive operation then have it
                // complete with the request information.

                QueueArray<AsyncResult<RequestInfo, ReplyChannel>> receiveQueue = listener.ReceiveRequestQueue;

                if (receiveQueue != null && receiveQueue.Count > 0)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;

                    arReceive = receiveQueue.Dequeue();
                    arReceive.Result = requestInfo;

                    arReceive.Notify();
                    return;
                }

                // There were no pending receive operations so queue the request.

                requestQueue.Enqueue(requestInfo);

                // Complete the first pending WaitForRequest() request, 
                // if there is one queued.

                QueueArray<AsyncResult<bool, ReplyChannel>> waitQueue = listener.WaitForRequestQueue;

                if (waitQueue != null && waitQueue.Count > 0)
                {
                    AsyncResult<bool, ReplyChannel> arWait;

                    arWait = waitQueue.Dequeue();
                    arWait.Result = true;
                    arWait.Notify();
                }
            }
        }

        /// <summary>
        /// Used internally by unit test to remove a request from the channel's
        /// pending requests table.
        /// </summary>
        /// <param name="ctx">The <see cref="LillTekRequestContext" />.</param>
        internal void RemovePendingRequest(LillTekRequestContext ctx) 
        {
            using (TimedLock.Lock(this)) 
            {
                if (pendingRequests != null && pendingRequests.ContainsKey(ctx.MsgRequestContext.SessionID))
                    pendingRequests.Remove(ctx.MsgRequestContext.SessionID);
            }
        }

        //---------------------------------------------------------------------
        // IChannel implementation

        /// <summary>
        /// Searches this level in the channel stack and below for a channel property
        /// of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the specified property.</typeparam>
        /// <returns>The property instance if found, <c>null</c> otherwise.</returns>
        public override T GetProperty<T>()
        {
            if (typeof(T) == this.GetType())
                return this as T;

            return base.GetProperty<T>();
        }

        //---------------------------------------------------------------------
        // LillTekChannelBase implementation

        /// <summary>
        /// Called by the <see cref="LillTekChannelBase" /> class when the channel is opened
        /// to determine whether the channel requires its <see cref="OnBkTask()" /> method
        /// to be called periodically on a background thread while the channel is open.
        /// </summary>
        /// <returns>
        /// The desired background task callback interval or <b>TimeSpan.Zero</b> if 
        /// callbacks are to be disabled.
        /// </returns>
        protected override TimeSpan GetBackgroundTaskInterval()
        {
            return TimeSpan.Zero;   // Disables periodic OnBkTask() callbacks
        }

        /// <summary>
        /// Called periodically on a background thread and within a <see cref="TimedLock" />
        /// if <see cref="GetBackgroundTaskInterval()" /> returned a positive interval.
        /// </summary>
        protected override void OnBkTask()
        {
            TimedLock.AssertLocked(this);   // Verify the lock
        }

        /// <summary>
        /// Terminates all pending operations with the exception passed.
        /// </summary>
        /// <param name="e">The termination exception.</param>
        protected override void TerminatePendingOperations(Exception e)
        {
            using (TimedLock.Lock(this))
            {
                // Send CancelExceptions back to the client for all queued requests.

                while (requestQueue.Count > 0)
                    requestQueue.Dequeue().Context.Cancel();

                // Send CancelExceptions back to the client for all requests
                // already being processed by this channel.

                if (pendingRequests != null)
                {
                    foreach (MsgRequestContext requestInfo in pendingRequests.Values)
                        requestInfo.Cancel();

                    pendingRequests.Clear();
                }
            }
        }

        //---------------------------------------------------------------------
        // IReplyChannel implementation

        /// <summary>
        /// Returns the <see cref="EndpointAddress" /> on which the reply channel receives requests.
        /// </summary>
        public EndpointAddress LocalAddress
        {
            get { return localAddress; }
        }

        /// <summary>
        /// Returns the <see cref="RequestContext" /> of the request received, if one is available. 
        /// If a context is not available, waits synchronously for the default timeout until one is available.
        /// </summary>
        /// <returns>The <see cref="RequestContext" />.</returns>
        public RequestContext ReceiveRequest()
        {
            return ReceiveRequest(this.DefaultReceiveTimeout);
        }

        /// <summary>
        /// Returns the <see cref="RequestContext" /> of the request received, if one is available. 
        /// If a context is not available, waits synchronously for the specified timespan until 
        /// one is available.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <returns>The <see cref="RequestContext" /> received.</returns>
        public RequestContext ReceiveRequest(TimeSpan timeout)
        {
            IAsyncResult ar;

            ar = BeginReceiveRequest(timeout, null, null);
            return EndReceiveRequest(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to receive a <see cref="RequestContext" />, using
        /// the default timeout.
        /// </summary>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginReceiveRequest(AsyncCallback,object)" /> must eventually be followed by
        /// a call to <see cref="EndReceiveRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceiveRequest(AsyncCallback callback, object state)
        {
            return BeginReceiveRequest(this.DefaultReceiveTimeout, callback, state);
        }

        /// <summary>
        /// Initiates an asynchronous operation to receive a <see cref="RequestContext" />
        /// with the specified timeout.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait for an incoming request.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginReceiveRequest(TimeSpan,AsyncCallback,object)" /> must eventually be followed by
        /// a call to <see cref="EndReceiveRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return null.

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;   // Note that TInternal==ReplyChannel.  This is used below in EndReceiveRequest()
                                                                        // to distinguish between IAsyncResults returned by this class and those
                                                                        // returned by the listener.                                    

                    arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                    arReceive.Result = null;
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }

                // If the channel already has a request queued, then setup to return it.

                if (requestQueue.Count > 0)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;   // Note that TInternal==ReplyChannel.  This is used below in EndReceiveRequest()
                                                                        // to distinguish between IAsyncResults returned by this class and those
                                                                        // returned by the listener.                                    

                    arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                    arReceive.Result = requestQueue.Dequeue();
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }
            }

            return listener.BeginReceiveRequest(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous request receive operation initiated by one of the <b>BeginReceiveRequest()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <b>BeginReceiveRequest()</b>.</param>
        /// <returns>The <see cref="RequestContext" /> received.</returns>
        public RequestContext EndReceiveRequest(IAsyncResult result)
        {
            AsyncResult<RequestInfo, ReplyChannel>  arReceive;
            RequestInfo                             requestInfo;

            arReceive = result as AsyncResult<RequestInfo, ReplyChannel>;
            if (arReceive != null)
            {
                // Operation completed in BeginReceiveRequest() above.

                arReceive.Wait();
                try
                {
                    if (arReceive.Exception != null)
                        throw arReceive.Exception;

                    requestInfo = arReceive.Result;
                    if (requestInfo == null)
                        return null;

                    using (TimedLock.Lock(this))
                        pendingRequests.Add(requestInfo.Context.SessionID, requestInfo.Context);

                    return new LillTekRequestContext(requestInfo.Context, requestInfo.Message, this);
                }
                finally
                {
                    arReceive.Dispose();
                }
            }

            requestInfo = listener.EndReceiveRequest(result);
            if (requestInfo == null)
                return null;

            using (TimedLock.Lock(this))
                pendingRequests.Add(requestInfo.Context.SessionID, requestInfo.Context);

            return new LillTekRequestContext(requestInfo.Context, requestInfo.Message, this);
        }

        /// <summary>
        /// Synchronously waits for a specified period of time for a request
        /// to be received by the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="context">Returns as the received <see cref="RequestContext" /> or <c>null</c>.</param>
        /// <returns><c>true</c> if a request was received.</returns>
        public bool TryReceiveRequest(TimeSpan timeout, out RequestContext context)
        {
            IAsyncResult ar;

            ar = BeginTryReceiveRequest(timeout, null, null);
            return EndTryReceiveRequest(ar, out context);
        }

        /// <summary>
        /// Initiates an asynchronous operation to wait for a specified period of time for
        /// a request to be received by the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginTryReceiveRequest" /> must eventually be followed 
        /// by a call to <see cref="EndTryReceiveRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginTryReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return true (context=null)

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;  // Note that TInternal==ReplyChannel.  This is used below in EndTryReceiveRequest()
                    // to distinguish between IAsyncResults returned by this class and those
                    // returned by the listener.                                    

                    arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                    arReceive.Result = null;
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }

                // If the channel already has a request queued, then setup to return it.

                if (requestQueue.Count > 0)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;  // Note that TInternal==ReplyChannel.  This is used below in EndTryReceiveRequest()
                    // to distinguish between IAsyncResults returned by this class and those
                    // returned by the listener.                                    

                    arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                    arReceive.Result = requestQueue.Dequeue();
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }
            }

            return listener.BeginTryReceiveRequest(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes the asynchronous operation initiated by a <see cref="BeginTryReceiveRequest" /> call.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTryReceiveRequest" />.</param>
        /// <param name="context">Returns as the received <see cref="RequestContext" /> (or <c>null</c>).</param>
        /// <returns><c>true</c> if a request was received.</returns>
        public bool EndTryReceiveRequest(IAsyncResult result, out RequestContext context)
        {
            AsyncResult<RequestInfo, ReplyChannel>  arReceive;
            RequestInfo                             requestInfo;

            context = null;
            arReceive = result as AsyncResult<RequestInfo, ReplyChannel>;
            if (arReceive != null)
            {
                // Operation completed in BeginTryReceiveRequest() above.

                context = null;
                arReceive.Wait();
                try
                {
                    if (arReceive.Exception != null)
                        return false;

                    requestInfo = arReceive.Result;
                    if (requestInfo == null)
                        return true;

                    context = new LillTekRequestContext(requestInfo.Context, requestInfo.Message, this);
                    return true;
                }
                finally
                {
                    arReceive.Dispose();
                }
            }

            if (listener.EndTryReceiveRequest(result, out requestInfo))
            {
                if (requestInfo == null)
                    return true;

                using (TimedLock.Lock(this))
                    pendingRequests.Add(requestInfo.Context.SessionID, requestInfo.Context);

                context = new LillTekRequestContext(requestInfo.Context, requestInfo.Message, this);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Synchronously waits for a specified period of time for a request to be
        /// received (or be queued) on the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <returns><c>true</c> if the channel has queued a received request.</returns>
        public bool WaitForRequest(TimeSpan timeout)
        {
            IAsyncResult ar;

            ar = BeginWaitForRequest(timeout, null, null);
            return EndWaitForRequest(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to wait for a request to be received
        /// (or queued) on the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginWaitForRequest" /> must eventually be followed 
        /// by a call to <see cref="EndWaitForRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginWaitForRequest(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return false

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<object, ReplyChannel> arWait;     // Note that TInternal==ReplyChannel.  This is used below in EndWaitForRequest()
                    // to distinguish between IAsyncResults returned by this class and those
                    // returned by the listener.                                    

                    arWait = new AsyncResult<object, ReplyChannel>(null, callback, state);
                    arWait.Result = false;
                    arWait.Started(ServiceModelHelper.AsyncTrace);
                    arWait.Notify();
                    return arWait;
                }

                // If the channel already has a request queued, then setup to return it.

                if (requestQueue.Count > 0)
                {
                    AsyncResult<object, ReplyChannel> arWait;     // Note that TInternal==ReplyChannel.  This is used below in EndWaitForRequest()
                    // to distinguish between IAsyncResults returned by this class and those
                    // returned by the listener.                                    

                    arWait = new AsyncResult<object, ReplyChannel>(null, callback, state);
                    arWait.Started(ServiceModelHelper.AsyncTrace);
                    arWait.Notify();
                    return arWait;
                }
            }

            timeout = ServiceModelHelper.ValidateTimeout(timeout);
            return listener.BeginWaitForRequest(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes the asynchronous operation initiated by a call to <see cref="BeginWaitForRequest" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWaitForRequest" />.</param>
        /// <returns><c>true</c> if the channel has queued a received request.</returns>
        public bool EndWaitForRequest(IAsyncResult result)
        {
            AsyncResult<bool, ReplyChannel> arWait;

            arWait = result as AsyncResult<bool, ReplyChannel>;
            if (arWait != null)
            {
                // Operation completed in BeginWaitForRequest() above.

                arWait.Wait();
                try
                {
                    return arWait.Exception == null && arWait.Result;
                }
                finally
                {
                    arWait.Dispose();
                }
            }

            return listener.EndWaitForRequest(result);
        }

        //---------------------------------------------------------------------
        // IReplyImplementation implementation

        /// <summary>
        /// Aborts processing of the context request such that a <see cref="CommunicationCanceledException" />
        /// will be thrown on the client.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        void IReplyImplementation.Abort(LillTekRequestContext context)
        {
            using (TimedLock.Lock(this))
            {
                if (pendingRequests.ContainsKey(context.MsgRequestContext.SessionID))
                    pendingRequests.Remove(context.MsgRequestContext.SessionID);

                context.MsgRequestContext.Cancel();
            }
        }

        /// <summary>
        /// Aborts processing of the context request such that no reply is sent to the
        /// client.  The client will begin seeing timeouts, potentially resubmitting
        /// the query.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        void IReplyImplementation.AbortWithoutReply(LillTekRequestContext context)
        {
            using (TimedLock.Lock(this))
            {
                if (pendingRequests.ContainsKey(context.MsgRequestContext.SessionID))
                    pendingRequests.Remove(context.MsgRequestContext.SessionID);

                context.MsgRequestContext.Abort();
            }
        }

        /// <summary>
        /// Closes the context.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        void IReplyImplementation.Close(LillTekRequestContext context)
        {
            using (TimedLock.Lock(this))
            {
                if (pendingRequests.ContainsKey(context.MsgRequestContext.SessionID))
                    pendingRequests.Remove(context.MsgRequestContext.SessionID);

                context.MsgRequestContext.Close();
            }
        }

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
        public IAsyncResult BeginReply(LillTekRequestContext context, Message message, AsyncCallback callback, object state)
        {
            AsyncResult arReply;

            // This operation is inherently asynchronous at the LillTek Messaging level
            // so we'll complete the operation immediately.

            using (MemoryStream ms = new MemoryStream(payloadEstimator.EstimateNextBufferSize()))
            {
                WcfEnvelopeMsg replyMsg = new WcfEnvelopeMsg();

                encoder.WriteMessage(message, ms);
                payloadEstimator.LastPayloadSize((int)ms.Length);

                replyMsg.Payload = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);

                using (TimedLock.Lock(this))
                {
                    if (pendingRequests.ContainsKey(context.MsgRequestContext.SessionID))
                        pendingRequests.Remove(context.MsgRequestContext.SessionID);

                    context.MsgRequestContext.Reply(replyMsg);
                }
            }

            arReply = new AsyncResult(null, callback, state);
            arReply.Started(ServiceModelHelper.AsyncTrace);
            arReply.Notify();

            return arReply;
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by one of the <b>BeginReply()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <b>BeginReply()</b>.</param>
        public void EndReply(IAsyncResult result)
        {
        }

        /// <summary>
        /// Synchronously replies to the context request.
        /// </summary>
        /// <param name="context">The <see cref="LillTekRequestContext" /> context.</param>
        /// <param name="message">The reply <see cref="Message" />.</param>
        public void Reply(LillTekRequestContext context, Message message)
        {
            IAsyncResult ar;

            ar = BeginReply(context, message, null, null);
            EndReply(ar);
        }
    }
}
