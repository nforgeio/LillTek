//-----------------------------------------------------------------------------
// FILE:        WorkerThread.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a method for dispatching work to a background
//              thread and providing a mechanism for waiting for the result.

using System;
using System.Threading;
using System.Reflection;

using LillTek.Common;

// $todo(jeff.lill): 
//
// This implementation is a bit sloppy about thread safety
// while the worker thread is in the process of being closed.
// I doubt though that this will cause much trouble in
// real life.

namespace LillTek.Advanced
{
    /// <summary>
    /// Specifies the delegate that can be called by a <see cref="WorkerThread" />.
    /// </summary>
    /// <param name="arg">The method parameter.</param>
    /// <returns>The method result.</returns>
    public delegate object WorkerThreadMethod(object arg);

    /// <summary>
    /// Implements a method for dispatching work to a background thread and 
    /// then providing a mechanism for waiting for the result.
    /// </summary>
    /// <threadsafety instance="true" />
    public sealed class WorkerThread
    {
        private Thread              thread;     // The background thread.
        private AutoResetEvent      waitEvent;  // Set when worker thread is not busy
        private AutoResetEvent      wakeEvent;  // Set when there's work to do
        private WorkerThreadMethod  method;     // The method to be called
        private object              arg;        // The method argument
        private AsyncResult         opAR;       // The async result for the operation

        /// <summary>
        /// Constructs a work thread.
        /// </summary>
        public WorkerThread()
        {
            this.waitEvent = new AutoResetEvent(true);
            this.wakeEvent = new AutoResetEvent(false);
            this.method    = null;
            this.arg       = null;
            this.opAR      = null;

            this.thread = new Thread(new ThreadStart(ThreadLoop));
            this.thread.Start();
        }

        /// <summary>
        /// Releases all resources associated with the worker thread.
        /// Any work in progress will be aborted with a <see cref="CancelException" />.
        /// </summary>
        public void Close()
        {
            if (thread == null)
                return;

            if (opAR != null)
                opAR.Notify(new CancelException("WorkerThread closed."));

            thread.Abort();
            thread = null;

            waitEvent.Close();
            waitEvent = null;

            wakeEvent.Close();
            wakeEvent = null;
        }

        /// <summary>
        /// Invokes a method on the worker thread, blocking the current thread
        /// until the operation completes.
        /// </summary>
        /// <param name="method">The method delegate.</param>
        /// <param name="arg">The method parameter.</param>
        /// <returns>The result returned by the delegate call.</returns>
        /// <remarks>
        /// <note>
        /// Exceptions thrown in the method call on the worker thread
        /// will be caught and rethrown on this thread.
        /// </note>
        /// </remarks>
        public object Invoke(WorkerThreadMethod method, object arg)
        {
            var ar = BeginInvoke(method, arg, null, null);

            return EndInvoke(ar);
        }

        /// <summary>
        /// Invokes a method on the worker thread, blocking the current thread
        /// until any operation already in progress complete.  Once the operation
        /// has been successfully submitted to the thread, this method will return
        /// to allow the operation to complete asynchronously.
        /// </summary>
        /// <param name="method">The method delegate.</param>
        /// <param name="arg">The method parameter.</param>
        /// <param name="callback">The delegate to be called when the timer fires (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        public IAsyncResult BeginInvoke(WorkerThreadMethod method, object arg, AsyncCallback callback, object state)
        {
            if (thread == null)
                throw new InvalidOperationException("WorkerThread is closed.");

            waitEvent.WaitOne();

            this.method = method;
            this.arg    = arg;
            this.opAR   = new AsyncResult(null, callback, state);
            this.opAR.Started();

            wakeEvent.Set();

            return this.opAR;
        }

        /// <summary>
        /// Completes the operation initiated with a <see cref="BeginInvoke" /> call.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginInvoke" />.</param>
        /// <returns>The method result.</returns>
        /// <remarks>
        /// <note>
        /// Exceptions thrown in the method call on the worker thread
        /// will be caught and rethrown on this thread.
        /// </note>
        /// </remarks>
        public object EndInvoke(IAsyncResult ar)
        {
            var opAR = (AsyncResult)ar;

            opAR.Wait();
            try
            {
                if (opAR.Exception != null)
                    throw opAR.Exception;
                else
                    return opAR.Result;
            }
            finally
            {
                opAR.Dispose();
            }
        }

        /// <summary>
        /// Implements the worker thread.
        /// </summary>
        private void ThreadLoop()
        {
            AsyncResult opAR;

            while (true)
            {
                wakeEvent.WaitOne();

                opAR = this.opAR;

                try
                {
                    opAR.Result = method(arg);
                    opAR.Notify();
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (TargetInvocationException e)
                {
                    opAR.Notify(e.InnerException);
                }
                catch (Exception e)
                {
                    opAR.Notify(e);
                }

                waitEvent.Set();
            }
        }
    }
}
