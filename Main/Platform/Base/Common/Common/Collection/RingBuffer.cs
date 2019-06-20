//-----------------------------------------------------------------------------
// FILE:        RingBuffer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a fixed length queue that discards the oldest items
//              in the queue when the queue size is exceeded.

using System;
using System.Collections;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Implements a fixed length queue that discards the oldest items
    /// in the queue when the queue size is exceeded.
    /// </summary>
    public class RingBuffer<TItem>
    {
        private TItem[]     items;      // The queue items
        private int         iNext;      // Index of slot for the next item to be added
        private bool        filled;     // True if the queue has been filled

        /// <summary>
        /// Initializes the queue.
        /// </summary>
        /// <param name="capacity">The maximum number of items allowed in the queue.</param>
        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException();

            this.Capacity = capacity;

            this.items  = new TItem[capacity];
            this.filled = false;
            this.iNext  = 0;
        }

        /// <summary>
        /// Adds the item passed to the queue, discarding the oldest item in the
        /// queue if necessary.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        public void Add(TItem item)
        {
            items[iNext++] = item;
            if (iNext >= items.Length)
            {
                filled = true;
                iNext  = 0;
            }
        }

        /// <summary>
        /// Returns the ring buffer capacity.
        /// </summary>
        public int Capacity { get; private set; }

        /// <summary>
        /// Returns the number of items currently in the queue.
        /// </summary>
        public int Count
        {
            get
            {
                if (filled)
                    return items.Length;
                else
                    return iNext;
            }
        }

        /// <summary>
        /// Returns the indexed item in the queue, where index=0 returns the most
        /// recently added item, index=1 returns the next most recent item
        /// and index=Count-1 returns the oldest item.
        /// </summary>
        public TItem this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new IndexOutOfRangeException();

                if (filled)
                {
                    if (index < iNext)
                        return items[iNext - index - 1];
                    else
                        return items[items.Length - (index - iNext) - 1];
                }
                else
                    return items[iNext - index - 1];
            }
        }
    }
}
