//-----------------------------------------------------------------------------
// FILE:        RouteDistance.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumeration that describes the routing distance between two
//              logical routes.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Enumeration that describes the routing distance between two logical routes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is calculated by <see cref="LogicalRouteTable.ComputeDistance" /> and
    /// is used for implementing local routing.  Note that the enumeration values are
    /// ordered so that closer distance values are less than further distance values.
    /// </para>
    /// </remarks>
    public enum RouteDistance
    {
        /// <summary>
        /// The endpoints are located in the same process.
        /// </summary>
        Process = 0,

        /// <summary>
        /// The endpoints are on the same machine.
        /// </summary>
        Machine = 1,

        /// <summary>
        /// The endpoints are on the same subnet.
        /// </summary>
        Subnet = 2,

        /// <summary>
        /// The endpoints are located in the same datacenter.  (Reserved for future use).
        /// </summary>
        Datacenter = 3,

        /// <summary>
        /// The endpoints are located in different datacenters.
        /// </summary>
        External = 4,

        /// <summary>
        /// The distance cannot be determined.
        /// </summary>
        Unknown = 5
    }
}
