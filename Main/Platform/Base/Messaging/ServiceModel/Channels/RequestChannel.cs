//-----------------------------------------------------------------------------
// FILE:        RequestChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends Windows Communication Foundation, adding a custom
//              transport using LillTek Messaging to implement IRequestChannel.

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

// $todo(jeff.lill): 
//
// This class should implement Close(timeout) by giving any query
// processing the chance to complete before the channel factory
// aborts the channel.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Extends Windows Communication Foundation, adding a custom
    /// transport using LillTek Messaging to implement <see cref="IRequestChannel" />.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The <b>Request()</b> related methods ignore the <b>timeout</b> values passed to them
    /// and use the default timeout specified by the LillTek <see cref="MsgRouter" />.  This
    /// should not be a problem in most cases since LillTek Messaging servers send internal
    /// keep-alive messages back to the query source while the service is actively processing
    /// the request.
    /// </note>
    /// </remarks>
    internal class RequestChannel : LillTekChannelBase, IRequestChannel
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
        /// <param name="channelManager">The channel manager responsible for this channel.</param>
        /// <param name="remoteAddress">The remote <see cref="EndpointAddress" />.</param>
        /// <param name="via">The first transport hop <see cref="Uri" />.</param>
        /// <param name="encoder">The <see cref="MessageEncoder" /> for serializing messages to the wire format.</param>
        public RequestChannel(ChannelManagerBase channelManager, EndpointAddress remoteAddress, Uri via, MessageEncoder encoder)
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
        }

        //---------------------------------------------------------------------
        // IRequestChannel implementation

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
        /// Synchronously sends a message-based request and then waits for the correlated message-based response.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <returns>The correlated response.</returns>
        /// <remarks>
        /// This override will use the default timeout value specified in the
        /// LillTek <see cref="MsgRouter" /> configuration settings.
        /// </remarks>
        public Message Request(Message message)
        {
            return Request(message, base.DefaultSendTimeout + base.DefaultReceiveTimeout);
        }

        /// <summary>
        /// Synchronously sends a message-based request and then waits for the correlated message-based response,
        /// using a specific timeout value.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <param name="timeout">The maximum <see cref="TimeSpan" /> to wait.</param>
        /// <returns>The correlated response.</returns>
        /// <remarks>
        /// This override will use <paramref name="timeout" /> value passed
        /// as the maximum time to wait for a response.
        /// </remarks>
        public Message Request(Message message, TimeSpan timeout)
        {
            IAsyncResult arRequest;

            ServiceModelHelper.ValidateTimeout(timeout);

            arRequest = BeginRequest(message, timeout, null, null);
            return EndRequest(arRequest);
        }

        /// <summary>
        /// Initiates an asynchronous request/response transmission.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">The application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <para>
        /// This override will use the default timeout value specified in the
        /// LillTek <see cref="MsgRouter" /> configuration settings.
        /// </para>
        /// <note>
        /// All successful calls to <see cref="BeginRequest(Message,AsyncCallback,object)" /> must 
        /// eventually be followed by a call to <see cref="EndRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
        {
            return BeginRequest(message, base.DefaultSendTimeout + base.DefaultReceiveTimeout, callback, state);
        }

        /// <summary>
        /// Initiates an asynchronous request/response transmission with a specific timeout.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">The application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginRequest(Message,TimeSpan,AsyncCallback,object)" /> must 
        /// eventually be followed by a call to <see cref="EndRequest" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                ThrowIfDisposedOrNotOpen();
                ServiceModelHelper.ValidateTimeout(timeout);

                try
                {
                    using (MemoryStream ms = new MemoryStream(payloadEstimator.EstimateNextBufferSize()))
                    {
                        WcfEnvelopeMsg requestMsg = new WcfEnvelopeMsg();

                        encoder.WriteMessage(message, ms);
                        payloadEstimator.LastPayloadSize((int)ms.Length);

                        requestMsg.Payload = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
                        return ChannelHost.Router.BeginQuery(ep, requestMsg, callback, state);
                    }
                }
                catch (Exception e)
                {
                    throw ServiceModelHelper.GetCommunicationException(e);
                }
            }
        }

        /// <summary>
        /// Completes an asynchronous operation initiated by <see cref="BeginRequest(Message,AsyncCallback,object)" /> 
        /// or <see cref="BeginRequest(Message,TimeSpan,AsyncCallback,object)" />.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <b>BeginRequest()</b>.</param>
        /// <returns>The correlated response message.</returns>
        public Message EndRequest(IAsyncResult result)
        {
            try
            {
                WcfEnvelopeMsg replyMsg;

                replyMsg = (WcfEnvelopeMsg)ChannelHost.Router.EndQuery(result);

                if (!base.CanAcceptMessages)
                {
                    // This is a bit of a hack to simulate aborting pending
                    // requests when the channel is closed.

                    throw ServiceModelHelper.CreateObjectDisposedException(this);
                }

                // Decode the reply

                using (BlockStream bs = new BlockStream((Block)replyMsg.Payload))
                    return encoder.ReadMessage(bs, ServiceModelHelper.MaxXmlHeaderSize);
            }
            catch (Exception e)
            {
                throw ServiceModelHelper.GetCommunicationException(e);
            }
        }
    }
}
