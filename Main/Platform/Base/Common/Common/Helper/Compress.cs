//-----------------------------------------------------------------------------
// FILE:        Compress.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumeration that defines the possible compression modes.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Enumeration that defines the possible compression modes.
    /// </summary>
    public enum Compress
    {
        /// <summary>
        /// Do not perform any compression.
        /// </summary>
        None,

        /// <summary>
        /// Always perform compression.
        /// </summary>
        Always,

        /// <summary>
        /// Use compression if the result is actually smaller.
        /// </summary>
        Best
    }
}
