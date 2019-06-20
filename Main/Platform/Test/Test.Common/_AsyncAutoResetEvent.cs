//-----------------------------------------------------------------------------
// FILE:        _AsyncAutoResetEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _AsyncAutoResetEvent
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        private class TaskState
        {
            public bool IsRunning;
            public bool IsComplete;
            public bool IsFaulted;
        }

        private class TaskStateCollection : List<TaskState>
        {
            public const int TaskCount = 10;

            public TaskStateCollection()
                : base(TaskCount)
            {
                for (int i = 0; i < TaskCount; i++)
                {
                    Add(new TaskState());
                }
            }

            public bool AllRunning
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsRunning)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AllComplete
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsComplete)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AllFaulted
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (!state.IsFaulted)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool AnyComplete
            {
                get
                {
                    foreach (var state in this)
                    {
                        if (state.IsComplete)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncAutoResetEvent_Basic()
        {
            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute.

            using (var autoEvent = new AsyncAutoResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await autoEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskInfo.AnyComplete);
            }

            // Verify that an event that starts out signalled but then
            // resetting it doesn't allow any tasks to execute.

            using (var autoEvent = new AsyncAutoResetEvent(true))
            {
                autoEvent.Reset();

                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await autoEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskInfo.AnyComplete);
            }

            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute and then that every time the event is signalled,
            // a single task is unblocked.

            using (var autoEvent = new AsyncAutoResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await autoEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskInfo.AnyComplete);

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    autoEvent.Set();

                    Helper.WaitFor(
                        () =>
                        {
                            return taskInfo.Where(ti => ti.IsComplete).Count() == i + 1;
                        },
                        defaultTimeout);
                }

                // Also verify that disposing the event multiple time isn't a problem.

                autoEvent.Dispose();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncAutoResetEvent_Error()
        {
            AsyncAutoResetEvent autoEvent;

            // Verify that we get and [ObjectDisposedException] for [Set()] and [Reset()]
            // a disposed event.

            autoEvent = new AsyncAutoResetEvent();

            autoEvent.Dispose();
            ExtendedAssert.Throws<ObjectDisposedException>(() => autoEvent.Set());
            ExtendedAssert.Throws<ObjectDisposedException>(() => autoEvent.Reset());
            Task.Run(() => ExtendedAssert.ThrowsAsync<ObjectDisposedException>(async () => await autoEvent.WaitAsync())).Wait();

            // Verify that disposing an event causes any waiting tasks
            // to unblock with an [ObjectDisposedException].

            autoEvent = new AsyncAutoResetEvent();

            var taskInfo = new TaskStateCollection();
            var badException = false;

            for (int i = 0; i < taskInfo.Count; i++)
            {
                new Task(
                    async state =>
                    {
                        int taskIndex = (int)state;

                        taskInfo[taskIndex].IsRunning = true;

                        try
                        {
                            await autoEvent.WaitAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            taskInfo[taskIndex].IsFaulted = true;
                        }
                        catch
                        {
                            badException = true;
                            taskInfo[taskIndex].IsFaulted = true;
                        }

                        taskInfo[taskIndex].IsComplete = true;
                    },
                    i).Start();
            }

            Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
            Assert.IsFalse(taskInfo.AnyComplete);

            autoEvent.Dispose();

            Helper.WaitFor(() => taskInfo.AllFaulted, defaultTimeout);
            Assert.IsFalse(badException);
        }
    }
}
