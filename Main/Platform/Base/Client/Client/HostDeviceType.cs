//-----------------------------------------------------------------------------
// FILE:        HostDeviceType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the type of device holsting the application.

using System;

namespace LillTek.Client
{
    /// <summary>
    /// Identifies the type of device hosting the application.
    /// </summary>
    public enum HostDeviceType
    {
        /// <summary>
        /// A standard personal including desktop, laptops, and notebooks.
        /// </summary>
        PC,

        /// <summary>
        /// A smart phone device or a device such as an iPod that has 
        /// a similar form factor and user interface.
        /// </summary>
        Phone,

        /// <summary>
        /// A tablet device.
        /// </summary>
        Tablet,
    }
}
