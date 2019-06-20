//-----------------------------------------------------------------------------
// FILE:        IAsyncResultOwner.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: 

using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// <see cref="AsyncResult" /> operation owners may optionally implement this interface
    /// to expose additional information to the <see cref="AsyncTracker" />.
    /// </summary>
    public interface IAsyncResultOwner
    {
        /// <summary>
        /// Returns a human-readable name identifying an owner (or <c>null</c>).
        /// </summary>
        string OwnerName { get; }

        /// <summary>
        /// Indicates that the AsyncTracker should not test any of this object's
        /// async operations for hung operations.
        /// </summary>
        bool DisableHangTest { get; }
    }
}
