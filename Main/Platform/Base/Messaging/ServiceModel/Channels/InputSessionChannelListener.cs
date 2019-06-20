//-----------------------------------------------------------------------------
// FILE:        InputSessionChannelListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an IChannelListener capable of accepting 
//              InputSessionChannels via LillTek Messaging.

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
// Hardcoding the maximum number of messages queued by InputSessionChannel
// and BkTaskInterval too.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements an <see cref="IChannelListener" /> capable of accepting 
    /// <see cref="InputSessionChannel" />s via LillTek Messaging.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Implementation Note</u></b></para>
    /// <para>
    /// This <see cref="InputSessionChannelListener" /> listener is very simple.  All it has to do
    /// is override <see cref="OnMessageReceived" /> so that the base <see cref="LillTekChannelListener{TChannel,TInternal}" />
    /// class can submit the received messages to the derived class.  If the listener has not yet accepted
    /// a channel one is created, adding the message received to its queue and the new
    /// channel is passed back to the base <see cref="LillTekChannelListener{TChannel,TInternal}.OnChannelCreated" /> method so
    /// any pending <b>WaitForChannel()</b> and <b>AcceptChannel()</b> operations will be
    /// completed.
    /// </para>
    /// </remarks>
    internal sealed class InputSessionChannelListener : LillTekChannelListener<IInputSessionChannel, InputSessionChannel>
    {
        private TimeSpan bkTaskInterval;     // Channel background task interval

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        internal InputSessionChannelListener(BindingContext context)
            : base(context)
        {
            this.bkTaskInterval = ServiceModelHelper.DefaultBkTaskInterval; // $todo(jeff.lill): Hardcoded
        }

        //---------------------------------------------------------------------
        // LillTekChannelListener overrides

        /// <summary>
        /// Derived classes may need to override this method to return the
        /// <see cref="SessionHandlerInfo" /> to be associated with the endpoint
        /// when it is added to the LillTek message router.  The base implementation
        /// returns <c>null</c>.
        /// </summary>
        /// <returns>The <see cref="SessionHandlerInfo" /> to be associated with the endpoint.</returns>
        protected override SessionHandlerInfo GetSessionHandlerInfo()
        {
            MsgSessionAttribute attr = new MsgSessionAttribute();

            // $todo(jeff.lill): 
            //
            // At some point, I need to come back and get
            // all of the session settings from the
            // application configuration.  We'll use the
            // default LillTek values for now.

            attr.Type = SessionTypeID.Duplex;
            attr.IsAsync = true;

            return new SessionHandlerInfo(attr);
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
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Called when the base class receives a LillTek duplex session connection
        /// attempt from the router.  Session oriented derived  classes must implement 
        /// this to accept a new channel or route the message to an existing channel.
        /// </summary>
        /// <param name="msg">The session opening <see cref="DuplexSessionMsg" />.</param>
        protected override void OnSessionConnect(DuplexSessionMsg msg)
        {
            InputSessionChannel     newChannel = null;
            string                  sessionID = msg._SessionID.ToString("D");

            if (base.State != CommunicationState.Opened)
                return;

            using (TimedLock.Lock(this))
            {
                // Ignore session create messages that may to an existing channel.

                if (base.Channels.ContainsKey(sessionID))
                    return;

                // Create a new channel for the session.

                newChannel = new InputSessionChannel(this, new EndpointAddress(this.Uri), sessionID, msg.Session);
                AddChannel(newChannel);
            }

            // Do this outside of the lock just to be safe

            if (newChannel != null)
                base.OnChannelCreated(newChannel);
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
        protected override InputSessionChannel GetAcceptChannel()
        {
            TimedLock.AssertLocked(this);   // Verify the lock
            return null;
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <remarks>
        /// This will be called periodically by the base listener class within a <see cref="TimedLock" />
        /// while the listener is open.
        /// </remarks>
        protected override void OnBkTask()
        {
        }

        //---------------------------------------------------------------------
        // CommunicationObject overrides

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnOpened()
        {
            base.OnOpened();
        }

        /// <summary>
        /// Internal event handler.
        /// </summary>
        protected override void OnClosed()
        {
            base.OnClosed();
        }
    }
}
