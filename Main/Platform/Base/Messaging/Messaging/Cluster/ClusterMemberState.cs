//-----------------------------------------------------------------------------
// FILE:        ClusterMemberState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible states of a ClusterMember instance.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Enumerates the possible states of a <see cref="ClusterMember" /> instance.
    /// </summary>
    public enum ClusterMemberState
    {
        /// <summary>
        /// The instance state is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The instance is not running.
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// The instance in a warm up state while it listening for cluster
        /// a status broadcast from a master instance, if one exists.
        /// </summary>
        Warmup = 2,

        /// <summary>
        /// The instance is online in slave mode.
        /// </summary>
        Slave = 3,

        /// <summary>
        /// The instance is online in master mode.
        /// </summary>
        Master = 4,

        /// <summary>
        /// The cluster master has appear to have gone offline and the
        /// cluster is in the process of electing a new master.  All instances
        /// will act as slaves until the election is completed.
        /// </summary>
        Election = 5,

        /// <summary>
        /// The instance is a passive participant in the cluster that will
        /// never be elected as master.  Member status for observers is
        /// replicated across the cluster.
        /// </summary>
        Observer = 6,

        /// <summary>
        /// The instance does not participate in the cluster other than
        /// to monitor the cluster status transmissions.
        /// </summary>
        Monitor = 7
    }
}