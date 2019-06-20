//-----------------------------------------------------------------------------
// FILE:        OutputSessionChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IOutputSessionChannel.

using System;
using System.IO;
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
    /// transport using LillTek Messaging to implement <see cref="IOutputSessionChannel" />.
    /// </summary>
    internal class OutputSessionChannel : LillTekChannelBase, IOutputSessionChannel, IOutputSession
    {
        private EndpointAddress         remoteAddress;      // Target remote address
        private Uri                     via;                // First hop address (always ignored internally)
        private MessageEncoder          encoder;            // The message encoder
        private MsgEP                   ep;                 // Target LillTek Messaging endpoint
        private PayloadSizeEstimator    payloadEstimator;   // Used to estimate the buffer required to serialize
                                                            // the next message sent
        private DuplexSession           session;            // Underlying LillTek Messaging session

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="remoteAddress">The remote <see cref="EndpointAddress" />.</param>
        /// <param name="via">The first transport hop <see cref="Uri" />.</param>
        /// <param name="encoder">The <see cref="MessageEncoder" /> for serializing messages to the wire format.</param>
        public OutputSessionChannel(ChannelManagerBase channelManager, EndpointAddress remoteAddress, Uri via, MessageEncoder encoder)
            : base(channelManager)
        {
            ServiceModelHelper.ValidateEP(remoteAddress.Uri);
            ServiceModelHelper.ValidateEP(via);

            this.ep = ServiceModelHelper.ToMsgEP(remoteAddress.Uri);
            if (ep.Broadcast)
                throw new ArgumentException("Sessionful channels cannot accept broadcast endpoints.", "remoteAddress");

            this.remoteAddress    = remoteAddress;
            this.via              = via;
            this.encoder          = encoder;
            this.payloadEstimator = new PayloadSizeEstimator(ServiceModelHelper.PayloadEstimatorSampleCount);
            this.session          = ChannelHost.Router.CreateDuplexSession();
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
        public IOutputSession Session
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
        // CommunicationObject implementation

        /// <summary>
        /// Closes the channel.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        /// <remarks>
        /// The base class implementation does nothing.
        /// </remarks>
        protected override void OnClose(TimeSpan timeout)
        {
            session.Close();
        }

        /// <summary>
        /// Inserts processing after a communication object transitions to the closing state 
        /// due to the invocation of an asynchronous close operation.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on close operation has to complete before timing out.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        /// <remarks>
        /// The base class implementation initiates an asynchronous operation that will
        /// complete immediately on another thread.
        /// </remarks>
        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult arClose;

            ServiceModelHelper.ValidateTimeout(timeout);
            session.Close();

            arClose = new AsyncResult(null, callback, state);
            arClose.Started(ServiceModelHelper.AsyncTrace);
            arClose.Notify();
            return arClose;
        }

        /// <summary>
        /// Completes an asynchronous operation on the close of a communication object.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="OnBeginClose" />.</param>
        /// <remarks>
        /// The base class implementation completes the asynchronous NOP initiated by <see cref="OnBeginClose" />.
        /// </remarks>
        protected override void OnEndClose(IAsyncResult result)
        {
            AsyncResult arClose = (AsyncResult)result;

            arClose.Wait();
            try
            {
                if (arClose.Exception != null)
                    throw arClose.Exception;
            }
            finally
            {
                arClose.Dispose();
            }
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions into the opening state which 
        /// must complete within a specified interval of time.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on open operation has to complete before timing out.</param>
        /// <remarks>
        /// The base class implementation does nothing.
        /// </remarks>
        protected override void OnOpen(TimeSpan timeout)
        {
            IAsyncResult ar;

            ar = OnBeginOpen(timeout, null, null);
            OnEndOpen(ar);
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the opening 
        /// state due to the invocation of an asynchronous open operation.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan" /> that specifies how long the on open operation has to complete before timing out.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// The base class implementation initiates an asynchronous operation that will
        /// complete immediately on another thread.
        /// </remarks>
        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            ServiceModelHelper.ValidateTimeout(timeout);

            session.ReceiveEvent += new DuplexReceiveDelegate(OnSessionReceive);
            session.QueryEvent += new DuplexQueryDelegate(OnSessionQuery);
            session.CloseEvent += new DuplexCloseDelegate(OnSessionClose);

            return session.BeginConnect(ep, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous operation to open a communication object.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="OnBeginOpen" />.</param>
        /// <remarks>
        /// The base class implementation completes the asynchronous NOP initiated by <see cref="OnBeginOpen" />.
        /// </remarks>
        protected override void OnEndOpen(IAsyncResult result)
        {
            session.EndConnect(result);
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
            if (session != null)
                session.Close();
        }

        //---------------------------------------------------------------------
        // IOutputSessionChannel implementation

        // Implementation Note:
        //
        // LillTek MsgRouter.Send() method is inherently asynchronous so the asynchronous
        // Send() methods below complete immediately but still call the completion delegate
        // on another thread.

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
            try
            {
                using (MemoryStream ms = new MemoryStream(payloadEstimator.EstimateNextBufferSize()))
                {
                    WcfEnvelopeMsg envelopeMsg = new WcfEnvelopeMsg();

                    encoder.WriteMessage(message, ms);
                    payloadEstimator.LastPayloadSize((int)ms.Length);

                    envelopeMsg.Payload = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
                    session.Send(envelopeMsg);
                }
            }
            catch (Exception e)
            {
                throw ServiceModelHelper.GetCommunicationException(e);
            }
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
            ServiceModelHelper.ValidateTimeout(timeout);

            Send(message);
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
            AsyncResult arSend;

            Send(message);

            arSend = new AsyncResult(null, callback, state);
            arSend.Started(ServiceModelHelper.AsyncTrace);
            arSend.Notify();
            return arSend;
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
            ServiceModelHelper.ValidateTimeout(timeout);
            return BeginSend(message, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by one of the <b>BeginSend()</b> overrides.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <b>BeginSend()</b>.</param>
        public void EndSend(IAsyncResult result)
        {
            AsyncResult arSend = (AsyncResult)result;

            arSend.Wait();
            try
            {
                if (arSend.Exception != null)
                    throw ServiceModelHelper.GetCommunicationException(arSend.Exception);
            }
            finally
            {
                arSend.Dispose();
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
            throw new InvalidOperationException(string.Format("[{0}] does not support message reception.", this.GetType().FullName));
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
            // This is a NOP for this channel type.
        }
    }
}
