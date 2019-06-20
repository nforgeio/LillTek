//-----------------------------------------------------------------------------
// FILE:        ReplyChannelListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an IChannelListener capable of accepting 
//              ReplyChannels via LillTek Messaging.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements an <see cref="IChannelListener" /> capable of accepting 
    /// <see cref="RequestChannel" />s via LillTek Messaging.
    /// </summary>
    internal sealed class ReplyChannelListener : LillTekChannelListener<IReplyChannel, ReplyChannel>
    {
        private LimitedQueue<RequestInfo>                           requestQueue;           // Received request queue
        private QueueArray<AsyncResult<bool, ReplyChannel>>         waitQueue;              // Queue of pending channel WaitForRequest() requests
        private QueueArray<AsyncResult<RequestInfo, ReplyChannel>>  receiveQueue;           // Queue of pending channel ReceiveRequest() requests
        private int                                                 maxRequestQueueSize;    // Maximum number of queued requests
        private TimeSpan                                            maxRequestQueueTime;    // Maximum time a request can remain queued before beign aborted
        private TimeSpan                                            bkTaskInterval;         // Channel background task interval

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        internal ReplyChannelListener(BindingContext context)
            : base(context)
        {

            this.maxRequestQueueSize = ServiceModelHelper.MaxAcceptedMessages;      // $todo(jeff.lill): Hardcoded
            this.maxRequestQueueTime = ServiceModelHelper.MaxRequestQueueTime;      //
            this.bkTaskInterval      = ServiceModelHelper.DefaultBkTaskInterval;    //

            this.requestQueue        = new LimitedQueue<RequestInfo>(maxRequestQueueSize);
            this.waitQueue           = new QueueArray<AsyncResult<bool, ReplyChannel>>();
            this.receiveQueue        = new QueueArray<AsyncResult<RequestInfo, ReplyChannel>>();
        }

        /// <summary>
        /// Returns the queue of pending <b>WaitForRequest()</b> operations.
        /// </summary>
        public QueueArray<AsyncResult<bool, ReplyChannel>> WaitForRequestQueue
        {
            get { return waitQueue; }
        }

        /// <summary>
        /// Returns the queue of pending <b>ReceiveRequest()</b> operations.
        /// </summary>
        public QueueArray<AsyncResult<RequestInfo, ReplyChannel>> ReceiveRequestQueue
        {
            get { return receiveQueue; }
        }

        /// <summary>
        /// Begins an asynchronous operation to wait for a specified period of time for a request to be received 
        /// for a channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// All successful calls to <see cref="BeginWaitForRequest" /> must eventually be followed by a call to <see cref="EndWaitForRequest" />.
        /// </remarks>
        public IAsyncResult BeginWaitForRequest(ReplyChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<bool, ReplyChannel> arWait;

            using (TimedLock.Lock(this))
            {

                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arWait = new AsyncResult<bool, ReplyChannel>(null, callback, state);
                arWait.TTD = SysTime.Now + timeout;
                arWait.InternalState = channel;
                arWait.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return false.

                if (base.State != CommunicationState.Opened)
                {
                    arWait.Notify();
                    return arWait;
                }

                // If we already have a queued request, dequeue it and add it to the 
                // channel's request queue, so a subsequent call to ReceiveRequest()
                // on the channel will be assured to succeed.  Then notify that 
                // the operation is complete.

                if (requestQueue.Count > 0)
                {
                    channel.Enqueue(requestQueue.Dequeue());
                    arWait.Result = true;
                    arWait.Notify();
                    return arWait;
                }

                // Otherwise queue the wait operation.

                waitQueue.Enqueue(arWait);
                return arWait;
            }
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by <see cref="BeginWaitForRequest" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWaitForRequest" />.</param>
        /// <returns><c>true</c> if the channel has queued a received request.</returns>
        public bool EndWaitForRequest(IAsyncResult result)
        {
            AsyncResult<bool, ReplyChannel> arWait = (AsyncResult<bool, ReplyChannel>)result;

            Assertion.Test(arWait.InternalState != null, "InternalState should have been set to the channel.");
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

        /// <summary>
        /// Begins an asynchronous operation to receive a channel request (using a specified timeout).
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The <see cref="TimeSpan" /> value specifying the maximum time to wait for a request.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginReceiveRequest" /> must eventually be followed by a call to <see cref="EndReceiveRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceiveRequest(ReplyChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<RequestInfo, ReplyChannel> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.InternalState = channel;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Check to see if we already have a queued request.

                if (requestQueue.Count > 0)
                {
                    arReceive.Result = requestQueue.Dequeue();
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous channel request receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <see cref="BeginReceiveRequest" />.</param>
        /// <returns>The <see cref="RequestInfo" /> received.</returns>
        public RequestInfo EndReceiveRequest(IAsyncResult result)
        {
            AsyncResult<RequestInfo, ReplyChannel> arReceive = (AsyncResult<RequestInfo, ReplyChannel>)result;

            Assertion.Test(arReceive.InternalState != null, "InternalState should have been set to the channel.");
            arReceive.Wait();
            try
            {
                if (arReceive.Exception != null)
                    throw ServiceModelHelper.GetCommunicationException(arReceive.Exception);

                return arReceive.Result;
            }
            finally
            {
                arReceive.Dispose();
            }
        }

        /// <summary>
        /// Initiates an asynchronous attempt to receive a request within the 
        /// specified period of time, where a boolean will ultimately be returned
        /// indicating success or failure rather than throwning an exception if
        /// an error is encountered.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The <see cref="TimeSpan" /> value specifying the maximum time to wait for a request.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginTryReceiveRequest" /> must eventually be followed by a call to <see cref="EndTryReceiveRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginTryReceiveRequest(ReplyChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<RequestInfo, ReplyChannel> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<RequestInfo, ReplyChannel>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.InternalState = channel;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return true (context=null)

                if (base.State != CommunicationState.Opened)
                {
                    arReceive.Notify();
                    return arReceive;
                }

                // Check to see if we already have a queued request.

                if (requestQueue.Count > 0)
                {
                    arReceive.Result = requestQueue.Dequeue();
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive request operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous channel try-receive request operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <see cref="BeginTryReceiveRequest" />.</param>
        /// <param name="requestInfo">Returns as the <see cref="RequestInfo" /> received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a request was received.</returns>
        public bool EndTryReceiveRequest(IAsyncResult result, out RequestInfo requestInfo)
        {
            AsyncResult<RequestInfo, ReplyChannel> arReceive = (AsyncResult<RequestInfo, ReplyChannel>)result;

            requestInfo = default(RequestInfo);
            Assertion.Test(arReceive.InternalState != null, "InternalState should have been set to the channel.");
            arReceive.Wait();
            try
            {
                if (arReceive.Exception != null)
                    return false;

                requestInfo = arReceive.Result;
                return true;
            }
            finally
            {
                arReceive.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // CommunicationObject overrides

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnClosing()
        {
            using (TimedLock.Lock(this))
            {
                // Abort all queued request transactions

                if (requestQueue != null)
                {
                    while (requestQueue.Count > 0)
                        requestQueue.Dequeue().Context.Cancel();
                }

                // Abort pending WaitForRequest() operations

                if (waitQueue != null)
                {
                    while (waitQueue.Count > 0)
                        waitQueue.Dequeue().Notify();
                }

                // Abort pending ReceiveRequest() operations

                if (receiveQueue != null)
                {
                    while (receiveQueue.Count > 0)
                        receiveQueue.Dequeue().Notify();
                }
            }

            base.OnClosing();
        }

        //---------------------------------------------------------------------
        // ILillTekChannelManager overrides

        /// <summary>
        /// Called by LillTek channels accepted by this listener when the
        /// channel is closed or aborted to terminate any pending operations.
        /// </summary>
        /// <param name="channel">The closed or aborted channel.</param>
        /// <param name="e">The exception to be used to terminate the operation.</param>
        public override void OnChannelCloseOrAbort(LillTekChannelBase channel, Exception e)
        {
            using (TimedLock.Lock(this))
            {
                // Abort any pending operations related to the channel.

                if (waitQueue != null)
                {
                    for (int i = waitQueue.Count - 1; i >= 0; i--)
                    {
                        Assertion.Test(waitQueue[i].InternalState != null, "InternalState should have been set to the channel.");
                        if (waitQueue[i].InternalState == channel)
                        {

                            waitQueue[i].Notify();
                            waitQueue.RemoveAt(i);
                        }
                    }
                }

                if (receiveQueue != null)
                {
                    for (int i = receiveQueue.Count - 1; i >= 0; i--)
                    {
                        Assertion.Test(receiveQueue[i].InternalState != null, "InternalState should have been set to the channel.");
                        if (receiveQueue[i].InternalState == channel)
                        {

                            receiveQueue[i].Notify();
                            receiveQueue.RemoveAt(i);
                        }
                    }
                }
            }

            base.OnChannelCloseOrAbort(channel, e);
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This will be called periodically by the base listener class within a <see cref="TimedLock" />.
        /// </note>
        /// </remarks>
        protected override void OnBkTask()
        {
            DateTime now = SysTime.Now;

            TimedLock.AssertLocked(this);   // Verify the lock

            // Scan for any queued requests that have been in the queue too long
            // and cancel them.

            if (requestQueue != null)
            {
                while (requestQueue.Count > 0)
                {
                    if (requestQueue.Peek().TTD > now)
                        break;

                    requestQueue.Dequeue().Context.Cancel();
                }
            }

            // Scan the queued ReceiveRequest() operations for any that have timed-out.

            if (receiveQueue != null && receiveQueue.Count > 0)
            {
                do
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;

                    arReceive = receiveQueue.Peek();
                    if (arReceive.TTD <= now)
                    {

                        receiveQueue.Dequeue();
                        arReceive.Notify(new TimeoutException());
                    }
                    else
                        break;

                } while (receiveQueue.Count > 0);
            }

            // Scan the queued WaitForRequest() operations for any that have timed-out.

            if (waitQueue != null && waitQueue.Count > 0)
            {
                do
                {
                    AsyncResult<bool, ReplyChannel> arWait;

                    arWait = waitQueue.Peek();
                    if (arWait.TTD <= now)
                    {

                        waitQueue.Dequeue();
                        arWait.Notify(new TimeoutException());
                    }
                    else
                        break;

                } while (waitQueue.Count > 0);
            }
        }

        //---------------------------------------------------------------------
        // LillTekChannelListener overrides

        /// <summary>
        /// Derived classes may need to override this method to return the
        /// <see cref="SessionHandlerInfo" /> to be associated with the endpoint
        /// when it is added to the LillTek message router.  The base implementation
        /// returns <c>null</c>.
        /// </summary>
        /// <returns>The <see cref="SessionHandlerInfo" /> to be associated with the endpoint.</returns>
        protected override SessionHandlerInfo GetSessionHandlerInfo()
        {
            MsgSessionAttribute attr = new MsgSessionAttribute();

            // $todo(jeff.lill): 
            //
            // At some point, I need to come back and get
            // all of the session settings from the
            // application configuration.  We'll use the
            // default LillTek values for now.

            attr.Type = SessionTypeID.Query;
            attr.IsAsync = true;

            return new SessionHandlerInfo(attr);
        }

        /// <summary>
        /// Called when the base class receives a LillTek envelope message with an
        /// encapsulated WCF message from the router.  Non-session oriented derived 
        /// classes must implement this to accept a new channel or route the message 
        /// to an existing channel.
        /// </summary>
        /// <param name="message">The decoded WCF <see cref="Message" />.</param>
        /// <param name="msg">The received LillTek <see cref="Msg" />.</param>
        /// <remarks>
        /// <para>
        /// This method takes different actions depending on whether there are
        /// any pending channel <b>WaitForRequest()</b> or <b>ReceiveRequest()</b> requests.
        /// </para>
        /// <para>
        /// If there are pending request receive operations, then these will be completed
        /// and the rfequest information will be queued to the associated channel as is appropriate.
        /// </para>
        /// <para>
        /// Finally, if no pending request receive requests and the base class has a 
        /// pending <b>WaitForChannel()</b> or <b>AcceptChannel()</b>, then the base class
        /// <see cref="LillTekChannelListener{IInputSessionChannel,InputSessionChannel}.OnChannelCreated" /> 
        /// method will be called so that a new channel will be accepted.
        /// </para>
        /// <para>
        /// Finally, if there are no pending request receive requests or base channel
        /// channel accept related requests, the message will be queued internally.
        /// </para>
        /// </remarks>
        protected override void OnMessageReceived(Message message, WcfEnvelopeMsg msg)
        {
            RequestInfo     requestInfo = new RequestInfo(message, msg.CreateRequestContext(), SysTime.Now + maxRequestQueueTime);
            ReplyChannel    newChannel = null;

            if (base.State != CommunicationState.Opened)
                return;

            using (TimedLock.Lock(this))
            {
                // Handle any pending channel ReceiveRequest() operations first.

                if (receiveQueue.Count > 0)
                {
                    AsyncResult<RequestInfo, ReplyChannel> arReceive;

                    arReceive = receiveQueue.Dequeue();
                    arReceive.Result = requestInfo;
                    arReceive.Notify();
                    return;
                }

                // Next, handle any pending channel WaitForRequest() operations.

                if (waitQueue.Count > 0)
                {
                    AsyncResult<bool, ReplyChannel> arWait;

                    // Queue the request information to the input channel so it will assured
                    // to be available when the WaitForRequest() completes and
                    // the application calls ReceiveRequest().

                    arWait = waitQueue.Dequeue();
                    arWait.Result = true;
                    arWait.InternalState.Enqueue(requestInfo);
                    arWait.Notify();
                    return;
                }

                // Queue the request.

                requestQueue.Enqueue(requestInfo);

                // Create new channel if there are pending channel accept 
                // or wait operations.

                if (base.HasPendingChannelOperation)
                {
                    newChannel = new ReplyChannel(this, new EndpointAddress(this.Uri), base.MessageEncoder);
                    AddChannel(newChannel);
                }
            }

            // Do this outside of the lock just to be safe

            if (newChannel != null)
                base.OnChannelCreated(newChannel);
        }

        /// <summary>
        /// Called when the base class receives a LillTek duplex session connection
        /// attempt from the router.  Session oriented derived  classes must implement 
        /// this to accept a new channel or route the message to an existing channel.
        /// </summary>
        /// <param name="msg">The session opening <see cref="DuplexSessionMsg" />.</param>
        protected override void OnSessionConnect(DuplexSessionMsg msg)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Called by the base class before it queues an <b>AcceptChannel()</b> operation
        /// giving derived classes a chance to decide whether they can accept a channel.
        /// </summary>
        /// <returns>The accepted channel or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// This is called within a <see cref="TimedLock" />.
        /// </note>
        /// </remarks>
        protected override ReplyChannel GetAcceptChannel()
        {
            TimedLock.AssertLocked(this);   // Verify the lock

            // Accept a channel if there are any queued requests.

            if (requestQueue.Count > 0)
            {
                ReplyChannel channel;

                channel = new ReplyChannel(this, new EndpointAddress(this.Uri), base.MessageEncoder); // I explicitly decided not to queue the request
                return channel;
            }
            else
                return null;
        }
    }
}
