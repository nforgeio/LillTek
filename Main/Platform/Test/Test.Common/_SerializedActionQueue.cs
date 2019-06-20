//-----------------------------------------------------------------------------
// FILE:        _Helper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for Helper

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _SerializedActionQueue
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_ExecuteInOrder()
        {
            // Make sure that actions submitted to a SerializedActionQueue are executed in order.

            var queue = new SerializedActionQueue();

            try
            {
                int count = 0;
                int action1Count = -1;
                int action2Count = -1;
                int action3Count = -1;
                int action4Count = -1;

                queue.EnqueueAction(() =>
                {
                    Interlocked.Increment(ref count);
                    action1Count = count;
                });

                queue.EnqueueAction(() =>
                {
                    Thread.Sleep(1000);
                    Interlocked.Increment(ref count);
                    action2Count = count;
                });

                queue.EnqueueAction(() =>
                {
                    Interlocked.Increment(ref count);
                    Thread.Sleep(1000);
                    action3Count = count;
                    Thread.Sleep(1000);
                });

                queue.EnqueueAction(() =>
                {
                    Interlocked.Increment(ref count);
                    action4Count = count;
                    Thread.Sleep(1000);
                });

                Helper.WaitFor(() => action1Count != -1 && action2Count != -1 && action3Count != -1 && action4Count != -1, TimeSpan.FromMilliseconds(5000));

                Assert.AreEqual(1, action1Count);
                Assert.AreEqual(2, action2Count);
                Assert.AreEqual(3, action3Count);
                Assert.AreEqual(4, action4Count);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Limit()
        {
            // Verify that the queue throws an [InvalidOperationException] if the number of items in
            // the queue are exceeded.  I'm going to do this by queuing a relatively long-running action
            // and then quickly submitting four more actions where the last one should exceed the limit
            // of three actions I'm going to set for the queue.

            var queue = new SerializedActionQueue(3);
            var done = false;
            var task1 = false;

            try
            {
                queue.EnqueueAction(() =>
                {
                    task1 = true;
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    done = true;
                });

                Helper.WaitFor(() => task1, TimeSpan.FromSeconds(1));

                queue.EnqueueAction(() => Thread.Sleep(0));
                queue.EnqueueAction(() => Thread.Sleep(0));
                queue.EnqueueAction(() => Thread.Sleep(0));

                ExtendedAssert.Throws<InvalidOperationException>(() => queue.EnqueueAction(() => Thread.Sleep(0)));

                Helper.WaitFor(() => done, TimeSpan.FromSeconds(5));
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Clear()
        {
            // Verify that Clear() actually works.  I'm going to do this by queuing a relatively long-running action
            // and then quickly submitting three more actions.  Each action will set a boolean if it executes.
            // I'm going to wait for an indication that the first action begun executing and then clear the 
            // queue and verify that the remaining actions did not execute.k

            var queue = new SerializedActionQueue();
            var task1 = false;
            var task2 = false;
            var task3 = false;
            var task4 = false;
            var done = false;

            try
            {
                queue.EnqueueAction(() =>
                {
                    task1 = true;
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                    done = true;
                });

                queue.EnqueueAction(() => task2 = true);
                queue.EnqueueAction(() => task3 = true);
                queue.EnqueueAction(() => task4 = true);

                Helper.WaitFor(() => task1, TimeSpan.FromSeconds(1));

                queue.Clear();

                Helper.WaitFor(() => done, TimeSpan.FromSeconds(5));

                Assert.IsFalse(task2);
                Assert.IsFalse(task3);
                Assert.IsFalse(task4);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_QueueWhileProcessing()
        {
            // Verify that the actions queued while the queue is processing another
            // action eventually get executed as well.

            var queue = new SerializedActionQueue();
            var task1 = false;
            var task2 = false;
            var task3 = false;
            var task4 = false;

            try
            {
                queue.EnqueueAction(() =>
                {
                    task1 = true;
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                });

                Helper.WaitFor(() => task1, TimeSpan.FromSeconds(1));

                queue.EnqueueAction(() => task2 = true);
                queue.EnqueueAction(() => task3 = true);
                queue.EnqueueAction(() => task4 = true);

                Helper.WaitFor(() => task1 && task2 && task3 && task4, TimeSpan.FromSeconds(5));
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_QueueWhileIdle()
        {
            // Verify that actions queued while the queue is idle get executed.

            var queue = new SerializedActionQueue();
            var done = false;

            try
            {
                for (int i = 0; i < 100; i++)
                {
                    done = false;
                    queue.EnqueueAction(() =>
                    {
                        done = true;
                    });

                    Helper.WaitFor(() => done, TimeSpan.FromSeconds(1));
                }
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Param1()
        {
            // Verify that a one parameter action gets invoked properly.

            var queue = new SerializedActionQueue();
            var value1 = (string)null;

            try
            {
                queue.EnqueueAction<string>("p1",
                    (p1) =>
                    {
                        value1 = p1;
                    });

                Helper.WaitFor(() => value1 != null, TimeSpan.FromSeconds(1));
                Assert.AreEqual("p1", value1);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Param2()
        {
            // Verify that a one parameter action gets invoked properly.

            var queue = new SerializedActionQueue();
            var value1 = (string)null;
            var value2 = (string)null;

            try
            {
                queue.EnqueueAction<string, string>("p1", "p2",
                    (p1, p2) =>
                    {
                        value1 = p1;
                        value2 = p2;
                    });

                Helper.WaitFor(() => value1 != null, TimeSpan.FromSeconds(1));
                Assert.AreEqual("p1", value1);
                Assert.AreEqual("p2", value2);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Param3()
        {
            // Verify that a one parameter action gets invoked properly.

            var queue = new SerializedActionQueue();
            var value1 = (string)null;
            var value2 = (string)null;
            var value3 = (string)null;

            try
            {
                queue.EnqueueAction<string, string, string>("p1", "p2", "p3",
                    (p1, p2, p3) =>
                    {
                        value1 = p1;
                        value2 = p2;
                        value3 = p3;
                    });

                Helper.WaitFor(() => value1 != null, TimeSpan.FromSeconds(1));
                Assert.AreEqual("p1", value1);
                Assert.AreEqual("p2", value2);
                Assert.AreEqual("p3", value3);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Param4()
        {
            // Verify that a one parameter action gets invoked properly.

            var queue = new SerializedActionQueue();
            var value1 = (string)null;
            var value2 = (string)null;
            var value3 = (string)null;
            var value4 = 0;

            try
            {
                queue.EnqueueAction<string, string, string, int>("p1", "p2", "p3", 4,
                    (p1, p2, p3, p4) =>
                    {

                        value1 = p1;
                        value2 = p2;
                        value3 = p3;
                        value4 = p4;
                    });

                Helper.WaitFor(() => value1 != null, TimeSpan.FromSeconds(1));
                Assert.AreEqual("p1", value1);
                Assert.AreEqual("p2", value2);
                Assert.AreEqual("p3", value3);
                Assert.AreEqual(4, value4);
            }
            finally
            {
                queue.Clear();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SerializedActionQueue_Shutdown()
        {
            // Verify that ShutDown() actually clears pending actions and also
            // disables queuing and execution of future actions.

            var queue = new SerializedActionQueue();
            var task0 = false;
            var task1 = false;
            var task2 = false;
            var task3 = false;
            var task4 = false;
            var done = false;

            try
            {
                // Verify that pending actions are cleared.

                queue.EnqueueAction(() =>
                {
                    task1 = true;
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                    done = true;
                });

                queue.EnqueueAction(() => task2 = true);
                queue.EnqueueAction(() => task3 = true);
                queue.EnqueueAction(() => task4 = true);

                Helper.WaitFor(() => task1, TimeSpan.FromSeconds(1));

                queue.Shutdown();

                Helper.WaitFor(() => done, TimeSpan.FromSeconds(5));

                Assert.IsFalse(task2);
                Assert.IsFalse(task3);
                Assert.IsFalse(task4);

                // Verify the new actions are ignored.

                task1 = task2 = task3 = task4 = false;

                queue.EnqueueAction(() => task0 = true);
                queue.EnqueueAction<int>(1, (p1) => task1 = true);
                queue.EnqueueAction<int, int>(1, 2, (p1, p2) => task2 = true);
                queue.EnqueueAction<int, int, int>(1, 2, 3, (p1, p2, p3) => task3 = true);
                queue.EnqueueAction<int, int, int, int>(1, 2, 3, 4, (p1, p2, p3, p4) => task4 = true);

                Thread.Sleep(TimeSpan.FromSeconds(2));

                Assert.IsFalse(task0 || task1 || task2 || task3 || task4);
            }
            finally
            {
                queue.Clear();
            }
        }
    }
}

