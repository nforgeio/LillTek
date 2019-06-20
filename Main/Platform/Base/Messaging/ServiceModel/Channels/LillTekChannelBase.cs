//-----------------------------------------------------------------------------
// FILE:        LillTekChannelBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for all LillTek channels.

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
    /// Base class for all LillTek channels that provides default implementations
    /// for the required <see cref="ICommunicationObject" /> overrides.
    /// </summary>
    /// <remarks>
    /// All channels are assigned a globally unique ID string when they are created.
    /// This is available as the <see cref="ID" /> property and will be used by
    /// channel managers for maintaining their collections of channels as well
    /// as the session ID for sessionful channels.
    /// </remarks>
    internal abstract class LillTekChannelBase : ChannelBase, ILockable
    {
        private ILillTekChannelManager  channelManager;         // The owning channel factory or listener
        private string                  id;                     // Channel ID/session ID
        private TimeSpan                bkTaskInterval;         // Background task interval (or TimeSpan.Zero)
        private AsyncCallback           onBkTask;               // Background task delegate (or null)
        private IAsyncResult            arBkTimer;              // Background timer async result

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <remarks>
        /// <note>
        /// This constructor overrides generates a globally unique channel ID string.
        /// </note>
        /// </remarks>
        public LillTekChannelBase(ChannelManagerBase channelManager)
            : base(channelManager)
        {
            this.channelManager = (ILillTekChannelManager)channelManager;
            this.id             = Guid.NewGuid().ToString("D");
            this.onBkTask       = new AsyncCallback(OnBkTask);
        }

        /// <summary>
        /// Constructor that allows the specification of the channel ID.
        /// </summary>
        /// <param name="channelManager">The responsible channel manager.</param>
        /// <param name="id">The globally unique channel ID string.</param>
        /// <remarks>
        /// <note>
        /// This constructor is available for sessionful channels that wish
        /// to set the channel ID to the globally unqiue session ID.
        /// </note>
        /// </remarks>
        public LillTekChannelBase(ChannelManagerBase channelManager, string id)
            : base(channelManager)
        {
            this.channelManager = (ILillTekChannelManager)channelManager;
            this.id             = id;
            this.onBkTask       = new AsyncCallback(OnBkTask);
        }

        /// <summary>
        /// The <see cref="ILillTekChannelManager" /> that is responsible for this channel.
        /// </summary>
        public new ILillTekChannelManager Manager
        {
            get { return channelManager; }
            set { channelManager = value; }
        }

        /// <summary>
        /// Returns the globally unique ID for this channel.
        /// </summary>
        /// <remarks>
        /// All channels are assigned a globally unique ID string when they are created.
        /// This is available as the <see cref="ID" /> property and will be used by
        /// channel managers for maintaining their collections of channels as well
        /// as the session ID for sessionful channels.
        /// </remarks>
        public string ID
        {
            get { return id; }
        }

        /// <summary>
        /// Returns <c>true</c> if the channel is in a state where it can
        /// accept messages.
        /// </summary>
        public bool CanAcceptMessages
        {
            get
            {
                // Note that I'm allowing channels to accept and queue
                // messages before they zare fully opened.

                return base.State == CommunicationState.Created ||
                       base.State == CommunicationState.Opening ||
                       base.State == CommunicationState.Opened;
            }
        }

        /// <summary>
        /// Internal channel initialization.
        /// </summary>
        private void Initialize()
        {
            using (TimedLock.Lock(this))
            {
                // Start the background task timer if required.

                bkTaskInterval = GetBackgroundTaskInterval();
                if (bkTaskInterval > TimeSpan.Zero)
                    arBkTimer = AsyncTimer.BeginTimer(bkTaskInterval, onBkTask, null);
            }
        }

        /// <summary>
        /// Internal channel cleanup.
        /// </summary>
        private void Cleanup()
        {
            onBkTask = null;    // Stops the background task callbacks
        }

        /// <summary>
        /// Terminates all pending operations with the exception passed.
        /// </summary>
        /// <param name="e">The termination exception.</param>
        /// <remarks>
        /// This method must be implemented by derived channel classes.
        /// </remarks>
        protected abstract void TerminatePendingOperations(Exception e);

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
        // CommunicationObject implementation

        /// <summary>
        /// Invoked during the transition of a communication object into the open state.
        /// </summary>
        /// <remarks>
        /// The base class performs initialization activities.
        /// </remarks>
        protected override void OnOpening()
        {
            Initialize();
            base.OnOpening();
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the faulted state.
        /// </summary>
        /// <remarks>
        /// The base class implementation calls <see cref="TerminatePendingOperations" />.
        /// </remarks>
        protected override void OnFaulted()
        {
            TerminatePendingOperations(new CommunicationObjectFaultedException(this.GetType().FullName));
            Cleanup();
            base.OnFaulted();
        }

        /// <summary>
        /// Invoked during the transition of a communication object into the closed state.
        /// </summary>
        /// <remarks>
        /// The base class implementation calls <see cref="TerminatePendingOperations" /> and
        /// the associated channel manager's <see cref="ILillTekChannelManager.OnChannelCloseOrAbort" /> method.
        /// </remarks>
        protected override void OnClosed()
        {
            Exception e = ServiceModelHelper.CreateObjectDisposedException(this);

            TerminatePendingOperations(e);
            Cleanup();
            channelManager.OnChannelCloseOrAbort(this, e);
            base.OnClosed();
        }

        /// <summary>
        /// Invoked when the channel is aborted.
        /// </summary>
        /// <remarks>
        /// The base class implementation does nothing.
        /// </remarks>
        protected override void OnAbort()
        {
            Exception e = new CommunicationObjectAbortedException(this.GetType().FullName + ": Has been aborted.");

            TerminatePendingOperations(e);
            Cleanup();
            channelManager.OnChannelCloseOrAbort(this, e);
        }

        /// <summary>
        /// Closes the channel.
        /// </summary>
        /// <param name="timeout">The timeout <see cref="TimeSpan" />.</param>
        /// <remarks>
        /// The base class implementation does nothing.
        /// </remarks>
        protected override void OnClose(TimeSpan timeout)
        {
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
            AsyncResult arOpen;

            ServiceModelHelper.ValidateTimeout(timeout);

            arOpen = new AsyncResult(null, callback, state);
            arOpen.Started(ServiceModelHelper.AsyncTrace);
            arOpen.Notify();
            return arOpen;
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
