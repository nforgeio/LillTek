//-----------------------------------------------------------------------------
// FILE:        ServiceState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible service states.

using System;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Describes the current state of a service.
    /// </summary>
    public enum ServiceState
    {
        /// <summary>
        /// The current service state is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// The service is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The service is in the process of starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The service is running normally.
        /// </summary>
        Running,

        /// <summary>
        /// The service is performing a graceful shutdown.
        /// </summary>
        Shutdown,

        /// <summary>
        /// The service is stopping.
        /// </summary>
        Stopping
    }
}