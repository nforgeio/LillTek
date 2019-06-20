//-----------------------------------------------------------------------------
// FILE:        InterlockedCounter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a high performance thread-safe 64-bit counter.

using System;
using System.Threading;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a high performance thread-safe 64-bit counter.
    /// </summary>
    public sealed class InterlockedCounter
    {
        private long counter = 0;

        /// <summary>
        /// Returns the current count.
        /// </summary>
        public long Count
        {
            get { return Interlocked.Read(ref counter); }
        }

        /// <summary>
        /// Increments the counter.
        /// </summary>
        public void Increment()
        {
            Interlocked.Increment(ref counter);
        }
    }
}
