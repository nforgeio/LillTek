//-----------------------------------------------------------------------------
// FILE:        TimeBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines the possible service time modes.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Defines the possible service time modes.
    /// </summary>
    public enum TimeBase
    {
        /// <summary>
        /// Indicates that services should use the local system time.
        /// </summary>
        Local,

        /// <summary>
        /// Indicates that services should use the local system time
        /// converted to UTC.
        /// </summary>
        UTC,

        /// <summary>
        /// Indicates that services within a cluster should negotate a mechanism
        /// to share a common time base across all cluster instances.
        /// </summary>
        Cluster
    }
}
