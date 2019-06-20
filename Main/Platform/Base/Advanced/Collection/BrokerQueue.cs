//-----------------------------------------------------------------------------
// FILE:        BrokerQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A thread-safe intermediary queue between code that produces
//              items and code that consumes them.

using System;
using System.Threading;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// A thread-safe intermediary queue between code that produces items and code 
    /// that consumes them.
    /// </summary>
    /// <typeparam name="TItem">Type of the item being produced and consumed.</typeparam>
    /// <remarks>
    /// <para>
    /// A somewhat tricky multithreading problem centers around applications
    /// that have one or more producer threads queuing objects that need to 
    /// be consumed by one or more consumer threads.  
    /// </para>
    /// <para>
    /// This generic class handles this easily.  Use <see cref="BrokerQueue{TItem}" />
    /// to construct an instance.  Producer threads will call <see cref="Enqueue" />
    /// to add items to the queue and consumer threads will call
    /// <see cref="Dequeue" /> to retrieve items from the queue.  <see cref="Dequeue" />
    /// blocks until an item is available.
    /// </para>
    /// <para>
    /// Call <see cref="Close" /> or <see cref="Dispose" /> when you're done with
    /// the queue.  Any pending or subsequent calls to <see cref="Dequeue" />
    /// will return <c>default(TItem)</c>.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class BrokerQueue<TItem> : IDisposable
    {
        private object              syncLock = new object();
        private QueueArray<TItem>   queue;
        private AutoResetEvent      wait;
        private bool                isOpen;
        private int                 cDequeue;

        /// <summary>
        /// Constructor.
        /// </summary>
        public BrokerQueue()
        {
            this.queue    = new QueueArray<TItem>();
            this.wait     = new AutoResetEvent(false);
            this.isOpen   = true;
            this.cDequeue = 0;
        }

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item">The new item.</param>
        public void Enqueue(TItem item)
        {
            lock (syncLock)
            {
                if (!isOpen)
                    throw new ObjectDisposedException(this.GetType().FullName);

                queue.Enqueue(item);

                if (cDequeue > 0)
                    wait.Set();
            }
        }

        /// <summary>
        /// Returns an item from the queue, blocking until an item is available
        /// or the queue is closed.
        /// </summary>
        /// <returns>The item retrieved or <c>default(TItem)</c> if the queue has been closed.</returns>
        public TItem Dequeue()
        {
            while (true)
            {
                lock (syncLock)
                {
                    if (!isOpen)
                        return default(TItem);

                    if (queue.Count > 0)
                        return queue.Dequeue();

                    cDequeue++;
                }

                wait.WaitOne();

                lock (syncLock)
                {
                    cDequeue--;
                    Assertion.Test(cDequeue >= 0);

                    try
                    {
                        if (!isOpen)
                            return default(TItem);

                        if (queue.Count > 0)
                            return queue.Dequeue();
                    }
                    finally
                    {
                        if (!isOpen || (cDequeue > 0 && queue.Count > 0))
                            wait.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the number of items in the queue.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncLock)
                    return queue.Count;
            }
        }

        /// <summary>
        /// Closes the broker.  All pending <see cref="Dequeue" /> 
        /// calls will return <c>null</c>.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (!isOpen)
                    return;

                queue = null;
                isOpen = false;

                if (cDequeue == 0)
                    wait.Close();
                else
                    wait.Set();
            }
        }

        /// <summary>
        /// Closes the broker.  All pending <see cref="Dequeue" /> 
        /// calls will return <c>null</c>.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
