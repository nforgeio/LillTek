//-----------------------------------------------------------------------------
// FILE:        ILockable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Lock target objects that implement this interface enable
//              TimedLock to provide extended information when deadlocks are 
//              detected.

using System;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
    /// <summary>
    /// Lock target objects that implement this interface enable
    /// <see cref="TimedLock" /> to provide extended information
    /// when deadlocks are detected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface requires the implementation of only the
    /// <see cref="GetLockKey" /> method.  This method returns
    /// a unique object suitable for use as a hash key by <see cref="TimedLock" />
    /// to locate this object instance within a hash table.  Most
    /// applications should use the <see cref="TimedLock.AllocLockKey" />
    /// method to allocate these keys.
    /// </para>
    /// <para>
    /// This functionality can be implemented with just a few lines
    /// of code and the improvements in deadlock debugging are well
    /// worth the effort.  Here's a sample implementation:
    /// </para>
    /// <code language="cs">
    /// public class MyLockable : ILockable {
    /// 
    ///     private object lockKey = TimedLock.AllocLockKey();
    /// 
    ///     public object GetLockKey() {
    /// 
    ///         return lockKey;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface ILockable
    {
        /// <summary>
        /// Returns the process unique, hashable key to be used to
        /// identify this object instance.
        /// </summary>
        /// <returns>The unique key.</returns>
        object GetLockKey();
    }
}
