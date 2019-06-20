//-----------------------------------------------------------------------------
// FILE:        ElapsedTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Uses the HiResTimer for measuring the execution time of a
//              section of code.

using System;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Uses the <see cref="HiResTimer" /> for measuring the execution time of a
    /// section of code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be used to measure the elapsed time
    /// it takes to run one or more sections of code, using the Windows
    /// <see cref="HiResTimer" /> to obtain accurate measurements.
    /// </para>
    /// <para>
    /// Use the <see cref="ElapsedTimer()" /> constructor but not start the timer
    /// immediately or <see cref="ElapsedTimer(bool)" /> to specify whether
    /// the timer the timer should be started or not.
    /// </para>
    /// <para>
    /// The <see cref="Start" /> method starts the timer (if it's not
    /// already running.  Call <see cref="Stop" /> to stop the timer.
    /// The <see cref="ElapsedTime" /> property returns the measured elapsed time.
    /// Note that <see cref="Start" /> and <see cref="Stop" /> can be
    /// called multiple times to accumulate time measurements over multiple
    /// sections of code or within a loop.  Call <see cref="Reset" />
    /// to set the accumulated time measurement back to zero or <see cref="Restart" />
    /// to reset the timer and start it in one call.
    /// </para>
    /// <para>
    /// The class implements <see cref="IDisposable" />, with <see cref="Dispose" />
    /// calling <see cref="Stop" />.  This makes it easy to surround the code
    /// you wish to measure with a <c>using</c> statement, as shown below:
    /// </para>
    /// <code language="cs">
    /// ElapsedTimer    timer;
    /// 
    /// timer = new ElapsedTimer(true);
    /// using (timer) {
    /// 
    ///     // Code to be measured
    /// }
    /// 
    /// Console.WriteLine("time = {0}",timer.Elapsed);
    /// </code>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class ElapsedTimer : IDisposable
    {

        private TimeSpan    elapsed;
        private bool        started;
        private long        counter;

        /// <summary>
        /// Constructs and but does not start a performance timer.
        /// </summary>
        public ElapsedTimer()
            : this(false)
        {
        }

        /// <summary>
        /// Constructs and optionally starts a performance timer.
        /// </summary>
        /// <param name="start">Pass <c>true</c> to start the timer.</param>
        public ElapsedTimer(bool start)
        {
            elapsed = TimeSpan.Zero;
            started = false;
            counter = 0;

            if (start)
                Start();
        }

        /// <summary>
        /// Starts or restarts the timer, if its not already running.
        /// </summary>
        public void Start()
        {
            if (started)
                return;

            started = true;
            counter = HiResTimer.Count;
        }

        /// <summary>
        /// Stops the timer if its running.
        /// </summary>
        public void Stop()
        {
            if (!started)
                return;

            elapsed += HiResTimer.CalcTimeSpan(counter);
            started = false;
        }

        /// <summary>
        /// Stops the timer if its running and resets the accumulated
        /// elapsed time measurement to zero.
        /// </summary>
        public void Reset()
        {
            started = false;
            elapsed = TimeSpan.Zero;
            counter = 0;
        }

        /// <summary>
        /// Stops the timer if its running and resets the accumulated
        /// elapsed time measurement to zero, and then starts the timer.
        /// </summary>
        /// <remarks>
        /// This is equivalant to calling <see cref="Reset" /> and then <see cref="Start" />.
        /// </remarks>
        public void Restart()
        {
            started = true;
            elapsed = TimeSpan.Zero;
            counter = HiResTimer.Count;
        }

        /// <summary>
        /// Calls <see cref="Stop" />.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Returns the accumulated elapsed time measurement.
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get
            {
                if (started)
                {
                    Stop();
                    Start();
                }

                return elapsed;
            }
        }
    }
}
