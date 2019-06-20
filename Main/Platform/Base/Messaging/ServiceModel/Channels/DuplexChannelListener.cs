//-----------------------------------------------------------------------------
// FILE:        DuplexChannelListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an IChannelListener capable of accepting 
//              DuplexChannels via LillTek Messaging.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements an <see cref="IChannelListener" /> capable of accepting 
    /// <see cref="DuplexChannel" />s via LillTek Messaging.
    /// </summary>
    internal sealed class DuplexChannelListener : LillTekChannelListener<IDuplexChannel, DuplexChannel>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        internal DuplexChannelListener(BindingContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Called when the base class receives a LillTek envelope message with an
        /// encapsulated WCF message from the router.  Non-session oriented derived 
        /// classes must implement this to accept a new channel or route the message 
        /// to an existing channel.
        /// </summary>
        /// <param name="message">The decoded WCF <see cref="Message" />.</param>
        /// <param name="msg">The received LillTek <see cref="Msg" />.</param>
        protected override void OnMessageReceived(Message message, WcfEnvelopeMsg msg)
        {
        }

        /// <summary>
        /// Called when the base class receives a LillTek duplex session connection
        /// attempt from the router.  Session oriented derived  classes must implement 
        /// this to accept a new channel or route the message to an existing channel.
        /// </summary>
        /// <param name="msg">The session opening <see cref="DuplexSessionMsg" />.</param>
        protected override void OnSessionConnect(DuplexSessionMsg msg)
        {
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
        protected override DuplexChannel GetAcceptChannel()
        {
            TimedLock.AssertLocked(this);   // Verify the lock

            return null;
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This will be called periodically by the base class within a <see cref="TimedLock" />.
        /// </note>
        /// </remarks>
        protected override void OnBkTask()
        {
        }
    }
}
