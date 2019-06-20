//-----------------------------------------------------------------------------
// FILE:        DuplexChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IDuplexChannel.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Extends Windows Communication Foundation, adding a custom
    /// transport using LillTek Messaging to implement <see cref="IDuplexChannel" />.
    /// </summary>
    internal class DuplexChannel : LillTekChannelBase, IDuplexChannel
    {
        private EndpointAddress     localAddress  = null;
        private EndpointAddress     remoteAddress = null;
        private Uri                 via           = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="localAddress">The local <see cref="EndpointAddress" />.</param>
        /// <param name="remoteAddress">The remote <see cref="EndpointAddress" />.</param>
        /// <param name="via">The first transport hop <see cref="Uri" />.</param>
        /// <param name="encoder">The <see cref="MessageEncoder" /> for serializing messages to the wire format.</param>
        public DuplexChannel(ChannelManagerBase channelManager, EndpointAddress localAddress,
                             EndpointAddress remoteAddress, Uri via,
                             MessageEncoder encoder)
            : base(channelManager)
        {
            this.localAddress  = localAddress;
            this.remoteAddress = remoteAddress;
            this.via           = via;
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
        }

        //---------------------------------------------------------------------
        // CommunicationObject implementation

        /// <summary>
        /// Inserts processing on a communication object after it transitions to 
        /// the closing state due to the invocation of a synchronous abort operation.
        /// </summary>
        protected override void OnAbort()
        {
            base.OnAbort();
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the 
        /// closing state due to the invocation of a synchronous close operation.
        /// </summary>
        /// <param name="timeout">
        /// The <see cref="TimeSpan" /> that specifies how long the on close 
        /// operation has to complete before timing out.
        /// </param>
        protected override void OnClose(TimeSpan timeout)
        {
            base.OnClose(timeout);
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the closing state.
        /// </summary>
        protected override void OnClosing()
        {
            base.OnClosing();
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the closing state.
        /// </summary>
        protected override void OnClosed()
        {
            base.OnClosed();
        }

        /// <summary>
        /// Inserts processing after a communication object transitions to the closing state 
        /// due to the invocation of an asynchronous close operation.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on close operation has to complete before timing out.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return null;
        }

        /// <summary>
        /// Completes an asynchronous operation on the close of a communication object.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="OnBeginClose" />.</param>
        protected override void OnEndClose(IAsyncResult result)
        {
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions into the opening state which 
        /// must complete within a specified interval of time.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on open operation has to complete before timing out.</param>
        protected override void OnOpen(TimeSpan timeout)
        {
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the opening state.
        /// </summary>
        protected override void OnOpening()
        {
            base.OnOpening();
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the opened state.
        /// </summary>
        protected override void OnOpened()
        {
            base.OnOpened();
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the opening 
        /// state due to the invocation of an asynchronous open operation.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on open operation has to complete before timing out.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return null;
        }

        /// <summary>
        /// Completes an asynchronous operation to open a communication object.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="OnBeginOpen" />.</param>
        protected override void OnEndOpen(IAsyncResult result)
        {
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the faulted state 
        /// due to the invocation of a synchronous fault operation.
        /// </summary>
        protected override void OnFaulted()
        {
            base.OnFaulted();
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
        // IInputChannel implementation

        /// <summary>
        /// Returns the <see cref="EndpointAddress" /> on which the input channel receives messages.
        /// </summary>
        EndpointAddress IInputChannel.LocalAddress
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
            return null;
        }

        /// <summary>
        /// Returns the message received, if one is available. If a message is not available, 
        /// blocks for a specified interval of time and waits for a message.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a message.</param>
        /// <returns>The received <see cref="Message" />.</returns>
        public Message Receive(TimeSpan timeout)
        {
            return null;
        }

        /// <summary>
        /// Begins an asynchronous operation to receive a message (while waiting indefinitely).
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
            return null;
        }

        /// <summary>
        /// Begins an asynchronous operation to receive a message (while waiting a limited amount of time).
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
            return null;
        }

        /// <summary>
        /// Completes an asynchronous message receive operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> returned by one of the <b>BeginReceive()</b> overrides.</param>
        /// <returns>The <see cref="Message" /> received.</returns>
        public Message EndReceive(IAsyncResult result)
        {
            return null;
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
            message = null;
            return false;
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
            return null;
        }

        /// <summary>
        /// Completes an asynchronous message receive operation initiated by <see cref="BeginTryReceive" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTryReceive" />.</param>
        /// <param name="message">Returns as the message received (or <c>null</c>).</param>
        /// <returns><c>true</c> if a message was received.</returns>
        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            message = null;
            return false;
        }

        /// <summary>
        /// Synchronously waits for a specified period of time for a message to be received 
        /// or queued internally by the channel.
        /// </summary>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <returns><c>true</c> if the channel has queued a received message.</returns>
        public bool WaitForMessage(TimeSpan timeout)
        {
            return false;
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
            return null;
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by <see cref="BeginWaitForMessage" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWaitForMessage" />.</param>
        /// <returns><c>true</c> if the channel has queued a received message.</returns>
        public bool EndWaitForMessage(IAsyncResult result)
        {
            return false;
        }

        //---------------------------------------------------------------------
        // IOutputChannel implementation

        /// <summary>
        /// Returns the ultimate destination <see cref="EndpointAddress" /> where the request messages will be sent.
        /// </summary>
        public EndpointAddress RemoteAddress
        {
            get { return remoteAddress; }
        }

        /// <summary>
        /// Returns the physical remote address where request messages will be sent.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The LillTek messaging layer implements its own message routing scheme.
        /// This property will be ignored.
        /// </note>
        /// </remarks>
        public Uri Via
        {
            get { return via; }
        }

        /// <summary>
        /// Synchronously sends a message on the output channel.
        /// </summary>
        /// <param name="message">The <see cref="Message" />.</param>
        /// <remarks>
        /// <note>
        /// This method does not guarantee delivery of the message.
        /// Messages can be silently dropped for reasons including lack of buffer
        /// space, network congestion, unavailable remote endpoint, etc.
        /// </note>
        /// </remarks>
        public void Send(Message message)
        {
        }

        /// <summary>
        /// Synchronously sends a message on the output channel, waiting a
        /// maximum amount of time for the message to be sent.
        /// </summary>
        /// <param name="message">The <see cref="Message" />.</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <remarks>
        /// <note>
        /// This method does not guarantee delivery of the message.
        /// Messages can be silently dropped for reasons including lack of buffer
        /// space, network congestion, unavailable remote endpoint, etc.
        /// </note>
        /// </remarks>
        public void Send(Message message, TimeSpan timeout)
        {
        }

        /// <summary>
        /// Initiates an asynchronous operation to send a message on the output channel.
        /// </summary>
        /// <param name="message">The <see cref="Message" />.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// This method does not guarantee delivery of the message.
        /// Messages can be silently dropped for reasons including lack of buffer
        /// space, network congestion, unavailable remote endpoint, etc.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginSend(Message,AsyncCallback,object)" /> must eventually be followed by a
        /// call to <see cref="EndSend" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            return null;
        }

        /// <summary>
        /// Initiates an asynchronous operation to send a message on the output channel, waiting a
        /// maximum amount of time for the message to be sent.
        /// </summary>
        /// <param name="message">The <see cref="Message" />.</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// This method does not guarantee delivery of the message.
        /// Messages can be silently dropped for reasons including lack of buffer
        /// space, network congestion, unavailable remote endpoint, etc.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginSend(Message,TimeSpan,AsyncCallback,object)" /> must eventually be followed by a
        /// call to <see cref="EndSend" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return null;
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by one of the <b>BeginSend()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <b>BeginSend()</b>.</param>
        public void EndSend(IAsyncResult result)
        {
        }
    }
}
