//-----------------------------------------------------------------------------
// FILE:        LillTekChannelListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used internally to implement common LillTek channel listener behaviors.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

// $todo(jeff.lill): 
//
// This class should implement Close(timeout) by giving any channel
// processing the chance to complete before the listener
// aborts the channels.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Used internally to implement common LillTek channel listener behaviors.
    /// </summary>
    /// <typeparam name="TChannel">The external channel type.</typeparam>
    /// <typeparam name="TInternal">The internal channel type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps derived channel specific listeners with some common
    /// implementation behaviors.  The implementation of a LillTek channel
    /// listener is split between this and the derived classed, with this
    /// class handling the actual route registration and processing of messages 
    /// received from LillTek message router as well as the implementation
    /// of the <b>AcceptChannel()</b> and <b>WaitChannel()</b> related methods.
    /// </para>
    /// <para>
    /// Messages received by this class are passed to the derived class via
    /// the abstract <see cref="OnMessageReceived" /> method.  The derived
    /// class must implement this method and decide whether a new channel
    /// should be accepted for the message of if it should be routed to
    /// an existing channel.  If a new channel is warrented, the derived
    /// will create one and pass it to the base class <see cref="OnChannelCreated" />
    /// method.  The derived class also handle routing messages to existing
    /// channels, as is appropriate.
    /// </para>
    /// <para>
    /// This class handles calls to <see cref="OnChannelCreated" /> by
    /// completing any pending <b>AcceptChannel()</b> and <b>WaitForChannel()</b>
    /// operations or queuing the channel for a subsequent accept or wait
    /// operation.
    /// </para>
    /// <para>
    /// The listener implements the internal <see cref="OnChannelCloseOrAbort" /> method
    /// which will be called when any accepted channels are closed.  The listener will
    /// remove any associations with closed channels.
    /// </para>
    /// <para>
    /// One additional virtual method is required to make this all work.  This class
    /// will call <see cref="GetSessionHandlerInfo" /> when registering the 
    /// endpoint with the LillTek message router.  Derived classes can override
    /// this to specify custom LillTek <see cref="SessionHandlerInfo" />
    /// settings for the endpoint.  The base implementation returns <c>null</c>.
    /// </para>
    /// <note>
    /// One important consequence of this design is that the channels accepted
    /// by this listener rely on the listener to receive messages from the
    /// underlying LillTek router.  Although closing the channel listener 
    /// does not automatically close any accepted channels, the accepted
    /// channels will no longer be able to continue processing any new
    /// messages.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    internal abstract class LillTekChannelListener<TChannel, TInternal> : ChannelListenerBase<TChannel>, ILillTekChannelManager, ILockable
        where TChannel : class, IChannel
        where TInternal : LillTekChannelBase
    {
        private Uri                                     uri;                    // The WCF endpoint URI
        private MsgEP                                   ep;                     // The LillTek endpoint
        private BindingContext                          context;                // Binding context for the listener
        private int                                     maxAcceptedChannels;    // Max # of queued accepted channels
        private Dictionary<string, TInternal>           channels;               // Active channels
        private LimitedQueue<TInternal>                 channelQueue;           // Queued accepted channels
        private Queue<AsyncResult<TInternal, object>>   acceptQueue;            // Pending AcceptChannel() requests
        private Queue<AsyncResult<bool, object>>        waitQueue;              // Pending WaitForChannel() requests
        private MessageEncoderFactory                   messageEncoderFactory;  // The message encoder factory
        private TimeSpan                                bkTaskInterval;         // Background task interval
        private AsyncCallback                           onBkTask;               // Background task delegate (or null)
        private IAsyncResult                            arBkTimer;              // Background timer async result
        private bool                                    sessionMode;            // True for WCF session channels
        private bool                                    hostStarted;            // Indicates whether call to ChannelHost.Start() is current

        //---------------------------------------------------------------------
        // Implementation 

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        /// <exception cref="InvalidOperationException">Thrown if problems were found with the binding parameters.</exception>
        internal LillTekChannelListener(BindingContext context)
        {
            this.maxAcceptedChannels = ServiceModelHelper.MaxAcceptedChannels;      // $todo(jeff.lill): Hardcoded
            bkTaskInterval           = ServiceModelHelper.DefaultBkTaskInterval;    //                   This too

            this.context             = context;
            this.uri                 = GetListenUri(context);
            this.ep                  = ServiceModelHelper.ToMsgEP(uri);
            this.onBkTask            = new AsyncCallback(OnBkTask);
            this.sessionMode         = false;
            this.hostStarted        = false;

            // Initialize the message encoder factory from the binding context if
            // one was specified.  Use the binary message encoding factory otherwise.

            if (context.BindingParameters.FindAll<MessageEncodingBindingElement>().Count > 1)
                throw new InvalidOperationException("Multiple MessageEncodingBindingElements were found in the BindingParameters of the BindingContext.");

            MessageEncodingBindingElement element = context.BindingParameters.Find<MessageEncodingBindingElement>();

            if (element != null)
                messageEncoderFactory = element.CreateMessageEncoderFactory();
            else
                messageEncoderFactory = new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory();
        }

        /// <summary>
        /// Extracts the listening URI from a binding context.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" />.</param>
        /// <returns>The <see cref="Uri" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if BindingContext.ListenUriBaseAddress is not specified.</exception>
        /// <exception cref="ArgumentException">Thrown if the URI does not have a valid LillTek URI scheme.</exception>
        protected Uri GetListenUri(BindingContext context)
        {
            Uri         uri;
            Uri         baseAddress;
            string      relativeAddress;

            baseAddress = context.ListenUriBaseAddress;
            relativeAddress = context.ListenUriRelativeAddress;

            if (baseAddress == null)
            {
                if (context.ListenUriMode == ListenUriMode.Unique)
                    baseAddress = ServiceModelHelper.CreateUniqueUri();
                else
                    throw new ArgumentNullException("BindingContext.ListenUriBaseAddress");
            }

            if (string.IsNullOrWhiteSpace(relativeAddress))
                uri = baseAddress;
            else
            {
                UriBuilder ub = new UriBuilder(baseAddress);

                if (!ub.Path.EndsWith("/"))
                    ub.Path += "/";

                uri = new Uri(ub.Uri, relativeAddress);
            }

            // Verify that the URI scheme is a valid LillTek scheme.

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "lilltek.logical":
                case "lilltek.abstract":

                    return uri;     // OK

                default:

                    throw new ArgumentException(string.Format("Invalid LillTek Messaging WCF Transport scheme [{0}].", uri.Scheme), "BindingContext.ListenUriBaseAddress");
            }
        }

        /// <summary>
        /// Returns the listener's endpoint <see cref="Uri" />.
        /// </summary>
        public override Uri Uri
        {
            get { return uri; }
        }

        /// <summary>
        /// Returns <c>true</c> if the listener has a pending <b>WaitForChannel()</b>
        /// or <b>AcceptChannel()</b> operation.
        /// </summary>
        /// <remarks>
        /// This call must be made inside of a <see cref="TimedLock" />.
        /// </remarks>
        protected bool HasPendingChannelOperation
        {
            get
            {
                TimedLock.AssertLocked(this);
                if (acceptQueue != null && waitQueue != null)
                    return acceptQueue.Count > 0 || waitQueue.Count > 0;
                else
                    return false;
            }
        }

        /// <summary>
        /// Returns the requested propery by type.
        /// </summary>
        /// <typeparam name="T">Type of the desired property.</typeparam>
        /// <returns>The property instance if it exists, <c>null</c> otherwise.</returns>
        public override T GetProperty<T>()
        {
            T messageEncoderProperty;

            messageEncoderProperty = messageEncoderFactory.Encoder.GetProperty<T>();
            if (messageEncoderProperty != null)
                return messageEncoderProperty;

            if (typeof(T) == typeof(MessageVersion))
                return (T)(object)messageEncoderFactory.Encoder.MessageVersion;

            return base.GetProperty<T>();
        }

        /// <summary>
        /// Returns the <see cref="MessageEncoder" /> to be used for serializing
        /// messages sent from channels belonging to this channel manager.
        /// </summary>
        protected MessageEncoder MessageEncoder
        {
            get { return messageEncoderFactory.Encoder; }
        }

        /// <summary>
        /// Called by derived classes when a new channel is created.
        /// </summary>
        /// <param name="channel">The new channel.</param>
        public virtual void AddChannel(TInternal channel)
        {
            using (TimedLock.Lock(this))
                channels.Add(channel.ID, channel);
        }

        /// <summary>
        /// Returns the collection of non-closed channels belonging to the manager.
        /// </summary>
        public virtual Dictionary<string, TInternal> Channels
        {
            get { return channels; }
        }

        private void Initialize()
        {
            // Basic initialization

            ChannelHost.Start();
            hostStarted  = true;

            channels     = new Dictionary<string, TInternal>();
            channelQueue = new LimitedQueue<TInternal>(maxAcceptedChannels);
            acceptQueue  = new Queue<AsyncResult<TInternal, object>>();
            waitQueue    = new Queue<AsyncResult<bool, object>>();

            // Register the endpoint with the router.

            SessionHandlerInfo sessionInfo;

            sessionInfo = this.GetSessionHandlerInfo();
            sessionMode = sessionInfo != null && sessionInfo.SessionType == typeof(DuplexSession);

            if (sessionMode)
                ChannelHost.Router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnReceive), ep, typeof(DuplexSessionMsg), false, sessionInfo);
            else
                ChannelHost.Router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnReceive), ep, typeof(WcfEnvelopeMsg), false, sessionInfo);

            // Start the background task timer

            arBkTimer = AsyncTimer.BeginTimer(bkTaskInterval, onBkTask, null);
        }

        private void Cleanup(Exception e)
        {
            using (TimedLock.Lock(ChannelHost.SyncRoot))
            {
                if (hostStarted)
                {
                    // Remove the route from the message router.

                    if (ChannelHost.Router != null)
                        ChannelHost.Router.Dispatcher.RemoveTarget(this);

                    hostStarted = false;
                    ChannelHost.Stop();
                }
            }

            List<LillTekChannelBase> abortChannels = null;

            using (TimedLock.Lock(this))
            {
                // Terminate any pending accepts

                if (acceptQueue != null)
                {
                    while (acceptQueue.Count > 0)
                        acceptQueue.Dequeue().Notify();

                    acceptQueue = null;
                }

                // Terminate any pending waits

                if (waitQueue != null)
                {
                    while (waitQueue.Count > 0)
                        waitQueue.Dequeue().Notify();

                    waitQueue = null;
                }

                // Abort any queued accepted channels

                if (channelQueue != null)
                {
                    while (channelQueue.Count > 0)
                    {
                        TInternal channel = channelQueue.Dequeue();

                        if (channel.State != CommunicationState.Closed)
                            channel.Abort();
                    }

                    channelQueue = null;
                }

                // $todo(jeff.lill): Delete this ------------------------

                // Setup to abort all of the listener's channels.

                if (channels != null && channels.Count > 0)
                {
                    abortChannels = new List<LillTekChannelBase>(channels.Count);
                    foreach (LillTekChannelBase channel in channels.Values)
                        abortChannels.Add(channel);
                }

                //---------------------------------------------------

                // Stop the background task timer

                onBkTask = null;
            }

            // Actually abort the channels outside of the lock.

            if (abortChannels != null)
            {
                foreach (LillTekChannelBase channel in abortChannels)
                    channel.Close();
            }
        }

        /// <summary>
        /// Called by the derived class when it has created a new channel to
        /// accept a message received by the base class and passed to the 
        /// derived class via <see cref="OnMessageReceived" />.
        /// </summary>
        /// <param name="channel">The accepted channel.</param>
        protected void OnChannelCreated(TInternal channel)
        {
            bool abortChannel = false;

            using (TimedLock.Lock(this))
            {
                // The derived class has accepted a new channel.  If we have a 
                // an AcceptChannel() or WaitFoprChannel() operation pending, complete 
                // them in that order.

                if (acceptQueue.Count > 0)
                {
                    AsyncResult<TInternal, object> arAccept = acceptQueue.Dequeue();

                    arAccept.Result = channel;
                    arAccept.Notify();
                    return;
                }

                // Queue the accepted channel

                channelQueue.Enqueue(channel);

                // Complete the first queued wait operation, if there is one.

                if (waitQueue.Count > 0)
                {
                    AsyncResult<bool, object> arWait;

                    arWait = waitQueue.Dequeue();
                    arWait.Result = true;
                    arWait.Notify();
                }
            }

            if (abortChannel)
                channel.Abort();    // Do this outside of the lock to be safe
        }

        /// <summary>
        /// Derived classes can override this to implement background tasks.
        /// </summary>
        /// <remarks>
        /// This will be called periodically by the base listener class within a <see cref="TimedLock" />
        /// while the listener is open.
        /// </remarks>
        protected abstract void OnBkTask();

        /// <summary>
        /// Executed on a worker thread to look for timed-out accept and wait requests.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            DateTime now = SysTime.Now;

            AsyncTimer.EndTimer(arBkTimer);
            using (TimedLock.Lock(this))
            {
                try
                {
                    // Give derived classes a chance to perform any necessary
                    // background tasks.

                    OnBkTask();

                    // Scan the queued accept requests for any that have timed-out.

                    if (acceptQueue != null && acceptQueue.Count > 0)
                    {
                        do
                        {
                            AsyncResult<TInternal, object> arAccept;

                            arAccept = acceptQueue.Peek();
                            if (arAccept.TTD <= now)
                            {
                                acceptQueue.Dequeue();
                                arAccept.Notify(new TimeoutException());
                            }
                            else
                                break;

                        } while (acceptQueue.Count > 0);
                    }

                    // Scan the queued wait requests for any that have timed-out.

                    if (waitQueue != null && waitQueue.Count > 0)
                    {
                        do
                        {
                            AsyncResult<bool, object> arWait;

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
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
                finally
                {
                    // Schedule the next timer if this functionality is
                    // still enabled.

                    if (onBkTask != null)
                        arBkTimer = AsyncTimer.BeginTimer(bkTaskInterval, onBkTask, null);
                }
            }
        }

        /// <summary>
        /// Decodes the WCF <see cref="Message" /> encapsulated within a LillTek <see cref="WcfEnvelopeMsg" />.
        /// </summary>
        /// <param name="msg">The LillTek message.</param>
        /// <returns>The WCF <see cref="Message" />.</returns>
        /// <exception cref="CommunicationException">Thrown if the message could not be decoded.</exception>
        public Message DecodeMessage(WcfEnvelopeMsg msg)
        {
            using (BlockStream bs = new BlockStream((Block)msg.Payload))
            {
                try
                {
                    return messageEncoderFactory.Encoder.ReadMessage(bs, ServiceModelHelper.MaxXmlHeaderSize);
                }
                catch (Exception e)
                {
                    throw ServiceModelHelper.GetCommunicationException(e);
                }
            }
        }

        /// <summary>
        /// Called when the message router receives a LillTek message.
        /// </summary>
        /// <param name="msg">The received message.</param>
        private void OnReceive(Msg msg)
        {
            try
            {
                if (sessionMode)
                {
                    DuplexSessionMsg duplexMsg = msg as DuplexSessionMsg;

                    // Handle client session connection attempts.

                    if (duplexMsg != null)
                    {
                        OnSessionConnect(duplexMsg);
                        return;
                    }
                }
                else
                {
                    // Handle encapsulated WCF messages.

                    WcfEnvelopeMsg envelopeMsg = msg as WcfEnvelopeMsg;
                    Message message;

                    if (envelopeMsg == null)
                        return;     // Discard non-envelope messages

                    message = DecodeMessage(envelopeMsg);

                    // Let the derived listener decide what to do with the message.

                    OnMessageReceived(message, envelopeMsg);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // ILillTekChannelManager implementation

        /// <summary>
        /// Called by LillTek channels belonging to this channel manager when the
        /// channel is closed or aborted to terminate any pending operations.
        /// </summary>
        /// <param name="channel">The closed or aborted channel.</param>
        /// <param name="e">The exception to be used to terminate the operation.</param>
        public virtual void OnChannelCloseOrAbort(LillTekChannelBase channel, Exception e)
        {
            using (TimedLock.Lock(this))
            {
                string channelID = channel.ID;

                if (channels.ContainsKey(channelID))
                    channels.Remove(channelID);
            }
        }

        //---------------------------------------------------------------------
        // Methods that can be overridden be derived listeners

        /// <summary>
        /// Derived classes may need to override this method to return the
        /// <see cref="SessionHandlerInfo" /> to be associated with the endpoint
        /// when it is added to the LillTek message router.  The base implementation
        /// returns <c>null</c>.
        /// </summary>
        /// <returns>The <see cref="SessionHandlerInfo" /> to be associated with the endpoint.</returns>
        protected virtual SessionHandlerInfo GetSessionHandlerInfo()
        {
            return null;
        }

        /// <summary>
        /// Called when the base class receives a LillTek envelope message with an
        /// encapsulated WCF message from the router.  Non-session oriented derived 
        /// classes must implement this to accept a new channel or route the message 
        /// to an existing channel.
        /// </summary>
        /// <param name="message">The decoded WCF <see cref="Message" />.</param>
        /// <param name="msg">The received LillTek <see cref="Msg" />.</param>
        protected abstract void OnMessageReceived(Message message, WcfEnvelopeMsg msg);

        /// <summary>
        /// Called when the base class receives a LillTek duplex session connection
        /// attempt from the router.  Session oriented derived  classes must implement 
        /// this to accept a new channel or route the message to an existing channel.
        /// </summary>
        /// <param name="msg">The session opening <see cref="DuplexSessionMsg" />.</param>
        protected abstract void OnSessionConnect(DuplexSessionMsg msg);

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
        protected abstract TInternal GetAcceptChannel();

        //---------------------------------------------------------------------
        // CommunicationObject overrides

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        protected override void OnOpen(TimeSpan timeout)
        {
            Initialize();
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult arOpen;

            timeout = ServiceModelHelper.ValidateTimeout(timeout);
            Initialize();

            arOpen = new AsyncResult(null, callback, state);
            arOpen.Started(ServiceModelHelper.AsyncTrace);
            arOpen.Notify();
            return arOpen;
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="result"></param>
        protected override void OnEndOpen(IAsyncResult result)
        {
            AsyncResult arOpen = (AsyncResult)result;

            arOpen.Wait();
            try
            {

                if (arOpen.Exception != null)
                    throw arOpen.Exception;
            }
            finally
            {

                arOpen.Dispose();
            }
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected override TChannel OnAcceptChannel(TimeSpan timeout)
        {
            IAsyncResult arAccept;

            timeout = ServiceModelHelper.ValidateTimeout(timeout);

            arAccept = BeginAcceptChannel(timeout, null, null);
            return EndAcceptChannel(arAccept);
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<TInternal, object> arAccept;

            arAccept = new AsyncResult<TInternal, object>(null, callback, state);
            arAccept.TTD = SysTime.Now + ServiceModelHelper.ValidateTimeout(timeout);
            arAccept.Started(ServiceModelHelper.AsyncTrace);

            using (TimedLock.Lock(this))
            {
                if (channelQueue.Count > 0)
                {
                    arAccept.Result = channelQueue.Dequeue();
                    arAccept.Notify();
                }
                else
                {
                    // Give the derived class a chance to accept a channel

                    TInternal acceptChannel;

                    acceptChannel = GetAcceptChannel();
                    if (acceptChannel != null)
                    {

                        arAccept.Result = acceptChannel;
                        arAccept.Notify();
                    }
                    else
                        acceptQueue.Enqueue(arAccept);
                }
            }

            return arAccept;
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        protected override TChannel OnEndAcceptChannel(IAsyncResult result)
        {
            AsyncResult<TInternal, object> arAccept = (AsyncResult<TInternal, object>)result;

            arAccept.Wait();
            try
            {
                if (arAccept.Exception != null)
                    throw arAccept.Exception;

                return (TChannel)(object)arAccept.Result;
            }
            finally
            {
                arAccept.Dispose();
            }
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected override bool OnWaitForChannel(TimeSpan timeout)
        {
            IAsyncResult arWait;

            timeout = ServiceModelHelper.ValidateTimeout(timeout);

            arWait = BeginWaitForChannel(timeout, null, null);
            return EndWaitForChannel(arWait);
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult<bool, object> arWait;

            arWait = new AsyncResult<bool, object>(null, callback, state);
            arWait.TTD = SysTime.Now + ServiceModelHelper.ValidateTimeout(timeout);
            arWait.Started(ServiceModelHelper.AsyncTrace);

            using (TimedLock.Lock(this))
            {
                if (channelQueue.Count > 0)
                {
                    arWait.Result = true;
                    arWait.Notify();
                }
                else
                {
                    // Give the derived class a chance to accept a channel

                    TInternal acceptChannel;

                    acceptChannel = GetAcceptChannel();
                    if (acceptChannel != null)
                    {
                        channelQueue.Enqueue(acceptChannel);
                        arWait.Result = true;
                        arWait.Notify();
                    }
                    else
                        waitQueue.Enqueue(arWait);
                }
            }

            return arWait;
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        protected override bool OnEndWaitForChannel(IAsyncResult result)
        {
            AsyncResult<bool, object> arWait = (AsyncResult<bool, object>)result;

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
        /// Internal event handler.
        /// </summary>
        protected override void OnAbort()
        {
            Cleanup(new CommunicationObjectAbortedException(this.GetType().FullName));
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnFaulted()
        {
            Cleanup(new CommunicationObjectFaultedException(this.GetType().FullName));
            base.OnFaulted();
        }

        /// <summary>
        /// Internal event handler
        /// </summary>
        protected override void OnClosing()
        {
            Cleanup(ServiceModelHelper.CreateObjectDisposedException(this));
            base.OnClosing();
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        protected override void OnClose(TimeSpan timeout)
        {
            Cleanup(ServiceModelHelper.CreateObjectDisposedException(this));
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult ar;

            Cleanup(ServiceModelHelper.CreateObjectDisposedException(this));
            timeout = ServiceModelHelper.ValidateTimeout(timeout);

            ar = new AsyncResult(null, callback, state);
            ar.Started(ServiceModelHelper.AsyncTrace);
            ar.Notify();
            return ar;
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        /// <param name="result"></param>
        protected override void OnEndClose(IAsyncResult result)
        {
            AsyncResult ar = (AsyncResult)result;

            ar.Wait();
            try
            {
                if (ar.Exception != null)
                    throw ar.Exception;
            }
            finally
            {
                ar.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
