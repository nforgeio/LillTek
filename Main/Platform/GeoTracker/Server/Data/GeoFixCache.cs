//-----------------------------------------------------------------------------
// FILE:        GeoFixCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the set of recent entity GeoFixes received by a GeoTracker server.

using System;
using System.Collections.Generic;
using System.Threading;

using LillTek.Common;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Holds the set of recent entity <see cref="GeoFix" />es received by a GeoTracker server.
    /// </summary>
    internal class GeoFixCache
    {
        private GeoTrackerServerSettings    settings;       // The node settings
        private bool                        isRunning;      // True if the cache is running
        private ReaderWriterLockSlim        rwLock;         // Used to protect the entity and goup tables
        private PolledTimer                 purgeTimer;     // Fires when its time to purge old cached fixed
        private Thread                      bkThread;       // Background thread that manages adding and purging

        // Dictionary mapping entityIDs to the entity fixes.

        private Dictionary<string, EntityState> entities = new Dictionary<string, EntityState>();

        // Dictionary mapping groupIDs to tables of group entities.

        private Dictionary<string, GroupEntityCollection> groups = new Dictionary<string, GroupEntityCollection>();

        /// <summary>
        /// Constructs and starts the cache instance.
        /// </summary>
        /// <param name="settings">The GeoTracker settings.</param>
        public GeoFixCache(GeoTrackerServerSettings settings)
        {
            this.settings   = settings;
            this.isRunning  = true;
            this.rwLock     = new ReaderWriterLockSlim();
            this.purgeTimer = new PolledTimer(settings.GeoFixPurgeInterval);

            this.bkThread = new Thread(new ThreadStart(BkThread));
            this.bkThread.Name = "GeoTracker: Cache";
            this.bkThread.Start();
        }

        /// <summary>
        /// Stops the cache.
        /// </summary>
        public void Stop()
        {
            if (bkThread == null)
                throw new InvalidOperationException("GeoFixCache is already stopped.");

            isRunning = false;
            bkThread.Join();
            bkThread = null;
        }

        /// <summary>
        /// Returns the approximate number of entities in the cache.
        /// </summary>
        public int EntityCount
        {
            get
            {
                rwLock.EnterReadLock();
                try
                {
                    return entities.Count;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Returns the approximate number of groups in the cache.
        /// </summary>
        public int GroupCount
        {
            get
            {
                rwLock.EnterReadLock();
                try
                {
                    return groups.Count;
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Adds an entity location fix to the cache.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fix">The location <see cref="GeoFix" />.</param>
        public void AddEntityFix(string entityID, string groupID, GeoFix fix)
        {
            AddEntityFixes(entityID, groupID, new GeoFix[] { fix });
        }

        /// <summary>
        /// Adds multiple entity location fixes to the cache.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fixes">The location <see cref="GeoFix" />es.</param>
        public void AddEntityFixes(string entityID, string groupID, GeoFix[] fixes)
        {
            if (entityID == null)
                throw new ArgumentNullException("entityID");

            if (fixes == null)
                throw new ArgumentNullException("fix");

            rwLock.EnterWriteLock();
            try
            {
                EntityState             entityState;
                GroupEntityCollection   group;
                GroupEntity             groupEntity;
                GeoFix                  curFix;
                bool                    addedEntity;
                DateTime                minFixTime;

                // Update the entities table.

                if (!entities.TryGetValue(entityID, out entityState))
                {
                    addedEntity = true;
                    entityState = new EntityState(entityID);
                    entities.Add(entityID, entityState);
                }
                else
                    addedEntity = false;

                // Update the entities fixes and get the current fix (if there is one).

                minFixTime = DateTime.UtcNow - settings.GeoFixRetentionInterval;

                foreach (var fix in fixes)
                {
                    if (!fix.TimeUtc.HasValue)
                        fix.TimeUtc = DateTime.UtcNow;
                    else if (fix.TimeUtc < minFixTime)
                        continue;   // Don't cache fixes that are already too old.

                    entityState.AddFix(fix, groupID);
                }

                curFix = entityState.CurrentFix;

                // This is a bit of a special case where the entity was not already 
                // in the cache but all of the fixes just submitted were too old.
                // So, rather than leaving the empty entity in the cache, we're going
                // to remove it and exit.

                if (curFix == null && addedEntity)
                {
                    entities.Remove(entityID);
                    return;
                }

                // Update the groups table if the entity belongs to a group.

                if (groupID != null)
                {
                    if (!groups.TryGetValue(groupID, out group))
                    {
                        group = new GroupEntityCollection(groupID);
                        groups.Add(groupID, group);
                    }

                    if (!group.TryGetValue(entityID, out groupEntity))
                    {
                        groupEntity = new GroupEntity(entityID, entityState);
                        group.Add(entityID, groupEntity);
                    }

                    groupEntity.UpdateTimeUtc = Helper.Max(DateTime.UtcNow, curFix.TimeUtc.Value);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Determines whether the entity is in the cache and returns an array holding
        /// its <see cref="GeoFix" />es.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        /// <returns>The array of fixes or <c>null</c> if the entity was not found.</returns>
        /// <remarks>
        /// <note>
        /// The fixes in the array returned are in the order received by
        /// the server, with the most recent fix being the first element
        /// in the array.  Note that the fixes may not be sorted by 
        /// <see cref="GeoFix.TimeUtc" /> property though, because some clients
        /// may hold fixes for a period of time and then upload them later.
        /// </note>
        /// </remarks>
        public GeoFix[] GetEntityFixes(string entityID)
        {
            rwLock.EnterReadLock();
            try
            {
                EntityState entity;

                if (entities.TryGetValue(entityID, out entity))
                    return entity.GetFixes();
                else
                    return null;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Determines whether the entity is in the cache and returns its most
        /// recent <see cref="GeoFix" />.
        /// </summary>
        /// <param name="entityID">The entityID.</param>
        /// <returns>The fix or <c>null</c> if the entity was not found.</returns>
        public GeoFix GetCurrentEntityFix(string entityID)
        {
            rwLock.EnterReadLock();
            try
            {
                EntityState entity;

                if (entities.TryGetValue(entityID, out entity))
                    return entity.CurrentFix;
                else
                    return null;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns current location information for the entities belonging to a group.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <returns>An array of <see cref="EntityFix" /> instances with each entity found.</returns>
        /// <remarks>
        /// <note>
        /// The method will return an empty array if <paramref name="groupID" /> is passed as
        /// <c>null</c> or if the requested group does not exist or is empty.
        /// </note>
        /// </remarks>
        public EntityFix[] GetGroupCurrentEntityFixes(string groupID)
        {
            if (groupID == null)
                return new EntityFix[0];

            rwLock.EnterReadLock();
            try
            {
                GroupEntityCollection   groupEntities;
                EntityFix[]             entities;
                int                     i;

                if (!groups.TryGetValue(groupID, out groupEntities))
                    return new EntityFix[0];

                entities = new EntityFix[groupEntities.Count];

                i = 0;
                foreach (var groupEntity in groupEntities.Values)
                    entities[i++] = new EntityFix(groupEntity.EntityID, groupEntity.EntityState.CurrentFix);

                return entities;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns location fixes for entities belonging to a group.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <returns>An array of <see cref="EntityState" /> instances with each entity found.</returns>
        /// <remarks>
        /// <note>
        /// The method will return an empty array if <paramref name="groupID" /> is passed as
        /// <c>null</c> or if the requested group does not exist or is empty.
        /// </note>
        /// </remarks>
        public EntityState[] GetGroupEntities(string groupID)
        {
            if (groupID == null)
                return new EntityState[0];

            rwLock.EnterReadLock();
            try
            {

                GroupEntityCollection   groupEntities;
                EntityState[]           entities;
                int                     i;

                if (!groups.TryGetValue(groupID, out groupEntities))
                    return new EntityState[0];

                entities = new EntityState[groupEntities.Count];

                i = 0;
                foreach (var groupEntity in groupEntities.Values)
                    entities[i++] = groupEntity.EntityState.Clone();

                return entities;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Determines whether a group exists in the cache.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <returns><c>true</c> if the identified group exists.</returns>
        public bool GroupExists(string groupID)
        {
            rwLock.EnterReadLock();
            try
            {
                return groups.ContainsKey(groupID);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Implements the background thread.
        /// </summary>
        private void BkThread()
        {
            while (true)
            {
                if (!isRunning)
                    return;

                if (purgeTimer.HasFired)
                {
                    var purgeTime        = DateTime.UtcNow - settings.GeoFixRetentionInterval;
                    var delEntitiesIDMap = new Dictionary<string, bool>();
                    var delGroupsIDs     = new List<string>();

                    rwLock.EnterWriteLock();
                    try
                    {
                        // Walk the entities removing all old fixes and also making a list
                        // of all entities with no fixes.  Then we'll remove these entities
                        // from the table.

                        foreach (var entity in entities.Values)
                        {
                            if (entity.Purge(purgeTime))
                                delEntitiesIDMap.Add(entity.EntityID, true);
                        }

                        foreach (var entityID in delEntitiesIDMap.Keys)
                            entities.Remove(entityID);

                        // Walk the groups table removing any deleted entities and also making
                        // a list of any groups that end up with no entities.  Then we'll remove
                        // these groups from the table.

                        var delEntityIDs = new List<string>();

                        foreach (var group in groups.Values)
                        {
                            delEntityIDs.Clear();

                            foreach (var entity in group.Values)
                                if (delEntitiesIDMap.ContainsKey(entity.EntityID) || entity.UpdateTimeUtc < purgeTime)
                                    delEntityIDs.Add(entity.EntityID);

                            foreach (var entityID in delEntityIDs)
                                group.Remove(entityID);

                            if (group.Count == 0)
                                delGroupsIDs.Add(group.GroupID);
                        }

                        foreach (var groupID in delGroupsIDs)
                            groups.Remove(groupID);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                    finally
                    {
                        rwLock.ExitWriteLock();
                        purgeTimer.Reset();
                    }
                }

                Thread.Sleep(settings.BkInterval);
            }
        }
    }
}
