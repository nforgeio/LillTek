//-----------------------------------------------------------------------------
// FILE:        AsyncTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a global timer that follows the BeginXXX/EndXXX
//              asynchronous programming model.

using System;
using System.Threading;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Implements a global timer that follows the BeginXXX/EndXXX asynchronous 
    /// programming model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call the class's <see cref="BeginTimer" /> to schedule an asynchronous
    /// timer event, passing the duration of the timer as a parameter.  The timer
    /// operation will be considered complete when at least the specified amount
    /// of time has elapsed.  At that point, the call back passed to <see cref="BeginTimer" />
    /// will be called (if any) and the <see cref="IAsyncResult.AsyncWaitHandle" /> 
    /// property in the <see cref="BeginTimer" /> will be set.  At this point, 
    /// <see cref="EndTimer" /> should be called to complete the operation.
    /// </para>
    /// <note>
    /// Unlike the <see cref="Timer" /> or <see cref="GatedTimer" />
    /// classes, this method only fires a timer only once per call to <see cref="BeginTimer" />.
    /// </note>
    /// <para><b><u>Implementation</u></b></para>
    /// <para>
    /// The class implemented using a single <see cref="GatedTimer" /> instance
    /// configured to fire on 500ms intervals by default.  Calls to <see cref="BeginTimer" />
    /// add information to an internal table relating the scheduled firing time specified
    /// to the <see cref="IAsyncResult" /> instance returned.  Every time the global
    /// <see cref="GatedTimer" /> is fired, this class will search for the timer events
    /// whose scheduled firing time has been reached and will perform the appropriate
    /// notifications.
    /// </para>
    /// <para>
    /// The period of the global <see cref="GatedTimer" /> instance can be adjusted
    /// by setting the <see cref="PollInterval" /> property.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public static class AsyncTimer
    {
        //---------------------------------------------------------------------
        // Local classes

        /// <summary>
        /// Used for tracking the timer operation.
        /// </summary>
        private sealed class AsyncTimerResult : AsyncResult
        {
            /// <summary>
            /// Time-to-fire for this timer (SYS).
            /// </summary>
            public DateTime TTF;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="ttf">Time-to-fire for this timer (SYS).</param>
            /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
            /// <param name="state">Application specific state.</param>
            public AsyncTimerResult(DateTime ttf, AsyncCallback callback, object state)
                : base(null, callback, state)
            {
                this.TTF = ttf;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static object                       syncLock = new object();
        private static LinkedList<AsyncTimerResult> pending;
        private static TimeSpan                     period;
        private static GatedTimer                   timer;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static AsyncTimer()
        {
            pending = new LinkedList<AsyncTimerResult>();
            period  = TimeSpan.FromMilliseconds(500);
            timer   = new GatedTimer(new TimerCallback(OnTimer), null, period);
        }

        /// <summary>
        /// The timer callback.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnTimer(object state)
        {
            var deleted = new List<AsyncTimerResult>();
            var now = SysTime.Now;

            lock (syncLock)
            {
                foreach (AsyncTimerResult ar in pending)
                {
                    if (ar.TTF <= now)
                    {
                        ar.Notify();
                        deleted.Add(ar);
                    }
                }

                for (int i = 0; i < deleted.Count; i++)
                    pending.Remove(deleted[i]);
            }
        }

        /// <summary>
        /// Specifies the period at which the underlying <see cref="GatedTimer" /> fires.
        /// </summary>
        public static TimeSpan PollInterval
        {
            get { return period; }

            set
            {
                lock (syncLock)
                {
                    timer.Dispose();

                    period = value;
                    timer  = new GatedTimer(new TimerCallback(OnTimer), null, period);
                }
            }
        }

        /// <summary>
        /// Schedules a timer event for a specified duration.
        /// </summary>
        /// <param name="duration">The time to wait before firing the timer.</param>
        /// <param name="callback">The delegate to be called when the timer fires (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// The operation will complete when at least the specified amount of time has
        /// elapsed.  Note that the background timer used to implement this functionality
        /// is configured by default to fire at the relatively coarse period of 500ms
        /// so this method should not be depended on to provide accurate timings.  This
        /// period can be altered by setting <see cref="PollInterval" />.
        /// </para>
        /// <para>
        /// Every call to this method must be matched with a call to <see cref="EndTimer" />.
        /// </para>
        /// </remarks>
        public static IAsyncResult BeginTimer(TimeSpan duration, AsyncCallback callback, object state)
        {
            AsyncTimerResult arTimer;

            arTimer = new AsyncTimerResult(SysTime.Now + duration, callback, state);
            arTimer.Started();

            lock (syncLock)
                pending.AddLast(arTimer);

            return arTimer;
        }

        /// <summary>
        /// Completes an asynchronous timer operation.
        /// </summary>
        /// <param name="ar">
        /// The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTimer" />.
        /// </param>
        /// <exception cref="CancelException">
        /// Thrown if the timer was cancelled via <see cref="CancelTimer" /> or <see cref="CancelAll" />.
        /// </exception>
        public static void EndTimer(IAsyncResult ar)
        {
            var arTimer = (AsyncTimerResult)ar;

            arTimer.Wait();
            try
            {
                if (arTimer.Exception != null)
                    throw arTimer.Exception;
            }
            finally
            {
                arTimer.Dispose();
            }
        }

        /// <summary>
        /// Causes an uncompleted asychronous timer operation to be cancelled.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTimer" />.</param>
        /// <remarks>
        /// <note>
        /// <see cref="EndTimer" /> must still be called in the callback or
        /// after waiting for the completion event to fire.  <see cref="EndTimer" />
        /// will throw a <see cref="CancelException" />.
        /// </note>
        /// </remarks>
        public static void CancelTimer(IAsyncResult ar)
        {
            var arTimer = (AsyncTimerResult)ar;

            lock (syncLock)
            {
                if (pending.Remove(arTimer))
                    arTimer.Notify(new CancelException());
            }
        }

        /// <summary>
        /// Cancels all pending timers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Each pending timer's completion event will still be fired
        /// and callback will still be called.  The application still needs
        /// to call <see cref="EndTimer" /> to promptly release resources
        /// associated with the timer.  Each <see cref="EndTimer" /> call will
        /// throw a <see cref="CancelException" />.
        /// </note>
        /// </remarks>
        public static void CancelAll()
        {
            lock (syncLock)
            {
                foreach (AsyncTimerResult arTimer in pending)
                    arTimer.Notify(new CancelException());

                pending.Clear();
            }
        }
    }
}
