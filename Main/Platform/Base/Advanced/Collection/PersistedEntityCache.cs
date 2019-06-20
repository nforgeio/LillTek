//-----------------------------------------------------------------------------
// FILE:        PersistedEntityCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A thread-safe cache of entities loaded from persistent storage.

using System;
using System.Collections.Generic;
using System.Linq;

using LillTek.Advanced;
using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// A thread-safe cache of entities loaded from persistent storage.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This cache stores only reference objects.
    /// </note>
    /// <para>
    /// This class is designed to be used cache entities loaded from a database or other
    /// persistent store or entities that can be reproduced on demand but when it takes
    /// a significant amount of resources to do so.
    /// </para>
    /// <para>
    /// The cache holds entities for a specified period of time and entities that have
    /// been cached for longer than this period will be evicted when an internal periodic
    /// timer is fired or when <see cref="Flush" /> is called.
    /// </para>
    /// <para>
    /// The constructor initializes the cache and starts the flush timer.  <see cref="Stop" />
    /// or <see cref="Dispose" /> stops the cache.  Note that once stopped, a cache cannot
    /// be restarted.  Use the <see cref="Add(TKey,TEntity)" /> method to add an entity to the 
    /// cache with the default lifespan or the <see cref="Add(TKey,TEntity,TimeSpan)" />
    /// override to specify the lifespan.  The <see cref="Get(TKey)" /> method returns the
    /// specified entity from the cache if it exists, <c>null</c> otherwise.
    /// </para>
    /// <para>
    /// The <see cref="Get(TKey,PersistedEntityRetriever{TEntity})" /> and 
    /// <see cref="Get(TKey,PersistedEntityRetriever{TEntity},TimeSpan)" /> methods
    /// can be used to obtain an entity from the cache if it exists or load and cache
    /// one from the database in a single operation.  You'll need to pass a 
    /// <see cref="PersistedEntityRetriever{TEntity}" /> callback that actually loads the
    /// object from persistent storage.
    /// </para>
    /// <para>
    /// The <see cref="Flush" /> method evicts any entities that have exceeded their lifespan
    /// and <see cref="Clear" /> removes all cached entities.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class PersistedEntityCache<TKey, TEntity> : IDisposable
        where TEntity : class
    {

        private object syncLock = new object();
        private TimedLRUCache<TKey, TEntity> lruCache;
        private TimeSpan defTTL;
        private GatedTimer bkTimer;

        /// <summary>
        /// Constructs and starts the cache.
        /// </summary>
        /// <param name="flushInterval">Interval at which the cache will flush expired entities.</param>
        /// <param name="defaultLifespan">The default entity lifespan.</param>
        public PersistedEntityCache(TimeSpan flushInterval, TimeSpan defaultLifespan)
            : this(flushInterval, defaultLifespan, null)
        {
        }

        /// <summary>
        /// Constructs and starts the cache with an optional equality comparer.
        /// </summary>
        /// <param name="flushInterval">Interval at which the cache will flush expired entitys.</param>
        /// <param name="defaultLifespan">The default entity lifespan.</param>
        /// <param name="comparer">The equality comparer or <c>null</c>.</param>
        public PersistedEntityCache(TimeSpan flushInterval, TimeSpan defaultLifespan, IEqualityComparer<TKey> comparer)
        {
            this.lruCache = new TimedLRUCache<TKey, TEntity>(comparer);
            this.bkTimer = new GatedTimer(s => Flush(), null, flushInterval);
            this.defTTL = defaultLifespan;
        }

        /// <summary>
        /// Stops the cache if it is running.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }
            }
        }

        /// <summary>
        /// Releases all resources associated with the cache (essentially stopping it).
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Returns the number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncLock)
                    return lruCache.Count;
            }
        }

        /// <summary>
        /// Adds an entity to the cache with the default lifespan..
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <param name="entity">The entity being cached.</param>
        /// <exception cref="ArgumentNullException">Thrown if either of <paramref name="key" /> or <paramref name="entity" /> is <c>null</c>.</exception>
        public void Add(TKey key, TEntity entity)
        {
            Add(key, entity, defTTL);
        }

        /// <summary>
        /// Adds an entity to the cache with the specified lifespan..
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <param name="entity">The entity being cached.</param>
        /// <param name="lifespan">The maximum duration the entity should remain in the cache.</param>
        /// <exception cref="ArgumentNullException">Thrown if either of <paramref name="key" /> or <paramref name="entity" /> is <c>null</c>.</exception>
        public void Add(TKey key, TEntity entity, TimeSpan lifespan)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (entity == null)
                throw new ArgumentNullException("entity");

            lock (syncLock)
            {
                lruCache.Add(key, entity, lifespan);
            }
        }

        /// <summary>
        /// Searches the cache for an entiyt with the specified key.
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <returns>The entity if found, <c>null</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key" /> is <c>null</c>.</exception>
        public TEntity Get(TKey key)
        {
            lock (syncLock)
            {
                TEntity entity;

                if (lruCache.TryGetValue(key, out entity))
                    return entity;
                else
                    return null;
            }
        }

        /// <summary>
        /// Searches the cache for an entity with the specified key using the optional
        /// entity retriever delegate to fetch or construct the entity if it is not present
        /// in the cache.  This override uses the default entity lifespan.
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <param name="retriever">The entity retriever delegate or <c>null</c>.</param>
        /// <returns>The cached or retreived entity or <c>null</c> if it could not be found or retrieved.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key" /> is <c>null</c>.</exception>
        public TEntity Get(TKey key, PersistedEntityRetriever<TEntity> retriever)
        {
            return Get(key, retriever, defTTL);
        }

        /// <summary>
        /// Searches the cache for an entity with the specified key using the optional
        /// entity retriever delegate to fetch or construct the entity if it is not present
        /// in the cache.  This override accepts a specific entity lifespan.
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <param name="retriever">The entity retriever delegate or <c>null</c>.</param>
        /// <param name="lifespan">The maximum duration that a retreive entities should remain in the cache.</param>
        /// <returns>The cached or retreived entity or <c>null</c> if it could not be found or retrieved.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key" /> is <c>null</c>.</exception>
        public TEntity Get(TKey key, PersistedEntityRetriever<TEntity> retriever, TimeSpan lifespan)
        {
            TEntity entity;

            entity = Get(key);
            if (entity != null)
                return entity;

            if (retriever == null)
                return null;

            entity = retriever();
            if (entity == null)
                return null;

            Add(key, entity, lifespan);

            return entity;
        }

        /// <summary>
        /// Removes a specific entity from the cache if it is present.
        /// </summary>
        /// <param name="key">The key used to identify the entity in the cache.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key" /> is <c>null</c>.</exception>
        public void Remove(TKey key)
        {
            lock (syncLock)
                lruCache.Remove(key);
        }

        /// <summary>
        /// Flushes cached eneities that who's lifetime has expired.
        /// </summary>
        public void Flush()
        {
            lock (syncLock)
                lruCache.Flush();
        }

        /// <summary>
        /// Removes all cached entities.
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
                lruCache.Clear();
        }
    }
}
