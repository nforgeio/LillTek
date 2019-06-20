//-----------------------------------------------------------------------------
// FILE:        EntityFix.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the current GeoFix for an entity.

using System;

using LillTek.Common;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Holds the current GeoFix for an entity.
    /// </summary>
    internal struct EntityFix
    {
        /// <summary>
        /// The entity ID.
        /// </summary>
        public readonly string EntityID;

        /// <summary>
        /// The current <see cref="GeoFix" /> for the entity.
        /// </summary>
        public readonly GeoFix Fix;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityID">The entity ID.</param>
        /// <param name="fix">The current <see cref="GeoFix" /> for the entity.</param>
        public EntityFix(string entityID, GeoFix fix)
        {
            this.EntityID = entityID;
            this.Fix      = fix;
        }
    }
}
