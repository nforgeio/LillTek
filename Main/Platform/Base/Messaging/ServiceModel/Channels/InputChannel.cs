//-----------------------------------------------------------------------------
// FILE:        InputChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IInputChannel.

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
    /// Extends Windows Communication Foundation, adding a custom
    /// transport using LillTek Messaging to implement <see cref="IInputChannel" />.
    /// </summary>
    internal class InputChannel : LillTekChannelBase, IInputChannel
    {
        private InputChannelListener    listener;               // Listener responsible for this channel
        private EndpointAddress         localAddress;           // Address on which the channel receives messages
        private LimitedQueue<Message>   msgQueue;               // Queued received messages
        private int                     maxReceiveQueueSize;    // Maximum size of the queue

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="localAddress">The local <see cref="EndpointAddress" /> this channel will use to receive requests.</param>
        public InputChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
            : base(channelManager)
        {
            this.maxReceiveQueueSize = ServiceModelHelper.MaxAcceptedMessages;  // $todo(jeff.lill): Hardcoding this

            this.listener            = (InputChannelListener)channelManager;
            this.localAddress        = localAddress;
            this.msgQueue            = new LimitedQueue<Message>(maxReceiveQueueSize);
        }

        /// <summary>
        /// Adds a message to the channel's receive queue, completing a
        /// pending receive related operation.
        /// </summary>
        /// <param name="message">The received message.</param>
        internal void Enqueue(Message message)
        {
            using (TimedLock.Lock(this))
            {
                if (!base.CanAcceptMessages)
                    return;

                // If there's a pending receive operation then have it
                // complete with the message.

                QueueArray<AsyncResult<Message, InputChannel>> receiveQueue = listener.ReceiveQueue;

                if (receiveQueue != null && receiveQueue.Count > 0)
                {
                    AsyncResult<Message, InputChannel> arReceive;

                    arReceive = receiveQueue.Dequeue();
                    arReceive.Result = message;

                    arReceive.Notify();
                    return;
                }

                // There were no pending receive operations so queue the message.

                msgQueue.Enqueue(message);

                // Complete the first pending WaitForMessage() request, 
                // if there is one queued.

                QueueArray<AsyncResult<bool, InputChannel>> waitQueue;

                waitQueue = listener.WaitForMessageQueue;
                if (waitQueue != null && waitQueue.Count > 0)
                {
                    AsyncResult<bool, InputChannel> arWait;

                    arWait = waitQueue.Dequeue();
                    arWait.Result = true;
                    arWait.Notify();
                }
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
                msgQueue.Clear();
        }

        //---------------------------------------------------------------------
        // IInputChannel implementation

        /// <summary>
        /// Returns the <see cref="EndpointAddress" /> on which the input channel receives messages.
        /// </summary>
        public EndpointAddress LocalAddress
        {
            get { return localAddress; }
        }

        /// <summary>
        /// Returns the message received, if one is available. If a message is not available, 
        /// blocks for a default interval of time.
        /// </summary>
        /// <returns>The received <see cref="Message" />.</returns>
        public Message Receive()
        {
            return Receive(this.DefaultReceiveTimeout);
        }

        /// <summary>
        /// Returns the message received, if one is available. If a message is not available, 
        /// blocking for a specified interval of time and waits for a message.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a message.</param>
        /// <returns>The received <see cref="Message" />.</returns>
        public Message Receive(TimeSpan timeout)
        {
            IAsyncResult ar;

            ar = BeginReceive(timeout, null, null);
            return EndReceive(ar);
        }

        /// <summary>
        /// Begins an asynchronous operation to receive a message (using the default timeout).
        /// </summary>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginReceive(AsyncCallback,object)" /> must eventually be followed by a call to <see cref="EndReceive" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return BeginReceive(this.DefaultReceiveTimeout, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous operation to receive a message (using the specified timeout).
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> value specifying the maximum time to wait for a message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginReceive(TimeSpan,AsyncCallback,object)" /> must eventually be followed by a call to <see cref="EndReceive" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return null.

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<Message, InputChannel> arReceive;   // Note that TInternal==InputChannel.  This is used below in EndReceive()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arReceive = new AsyncResult<Message, InputChannel>(null, callback, state);
                    arReceive.Result = null;
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }

                // If the channel already has a message queued, then setup to return it.

                if (msgQueue.Count > 0)
                {
                    AsyncResult<Message, InputChannel> arReceive;   // Note that TInternal==InputChannel.  This is used below in EndReceive()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arReceive = new AsyncResult<Message, InputChannel>(null, callback, state);
                    arReceive.Result = msgQueue.Dequeue();
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }
            }

            return listener.BeginReceive(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous message receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by one of the <b>BeginReceive()</b> overrides.</param>
        /// <returns>The <see cref="Message" /> received.</returns>
        public Message EndReceive(IAsyncResult result)
        {
            AsyncResult<Message, InputChannel> arReceive;

            arReceive = result as AsyncResult<Message, InputChannel>;
            if (arReceive != null)
            {
                // Operation completed in BeginReceive() above.

                arReceive.Wait();
                try
                {
                    if (arReceive.Exception != null)
                        throw arReceive.Exception;

                    return arReceive.Result;
                }
                finally
                {
                    arReceive.Dispose();
                }
            }

            return listener.EndReceive(result);
        }

        /// <summary>
        /// Synchronously attempts to receive a message within the specified period of time,
        /// returning a boolean indicating success or failure rather than
        /// throwning exceptions on an error.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="message">Returns as the <see cref="Message" /> received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a message was received.</returns>
        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            IAsyncResult ar;

            ar = BeginTryReceive(timeout, null, null);
            return EndTryReceive(ar, out message);
        }

        /// <summary>
        /// Initiates an asynchronous attempt to receive a message within the 
        /// specified period of time, where a boolean will ultimately be returned
        /// indicating success or failure rather than throwning an exception if
        /// an error is encountered.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait for the message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// All successful calls to <see cref="BeginTryReceive" /> must eventually be followed by a
        /// call to <see cref="EndTryReceive" />.
        /// </remarks>
        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return true (message=null).

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<Message, InputChannel> arReceive;   // Note that TInternal==InputChannel.  This is used below in EndTryReceive()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arReceive = new AsyncResult<Message, InputChannel>(null, callback, state);
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }

                // If the channel already has a message queued, then setup to return it.

                if (msgQueue.Count > 0)
                {
                    AsyncResult<Message, InputChannel> arReceive;   // Note that TInternal==InputChannel.  This is used below in EndTryReceive()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arReceive        = new AsyncResult<Message, InputChannel>(null, callback, state);
                    arReceive.Result = msgQueue.Dequeue();
                    arReceive.Started(ServiceModelHelper.AsyncTrace);
                    arReceive.Notify();
                    return arReceive;
                }
            }

            return listener.BeginTryReceive(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous message receive operation initiated by <see cref="BeginTryReceive" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTryReceive" />.</param>
        /// <param name="message">Returns as the message received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a message was received.</returns>
        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            AsyncResult<Message, InputChannel> arReceive;

            arReceive = result as AsyncResult<Message, InputChannel>;
            if (arReceive != null)
            {
                // Operation completed in BeginTryReceive() above.

                message = null;
                arReceive.Wait();
                try
                {
                    if (arReceive.Exception != null)
                        return false;

                    message = arReceive.Result;
                    return true;
                }
                finally
                {
                    arReceive.Dispose();
                }
            }

            return listener.EndTryReceive(result, out message);
        }

        /// <summary>
        /// Synchronously waits for a specified period of time for a message to be received 
        /// or queued internally by the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <returns><c>true</c> if the channel has queued a received message.</returns>
        public bool WaitForMessage(TimeSpan timeout)
        {
            IAsyncResult ar;

            ar = BeginWaitForMessage(timeout, null, null);
            return EndWaitForMessage(ar);
        }

        /// <summary>
        /// Begins an asynchronous operation to wait for a specified period of time for a message to be received 
        /// or queued internally by the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// All successful calls to <see cref="BeginWaitForMessage" /> must eventually be followed by a call to <see cref="EndWaitForMessage" />.
        /// </remarks>
        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                // Non-open channels always return false.

                if (base.State != CommunicationState.Opened)
                {
                    AsyncResult<bool, InputChannel> arWait;         // Note that TInternal==InputChannel.  This is used below in EndWaitForMessage()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arWait = new AsyncResult<bool, InputChannel>(null, callback, state);
                    arWait.Result = false;
                    arWait.Started(ServiceModelHelper.AsyncTrace);
                    arWait.Notify();
                    return arWait;
                }

                // If the channel already has a message queued, then setup to return it.

                if (msgQueue.Count > 0)
                {
                    AsyncResult<bool, InputChannel> arWait;         // Note that TInternal==InputChannel.  This is used below in EndWaitForMessage()
                                                                    // to distinguish between IAsyncResults returned by this class and those
                                                                    // returned by the listener.                                    

                    arWait = new AsyncResult<bool, InputChannel>(null, callback, state);
                    arWait.Result = true;
                    arWait.Started(ServiceModelHelper.AsyncTrace);
                    arWait.Notify();
                    return arWait;
                }
            }

            timeout = ServiceModelHelper.ValidateTimeout(timeout);
            return listener.BeginWaitForMessage(this, timeout, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by <see cref="BeginWaitForMessage" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWaitForMessage" />.</param>
        /// <returns><c>true</c> if the channel has queued a received message.</returns>
        public bool EndWaitForMessage(IAsyncResult result)
        {
            AsyncResult<bool, InputChannel> arWait;

            arWait = result as AsyncResult<bool, InputChannel>;
            if (arWait != null)
            {
                // Operation completed in BeginWaitForMessage() above.

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

            return listener.EndWaitForMessage(result);
        }
    }
}
