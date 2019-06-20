//-----------------------------------------------------------------------------
// FILE:        AppStoreMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible AppStoreHandler operation modes.

using System;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Defines the possible <see cref="AppStoreHandler" /> operation modes.
    /// </summary>
    public enum AppStoreMode
    {
        /// <summary>
        /// Indicates that the <see cref="AppStoreHandler" /> instance should operate
        /// as the primary store in the cluster.
        /// </summary>
        Primary,

        /// <summary>
        /// Indicates that the <see cref="AppStoreHandler" /> instance should operate
        /// as a caching store in the cluster.
        /// </summary>
        Cache
    }
}
