//-----------------------------------------------------------------------------
// FILE:        LimitedQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a queue that limits the number of items based on
//              item count or total queue size.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a queue that limits the number of items based on
    /// item count or total queue size.
    /// </summary>
    /// <typeparam name="TItem">Type of the queued values.</typeparam>
    /// <remarks>
    /// <para>
    /// This class works much like the underlying <see cref="Queue{T}" /> class.  The main
    /// difference is that this implementation can be configured to limit the number
    /// of queued items based on total count or total size (or both).
    /// </para>
    /// <para>
    /// To limit the total number of queued items, set the <see cref="CountLimit" />
    /// parameter to a positive number less than <see cref="int.MaxValue" />.  Then,
    /// if <see cref="Enqueue" /> is called when the number of items queued exceeds
    /// the limit, <see cref="Enqueue" /> will remove items from the front of the
    /// queue until the number of queued items will equal <see cref="CountLimit" />-1
    /// and then the new item will be queued.
    /// </para>
    /// <para>
    /// To limit the total size of the queued items, the <b>TItem</b> type must 
    /// implement <see cref="ISizedItem" /> and <see cref="SizeLimit" /> must
    /// be set to a positive value less than <see cref="int.MaxValue" />.  Then
    /// if <see cref="Enqueue" /> is called when the cummulative size of the
    /// items plus the size of the new item is greater than the limit, the
    /// method will removed items from the front of the queue so that the
    /// size limit will not be exceeded after adding the new item.  The current
    /// cummulative size can be obtained using the <see cref="Size" />
    /// property.
    /// </para>
    /// <para>
    /// <see cref="CountLimit" /> and <see cref="SizeLimit" /> can both be set to
    /// limit both the count and cummulative size of the items in the queue.
    /// </para>
    /// <para>
    /// As discussed above, items are purged automatically from the queue when 
    /// <see cref="Enqueue" /> is called and the class determines that one or
    /// both of the limits will be exceeded.  The class also automatically
    /// purges items from the queue when <see cref="CountLimit" /> or 
    /// <see cref="SizeLimit" /> is set to a threshold below the current state
    /// of the instance.  The property setter will ensure that items are
    /// purged such that the new limit is not exceeded.
    /// </para>
    /// <para>
    /// For queue item types that implement the <see cref="IDisposable" /> interface,
    /// class instances can be configured to call each item's <see cref="IDisposable.Dispose" />
    /// method when the item is automatically purged (or when <see cref="Clear" /> is called).
    /// Simply set the <see cref="AutoDispose" /> property to <c>true</c>.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class LimitedQueue<TItem> : Queue<TItem>
    {
        private const string NotDisposableMsg = "Queued item type does not implement IDisposable.";

        private int     countLimit  = int.MaxValue;     // Maximum number of items allowed
        private int     sizeLimit   = int.MaxValue;     // Maximum cumulative size 
        private bool    autoDispose = false;            // True to automatically call Dispose()
        private int     size        = 0;                // Total size of the queued items
        private bool    isDisposableItem;               // True if TItem implements IDisposable
        private bool    isSizedItem;                    // True if TItem implements ISizedItem

        /// <summary>
        /// Constructor.
        /// </summary>
        public LimitedQueue()
            : base()
        {
            isDisposableItem = typeof(TItem).GetInterface(typeof(IDisposable).FullName) != null;
            isSizedItem      = typeof(TItem).GetInterface(typeof(ISizedItem).FullName) != null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity of the queue.</param>
        public LimitedQueue(int capacity)
            : base(capacity)
        {
            isDisposableItem = typeof(TItem).GetInterface(typeof(IDisposable).FullName) != null;
            isSizedItem      = typeof(TItem).GetInterface(typeof(ISizedItem).FullName) != null;
        }

        /// <summary>
        /// Specifies the maxumim allowed cummulative size of the items in the queue.
        /// </summary>
        /// <remarks>
        /// If a limit is set below the current <see cref="Size" /> then the
        /// property will purge items from the front of the queue until the new
        /// <see cref="Size" /> is below the new limit.  This property defaults
        /// to <see cref="int.MaxValue" />.
        /// </remarks>
        /// <exception cref="NotSupportedException">Thrown if the item type doesn't implement <see cref="ISizedItem" />.</exception>
        /// <exception cref="ArgumentException">Thrown if the value passed is not valid.</exception>
        public int SizeLimit
        {
            get { return sizeLimit; }

            set
            {
                if (value <= 0)
                    throw new ArgumentException("SizeLimit must be greater than zero.");

                sizeLimit = value;
                Purge();
            }
        }

        /// <summary>
        /// Specifies the maximum number of items allowed in the queue.
        /// </summary>
        /// <remarks>
        /// If a limit is set below the current <see cref="Queue{T}.Count" /> then the
        /// property will purge items from the front of the queue until the new
        /// <see cref="Queue{T}.Count" /> is below the new limit.  This property defaults
        /// to <see cref="int.MaxValue" />.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the value passed is not valid.</exception>
        public int CountLimit
        {
            get { return countLimit; }

            set
            {
                if (value <= 0)
                    throw new ArgumentException("CountLimit must be greater than zero.");

                countLimit = value;
                Purge();
            }
        }

        /// <summary>
        /// Indicates that the items that implement <see cref="IDisposable" /> should be
        /// automatically disposed when the item is purged to maintain a limit or
        /// when <see cref="Clear" /> is called.
        /// </summary>
        /// <remarks>
        /// This property defaults to <c>false</c>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the queued item type does not implement <see cref="IDisposable" />.</exception>
        public bool AutoDispose
        {
            get
            {
                if (!isDisposableItem)
                    throw new InvalidOperationException(NotDisposableMsg);

                return autoDispose;
            }

            set
            {

                if (!isDisposableItem)
                    throw new InvalidOperationException(NotDisposableMsg);

                autoDispose = value;
            }
        }

        /// <summary>
        /// Returns the current cummulative size of the items in the queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the item type doesn't implement <see cref="ISizedItem" />.</exception>
        public int Size
        {
            get
            {
                if (!isSizedItem)
                    throw new InvalidOperationException("Queued item type does not implement ISizedItem.");

                return size;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the item type implements <see cref="ISizedItem" />.
        /// </summary>
        protected bool Sizedtem
        {
            get { return isSizedItem; }
        }

        /// <summary>
        /// Returns <c>true</c> if the item implements <see cref="IDisposable" />.
        /// </summary>
        protected bool DisposableItem
        {
            get { return isDisposableItem; }
        }

        /// <summary>
        /// Returns the internal (non-exception throwing) indicator as to whether
        /// AutoDispose is enabled for the instance.
        /// </summary>
        protected bool AutoDisposeInternal
        {
            get { return autoDispose; }
        }

        /// <summary>
        /// Appends an item to the end of the queue.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <remarks>
        /// The method will first remove any items from the front of the queue
        /// so that when the item is added the size or count limits will not
        /// be exceeded.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the size of the item added exceeds the queue limit.</exception>
        public new void Enqueue(TItem item)
        {
            int itemSize = 0;

            if (isSizedItem)
            {
                itemSize = ((ISizedItem)item).Size;

                if (itemSize < 0)
                    throw new ArgumentException("Item size must be >= 0.");

                if (itemSize > SizeLimit)
                    throw new ArgumentException("Item size exceeds SizeLimit.");

                if (size + itemSize < 0)
                    throw new InvalidOperationException("Size is too close to int.MaxValue.");
            }

            base.Enqueue(item);
            size += itemSize;
            Purge();
        }

        /// <summary>
        /// Removes an item from the front of the queue and returns it.
        /// </summary>
        /// <returns>The dequeued item.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
        public new TItem Dequeue()
        {
            TItem   item;
            int     itemSize = 0;

            item = base.Dequeue();

            if (isSizedItem)
                itemSize = ((ISizedItem)item).Size;

            size -= itemSize;
            Assertion.Test(size >= 0);

            return item;
        }

        /// <summary>
        /// Purge any items from the queue to ensure that the limits are not exceeded.
        /// </summary>
        private void Purge()
        {
            if (isSizedItem && sizeLimit < int.MaxValue)
            {
                while (size > sizeLimit)
                {
                    var item = base.Dequeue();

                    size -= ((ISizedItem)item).Size;
                    if (isDisposableItem && autoDispose)
                        ((IDisposable)item).Dispose();

                    Assertion.Test(size >= 0);
                }
            }

            while (base.Count > countLimit)
            {
                var item = base.Dequeue();

                if (isSizedItem)
                    size -= ((ISizedItem)item).Size;

                if (isDisposableItem && autoDispose)
                    ((IDisposable)item).Dispose();

                Assertion.Test(size >= 0);
            }
        }

        /// <summary>
        /// Removes all items from the queue.
        /// </summary>
        public new void Clear()
        {
            if (isDisposableItem && autoDispose)
            {
                while (base.Count > 0)
                    ((IDisposable)base.Dequeue()).Dispose();
            }
            else
                base.Clear();

            size = 0;
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
            base.TrimExcess();
        }
    }
}
