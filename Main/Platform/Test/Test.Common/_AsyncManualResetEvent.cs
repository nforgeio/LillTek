//-----------------------------------------------------------------------------
// FILE:        _AsyncManualResetEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _AsyncManualResetEvent
    {
        private TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncManualResetEvent_Basic()
        {
            bool taskRunning;
            bool taskCompleted;

            // Verify that an event that starts out unsignalled doesn't allow
            // a task to execute.

            using (var manualEvent = new AsyncManualResetEvent())
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                    {
                        taskRunning = true;
                        await manualEvent.WaitAsync();
                        taskCompleted = true;
                    });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskCompleted);
            }

            // Verify that an event that starts out signalled does allow
            // a task to execute.

            using (var manualEvent = new AsyncManualResetEvent(true))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled and is subsequently
            // signalled allows a task to complete.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.IsFalse(taskCompleted);
                manualEvent.Set();
                Helper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that can be signalled while already signalled
            // without a problem.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.IsFalse(taskCompleted);
                manualEvent.Set();
                manualEvent.Set();
                manualEvent.Set();
                manualEvent.Set();
                Helper.WaitFor(() => taskCompleted, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled is subsequently
            // signalled allows a task to complete, that another task also completes
            // on the signalled event and then when the event is reset, the next
            // task will block.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Assert.IsFalse(taskCompleted);
                manualEvent.Set();
                Helper.WaitFor(() => taskCompleted, defaultTimeout);

                // Verify that we can another task won't block on the already 
                // signalled event.

                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Helper.WaitFor(() => taskCompleted, TimeSpan.FromSeconds(5));
                Assert.IsTrue(taskCompleted);

                // Now reset the event and verify that the next task blocks.

                manualEvent.Reset();

                taskRunning = false;
                taskCompleted = false;

                Task.Run(async () =>
                {
                    taskRunning = true;
                    await manualEvent.WaitAsync();
                    taskCompleted = true;
                });

                Helper.WaitFor(() => taskRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskCompleted);
            }

            // Verify that we can reuse an event multiple times.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                for (int i = 0; i < 10; i++)
                {
                    taskRunning = false;
                    taskCompleted = false;

                    Task.Run(async () =>
                    {
                        taskRunning = true;
                        await manualEvent.WaitAsync();
                        taskCompleted = true;
                    });

                    Helper.WaitFor(() => taskRunning, defaultTimeout);
                    manualEvent.Set();
                    Helper.WaitFor(() => taskCompleted, defaultTimeout);

                    manualEvent.Reset();
                }
            }

            // Verify that we can dispose an event multiple times without an error.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                manualEvent.Dispose();
            }
        }

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
        public void AsyncManualResetEvent_MultipleThreads()
        {
            // Verify that an event that starts out unsignalled doesn't allow
            // any tasks to execute.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await manualEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Thread.Sleep(TimeSpan.FromSeconds(5));
                Assert.IsFalse(taskInfo.AnyComplete);
            }

            // Verify that an event that starts out signalled allows
            // multiple tasks to execute.

            using (var manualEvent = new AsyncManualResetEvent(true))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await manualEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Helper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
            }

            // Verify that an event that starts out unsignalled and is subsequently
            // signalled allows multiple tasks to complete.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                var taskInfo = new TaskStateCollection();

                for (int i = 0; i < taskInfo.Count; i++)
                {
                    new Task(
                        async state =>
                        {
                            int taskIndex = (int)state;

                            taskInfo[taskIndex].IsRunning = true;
                            await manualEvent.WaitAsync();
                            taskInfo[taskIndex].IsComplete = true;
                        },
                        i).Start();
                }

                Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                Assert.IsFalse(taskInfo.AnyComplete);
                manualEvent.Set();
                Helper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
            }

            // Verify that we can reuse an event multiple times for multiple tasks.

            using (var manualEvent = new AsyncManualResetEvent(false))
            {
                for (int j = 0; j < 10; j++)
                {
                    var taskInfo = new TaskStateCollection();

                    for (int i = 0; i < taskInfo.Count; i++)
                    {
                        new Task(
                            async state =>
                            {
                                int taskIndex = (int)state;

                                taskInfo[taskIndex].IsRunning = true;
                                await manualEvent.WaitAsync();
                                taskInfo[taskIndex].IsComplete = true;
                            },
                            i).Start();
                    }

                    Helper.WaitFor(() => taskInfo.AllRunning, defaultTimeout);
                    Assert.IsFalse(taskInfo.AnyComplete);
                    manualEvent.Set();
                    Helper.WaitFor(() => taskInfo.AllComplete, defaultTimeout);
                    manualEvent.Reset();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncManualResetEvent_Error()
        {
            AsyncManualResetEvent manualEvent;

            // Verify that we get and [ObjectDisposedException] for [Set()] and [Reset()]
            // a disposed event.

            manualEvent = new AsyncManualResetEvent();

            manualEvent.Dispose();
            ExtendedAssert.Throws<ObjectDisposedException>(() => manualEvent.Set());
            ExtendedAssert.Throws<ObjectDisposedException>(() => manualEvent.Reset());
            Task.Run(() => ExtendedAssert.ThrowsAsync<ObjectDisposedException>(async () => await manualEvent.WaitAsync())).Wait();

            // Verify that disposing an event causes any waiting tasks
            // to unblock with an [ObjectDisposedException].

            manualEvent = new AsyncManualResetEvent();

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
                            await manualEvent.WaitAsync();
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

            manualEvent.Dispose();

            Helper.WaitFor(() => taskInfo.AllFaulted, defaultTimeout);
            Assert.IsFalse(badException);
        }
    }
}
