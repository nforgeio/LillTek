//-----------------------------------------------------------------------------
// FILE:        InputChannelListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an IChannelListener capable of accepting 
//              InputChannels via LillTek Messaging.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Messaging;
using LillTek.ServiceModel;

// $todo(jeff.lill): 
//
// Hardcoding the maximum number of messages queued by InputChannel
// and BkTaskInterval too.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements an <see cref="IChannelListener" /> capable of accepting 
    /// <see cref="InputChannel" />s via LillTek Messaging.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Implementation Note</u></b></para>
    /// <para>
    /// This <see cref="InputChannelListener" /> listener is very simple.  All it has to do
    /// is override <see cref="OnMessageReceived" /> so that the base <see cref="LillTekChannelListener{TChannel,TInternal}" />
    /// class can submit the received messages to the derived class.  If the listener has not yet accepted
    /// a channel one is created, adding the message received to its queue and the new
    /// channel is passed back to the base <see cref="LillTekChannelListener{TChannel,TInternal}.OnChannelCreated" /> method so
    /// any pending <b>WaitForChannel()</b> and <b>AcceptChannel()</b> operations will be
    /// completed.
    /// </para>
    /// <para>
    /// Any messages received are queued internally by the <see cref="InputChannelListener" />
    /// and are made available to the listener's channels via the <see cref="BeginReceive" />
    /// and <see cref="EndReceive" /> methods.
    /// </para>
    /// </remarks>
    internal sealed class InputChannelListener : LillTekChannelListener<IInputChannel, InputChannel>
    {
        private LimitedQueue<Message>                           msgQueue;               // Received message queue
        private QueueArray<AsyncResult<bool, InputChannel>>     waitQueue;              // Queue of pending channel WaitForMessage() requests
        private QueueArray<AsyncResult<Message, InputChannel>>  receiveQueue;           // Queue of pending channel Receive() requests
        private int                                             maxReceiveQueueSize;    // Maximum number of queued messages
        private TimeSpan                                        bkTaskInterval;         // Channel background task interval

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        internal InputChannelListener(BindingContext context)
            : base(context)
        {
            this.maxReceiveQueueSize = ServiceModelHelper.MaxAcceptedMessages;      // $todo(jeff.lill): Hardcoded
            this.bkTaskInterval      = ServiceModelHelper.DefaultBkTaskInterval;    //                   This too

            this.msgQueue            = new LimitedQueue<Message>(maxReceiveQueueSize);
            this.waitQueue           = new QueueArray<AsyncResult<bool, InputChannel>>();
            this.receiveQueue        = new QueueArray<AsyncResult<Message, InputChannel>>();
        }

        /// <summary>
        /// Returns the queue of pending <b>WaitForMessage()</b> operations.
        /// </summary>
        public QueueArray<AsyncResult<bool, InputChannel>> WaitForMessageQueue
        {
            get { return waitQueue; }
        }

        /// <summary>
        /// Returns the queue of pending <b>Receive()</b> message operations.
        /// </summary>
        public QueueArray<AsyncResult<Message, InputChannel>> ReceiveQueue
        {
            get { return receiveQueue; }
        }

        /// <summary>
        /// Begins an asynchronous operation to wait for a specified period of time for a message to be received 
        /// for a channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// All successful calls to <see cref="BeginWaitForMessage" /> must eventually be followed by a call to <see cref="EndWaitForMessage" />.
        /// </remarks>
        public IAsyncResult BeginWaitForMessage(InputChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<bool, InputChannel> arWait;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arWait = new AsyncResult<bool, InputChannel>(null, callback, state);
                arWait.TTD = SysTime.Now + timeout;
                arWait.InternalState = channel;
                arWait.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return false.

                if (base.State != CommunicationState.Opened)
                {
                    arWait.Notify();
                    return arWait;
                }

                // If we already have a queued message, dequeue it and add it to the 
                // channel's message queue, so a subsequent call Receive() on the channel 
                // will be assured to succeed.  Then notify that the operation is complete.

                if (msgQueue.Count > 0)
                {
                    channel.Enqueue(msgQueue.Dequeue());

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
        /// Completes an asynchronous operation initiated by <see cref="BeginWaitForMessage" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWaitForMessage" />.</param>
        /// <returns><c>true</c> if the channel has queued a received message.</returns>
        public bool EndWaitForMessage(IAsyncResult result)
        {
            AsyncResult<bool, InputChannel> arWait = (AsyncResult<bool, InputChannel>)result;

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
        /// Begins an asynchronous operation to receive a channel message (using a specified timeout).
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The <see cref="TimeSpan" /> value specifying the maximum time to wait for a message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginReceive" /> must eventually be followed by a call to <see cref="EndReceive" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceive(InputChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<Message, InputChannel> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<Message, InputChannel>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.InternalState = channel;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return null.

                if (base.State != CommunicationState.Opened)
                {
                    arReceive.Result = null;
                    arReceive.Notify();
                    return arReceive;
                }

                // Check to see if we already have a queued message.

                if (msgQueue.Count > 0)
                {
                    arReceive.Result = msgQueue.Dequeue();
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous channel message receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <see cref="BeginReceive" />.</param>
        /// <returns>The <see cref="Message" /> received.</returns>
        public Message EndReceive(IAsyncResult result)
        {
            AsyncResult<Message, InputChannel> arReceive = (AsyncResult<Message, InputChannel>)result;

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
        /// Initiates an asynchronous attempt to receive a message within the 
        /// specified period of time, where a boolean will ultimately be returned
        /// indicating success or failure rather than throwning an exception if
        /// an error is encountered.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="timeout">The <see cref="TimeSpan" /> value specifying the maximum time to wait for a message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginTryReceive" /> must eventually be followed by a call to <see cref="EndTryReceive" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginTryReceive(InputChannel channel, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<Message, InputChannel> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<Message, InputChannel>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.InternalState = channel;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return true (message=null).

                if (base.State != CommunicationState.Opened)
                {
                    arReceive.Notify();
                    return arReceive;
                }

                // Check to see if we already have a queued message.

                if (msgQueue.Count > 0)
                {
                    arReceive.Result = msgQueue.Dequeue();
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous channel message try-receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by <see cref="BeginTryReceive" />.</param>
        /// <param name="message">Returns as the <see cref="Message" /> received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a message was received.</returns>
        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            AsyncResult<Message, InputChannel> arReceive = (AsyncResult<Message, InputChannel>)result;

            message = null;
            Assertion.Test(arReceive.InternalState != null, "InternalState should have been set to the channel.");
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

            // Scan the queued receive requests for any that have timed-out.

            if (receiveQueue != null && receiveQueue.Count > 0)
            {
                do
                {
                    AsyncResult<Message, InputChannel> arReceive;

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

            // Scan the queued wait requests for any that have timed-out.

            if (waitQueue != null && waitQueue.Count > 0)
            {
                do
                {
                    AsyncResult<bool, InputChannel> arWait;

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
        // CommunicationObject overrides

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnClosing()
        {
            using (TimedLock.Lock(this))
            {
                // Abort pending WaitForMessage() operations

                if (waitQueue != null)
                {
                    while (waitQueue.Count > 0)
                        waitQueue.Dequeue().Notify();
                }

                // Abort pending Receive() operations

                if (receiveQueue != null)
                {
                    while (receiveQueue.Count > 0)
                        receiveQueue.Dequeue().Notify();
                }
            }

            base.OnClosing();
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
        /// any pending channel <b>WaitForMessage()</b> or <b>Receive()</b> requests.
        /// </para>
        /// <para>
        /// If there are pending message receive operations, then these will be completed
        /// and the message queued to the associated channel as is appropriate.
        /// </para>
        /// <para>
        /// Finally, if no pending message receive requests and the base class has a 
        /// pending <b>WaitForChannel()</b> or <b>AcceptChannel()</b>, then the base class
        /// <see cref="LillTekChannelListener{IInputSessionChannel,InputSessionChannel}.OnChannelCreated" /> 
        /// method will be called so that a new channel will be accepted.
        /// </para>
        /// <para>
        /// Finally, if there are no pending message receive requests or base channel
        /// channel accept related requests, the message will be queued internally.
        /// </para>
        /// </remarks>
        protected override void OnMessageReceived(Message message, WcfEnvelopeMsg msg)
        {
            InputChannel newChannel = null;

            if (base.State != CommunicationState.Opened)
                return;

            if (msg._SessionID != Guid.Empty)
                return;     // Reject messages that are part of a session

            using (TimedLock.Lock(this))
            {
                // Handle any pending channel Receive() operations first.

                if (receiveQueue.Count > 0)
                {

                    AsyncResult<Message, InputChannel> arReceive;

                    arReceive = receiveQueue.Dequeue();
                    arReceive.Result = message;
                    arReceive.Notify();
                    return;
                }

                // Next, handle any pending channel WaitForMessage() operations.

                if (waitQueue.Count > 0)
                {
                    AsyncResult<bool, InputChannel> arWait;

                    // Queue the message to the input channel so it will assured
                    // to be available when the WaitForMessage() completes and
                    // the application calls Receive().

                    arWait = waitQueue.Dequeue();
                    arWait.Result = true;
                    arWait.InternalState.Enqueue(message);
                    arWait.Notify();
                    return;
                }

                // Queue the message.

                msgQueue.Enqueue(message);

                // Create new channel if there are pending channel accept 
                // or wait operations.

                if (base.HasPendingChannelOperation)
                {
                    newChannel = new InputChannel(this, new EndpointAddress(this.Uri));
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
        protected override InputChannel GetAcceptChannel()
        {
            TimedLock.AssertLocked(this);   // Verify the lock

            // Accept a channel if there are any queued messages.

            if (msgQueue.Count > 0)
            {
                InputChannel channel;

                channel = new InputChannel(this, new EndpointAddress(this.Uri));     // I explicitly decided not to queue the message
                // to the new channel
                return channel;
            }
            else
                return null;
        }
    }
}
