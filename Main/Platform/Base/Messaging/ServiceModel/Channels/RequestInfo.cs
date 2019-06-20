//-----------------------------------------------------------------------------
// FILE:        RequestInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the information necessary to queue and then later,
//              process a request message.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Holds the information necessary to queue and then later,
    /// process a request message.
    /// </summary>
    internal sealed class RequestInfo : IDisposable
    {
        /// <summary>
        /// The LillTek Messaging <see cref="MsgRequestContext" />.
        /// </summary>
        public readonly MsgRequestContext Context;

        /// <summary>
        /// The decoded WCF message.
        /// </summary>
        public readonly Message Message;

        /// <summary>
        /// The request time-to-die (SYS) while still queued.
        /// </summary>
        public readonly DateTime TTD;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The decoded WCF message.</param>
        /// <param name="context">The LillTek Messaging <see cref="MsgRequestContext" />.</param>
        /// <param name="ttd">The request time-to-die (SYS) while still queued.</param>
        public RequestInfo(Message message, MsgRequestContext context, DateTime ttd)
        {
            this.Message = message;
            this.Context = context;
            this.TTD     = ttd;
        }

        /// <summary>
        /// Releases any resources associated with this instance (basically
        /// calls the <see cref="Context" />'s <see cref="MsgRequestContext.Dispose" />
        /// method.
        /// </summary>
        public void Dispose()
        {
            Context.Dispose();
        }
    }
}
