//-----------------------------------------------------------------------------
// FILE:        EntityState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the state of an entity including its recent GeoFixes.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Holds the state of an entity including its recent <see cref="GeoFix" />es.
    /// </summary>
    internal class EntityState
    {
        //---------------------------------------------------------------------
        // Private types

        private struct GroupMembership
        {
            public string       GroupID;
            public DateTime     FixTimeUtc;
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The maximum number of to be cached per entity.
        /// </summary>
        public static int MaxEntityFixes = 10;

        private static string[] NoGroups = new string[0];

        /// <summary>
        /// <b>Unit testing only:</b> Used by unit tests to reset the global class state.
        /// </summary>
        internal static void Reset()
        {
            MaxEntityFixes = 10;
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<GeoFix>        fixes;      // The cached location fixes (also the thread sync root)
        private GroupMembership[]   groups;     // The groups this entity belongs to (or null).  An
                                                // assigned array will be considered to be invariant.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        public EntityState(string entityID)
        {
            this.EntityID   = entityID;
            this.groups     = null;
            this.CurrentFix = null;
            this.fixes      = new List<GeoFix>(MaxEntityFixes);
        }

        /// <summary>
        /// Constructs a clone of the instance passed.
        /// </summary>
        /// <param name="source">The source instance.</param>
        private EntityState(EntityState source)
        {
            this.EntityID   = source.EntityID;
            this.groups     = source.groups;
            this.CurrentFix = source.CurrentFix;
            this.fixes      = new List<GeoFix>(source.fixes.Count);

            foreach (var fix in source.fixes)
                fixes.Add(fix);
        }

        /// <summary>
        /// Returns a deepish clone of the instance.
        /// </summary>
        /// <returns>The clone.</returns>
        /// <remarks>
        /// The clone includes a new copy of the fixes list.
        /// </remarks>
        public EntityState Clone()
        {
            return new EntityState(this);
        }

        /// <summary>
        /// Returns the antity ID.
        /// </summary>
        public readonly string EntityID;

        /// <summary>
        /// Returns the latest fix for the entity or <c>null</c>.
        /// </summary>
        public GeoFix CurrentFix { get; private set; }

        /// <summary>
        /// Prepends a <see cref="GeoFix" /> to the head of the list of fixes,
        /// dropping the fix from the end to keep the total number of fixes
        /// within the limit passed to the constructor.
        /// </summary>
        /// <param name="fix">The received fix.</param>
        /// <param name="groupID">The associated group ID or <c>null</c>.</param>
        public void AddFix(GeoFix fix, string groupID)
        {
            // Use server time for the timestamp if necessary.

            if (fix.TimeUtc.HasValue)
            {
                var now = DateTime.UtcNow;

                // Don't allow fixes to be recorded for the future.  This could happen due
                // to clock skew from the source or possibly software bugs.  Allowing a fix
                // from the future would effectively cause all subsequent fixes to be ignored
                // which seems like a really bad thing (perhaps requiring server restart in
                // extreme circumstances).

                if (fix.TimeUtc > now)
                    fix.TimeUtc = now;
            }
            else
                fix.TimeUtc = DateTime.UtcNow;

            lock (fixes)
            {
                // This is really simple and somewhat inefficient.  I'm just going
                // to insert the new fix at the beginning of the list, making sure 
                // that the list size remains below the limit.

                while (fixes.Count >= MaxEntityFixes)
                    fixes.RemoveAt(fixes.Count - 1);

                fixes.Insert(0, fix);

                if (CurrentFix == null)
                    CurrentFix = fix;
                else if (CurrentFix.TimeUtc <= fix.TimeUtc)
                    CurrentFix = fix;

                if (groupID != null)
                    AddGroup(groupID, fix.TimeUtc.Value);
            }
        }

        /// <summary>
        /// Returns an array holding the entities <see cref="GeoFix" />es.
        /// </summary>
        /// <returns>An array of the fixes held by the entity.</returns>
        /// <remarks>
        /// <note>
        /// The fixes in the array returned are in the order received by
        /// the server, with the most recent fix being the first element
        /// in the array.  Note that the fixes may not be sorted by 
        /// <see cref="GeoFix.TimeUtc" /> property though, because some clients
        /// may hold fixes for a period of time and then upload them later.
        /// </note>
        /// </remarks>
        public GeoFix[] GetFixes()
        {
            lock (fixes)
                return fixes.ToArray();
        }

        /// <summary>
        /// Returns an invariant array of the groups this entity belongs to.
        /// </summary>
        /// <returns>The array or group IDs.</returns>
        public string[] GetGroups()
        {
            lock (fixes)
            {
                if (groups == null)
                    return NoGroups;

                var result = new string[groups.Length];

                for (int i = 0; i < groups.Length; i++)
                    result[i] = groups[i].GroupID;

                return result;
            }
        }

        /// <summary>
        /// Removes all fixes and group memberships with timestamps older than the specified value.
        /// </summary>
        /// <param name="purgeTimeUtc">The minimum age for retained fixes.</param>
        /// <returns><c>true</c> if the all fixes have been purged from the entity.</returns>
        public bool Purge(DateTime purgeTimeUtc)
        {
            int cPurged = 0;

            lock (fixes)
            {
                // Set the slots for purged fixes to NULL.

                for (int i = 0; i < fixes.Count; i++)
                    if (fixes[i].TimeUtc < purgeTimeUtc)
                    {

                        fixes[i] = null;
                        cPurged++;
                    }

                // Now go back and collapse out the null entries.

                if (cPurged == 0)
                {

                    CurrentFix = null;
                    return fixes.Count == 0;
                }

                int pos = 0;

                for (int i = 0; i < fixes.Count; i++)
                    if (fixes[i] != null)
                        fixes[pos++] = fixes[i];

                for (int i = 0; i < cPurged; i++)
                    fixes.RemoveAt(fixes.Count - 1);

                if (fixes.Count == 0)
                {

                    groups = null;
                    CurrentFix = null;
                    return true;
                }
                else
                {
                    if (groups != null)
                    {
                        // Determine whether we need to remove any group references.

                        int retainCount = 0;

                        foreach (var group in groups)
                            if (group.FixTimeUtc >= purgeTimeUtc)
                                retainCount++;

                        if (retainCount < groups.Length)
                        {
                            var newGroups = new GroupMembership[retainCount];

                            pos = 0;
                            foreach (var group in groups)
                                if (group.FixTimeUtc >= purgeTimeUtc)
                                    newGroups[pos++] = group;

                            groups = newGroups;
                        }
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Adds membership to a group if the membership is not already known.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <param name="fixTimeUtc">The fix time.</param>
        /// <remarks>
        /// <note>
        /// This implementation assumes that entities will be members of only a handfull
        /// of groups (something like 10 or less),
        /// </note>
        /// </remarks>
        private void AddGroup(string groupID, DateTime fixTimeUtc)
        {
            Assertion.Test(groupID != null);

            if (groups == null)
            {
                groups =
                    new GroupMembership[] {
                    
                        new GroupMembership() {

                            GroupID    = groupID,
                            FixTimeUtc = fixTimeUtc
                        }
                    };

                return;
            }

            if (IsMemberOf(groupID))
                return;

            var newGroups = new GroupMembership[groups.Length + 1];

            // I'm putting the new group at the front of the list since
            // under many circumstances fixes for older groups will tend not
            // to be seen anymore and will eventually be flushed from the
            // cache.  This will improve IsMemberOf() performance for many
            // real world cases.

            newGroups[0] = new GroupMembership()
            {
                GroupID = groupID,
                FixTimeUtc = fixTimeUtc
            };

            Array.Copy(groups, 0, newGroups, 1, groups.Length);
            groups = newGroups;
        }

        /// <summary>
        /// Determines whether the entity is a member of a specific group.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <returns><c>true</c> if the entity is a member of the group.</returns>
        public bool IsMemberOf(string groupID)
        {
            lock (fixes)
            {
                if (groups == null)
                    return false;

                foreach (var group in groups)
                    if (String.Compare(groupID, group.GroupID, true) == 0)
                        return true;

                return false;
            }
        }
    }
}
