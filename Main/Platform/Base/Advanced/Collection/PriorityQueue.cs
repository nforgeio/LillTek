//-----------------------------------------------------------------------------
// FILE:        PriorityQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends the LimitedQueue class to implement support for 
//              high priority items that move to the front of any low
//              priority items in the queue and where high priority items
//              are not impacted by count or size limits.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Extends the <see cref="LimitedQueue{T}" /> class to implement support for high priority 
    /// items that move to the front of any low priority items in the queue and 
    /// where high priority items are not impacted by count or size limits.
    /// </summary>
    /// <typeparam name="TItem">The queued item type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class extends the behavior of <see cref="LimitedQueue{T}" /> by adding the 
    /// <see cref="EnqueuePriority" /> method.  This method adds the item to the queue
    /// placing it before any items already added to the queue via the standard
    /// <see cref="Queue{T}.Enqueue" /> method but after any other priority items already in
    /// the queue.  Priority items are retrieved from the queue like all other
    /// items via the <see cref="Dequeue" /> method.
    /// </para>
    /// <para>
    /// Priority items are not impacted by the queue's <see cref="LimitedQueue{T}.SizeLimit" /> or
    /// <see cref="LimitedQueue{T}.CountLimit" /> settings.  Priority items are always added to
    /// the queue and are never automatically discarded with the exception of 
    /// items purged by <see cref="Clear" />.
    /// </para>
    /// <para>
    /// The class also implements the <see cref="PriorityCount" /> property that
    /// returns the number of priority items in the queue.
    /// </para>
    /// <para>
    /// One final note, for queues of item types that implement <see cref="ISizedItem" />,
    /// the <see cref="ISizedItem.Size" /> property returns the total size of the non-priority items.
    /// The priority item sizes are not included in this count.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class PriorityQueue<TItem> : LimitedQueue<TItem>
    {
        private Queue<TItem> priorityQueue;

        /// <summary>
        /// Constructor.
        /// </summary>
        public PriorityQueue()
            : base()
        {
            priorityQueue = new Queue<TItem>();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity of the queue.</param>
        public PriorityQueue(int capacity)
            : base(capacity)
        {
            priorityQueue = new Queue<TItem>(capacity);
        }

        /// <summary>
        /// Adds a priority item to the queue.  The item will be position after
        /// all other priority items already in the queue but before any non-priority 
        /// items.  The item will always be added regardless of the queue's
        /// <see cref="LimitedQueue{T}.SizeLimit" /> and <see cref="LimitedQueue{T}.CountLimit" /> 
        /// settings.
        /// </summary>
        /// <param name="item">The priority item.</param>
        public void EnqueuePriority(TItem item)
        {
            priorityQueue.Enqueue(item);
        }

        /// <summary>
        /// Returns the next item from the queue.
        /// </summary>
        /// <returns>The next item.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public new TItem Dequeue()
        {
            if (priorityQueue.Count > 0)
                return priorityQueue.Dequeue();
            else
                return base.Dequeue();
        }

        /// <summary>
        /// Returns the item at the front of the queue without removing it.
        /// </summary>
        /// <returns>The next item.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public new TItem Peek()
        {
            if (priorityQueue.Count > 0)
                return priorityQueue.Peek();
            else
                return base.Peek();
        }

        /// <summary>
        /// Returns the total number of items in the queue.
        /// </summary>
        public new int Count
        {
            get { return priorityQueue.Count + base.Count; }
        }

        /// <summary>
        /// Returns the number of priority items in the queue.
        /// </summary>
        public int PriorityCount
        {
            get { return priorityQueue.Count; }
        }

        /// <summary>
        /// Removes all items from the queue.
        /// </summary>
        /// <remarks>
        /// <note>
        /// If the item type implements <see cref="IDisposable" /> and 
        /// <see cref="LimitedQueue{T}.AutoDispose" /> is set to <c>true</c> then each item's
        /// <see cref="IDisposable.Dispose" /> method will be called as it is
        /// removed from the queue.
        /// </note>
        /// </remarks>
        public new void Clear()
        {
            if (base.DisposableItem && base.AutoDisposeInternal)
            {
                while (priorityQueue.Count > 0)
                    ((IDisposable)priorityQueue.Dequeue()).Dispose();
            }
            else
                priorityQueue.Clear();

            base.Clear();
        }

        /// <summary>
        /// Copies the items in the queue to the array passed starting
        /// at the specified position.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="index">Index where the first item is to be copied.</param>
        public new void CopyTo(TItem[] array, int index)
        {
            priorityQueue.CopyTo(array, index);
            base.CopyTo(array, index + priorityQueue.Count);
        }

        /// <summary>
        /// Returns the queued items as an array.
        /// </summary>
        /// <returns></returns>
        public new TItem[] ToArray()
        {
            TItem[] array;

            array = new TItem[priorityQueue.Count + base.Count];
            CopyTo(array, 0);
            return array;
        }

        /// <summary>
        /// Returns <c>true</c> if an item is present in the queue.
        /// </summary>
        /// <param name="item">The item to be located.</param>
        /// <returns><c>true</c> if the item is present.</returns>
        public new bool Contains(TItem item)
        {
            return priorityQueue.Contains(item) || base.Contains(item);
        }

        /// <summary>
        /// Sets the capcacity of the collection to the actual number of items present,
        /// if that number is less than a threshold value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This class follows the .NET Framework standard by not reallocating the 
        /// collection unless that actual number of items is less then 90% of the
        /// current capacity.
        /// </para>
        /// <note>
        /// The threshold comuputation may change for future releases.
        /// </note>
        /// </remarks>
        public new void TrimExcess()
        {
            this.TrimExcess();
            priorityQueue.TrimExcess();
        }
    }
}
