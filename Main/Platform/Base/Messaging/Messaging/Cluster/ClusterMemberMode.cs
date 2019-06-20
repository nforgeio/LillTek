//-----------------------------------------------------------------------------
// FILE:        ClusterMemberMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible ClusterMember startup modes.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Enumerates the possible <see cref="ClusterMember" /> startup modes as
    /// specified by <see cref="ClusterMemberSettings" />.<see cref="ClusterMemberSettings.Mode" />.
    /// </summary>
    public enum ClusterMemberMode
    {
        /// <summary>
        /// Indicates that the mode is not known.  You may see this mode in
        /// <see cref="ClusterMemberStatus" /> if cluster instances are running
        /// on wildly different LillTek Platform versions.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Indicates that a <see cref="ClusterMember" /> should go through 
        /// the normal master election cycle and eventually enter into the
        /// <see cref="ClusterMemberState.Master" /> or <see cref="ClusterMemberState.Slave" />
        /// state.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Indicates that a <see cref="ClusterMember" /> should immediately enter the
        /// <see cref="ClusterMemberState.Observer" /> state and remain there.  Cluster
        /// observer member state is replicated across the cluster so other instances know
        /// about these instances but observers will never be elected as the master.
        /// </summary>
        Observer = 2,

        /// <summary>
        /// Indicates that a <see cref="ClusterMember" /> should immediately enter the 
        /// <see cref="ClusterMemberState.Monitor" /> state and remain there.  Monitors
        /// collect and maintain cluster status but do not actively participate in the
        /// cluster.  No member status information about a monitor will be replicated
        /// across the cluster.
        /// </summary>
        Monitor = 3,

        /// <summary>
        /// Indicates that a <see cref="ClusterMember" /> instance prefers to be
        /// started as a cluster slave if there's another instance running without
        /// this preference.
        /// </summary>
        PreferSlave = 4,

        /// <summary>
        /// Indicates that a <see cref="ClusterMember" /> instances prefers to be
        /// started as the cluster master.  If a master is already running and
        /// it does not have this preference then a master election will be called.
        /// </summary>
        PreferMaster = 5
    }
}
