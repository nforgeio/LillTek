//-----------------------------------------------------------------------------
// FILE:        OutputChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IOutputChannel.

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
    /// transport using LillTek Messaging to implement <see cref="IOutputChannel" />.
    /// </summary>
    internal class OutputChannel : LillTekChannelBase, IOutputChannel
    {
        private EndpointAddress         remoteAddress;      // Target remote address
        private Uri                     via;                // First hop address (always ignored internally)
        private MessageEncoder          encoder;            // The message encoder
        private MsgEP                   ep;                 // Target LillTek Messaging endpoint
        private PayloadSizeEstimator    payloadEstimator;   // Used to estimate the buffer required to serialize
                                                            // the next message sent
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="remoteAddress">The remote <see cref="EndpointAddress" />.</param>
        /// <param name="via">The first transport hop <see cref="Uri" />.</param>
        /// <param name="encoder">The <see cref="MessageEncoder" /> for serializing messages to the wire format.</param>
        public OutputChannel(ChannelManagerBase channelManager, EndpointAddress remoteAddress, Uri via, MessageEncoder encoder)
            : base(channelManager)
        {
            ServiceModelHelper.ValidateEP(remoteAddress.Uri);
            ServiceModelHelper.ValidateEP(via);

            this.remoteAddress    = remoteAddress;
            this.via              = via;
            this.encoder          = encoder;
            this.ep               = ServiceModelHelper.ToMsgEP(remoteAddress.Uri);
            this.payloadEstimator = new PayloadSizeEstimator(ServiceModelHelper.PayloadEstimatorSampleCount);
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
            // Nothing to terminate for this channel type.
        }

        //---------------------------------------------------------------------
        // IOutputChannel implementation

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
                    ChannelHost.Router.SendTo(ep, envelopeMsg);
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
    }
}
