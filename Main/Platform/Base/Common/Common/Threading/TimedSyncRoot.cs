//-----------------------------------------------------------------------------
// FILE:        TimedSyncRoot.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simple implementation of ILockable to be used by as a target
//              by TimedLock for thread synchronization.

using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// A simple implementation of <see cref="ILockable" /> to be used by as a target
    /// by <see cref="TimedLock" /> for thread synchronization.
    /// </summary>
    public sealed class TimedSyncRoot : ILockable
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public TimedSyncRoot()
        {
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
