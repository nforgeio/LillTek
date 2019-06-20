//-----------------------------------------------------------------------------
// FILE:        RequestChannelFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a DuplexChannelFactory factory.

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

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements a RequestChannelFactory factory.
    /// </summary>
    internal sealed class RequestChannelFactory : LillTekChannelFactory<IRequestChannel, RequestChannel>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" />.</param>
        public RequestChannelFactory(BindingContext context)
            : base(context)
        {
        }

        //---------------------------------------------------------------------
        // ChannelFactoryBase implementation

        /// <summary>
        /// Handles the actual creation of a channel.
        /// </summary>
        /// <param name="address">The endpoint address.</param>
        /// <param name="via">The transport output address (or <c>null</c>).</param>
        /// <returns>The new channel.</returns>
        protected override IRequestChannel OnCreateChannel(EndpointAddress address, Uri via)
        {
            RequestChannel channel;

            channel = new RequestChannel(this, address, via, base.MessageEncoder);
            base.AddChannel(channel);
            return channel;
        }

        //---------------------------------------------------------------------
        // LillTekChannelFactory implementation

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
    }
}
