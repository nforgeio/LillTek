//-----------------------------------------------------------------------------
// FILE:        LRUCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a size limited dictionary cache that prunes items
//              via an LRU algorithm.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a size limited cache that prunes items via 
    /// an LRU algorithm.
    /// </summary>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class implements a cache that limits the number of items stored
    /// within it to the value specified by <see cref="MaxItems" />.   This value
    /// defaults to <see cref="int.MaxValue" /> (essentially unlimited) but this
    /// value can be changed at any time.
    /// </para>
    /// <para>
    /// When the maximum number of items is reached the class will remove one
    /// item for each new item added to keep the total number of items at
    /// the maximum.  Items will be selected for pruning based on how recently
    /// the item was accessed.  Less recently referenced items will be pruned
    /// before more recently referenced items.  The idea here is that recently
    /// accessed items are more likely to be referenced again than items that
    /// haven't been referenced in quiet a while.
    /// </para>
    /// <para>
    /// Items are considered to be referenced when:
    /// </para>
    /// <list type="bullet">
    ///     <item>They are first added to the cache.</item>
    ///     <item>They are referenced by <see cref="ContainsKey" />.</item>
    ///     <item>They are referenced by the indexer or <see cref="TryGetValue" />.</item>
    ///     <item>They are passed to <see cref="Touch" />.</item>
    /// </list>
    /// <para>
    /// The <see cref="AutoDispose" /> property controls whether the class
    /// automatically calls <see cref="IDisposable.Dispose" /> when cached
    /// objects implementing this interface are explicitly or implicitly
    /// removed from the cache.  <b>AutoDispose</b> is initialized to <c>false</c>.
    /// </para>
    /// <para>
    /// Call <see cref="GetHitStats" /> to obtain cache hit/miss statistics.
    /// </para>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public class LRUCache<TKey, TValue>
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Used to hold references to a cache item's key and value so the
        /// item can be maintained by the LRUList.
        /// </summary>
        private sealed class LRUItem : IDLElement
        {

            private TKey        key;
            private TValue      value;
            private object      previous = null;
            private object      next     = null;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="key">The item key.</param>
            /// <param name="value">The item value.</param>
            public LRUItem(TKey key, TValue value)
            {
                this.key   = key;
                this.value = value;
            }

            /// <summary>
            /// Returns the item key.
            /// </summary>
            public TKey Key
            {
                get { return key; }
            }

            /// <summary>
            /// The item value.
            /// </summary>
            public TValue Value
            {
                get { return this.value; }
                set { this.value = value; }
            }

            /// <summary>
            /// The previous object in the list (or the list instance).
            /// </summary>
            public object Previous
            {
                get { return previous; }
                set { previous = value; }
            }

            /// <summary>
            /// The next object in the list (or the list instance).
            /// </summary>
            public object Next
            {
                get { return next; }
                set { next = value; }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private int                         maxItems = int.MaxValue;    // Maximum number of items allowed in the cache
        private LRUList                     lru = new LRUList();        // Least recently used list
        private Dictionary<TKey, LRUItem>   items;                      // The items dictionary table
        private bool                        autoDispose = false;        // True if IDisposable.Dispose() should be called
                                                                        // when cached instances that implement IDisposable
                                                                        // are removed from the cache.
        private int                         cCacheHit = 0;              // # of cache hits since last GetHitRatio()
        private int                         cCacheMiss = 0;             // # of cache misses since last GetHitRatio()

        /// <summary>
        /// Constructs an empty cache.
        /// </summary>
        public LRUCache()
        {
            items = new Dictionary<TKey, LRUItem>();
        }

        /// <summary>
        /// Constructs an empty cache with a specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        public LRUCache(int capacity)
        {
            items = new Dictionary<TKey, LRUItem>(capacity);
        }

        /// <summary>
        /// Constructs an empty cache and associates the specified key comparer.
        /// </summary>
        /// <param name="comparer">The key comparer.</param>
        public LRUCache(IEqualityComparer<TKey> comparer)
        {
            items = new Dictionary<TKey, LRUItem>(comparer);
        }

        /// <summary>
        /// Controls whether the class automatically calls <see cref="IDisposable.Dispose" /> 
        /// when cached objects implementing this interface are explicitly or implicitly
        /// removed from the cache.  <b>AutoDispose</b> is initialized to <c>false</c>.
        /// </summary>
        public bool AutoDispose
        {
            get { return autoDispose; }
            set { autoDispose = value; }
        }

        /// <summary>
        /// Returns the number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                Assertion.Test(items.Count == lru.Count);
                return items.Count;
            }
        }

        /// <summary>
        /// Disposes the item's value if required.
        /// </summary>
        /// <param name="item">The item being removed from the cache.</param>
        private void DisposeItem(LRUItem item)
        {
            IDisposable o;

            if (!autoDispose)
                return;

            o = item.Value as IDisposable;
            if (o != null)
                o.Dispose();
        }

        /// <summary>
        /// The maximum number of items allowed in the cache.
        /// </summary>
        public int MaxItems
        {
            get { return this.maxItems; }

            set
            {
                if (value <= 1)
                    throw new ArgumentException("[MaxItems] must be >= 1.");

                this.maxItems = value;

                // Prune the entries down to maxItems

                while (items.Count > maxItems)
                {

                    LRUItem item;

                    item = (LRUItem)lru.RemoveLRU();
                    items.Remove(item.Key);
                    DisposeItem(item);
                }
            }
        }

        /// <summary>
        /// Returns the collection of cached keys.
        /// </summary>
        public ICollection Keys
        {
            get { return items.Keys; }
        }

        /// <summary>
        /// Returns the collection of cached items.
        /// </summary>
        public ICollection Values
        {
            get { return items.Values; }
        }

        /// <summary>
        /// Returns the cache hit an miss counts since the last time
        /// this method was called.
        /// </summary>
        /// <param name="cHits">Returns as the number of cache hits.</param>
        /// <param name="cMisses">Returns as the number of cache misses.</param>
        /// <remarks>
        /// The class obtains these counts by watching the results
        /// of calls to the <see cref="TryGetValue" /> method and the
        /// class' indexer.
        /// </remarks>
        public void GetHitStats(out int cHits, out int cMisses)
        {
            cHits      = cCacheHit;
            cMisses    = cCacheMiss;
            cCacheHit  = 0;
            cCacheMiss = 0;
        }

        /// <summary>
        /// Adds an item to the cache.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        public void Add(TKey key, TValue value)
        {
            LRUItem item;

            Assertion.Test(items.Count == lru.Count);

            // If the cache is already maxed out then remove
            // the least recently used item to make room for the
            // new one.

            if (items.Count >= maxItems)
            {
                item = (LRUItem)lru.RemoveLRU();
                items.Remove(item.Key);
                DisposeItem(item);
            }

            // Add the new item

            item = new LRUItem(key, value);
            items.Add(key, item);
            lru.Add(item);
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The key of the item to be removed.</param>
        /// <remarks>
        /// <note>
        /// It is is not an error to attempt to remove an
        /// item that is not present in the cache.
        /// </note>
        /// </remarks>
        public void Remove(TKey key)
        {
            LRUItem item;

            Assertion.Test(items.Count == lru.Count);

            if (!items.TryGetValue(key, out item))
                return;

            items.Remove(key);
            lru.Remove(item);
            DisposeItem(item);
        }

        /// <summary>
        /// Removes all items from the cache.
        /// </summary>
        public void Clear()
        {
            Assertion.Test(items.Count == lru.Count);

            if (autoDispose)
            {
                for (int i = 0; i < lru.Count; i++)
                    DisposeItem((LRUItem)lru[i]);
            }

            items.Clear();
            lru.Clear();
        }

        /// <summary>
        /// Returns <c>true</c> if an item referenced by a specified key exists in 
        /// the cache.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns><c>true</c> if the items exists.</returns>
        public bool ContainsKey(TKey key)
        {
            LRUItem item;

            Assertion.Test(items.Count == lru.Count);

            if (!items.TryGetValue(key, out item))
                return false;

            lru.Touch(item);
            return true;
        }

        /// <summary>
        /// Tests to see if an item exists for the specified key, returning
        /// the value if one exists.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">Returns as the value if one exists.</param>
        /// <returns><c>true</c> if the item exists.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            LRUItem item;

            Assertion.Test(items.Count == lru.Count);

            if (!items.TryGetValue(key, out item))
            {
                value = default(TValue);
                cCacheMiss++;
                return false;
            }

            lru.Touch(item);
            value = item.Value;
            cCacheHit++;
            return true;
        }

        /// <summary>
        /// Touches an item by moving it to the most recently used
        /// end of the cache's LRU list.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <remarks>
        /// It is not an error to pass a key for an item that is 
        /// not present in the cache.
        /// </remarks>
        public void Touch(TKey key)
        {
            ContainsKey(key);
        }

        /// <summary>
        /// References the item specified by the key.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The referenced value.</returns>
        public TValue this[TKey key]
        {
            get
            {
                LRUItem item;

                Assertion.Test(items.Count == lru.Count);

                try
                {
                    item = items[key];
                    cCacheHit++;
                    lru.Touch(item);
                    return item.Value;
                }
                catch
                {
                    cCacheMiss++;
                    throw;
                }
            }

            set
            {
                LRUItem item;

                Assertion.Test(items.Count == lru.Count);

                if (items.TryGetValue(key, out item))
                {
                    if (autoDispose && !object.ReferenceEquals(item.Value, value))
                    {
                        IDisposable o = item.Value as IDisposable;

                        if (o != null)
                            o.Dispose();
                    }

                    item.Value = value;
                    lru.Touch(item);
                }
                else
                    Add(key, value);
            }
        }
    }
}
