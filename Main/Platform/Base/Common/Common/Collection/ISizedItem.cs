//-----------------------------------------------------------------------------
// FILE:        ISizedItem.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface used to obtain the byte size of an object
//              instance.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the interface used to obtain the byte size of an object instance.
    /// This is typically used to implement size limited caches and queues.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The size of an instance is assumed to be fixed during the
    /// lifetime of the object.
    /// </note>
    /// </remarks>
    public interface ISizedItem
    {
        /// <summary>
        /// Returns the size of the instance.
        /// </summary>
        /// <remarks>
        /// Most implementations will return the approximate size of the instance
        /// in bytes but the interpretation of the result is actually application
        /// defined.
        /// </remarks>
        int Size { get; }
    }
}
