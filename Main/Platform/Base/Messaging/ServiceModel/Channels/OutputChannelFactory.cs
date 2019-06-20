//-----------------------------------------------------------------------------
// FILE:        OutputChannelFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an OutputChannel factory.

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
    /// Implements an OutputChannel factory.
    /// </summary>
    internal sealed class OutputChannelFactory : LillTekChannelFactory<IOutputChannel, OutputChannel>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The <see cref="BindingContext" />.</param>
        public OutputChannelFactory(BindingContext context)
            : base(context)
        {
        }

        //---------------------------------------------------------------------
        // ChannelFactoryBase implementation

        /// <summary>
        /// Creates the custom LillTek channel.
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndpointAddress" /> where the channel will deliver sent messages.</param>
        /// <param name="via">The transport's first hop <see cref="Uri" /> (ignored by LillTek channels).</param>
        /// <returns>The new channel.</returns>
        protected override IOutputChannel OnCreateChannel(EndpointAddress remoteAddress, Uri via)
        {
            OutputChannel channel;

            channel = new OutputChannel(this, remoteAddress, via, base.MessageEncoder);
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
