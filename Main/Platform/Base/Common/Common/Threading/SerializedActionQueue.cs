//-----------------------------------------------------------------------------
// FILE:        SerializedActionQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Maintains a queue of Actions that will be executed on a worker
//              thread in the order they were appended to the queue.

using System;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Maintains a queue of <see cref="Action" />s that will be executed on a
    /// worker thread in the order they were appended to the queue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is useful when decoupling components in a multi-threaded environment
    /// where it is important for actions flowing from one component to another to be
    /// executed in the order they were invoked, but on a worker thread.  The default
    /// constructor <see cref="SerializedActionQueue" /> constructs a queue that can
    /// hold an unrestricted number of actions.  Simple call one of the generic
    /// <see cref="EnqueueAction(Action)" /> overrides to submit an action that takes
    /// from zero to four parameters.  The queue will execute the actions in order on 
    /// a worker thread.
    /// </para>
    /// <para>
    /// You obtain the current number of queued actions via the <see cref="Count" />
    /// property and you may use <see cref="Clear" /> to empty the queue.  This will
    /// abort the processing of all pending actions.
    /// </para>
    /// <para>
    /// You may use the <see cref="SerializedActionQueue(int)" /> constructor to create
    /// a queue that restricts the number of action.  This is useful for preventing situations
    /// where one component is submitting actions faster than another can process them,
    /// ultimately resulting in an out-of-memory situation.
    /// </para>
    /// <para>
    /// Applications may also call the <see cref="Shutdown" /> method.  This method clears
    /// all pending actions and causes the queue to ignore all new actions submitted.  
    /// This is useful in some situations such as during application termination sequences.
    /// </para>
    /// <note>
    /// <para>
    /// This class provides somewhat similar functionality to what the various
    /// <see cref="Helper.EnqueueSerializedAction(Action)" /> overrides provides.
    /// The main difference that the <see cref="Helper" /> methods are global and
    /// serialize work across the entire assembly, whereas this class provides
    /// a means for maintaining multiple separate queues.
    /// </para>
    /// <para>
    /// In fact, <see cref="Helper.EnqueueSerializedAction(Action)" /> is implemented
    /// using the <see cref="SerializedActionQueue" /> class.
    /// </para>
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SerializedActionQueue
    {
        //---------------------------------------------------------------------
        // Private types

        private interface ISerializedAction
        {
            void Invoke();
        }

        private class SerializedAction : ISerializedAction
        {
            private Action action;

            public SerializedAction(Action action)
            {
                this.action = action;
            }

            public void Invoke()
            {
                action();
            }
        }

        private class SerializedAction<T1> : ISerializedAction
        {
            private Action<T1> action;
            private T1 p1;

            public SerializedAction(T1 p1, Action<T1> action)
            {
                this.p1 = p1;
                this.action = action;
            }

            public void Invoke()
            {
                action(p1);
            }
        }

        private class SerializedAction<T1, T2> : ISerializedAction
        {
            private Action<T1, T2> action;
            private T1 p1;
            private T2 p2;

            public SerializedAction(T1 p1, T2 p2, Action<T1, T2> action)
            {
                this.p1 = p1;
                this.p2 = p2;
                this.action = action;
            }

            public void Invoke()
            {
                action(p1, p2);
            }
        }

        private class SerializedAction<T1, T2, T3> : ISerializedAction
        {
            private Action<T1, T2, T3> action;
            private T1 p1;
            private T2 p2;
            private T3 p3;

            public SerializedAction(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> action)
            {
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;
                this.action = action;
            }

            public void Invoke()
            {
                action(p1, p2, p3);
            }
        }

        private class SerializedAction<T1, T2, T3, T4> : ISerializedAction
        {
            private Action<T1, T2, T3, T4> action;
            private T1 p1;
            private T2 p2;
            private T3 p3;
            private T4 p4;

            public SerializedAction(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> action)
            {
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;
                this.p4 = p4;
                this.action = action;
            }

            public void Invoke()
            {
                action(p1, p2, p3, p4);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object                      syncLock     = new object();
        private Queue<ISerializedAction>    actionQueue  = new Queue<ISerializedAction>();
        private bool                        isEnabled    = true;
        private bool                        isProcessing = false;
        private int                         maxQueueLength;
        private Action                      processActions;

        /// <summary>
        /// Constructs an action queue with an unrestricted queue length.
        /// </summary>
        public SerializedActionQueue()
            : this(int.MaxValue)
        {
        }

        /// <summary>
        /// Constructs an action queue with a restricted queue length.
        /// </summary>
        /// <param name="maxQueueLength">The maximum queue length.</param>
        /// <exception cref="ArgumentException">Thrown if <pararef name="maxQueueLength" /> is not a positive number.</exception>
        public SerializedActionQueue(int maxQueueLength)
        {
            if (maxQueueLength <= 0)
                throw new ArgumentException("Value must be positive.", "maxQueueLength");

            this.maxQueueLength = maxQueueLength;
            this.processActions = new Action(ProcessActions);
        }

        /// <summary>
        /// Handles the processing of all queued actions on a worker thread.
        /// </summary>
        private void ProcessActions()
        {
            while (true)
            {
                ISerializedAction serializedAction;

                lock (syncLock)
                {
                    if (actionQueue.Count == 0)
                    {
                        isProcessing = false;
                        return;
                    }

                    serializedAction = actionQueue.Dequeue();
                }

                try
                {
                    serializedAction.Invoke();
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        ///<summary>
        /// Clears all pending actions and causes the queue to ignore all new actions submitted.  
        /// This is useful in some situations such as during application termination sequences.
        ///</summary>
        public void Shutdown()
        {
            isEnabled = false;

            Clear();
        }

        /// <summary>
        /// Verifies that another action can be added to the queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the queue already holds the maximum possible actions.</exception>
        private void CheckQueueLength()
        {
            if (actionQueue.Count >= maxQueueLength)
                throw new InvalidOperationException(string.Format("Action queue length cannot exceed [{0}] actions.", maxQueueLength));
        }

        /// <summary>
        /// Queues an <see cref="Action" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the queue already holds the maximum possible actions.</exception>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!isEnabled)
                return;

            lock (syncLock)
            {
                CheckQueueLength();
                actionQueue.Enqueue(new SerializedAction(action));

                if (!isProcessing)
                {
                    isProcessing = true;
                    Helper.EnqueueAction(processActions);
                }
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1>(T1 p1, Action<T1> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!isEnabled)
                return;

            lock (syncLock)
            {
                CheckQueueLength();
                actionQueue.Enqueue(new SerializedAction<T1>(p1, action));

                if (!isProcessing)
                {
                    isProcessing = true;
                    Helper.EnqueueAction(processActions);
                }
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
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2>(T1 p1, T2 p2, Action<T1, T2> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!isEnabled)
                return;

            lock (syncLock)
            {
                CheckQueueLength();
                actionQueue.Enqueue(new SerializedAction<T1, T2>(p1, p2, action));

                if (!isProcessing)
                {
                    isProcessing = true;
                    Helper.EnqueueAction(processActions);
                }
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
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!isEnabled)
                return;

            lock (syncLock)
            {
                CheckQueueLength();
                actionQueue.Enqueue(new SerializedAction<T1, T2, T3>(p1, p2, p3, action));

                if (!isProcessing)
                {
                    isProcessing = true;
                    Helper.EnqueueAction(processActions);
                }
            }
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2,T3,T4}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="p4">The fourth action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <typeparam name="T4">Type of the fourth action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public void EnqueueAction<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!isEnabled)
                return;

            lock (syncLock)
            {
                CheckQueueLength();
                actionQueue.Enqueue(new SerializedAction<T1, T2, T3, T4>(p1, p2, p3, p4, action));

                if (!isProcessing)
                {
                    isProcessing = true;
                    Helper.EnqueueAction(processActions);
                }
            }
        }

        /// <summary>
        /// Returns the number of queued actions.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncLock)
                {
                    return actionQueue.Count;
                }
            }
        }

        /// <summary>
        /// Removes any unexecuted actions from the queue.
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
            {
                actionQueue.Clear();
            }
        }
    }
}
