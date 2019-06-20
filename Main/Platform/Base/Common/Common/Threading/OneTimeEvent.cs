//-----------------------------------------------------------------------------
// FILE:        OneTimeEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an auto disposing, one-time use thread
//              synchronization event.

using System;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// Implements an auto disposing, one-time use thread synchronization event.
    /// </summary>
    public class OneTimeEvent
    {
        private ManualResetEvent    waitEvent;
        private int                 refCount;
        private bool                setCalled;

        /// <summary>
        /// Constructs an <see cref="OneTimeEvent" /> with the <b>reset</b> state.
        /// </summary>
        public OneTimeEvent()
        {
            waitEvent = new ManualResetEvent(false);
            refCount  = 0;
            setCalled = false;
        }

        /// <summary>
        /// Blocks the current thread until <see cref="Set" /> is called
        /// on another thread.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method maintains a thread reference count and disposes itself
        /// once the last waiting thread is released.
        /// </para>
        /// </remarks>
        public void Wait()
        {
            if (setCalled)
                return;

            Interlocked.Increment(ref refCount);

            waitEvent.WaitOne();

            if (Interlocked.Decrement(ref refCount) <= 0)
                waitEvent.Close();
        }

        /// <summary>
        /// Unlocks any waiting threads.
        /// </summary>
        /// <remarks>
        /// <node>
        /// This method can only be called once per instance.
        /// </node>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Set" /> has already been called.</exception>
        public void Set()
        {
            if (setCalled)
                throw new InvalidOperationException("OnTimeEvent can only be used once.");

            setCalled = true;
            waitEvent.Set();
        }
    }
}
