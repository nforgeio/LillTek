//-----------------------------------------------------------------------------
// FILE:        GroupEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to map entities and groups.

using System;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Used to map entities and groups.
    /// </summary>
    internal class GroupEntity
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        /// <param name="entityState">The entity state.</param>
        public GroupEntity(string entityID, EntityState entityState)
        {
            this.EntityID      = entityID;
            this.EntityState   = entityState;
            this.UpdateTimeUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns the entity ID.
        /// </summary>
        public string EntityID { get; private set; }

        /// <summary>
        /// Returns the entity's location fixes.
        /// </summary>
        public EntityState EntityState { get; private set; }

        /// <summary>
        /// The last time (UTC) that a location fix was received for this
        /// entity and the mapped group.
        /// </summary>
        public DateTime UpdateTimeUtc { get; set; }
    }
}
