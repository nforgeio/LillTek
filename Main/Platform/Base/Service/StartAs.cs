//-----------------------------------------------------------------------------
// FILE:        StartAs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the StartAs enum.

using System;

namespace LillTek.Service
{
    /// <summary>
    /// Identifies how a service is to be started.
    /// </summary>
    public enum StartAs
    {
        /// <summary>
        /// The service started in its default mode.
        /// </summary>
        Default,

        /// <summary>
        /// The service started as a native Windows service.
        /// </summary>
        Native,

        /// <summary>
        /// The service started as a Windows form application.
        /// </summary>
        Form,

        /// <summary>
        /// The service started as a console application.
        /// </summary>
        Console,

        /// <summary>
        /// The service is hosted by a NeonSwitch instance.
        /// </summary>
        NeonSwitch,

        /// <summary>
        /// The service started within another application.
        /// </summary>
        Application,
    }
}
