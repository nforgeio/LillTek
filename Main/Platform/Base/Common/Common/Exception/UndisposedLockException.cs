//-----------------------------------------------------------------------------
// FILE:        UndisposedLockException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown for DEBUG builds when a TimedLock instance
//              is never properly disposed.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown for DEBUG builds when a TimedLock instance is never properly disposed.
    /// </summary>
    public sealed class UndisposedLockException : ApplicationException
    {

        private TimedLock tLock;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tLock">The lock instance.</param>
        internal UndisposedLockException(TimedLock tLock)
            : base("A TimedLock instance was never properly disposed.")
        {
            this.tLock = tLock;
        }

        /// <summary>
        /// Returns the failed lock target instance.
        /// </summary>
        public object Target
        {
            get { return tLock.Target; }
        }
    }
}
