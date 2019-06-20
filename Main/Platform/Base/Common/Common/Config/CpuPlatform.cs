//-----------------------------------------------------------------------------
// FILE:        CpuPlatform.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the CPU type and mode under which the current 
//              application is running.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Describes the CPU type and mode under which the current 
    /// application is running
    /// </summary>
    public enum CpuPlatform
    {
        /// <summary>
        /// The current platform cannot be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 32-bit Intel x86 compatible.
        /// </summary>
        x86_32,

        /// <summary>
        /// 32-bit Intel x86 compatible.
        /// </summary>
        x86_64,

        /// <summary>
        /// ARM processor.
        /// </summary>
        ARM
    }
}
