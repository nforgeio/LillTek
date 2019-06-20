//-----------------------------------------------------------------------------
// FILE:        RoutingScope.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumeration that defines how far a message can be routed.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Enumeration that defines how far a message can be routed.
    /// </summary>
    public enum RoutingScope
    {
        /// <summary>
        /// No limits are placed on the message routing scope.
        /// </summary>
        Unlimited = 0,

        /// <summary>
        /// Messages will not be routed outside the current process.
        /// </summary>
        Process = 1,

        /// <summary>
        /// Messages will not be routed outside the current machine.
        /// </summary>
        Machine = 2,

        /// <summary>
        /// Messages will not be routed outside the current subnet.
        /// </summary>
        Subnet = 3
    }
}
