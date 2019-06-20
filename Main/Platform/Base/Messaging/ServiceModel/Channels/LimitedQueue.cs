//-----------------------------------------------------------------------------
// FILE:        LimitedQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a generic queue that won't exceed
//              a specified size.

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
    /// Implements a generic queue that won't exceed a specified size.
    /// </summary>
    /// <remarks>
    /// The queue will remove the oldest item if necessary to keep the total
    /// number of items within the limit, call its <b>Dispose()</b> method if
    /// the item type implements <see cref="IDisposable" />.
    /// </remarks>
    /// <threadsafety instance="false" />
    internal sealed class LimitedQueue<TItem> : Queue<TItem>
    {
        private int     maxItems;
        private bool    disposable;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="maxItems">Maximum number of items allowed.</param>
        public LimitedQueue(int maxItems)
            : base()
        {
            if (maxItems < 1)
                throw new ArgumentException("[maxItems] must be >= 1.", "maxItems");

            this.maxItems   = maxItems;
            this.disposable = typeof(IDisposable).IsAssignableFrom(typeof(TItem));
        }

        /// <summary>
        /// Appends an item to the queue, potentially removing the
        /// oldest item to maintain the queue size limit.
        /// </summary>
        /// <param name="message">The message to be appended.</param>
        public new void Enqueue(TItem message)
        {
            if (this.Count >= maxItems)
            {
                if (disposable)
                    ((IDisposable)base.Dequeue()).Dispose();
                else
                    base.Dequeue();
            }

            base.Enqueue(message);
        }
    }
}
