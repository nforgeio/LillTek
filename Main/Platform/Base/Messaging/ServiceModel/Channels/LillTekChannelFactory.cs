//-----------------------------------------------------------------------------
// FILE:        LillTekChannelFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used internally to implement common LillTek channel factory behaviors.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

// $todo(jeff.lill): 
//
// This class should implement Close(timeout) by giving any owned channels
// the chance to close gracefully before aborting them.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Used internally to implement common LillTek channel factory behaviors.
    /// </summary>
    /// <typeparam name="TChannel">The external channel type.</typeparam>
    /// <typeparam name="TInternal">The internal channel type.</typeparam>
    internal abstract class LillTekChannelFactory<TChannel, TInternal> : ChannelFactoryBase<TChannel>, ILillTekChannelManager, ILockable
        where TChannel : class, IChannel
        where TInternal : LillTekChannelBase
    {
        private Dictionary<string, TInternal>   channels;               // Active channels
        private MessageEncoderFactory           messageEncoderFactory;  // The message encoder factory
        private TimeSpan                        bkTaskInterval;         // Background task interval (or TimeSpan.Zero)
        private AsyncCallback                   onBkTask;               // Background task delegate (or null)
        private IAsyncResult                    arBkTimer;              // Background timer async result
        private bool                            hostStarted;            // Indicates whether call to ChannelHost.Start() is current

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" />.</param>
        /// <exception cref="InvalidOperationException">Thrown if problems were found with the binding parameters.</exception>
        public LillTekChannelFactory(BindingContext context)
            : base()
        {
            this.channels    = new Dictionary<string, TInternal>();
            this.hostStarted = false;

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
        /// Called by derived classes when a new channel is created.
        /// </summary>
        /// <param name="channel">The new channel.</param>
        public virtual void AddChannel(LillTekChannelBase channel)
        {
            using (TimedLock.Lock(this))
            {
                if (channels == null)
                    throw ServiceModelHelper.CreateObjectDisposedException(this);

                channels.Add(channel.ID, (TInternal)channel);
            }
        }

        /// <summary>
        /// Returns the collection of non-closed channels belonging to the manager
        /// or <c>null</c> if the factor has been closed.
        /// </summary>
        public Dictionary<string, TInternal> Channels
        {
            get { return channels; }
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
        /// Internal factory initialization.
        /// </summary>
        private void Initialize()
        {
            using (TimedLock.Lock(this))
            {
                ChannelHost.Start();
                hostStarted = true;

                // Start the background task timer if required.

                bkTaskInterval = GetBackgroundTaskInterval();
                if (bkTaskInterval > TimeSpan.Zero)
                    arBkTimer = AsyncTimer.BeginTimer(bkTaskInterval, onBkTask, null);
            }
        }

        /// <summary>
        /// Internal factory cleanup.
        /// </summary>
        /// <param name="abort"><c>true</c> for a abort cleanup, <c>false</c> for normal.</param>
        private void Cleanup(bool abort)
        {
            Dictionary<string, TInternal> channelsCopy;

            using (TimedLock.Lock(this))
            {
                if (hostStarted)
                {
                    hostStarted = false;
                    ChannelHost.Stop();
                }

                onBkTask = null;            // Stops the background task callbacks

                // Make a copy of the channels table and then clear the
                // member variable.   I'm doing this so the channels will
                // be closed below, outside of the lock.

                channelsCopy = channels;
                channels = null;
            }

            if (channelsCopy != null)
            {
                foreach (TInternal channel in channelsCopy.Values)
                {
                    try
                    {
                        if (abort)
                            channel.Abort();
                        else
                            channel.Close();
                    }
                    catch
                    {
                        // Ignore errors due to channels already being closed, etc.
                    }
                }
            }
        }

        /// <summary>
        /// Called by the <see cref="LillTekChannelBase" /> class when the channel is opened
        /// to determine whether the channel requires its <see cref="OnBkTask()" /> method
        /// to be called periodically on a background thread while the channel is open.
        /// </summary>
        /// <returns>
        /// The desired background task callback interval or <b>TimeSpan.Zero</b> if 
        /// callbacks are to be disabled.
        /// </returns>
        protected abstract TimeSpan GetBackgroundTaskInterval();

        /// <summary>
        /// Called periodically on a background thread and within a <see cref="TimedLock" />
        /// if <see cref="GetBackgroundTaskInterval()" /> returned a positive interval.
        /// </summary>
        protected abstract void OnBkTask();

        /// <summary>
        /// Internal background task method.
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

        //---------------------------------------------------------------------
        // ILillTekChannelManager implementation

        /// <summary>
        /// Called by LillTek channels accepted by this listener when the
        /// channel is closed or aborted to terminate any pending operations.
        /// </summary>
        /// <param name="channel">The closed or aborted channel.</param>
        /// <param name="e">The exception to be used to terminate the operation.</param>
        public virtual void OnChannelCloseOrAbort(LillTekChannelBase channel, Exception e)
        {
            using (TimedLock.Lock(this))
            {
                string channelID = channel.ID;

                if (channels == null)
                    return;

                if (channels.ContainsKey(channelID))
                    channels.Remove(channelID);
            }
        }

        //---------------------------------------------------------------------
        // ChannelFactoryBase overrides

        /// <summary>
        /// Inserts processing on a communication object after it transitions into the opening state 
        /// which must complete within a specified interval of time.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        protected override void OnOpen(TimeSpan timeout)
        {
            Initialize();
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the opening state due to 
        /// the invocation of an asynchronous open operation.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            // Initiate an async operation that will complete immediately on
            // another thread.

            AsyncResult ar;

            Initialize();
            ar = new AsyncResult(null, callback, state);
            ar.Started(ServiceModelHelper.AsyncTrace);
            ar.Notify();
            return ar;
        }

        /// <summary>
        /// Inserts processing on a communication object after it transitions to the opening state due to 
        /// the invocation of a synchronous open operation.
        /// </summary>
        /// <param name="result">The <see cref="IAsyncResult" /> instance returned by <see cref="OnBeginOpen" />.</param>
        protected override void OnEndOpen(IAsyncResult result)
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

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnAbort()
        {
            Cleanup(true);
            base.OnAbort();
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnFaulted()
        {
            Cleanup(true);
            base.OnFaulted();
        }

        /// <summary>
        /// Internal event handler
        /// </summary>
        protected override void OnClosing()
        {
            Cleanup(false);
            base.OnClosing();
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
