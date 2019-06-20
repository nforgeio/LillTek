//-----------------------------------------------------------------------------
// FILE:        ClusterMemberEventHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the ClusterMember event related types.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds the arguments passed when a <see cref="ClusterMember" /> instance
    /// raises one of its events.
    /// </summary>
    public sealed class ClusterMemberEventArgs
    {
        /// <summary>
        /// Indicates the orignal instance state before a state change.  This is valid
        /// when the <see cref="ClusterMember.StateChange" /> event is raised.
        /// </summary>
        public ClusterMemberState OriginalState;

        /// <summary>
        /// Indicates the new instance state after a state change.  This is valid
        /// when the <see cref="ClusterMember.StateChange" /> event is raised.
        /// </summary>
        public ClusterMemberState NewState;
    }

    /// <summary>
    /// Used to define method delegates that will called when particular
    /// events are raised by a <see cref="ClusterMember" /> instance.
    /// </summary>
    /// <param name="sender">The <see cref="ClusterMember" /> instance that raised this event.</param>
    /// <param name="args">The event arguments.</param>
    public delegate void ClusterMemberEventHandler(ClusterMember sender, ClusterMemberEventArgs args);

}