//-----------------------------------------------------------------------------
// FILE:        GroupEntityCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to hold the EntityFixes that for the entities that belong to a group.

using System;
using System.Collections.Generic;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Used to hold the <see cref="EntityState" /> that for the entities that belong to a group.
    /// </summary>
    internal class GroupEntityCollection : Dictionary<string, GroupEntity>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        public GroupEntityCollection(string groupID)
        {
            this.GroupID = groupID;
        }

        /// <summary>
        /// The group ID.
        /// </summary>
        public string GroupID { get; private set; }
    }
}
