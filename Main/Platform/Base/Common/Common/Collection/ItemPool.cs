//-----------------------------------------------------------------------------
// FILE:        ItemPool.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to reference a subset of a byte buffer.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Implements pool of object instances that can be reused over period of time
    /// without incurring the overhead of allocating and garbage collecting from the head.
    /// </summary>
    /// <typeparam name="T">The type of the object being pooled.</typeparam>
    /// <remarks>
    /// <para>
    /// The class can be useful for high-performance applications where the overhead of 
    /// allocating and garbage collecting objects is detrimental.
    /// </para>
    /// <para>
    /// By default, the constructor will create an empty pool that uses the type's
    /// default parameterless constructor to allocate item instances.  Optional
    /// parameters may be passed to specify a custom item allocation function, the
    /// maximum number of items to be allocated by the pool, and whether the
    /// constructor should preallocate the items.
    /// </para>
    /// <para>
    /// The synchronous <see cref="Allocate"/> and see <see cref="Release"/> methods
    /// can be used to allocate items and then return them to the pool.  Note that
    /// the application must take care to ensure that all allocated items are eventually
    /// released even as exceptions are thrown and cancelled.
    /// </para>
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    public class ItemPool<T>
        where T : class
    {
        private object      syncLock = new object();
        private Func<T>     allocator;
        private int         maxItems;
        private List<T>     items;
        private int         count;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="allocator">Optional function that performs custom allocation.</param>
        /// <param name="maxItems">Optionally specifies the maximum number of items to be allocated.</param>
        /// <param name="preallocate">Optionally specifies that the maximum number of items will be preallocated by the constructor.</param>
        /// <param name="gcPromote">
        /// Optionally specifies that <see cref="Helper.GCPromote"/> will be called on the last preallocated
        /// item to ensure that it and any other older live items in the heap will be moved to the oldest
        /// object generation.  See <see cref="Helper.GCPromote"/> for more information.
        /// </param>
        /// <remarks>
        /// <para>
        /// By default, the pool will use the item type's default constructor to create items.  Item
        /// allocation can be customized by passing a custom method as <paramref name="allocator"/>.
        /// </para>
        /// <para>
        /// The maximum number of items that will be allocated by the pool can be specified via
        /// <paramref name="maxItems"/> and <paramref name="preallocate"/> can be passed as <c>true</c>
        /// to have the constructor preallocate the items.
        /// </para>
        /// </remarks>
        public ItemPool(Func<T> allocator = null, int maxItems = int.MaxValue, bool preallocate = false, bool gcPromote = false)
        {
            if (maxItems <= 0)
            {
                throw new ArgumentException("[maxItems] must be positive.");
            }

            if (!(!preallocate || maxItems < int.MaxValue))
            {
                throw new InvalidOperationException("ItemPool: Cannot preallocate items unless [maxItems] is set.");
            }

            if (allocator == null)
            {
                var assembly = typeof(T).Assembly;
                var typeName = typeof(T).FullName;

                allocator = () => (T)assembly.CreateInstance(typeName);
            }

            this.allocator = allocator;
            this.maxItems  = maxItems;

            if (preallocate)
            {
                this.items = new List<T>(maxItems);

                for (int i = 0; i < maxItems; i++)
                {
                    this.items.Add(allocator());
                }

                this.count = maxItems;

                if (gcPromote && maxItems > 0)
                {
                    Helper.GCPromote(this.items.Last());
                }
            }
            else
            {
                this.items = new List<T>();
                this.count = 0;
            }
        }

        /// <summary>
        /// Returns the maximum number of items allowed in the pool.
        /// </summary>
        public int MaxItems
        {
            get { return this.maxItems; }
        }

        /// <summary>
        /// Returns the current number of items in the pool.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncLock)
                {
                    return items.Count;
                }
            }
        }

        /// <summary>
        /// Allocates an item from the pool.
        /// </summary>
        /// <returns>The item allocated or <c>null</c> if the maximum number of items have already been allocated.</returns>
        public T Allocate()
        {
            T item = null;

            lock (syncLock)
            {
                // Try to allocate an item from the pool.

                if (items.Count > 0)
                {
                    var lastIndex = items.Count - 1;
                    
                    item = items[lastIndex];
                    items.RemoveAt(lastIndex);
                    
                    return item;
                }

                if (item != null)
                {
                    return item;
                }

                // The pool was empty so allocate a new instance if we haven't
                // reached the instance count limit.

                if (count < maxItems)
                {
                    count++;
                    return allocator();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns an item to the pool.
        /// </summary>
        /// <param name="item">The item being returned.</param>
        public void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            lock (syncLock)
            {
                items.Add(item);
            }
        }
    }
}
