//-----------------------------------------------------------------------------
// FILE:        DelayThrottle.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a simple throttling mechanism based on delaying
//              thread execution every time a specified number of operations
//              have completed.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a simple throttling mechanism based on delaying thread execution 
    /// every time a specified number of operations have completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class works by counting the number of times <see cref="Delay" /> is called
    /// and comparing this to the limit passed to the constructor.  Whenever the
    /// count equals or exceeds the limit then the <see cref="Delay" /> method will
    /// cause the current thread to sleep for the delayTime passed to the constructor
    /// and counter will be reset to 0.
    /// </para>
    /// <para>
    /// This class is designed to be used within the context of a single thread.
    /// It is not thread safe.
    /// </para>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public sealed class DelayThrottle
    {
        private static TimeSpan maxDelay = TimeSpan.FromDays(1.0);

        private int     limit;
        private int     count;
        private int     delay;  // milliseconds

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="limit">
        /// Specifies the number of times <see cref="Delay" /> can
        /// be called without actually delaying the current thread.
        /// </param>
        /// <param name="delay">The time delay.</param>
        public DelayThrottle(int limit, TimeSpan delay)
        {
            if (limit <= 0)
                throw new ArgumentException("[limit] must be >= 1.", "limit");

            this.limit = limit;
            this.count = 0;

            if (delay > maxDelay)
                delay = maxDelay;

            this.delay = (int)delay.TotalMilliseconds;
        }

        /// <summary>
        /// Delays the current thread every Nth timer the method
        /// is called where N is the limit parameter passed to
        /// the constructor,
        /// </summary>
        public void Delay()
        {
            count++;
            if (count >= limit)
            {
                Thread.Sleep(delay);
                count = 0;
            }
        }
    }
}
