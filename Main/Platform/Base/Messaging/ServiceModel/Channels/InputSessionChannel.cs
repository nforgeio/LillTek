//-----------------------------------------------------------------------------
// FILE:        InputSessionChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IInputSessionChannel.

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
    /// transport using LillTek Messaging to implement <see cref="IInputSessionChannel" />.
    /// </summary>
    internal class InputSessionChannel : LillTekChannelBase, IInputSessionChannel, IInputSession
    {
        private InputSessionChannelListener                 listener;               // The listener class managing this channel
        private EndpointAddress                             localAddress;           // Address on which the channel receives messages
        private Queue<Message>                              msgQueue;               // Queue of messages waiting to be received
        private QueueArray<AsyncResult<bool, object>>       waitQueue;              // Queue of pending channel WaitForMessage() requests
        private QueueArray<AsyncResult<Message, object>>    receiveQueue;           // Queue of pending channel Receive() requests
        private int                                         maxReceiveQueueSize;    // Maximum number of queued messages
        private TimeSpan                                    bkTaskInterval;         // The background task interval
        private DuplexSession                               session;                // The underlying LillTek session

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="localAddress">The local <see cref="EndpointAddress" /> this channel will use to receive requests.</param>
        /// <param name="sessionID">The globally unique session ID for this channel.</param>
        /// <param name="session">The underlying LillTek <see cref="DuplexSession" />.</param>
        public InputSessionChannel(ChannelManagerBase channelManager, EndpointAddress localAddress, string sessionID, DuplexSession session)
            : base(channelManager, sessionID)
        {
            this.maxReceiveQueueSize = ServiceModelHelper.MaxAcceptedMessages;      // $todo(jeff.lill): Hardcoding this
            this.bkTaskInterval      = ServiceModelHelper.DefaultBkTaskInterval;    //                   and this

            this.listener            = (InputSessionChannelListener)channelManager;
            this.localAddress        = localAddress;
            this.msgQueue            = new LimitedQueue<Message>(maxReceiveQueueSize);
            this.waitQueue           = new QueueArray<AsyncResult<bool, object>>();
            this.receiveQueue        = new QueueArray<AsyncResult<Message, object>>();
            this.session             = session;

            // Initialize the underlying LillTek session event handlers

            session.ReceiveEvent += new DuplexReceiveDelegate(OnSessionReceive);
            session.QueryEvent   += new DuplexQueryDelegate(OnSessionQuery);
            session.CloseEvent   += new DuplexCloseDelegate(OnSessionClose);
        }

        /// <summary>
        /// Adds a message to the channel's receive queue, completing any
        /// pending <b>WaitForMessage()</b> operations.
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

                if (receiveQueue != null && receiveQueue.Count > 0)
                {
                    AsyncResult<Message, object> arReceive;

                    arReceive = receiveQueue.Dequeue();
                    arReceive.Result = message;

                    arReceive.Notify();
                    return;
                }

                // There were no pending receive operations so queue the message.

                msgQueue.Enqueue(message);

                // Complete the first pending WaitForMessage() request if one is queued.

                if (waitQueue != null && waitQueue.Count > 0)
                {
                    AsyncResult<bool, object> arWait;

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
        // ISession and IInputSession implementation

        /// <summary>
        /// Returns the globally unique identifier of the session for this channel.
        /// </summary>
        public IInputSession Session
        {
            get { return this; }
        }

        /// <summary>
        /// Returns the globally unique session ID.
        /// </summary>
        public string Id
        {
            get { return base.ID; }
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
            return bkTaskInterval;
        }

        /// <summary>
        /// Called periodically on a background thread and within a <see cref="TimedLock" />
        /// if <see cref="GetBackgroundTaskInterval()" /> returned a positive interval.
        /// </summary>
        protected override void OnBkTask()
        {
            TimedLock.AssertLocked(this);   // Verify the lock

            // Terminate any pending operations that have exceeded their timeout.

            DateTime now = SysTime.Now;

            while (waitQueue.Count > 0 && waitQueue.Peek().TTD <= now)
                waitQueue.Dequeue().Notify(new TimeoutException());

            while (receiveQueue.Count > 0 && receiveQueue.Peek().TTD <= now)
                receiveQueue.Dequeue().Notify(new TimeoutException());
        }

        /// <summary>
        /// Terminates all pending operations with the exception passed.
        /// </summary>
        /// <param name="e">The termination exception.</param>
        protected override void TerminatePendingOperations(Exception e)
        {
            using (TimedLock.Lock(this))
            {
                if (waitQueue != null)
                    while (waitQueue.Count > 0)
                        waitQueue.Dequeue().Notify();

                if (receiveQueue != null)
                    while (receiveQueue.Count > 0)
                        receiveQueue.Dequeue().Notify();

                if (session != null)
                    session.Close();
            }
        }

        //---------------------------------------------------------------------
        // IInputSessionChannel implementation

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
        /// blocks for a specified interval of time and waits for a message.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a message.</param>
        /// <returns>The <see cref="Message" /> received or <c>null</c> if the remote side of the session has been closed.</returns>
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
            AsyncResult<Message, object> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<Message, object>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return null

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

                // Setup to return null if the remote side of the connection
                // has been closed.

                if (!session.IsConnected)
                {
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous message receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by one of the <b>BeginReceive()</b> overrides.</param>
        /// <returns>The <see cref="Message" /> received or <c>null</c> if the remote side of the session has been closed.</returns>
        public Message EndReceive(IAsyncResult result)
        {
            AsyncResult<Message, object> arReceive = (AsyncResult<Message, object>)result;

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
            AsyncResult<Message, object> arReceive;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arReceive = new AsyncResult<Message, object>(null, callback, state);
                arReceive.TTD = SysTime.Now + timeout;
                arReceive.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return true (message=null)

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

                // Setup to return null if the remote side of the connection
                // has been closed.

                if (!session.IsConnected)
                {
                    arReceive.Notify();
                    return arReceive;
                }

                // Otherwise queue the receive operation.

                receiveQueue.Enqueue(arReceive);
                return arReceive;
            }
        }

        /// <summary>
        /// Completes an asynchronous message receive operation initiated by <see cref="BeginTryReceive" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTryReceive" />.</param>
        /// <param name="message">Returns as the message received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a message was received.</returns>
        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            AsyncResult<Message, object> arReceive = (AsyncResult<Message, object>)result;

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
            AsyncResult<bool, object> arWait;

            using (TimedLock.Lock(this))
            {
                timeout = ServiceModelHelper.ValidateTimeout(timeout);

                arWait = new AsyncResult<bool, object>(null, callback, state);
                arWait.TTD = SysTime.Now + timeout;
                arWait.Started(ServiceModelHelper.AsyncTrace);

                // Non-open channels always return false

                if (base.State != CommunicationState.Opened)
                {
                    arWait.Result = false;
                    arWait.Notify();
                    return arWait;
                }

                // Check to see if we already have a queued message.

                if (msgQueue.Count > 0)
                {
                    arWait.Result = true;
                    arWait.Notify();
                    return arWait;
                }

                // Setup to return false if the remote side of the session
                // has been closed.

                if (!session.IsConnected)
                {
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
            AsyncResult<bool, object> arWait = (AsyncResult<bool, object>)result;

            arWait.Wait();
            try
            {
                if (arWait.Exception != null)
                    return false;

                return arWait.Result;
            }
            finally
            {
                arWait.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // DuplexSession event handlers

        /// <summary>
        /// Handles messages received on the underlying LillTek <see cref="DuplexSession" /> session.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" />.</param>
        /// <param name="msg">The received LillTek message.</param>
        private void OnSessionReceive(DuplexSession session, Msg msg)
        {
            WcfEnvelopeMsg envelopeMsg = msg as WcfEnvelopeMsg;

            if (envelopeMsg == null)
                return;     // Discard anything but WCF messages

            Enqueue(listener.DecodeMessage(envelopeMsg));
        }

        /// <summary>
        /// Handles querues received on the underlying LillTek <see cref="DuplexSession" /> session.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" />.</param>
        /// <param name="msg">The received LillTek query message.</param>
        /// <param name="isAsync">Returns as <c>true</c> if the query will be completed asynchronously.</param>
        private Msg OnSessionQuery(DuplexSession session, Msg msg, out bool isAsync)
        {
            // This channel type does not support queries.

            throw new NotImplementedException(string.Format("[{0}] does not support queries.", this.GetType().FullName));
        }

        /// <summary>
        /// Handles remote side closure of the underlying LillTek <see cref="DuplexSession" />.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" />.</param>
        /// <param name="timeout">
        /// <c>true</c> if the session closed due to a keep-alive timeout,
        /// <c>false</c> if the remote side explicitly closed its side
        /// of the session.
        /// </param>
        private void OnSessionClose(DuplexSession session, bool timeout)
        {
            using (TimedLock.Lock(this))
            {
                // Terminate any pending Wait() operations with false.

                if (waitQueue != null)
                    while (waitQueue.Count > 0)
                        waitQueue.Dequeue().Notify();

                // Terminate any pending Receive() operations with null.

                if (receiveQueue != null)
                    while (receiveQueue.Count > 0)
                        receiveQueue.Dequeue().Notify();
            }
        }
    }
}
