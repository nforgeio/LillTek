//-----------------------------------------------------------------------------
// FILE:        QueuedAsyncResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A specialized IAsyncResult implementation used to queue an
//              IAsyncResult operation that completed synchronously to 
//              another worker thread.

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
    /// <summary>
    /// A specialized <see cref="IAsyncResult" /> implementation used to queue an
    /// IAsyncResult operation that completed synchronously to 
    /// another worker thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is useful in situations where the underlying .NET framework
    /// actually implements synchronous async operations.  The idea is to
    /// use the <see cref="QueueSynchronous" /> method within the operation's completion 
    /// callback to handle the detection and queuing of operations that
    /// completed synchronously.
    /// </para>
    /// <code language="cs">
    /// private void OnCompletion(IAsyncResult ar) {
    /// 
    ///     ar = QueuedAsyncResult.QueueSynchronous(ar,new AsyncCallback(OnCompletion));
    ///     if (ar == null)
    ///         return;
    /// 
    ///     // Your completion handling code
    /// }
    /// </code>
    /// <para>
    /// The <see cref="QueueSynchronous" /> method examines the <see cref="IAsyncResult" /> instance passed.
    /// If it indicates that the operation was completed synchronously, it will
    /// queue the result back to the completion method on another thread and
    /// return null.
    /// </para>
    /// <para>
    /// If the IAsyncResult instance was not completed synchronously, then 
    /// <see cref="QueueSynchronous" /> will simply return the proper <see cref="IAsyncResult" /> instance
    /// to be used to complete the operation.
    /// </para>
    /// </remarks>
    public sealed class QueuedAsyncResult : IAsyncResult
    {
        //---------------------------------------------------------------------
        // Static members

        private static WaitCallback onDequeue = new WaitCallback(OnDequeue);

        /// <summary>
        /// Handles the queuing of synchronously completed asynchronous operations
        /// to another worker thread.
        /// </summary>
        /// <param name="ar">The received asynchronous result.</param>
        /// <param name="callback">The operation's completion callback.</param>
        /// <returns>
        /// <c>null</c> if the completion was queued to another thread, otherwise the
        /// <see cref="IAsyncResult" /> instance to be used to complete the operation.
        /// </returns>
        public static IAsyncResult QueueSynchronous(IAsyncResult ar, AsyncCallback callback)
        {
            QueuedAsyncResult queued;

            queued = ar as QueuedAsyncResult;
            if (queued != null)
                return queued.queuedAR;
            else if (ar.CompletedSynchronously && callback != null)
            {
                Helper.UnsafeQueueUserWorkItem(onDequeue, new QueuedAsyncResult(ar, callback));
                return null;
            }
            else
                return ar;
        }

        /// <summary>
        /// Handles the dispatching of queued completions to the completion
        /// callback.
        /// </summary>
        /// <param name="state">The QueuedAsyncResult instance.</param>
        private static void OnDequeue(object state)
        {
            QueuedAsyncResult queued;

            queued = (QueuedAsyncResult)state;
            queued.callback(queued);
        }

        //---------------------------------------------------------------------
        // Instance members

        private IAsyncResult    queuedAR;
        private AsyncCallback   callback;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ar">The original IAsyncResult to be queued.</param>
        /// <param name="callback">The operation's completion callback.</param>
        private QueuedAsyncResult(IAsyncResult ar, AsyncCallback callback)
        {
            this.queuedAR = ar;
            this.callback = callback;
        }

        // IAsyncResult implementations

        /// <summary>
        /// The application state associated with the operation.
        /// </summary>
        public object AsyncState
        {
            get { return queuedAR.AsyncState; }
        }

        /// <summary>
        /// Returns the wait object to be used to explicitly wait for 
        /// the operation to complete.
        /// </summary>
        public WaitHandle AsyncWaitHandle
        {
            get { return queuedAR.AsyncWaitHandle; }
        }

        /// <summary>
        /// Indicates whether or not the operation was completed synchronously.
        /// </summary>
        public bool CompletedSynchronously
        {
            get { return false; }
        }

        /// <summary>
        /// Returns <c>true</c> if the operation has been completed.
        /// </summary>
        public bool IsCompleted
        {
            get { return queuedAR.IsCompleted; }
        }
    }
}
