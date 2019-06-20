//-----------------------------------------------------------------------------
// FILE:        _LimitedThreadPool.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _LimitedThreadPool
    {
        private class TaskState
        {
            public int TaskID;
            public int Delay;

            public TaskState(int taskID, int delay)
            {
                this.TaskID = taskID;
                this.Delay = delay;
            }
        }

        // This table is indexed by integer task ID and the value is set
        // to true if the task was executed, false if it was discarded.

        private object syncLock = new object();
        private Dictionary<int, bool> completed = new Dictionary<int, bool>();

        private void OnExecute(object state)
        {
            TaskState task = (TaskState)state;

            if (task.Delay > 0)
                Thread.Sleep(task.Delay);

            lock (syncLock)
                completed.Add(task.TaskID, true);
        }

        private void OnDiscard(object state)
        {
            TaskState task = (TaskState)state;

            lock (syncLock)
                completed.Add(task.TaskID, false);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void LimitedThreadPool_Basic()
        {
            LimitedThreadPool pool = new LimitedThreadPool(2, 2);
            WaitCallback onExecute = new WaitCallback(OnExecute);

            pool.DiscardTask += new WaitCallback(OnDiscard);

            Assert.AreEqual(0, pool.LocalCount);
            Assert.AreEqual(0, pool.ExecuteCount);

            // Verify that a single task gets queued and executed

            pool.Clear();
            completed.Clear();

            pool.QueueTask(onExecute, new TaskState(0, 0));
            Thread.Sleep(300);
            lock (syncLock)
                Assert.IsTrue(completed.ContainsKey(0));

            // Verify that when queuing 5 tasks with 250ms delays that
            // the first two tasks are executed immediately, the third
            // task is discarded since the local limit of 2 is exceeded
            // and then that the final two tasks are ultimately executed.

            pool.Clear();
            completed.Clear();

            for (int i = 0; i < 5; i++)
                pool.QueueTask(onExecute, new TaskState(i, i <= 2 ? 250 : 500));

            Thread.Sleep(300);
            lock (syncLock)
            {
                Assert.IsTrue(completed.ContainsKey(0));
                Assert.IsTrue(completed.ContainsKey(1));
                Assert.IsTrue(completed.ContainsKey(2));
            }

            Thread.Sleep(550);
            lock (syncLock)
            {
                Assert.IsTrue(completed.ContainsKey(3));
                Assert.IsTrue(completed.ContainsKey(4));
            }

            // Verify that priority tasks are not queued locally

            pool.Clear();
            completed.Clear();

            pool.QueueTask(onExecute, new TaskState(0, 250));
            pool.QueueTask(onExecute, new TaskState(1, 250));
            pool.QueuePriorityTask(onExecute, new TaskState(2, 500));
            Thread.Sleep(300);
            pool.Clear();
            Thread.Sleep(550);

            Assert.IsTrue(completed.ContainsKey(0));
            Assert.IsTrue(completed.ContainsKey(1));
            Assert.IsTrue(completed.ContainsKey(2));

            // Verify that Clear() actually deletes locally queued tasks

            pool = new LimitedThreadPool(3, 10);
            completed.Clear();

            for (int i = 0; i < 4; i++)
                pool.QueueTask(onExecute, new TaskState(i, i <= 2 ? 250 : 500));

            Thread.Sleep(100);
            pool.Clear();
            Thread.Sleep(550);

            lock (syncLock)
            {
                Assert.IsTrue(completed.ContainsKey(0));
                Assert.IsTrue(completed.ContainsKey(1));
                Assert.IsTrue(completed.ContainsKey(2));
                Assert.IsFalse(completed.ContainsKey(3));
            }
        }
    }
}

