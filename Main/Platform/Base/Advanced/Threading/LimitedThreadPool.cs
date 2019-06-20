//-----------------------------------------------------------------------------
// FILE:        LimitedThreadPool.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a count limited thread pool that limits the
//              number of work items queued to the underlying .NET
//              thread pool.

using System;
using System.Threading;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill): 
//
// A potentially better way to implement this that rather than
// queuing task information in to the .NET thread pool, we
// just queue a callback and then select the task to execute
// when this call, pulling tasks off a priority queue
// first. I could also implement Dispose() that would 
// set a flag causing all pending tasks to be aborted.

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a count limited thread pool that limits the number of tasks 
    /// queued to the underlying .NET thread pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides important functionality for applications that run the
    /// risk of overwhelming the underlying .NET thread pool with tasks.
    /// This can happen if tasks are submitted to the pool faster than
    /// they can be executed.
    /// </para>
    /// <para>
    /// The <see cref="LimitedThreadPool" /> works by limiting the number of tasks
    /// submitted to the underlying .NET thread pool at any one time and also
    /// by limiting the number of tasks queued locally  within an instance.  When the
    /// number of tasks queued locally exceed the limit, tasks at the front of the
    /// queue are discarded when new tasks are queued.
    /// </para>
    /// <para>
    /// The class implements two constructors.  One accepts no parameters and 
    /// initializes the instance with reasonable default settings for the queued
    /// task limits.  The other allows these limits to be set to specific values.
    /// </para>
    /// <para>
    /// A task is simply an application defined state object instance (or <c>null</c>) and 
    /// a <see cref="WaitCallback" /> delegate instance.  Use the <see cref="QueueTask" /> 
    /// method to submit a method to the pool.  When the task is executed on a .NET 
    /// pool thread, the delegate will be called and the state instance will be passed as the
    /// parameter.
    /// </para>
    /// <para>
    /// High priority tasks that should be immediately queued to the underlying
    /// .NET thread pool (without the potential of being queued locally) should 
    /// be submitted using the <see cref="QueuePriorityTask" /> method.
    /// </para>
    /// <para>
    /// The <see cref="DiscardTask" /> event can be used to perform custom actions
    /// when a task is discarded from the pool when the pool has reached its size
    /// limit.  This event will be raised passing the task's state instance when a
    /// task is discarded.
    /// </para>
    /// <para>
    /// The <see cref="LocalCount" /> property returns the number of tasks currently
    /// queued locally and <see cref="ExecuteCount" /> returns the number of tasks
    /// submitted for execution to the underlying .NET thread pool.
    /// </para>
    /// <para>
    /// The <see cref="Clear" /> method can be used to cancel all tasks that are
    /// queued locally.  Note that tasks that have already been submitted to the
    /// underlying .NET thread pool cannot be canceled.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class LimitedThreadPool
    {
        //---------------------------------------------------------------------
        // Private classes

        private sealed class Task : IDisposable
        {
            public readonly LimitedThreadPool   Pool;
            public readonly WaitCallback        Callback;
            public readonly object              State;

            public Task(LimitedThreadPool pool, WaitCallback callback, object state)
            {
                this.Pool     = pool;
                this.Callback = callback;
                this.State    = state;
            }

            public void Dispose()
            {
                Pool.OnDiscard(this);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object              syncLock = new object();
        private LimitedQueue<Task>  localQueue;         // The local task queue
        private int                 cMaxPoolTasks;      // Maximum number of tasks submitted to the underlying .NET thread pool
        private int                 cMaxLocalTasks;     // Maximum number of tasks to queue locally
        private int                 cPoolTasks;         // Current number of tasks queued to the underlying .NET thread pool
        private WaitCallback        onExecute;          // The task dispatcher

        /// <summary>
        /// Raised when a task is discarded from the queue without being executed.
        /// </summary>
        public event WaitCallback DiscardTask;

        /// <summary>
        /// Constructs an instance with reasonable default queued task limits.
        /// </summary>
        /// <remarks>
        /// The current implementation limits the number of tasks queued to the 
        /// .NET thread pool to 25 times the number of processors present on the
        /// machine and the number of tasks queued locally to 50 times the
        /// .NET thread pool limit (or 1250 times the number of processors).
        /// </remarks>
        public LimitedThreadPool()
            : this(Environment.ProcessorCount * 25, Environment.ProcessorCount * 25 * 50)
        {
        }

        /// <summary>
        /// Constructs an instance with the specified queued task limits.
        /// </summary>
        /// <param name="cMaxPoolTasks">Maximum number of tasks to submit to the underlying .NET thread pool.</param>
        /// <param name="cMaxLocalTasks">Maximum number of tasks to queue locally.</param>
        public LimitedThreadPool(int cMaxPoolTasks, int cMaxLocalTasks)
        {
            if (cMaxPoolTasks <= 0)
                throw new ArgumentException("Argument must be >= 0.", "cMaxPoolTasks");

            if (cMaxLocalTasks <= 0)
                throw new ArgumentException("Argument must be >= 0.", "cMaxLocalTasks");

            this.cMaxPoolTasks         = cMaxPoolTasks;
            this.cMaxLocalTasks         = cMaxLocalTasks;
            this.onExecute              = new WaitCallback(OnExecute);
            this.localQueue             = new LimitedQueue<Task>();
            this.localQueue.CountLimit  = cMaxLocalTasks;
            this.localQueue.AutoDispose = true;
        }

        /// <summary>
        /// Returns the number of tasks currently queued locally to the pool.
        /// </summary>
        public int LocalCount
        {
            get
            {
                lock (syncLock)
                    return localQueue.Count;
            }
        }

        /// <summary>
        /// Returns the number of tasks currently submitted for execution by the
        /// underlying .NET thread pool.
        /// </summary>
        public int ExecuteCount
        {
            get
            {
                lock (syncLock)
                    return cPoolTasks;
            }
        }

        /// <summary>
        /// Queues a task.
        /// </summary>
        /// <param name="callback">The delegate to be called when the task is scheduled for execution.</param>
        /// <param name="state">The task's state instance (or <c>null</c>).</param>
        public void QueueTask(WaitCallback callback, object state)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            lock (syncLock)
            {
                if (cPoolTasks < cMaxPoolTasks)
                {
                    Helper.UnsafeQueueUserWorkItem(onExecute, new Task(this, callback, state));
                    cPoolTasks++;
                }
                else
                    localQueue.Enqueue(new Task(this, callback, state));
            }
        }

        /// <summary>
        /// Queues a high priority task that will be immediately submitted to the underlying
        /// .NET thread pool without the possibility of being queued locally.
        /// </summary>
        /// <param name="callback">The delegate to be called when the task is scheduled for execution.</param>
        /// <param name="state">The task's state instance (or <c>null</c>).</param>
        public void QueuePriorityTask(WaitCallback callback, object state)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            lock (syncLock)
            {
                Helper.UnsafeQueueUserWorkItem(onExecute, new Task(this, callback, state));
                cPoolTasks++;
            }
        }

        /// <summary>
        /// Handles the dispatching of queued tasks on underlying pool threads.
        /// </summary>
        /// <param name="o">The task instance.</param>
        private void OnExecute(object o)
        {
            var task = (Task)o;

            try
            {
                task.Callback(task.State);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            lock (syncLock)
            {
                cPoolTasks--;
                if (cPoolTasks < cMaxPoolTasks && localQueue.Count > 0)
                {
                    task = localQueue.Dequeue();
                    Helper.UnsafeQueueUserWorkItem(onExecute, task);
                    cPoolTasks++;
                }
            }
        }

        /// <summary>
        /// Called when a task is discarded from the local queue.
        /// </summary>
        /// <param name="task">The discarded task.</param>
        private void OnDiscard(Task task)
        {
            if (DiscardTask != null)
            {
                try
                {
                    DiscardTask(task.State);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Discards all tasks still queued locally.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Tasks already submitted to the underlying .NET thread pool
        /// cannot be discarded.
        /// </note>
        /// </remarks>
        public void Clear()
        {
            List<Task> discardList = null;

            lock (syncLock)
            {
                if (localQueue.Count > 0 && DiscardTask != null)
                {
                    discardList = new List<Task>(localQueue.Count);

                    while (localQueue.Count > 0)
                        discardList.Add(localQueue.Dequeue());
                }
            }

            if (discardList != null)
            {
                for (int i = 0; i < discardList.Count; i++)
                    DiscardTask(discardList[i].State);
            }
        }
    }
}
