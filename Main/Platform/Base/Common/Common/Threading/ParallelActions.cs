//-----------------------------------------------------------------------------
// FILE:        ParallelActions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Provides a mechanism for dispatching multiple actions on
//              worker threads and then waiting until all of the actions 
//              have been completed.

using System;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// Provides a mechanism for dispatching actions activites on
    /// worker threads and then waiting until all of the actions have
    /// been completed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is easy to use.  Simply construct an instance, optionally specifying
    /// a polling interval, then call one or more of the <b>EnqueueAction()</b> methods to
    /// submit actions to background threads, and then call one of the <b>Join()</b>
    /// methods to wait for the actions to complete.
    /// </para>
    /// <para>
    /// Note that this class is designed for one-time use per instance.  It is not possible
    /// to queue more actions after <b>Join()</b> is called and <b>Join()</b> may be 
    /// called only once per instance.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class ParallelActions
    {
        private const string JoinErrorMsg = "ParallelTasks: Cannot call [EnqueueAction()] after [Join()] has been called.";

        private object      syncLock = new object();    // Thread synchronization object
        private DateTime    abortTime;                  // Time to abort the operation (SYS)
        private TimeSpan    pollInterval;               // Completion polling interval
        private int         cPending;                   // # of tasks still executing
        private bool        joinCalled;                 // True if Join() has been called

        /// <summary>
        /// Constructs an instance that will use a default polling interval of 500 milliseconds.
        /// </summary>
        public ParallelActions()
            : this(TimeSpan.FromMilliseconds(500))
        {
        }

        /// <summary>
        /// Constructs an instance that will use the specified polling interval expressed in milliseconds.
        /// </summary>
        /// <param name="pollInterval">The polling interval in milliseconds.</param>
        public ParallelActions(int pollInterval)
            : this(TimeSpan.FromMilliseconds(pollInterval))
        {
        }

        /// <summary>
        /// Constructs an instance that will use the specified polling interval expressed a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="pollInterval">The polling interval.</param>
        public ParallelActions(TimeSpan pollInterval)
        {

            this.abortTime    = DateTime.MaxValue;
            this.pollInterval = pollInterval;
            this.cPending     = 0;
            this.joinCalled   = false;
        }

        /// <summary>
        /// Queues an <see cref="Action" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has been called.</exception>
        /// <remarks>
        /// <note>
        /// Calling any of the <see cref="EnqueueAction(Action)" /> methods after
        /// <see cref="Join()" /> has been called will result in an <see cref="InvalidOperationException" />.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction(Action action)
        {
            lock (syncLock)
            {
                if (joinCalled)
                    throw new InvalidOperationException(JoinErrorMsg);

                Interlocked.Increment(ref cPending);
                Helper.UnsafeQueueUserWorkItem(
                    s =>
                    {
                        try
                        {
                            ((Action)s)();
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }

                        Interlocked.Decrement(ref cPending);

                    }, action);
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has been called.</exception>
        /// <remarks>
        /// <note>
        /// Calling any of the <see cref="EnqueueAction(Action)" /> methods after
        /// <see cref="Join()" /> has been called will result in an <see cref="InvalidOperationException" />.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1>(T1 p1, Action<T1> action)
        {
            lock (syncLock)
            {
                if (joinCalled)
                    throw new InvalidOperationException(JoinErrorMsg);

                Interlocked.Increment(ref cPending);
                Helper.UnsafeQueueUserWorkItem(
                    s =>
                    {
                        try
                        {
                            ((Action<T1>)s)(p1);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }

                        Interlocked.Decrement(ref cPending);

                    }, action);
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has been called.</exception>
        /// <remarks>
        /// <note>
        /// Calling any of the <see cref="EnqueueAction(Action)" /> methods after
        /// <see cref="Join()" /> has been called will result in an <see cref="InvalidOperationException" />.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2>(T1 p1, T2 p2, Action<T1, T2> action)
        {
            lock (syncLock)
            {

                if (joinCalled)
                    throw new InvalidOperationException(JoinErrorMsg);

                Interlocked.Increment(ref cPending);
                Helper.UnsafeQueueUserWorkItem(
                    s =>
                    {
                        try
                        {
                            ((Action<T1, T2>)s)(p1, p2);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }

                        Interlocked.Decrement(ref cPending);

                    }, action);
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2,T3}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has been called.</exception>
        /// <remarks>
        /// <note>
        /// Calling any of the <see cref="EnqueueAction(Action)" /> methods after
        /// <see cref="Join()" /> has been called will result in an <see cref="InvalidOperationException" />.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> action)
        {
            lock (syncLock)
            {
                if (joinCalled)
                    throw new InvalidOperationException(JoinErrorMsg);

                Interlocked.Increment(ref cPending);
                Helper.UnsafeQueueUserWorkItem(
                    s =>
                    {
                        try
                        {
                            ((Action<T1, T2, T3>)s)(p1, p2, p3);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }

                        Interlocked.Decrement(ref cPending);

                    }, action);
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2,T3,T4}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="p4">The fourth action parameter.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <typeparam name="T4">Type of the fourth action parameter.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has been called.</exception>
        /// <remarks>
        /// <note>
        /// Calling any of the <see cref="EnqueueAction(Action)" /> methods after
        /// <see cref="Join()" /> has been called will result in an <see cref="InvalidOperationException" />.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> action)
        {
            lock (syncLock)
            {
                if (joinCalled)
                    throw new InvalidOperationException(JoinErrorMsg);

                Interlocked.Increment(ref cPending);
                Helper.UnsafeQueueUserWorkItem(
                    s =>
                    {
                        try
                        {
                            ((Action<T1, T2, T3, T4>)s)(p1, p2, p3, p4);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }

                        Interlocked.Decrement(ref cPending);

                    }, action);
            }
        }

        /// <summary>
        /// Waits for the queued work items to complete.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has already been called.</exception>
        /// <exception cref="TimeoutException">Thrown if the timeout threshold has been exceeded.</exception>
        public void Join()
        {
            lock (syncLock)
            {
                if (joinCalled)
                    throw new InvalidOperationException("ParallelTasks: Join() can be called only once per instance.");

                joinCalled = true;
            }

            while (true)
            {
                int c = cPending;

                if (c == 0)
                    return;     // All tasks have completed

                if (abortTime <= SysTime.Now)
                    throw new TimeoutException(string.Format("ParallelTasks: Join() timed out with [{0}] tasks still pending.", c));

                Thread.Sleep(pollInterval);
            }
        }

        /// <summary>
        /// Waits for the queued work items to complete.
        /// </summary>
        /// <param name="timeout">The timout expressed as milliseconds.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has already been called.</exception>
        /// <exception cref="TimeoutException">Thrown if the timeout threshold has been exceeded.</exception>
        public void Join(int timeout)
        {
            abortTime = SysTime.Now + TimeSpan.FromMilliseconds(timeout);
            Join();
        }

        /// <summary>
        /// Waits for the queued work items to complete.
        /// </summary>
        /// <param name="timeout">The timout expressed as a <see cref="TimeSpan" />.</param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Join()" /> has already been called.</exception>
        /// <exception cref="TimeoutException">Thrown if the timeout threshold has been exceeded.</exception>
        public void Join(TimeSpan timeout)
        {
            abortTime = SysTime.Now + timeout;
            Join();
        }
    }
}
