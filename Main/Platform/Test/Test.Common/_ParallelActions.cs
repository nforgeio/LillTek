//-----------------------------------------------------------------------------
// FILE:        _ParallelActions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ParallelActions
    {
        private TimeSpan timeout = TimeSpan.FromSeconds(2);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_Basic()
        {
            // Test the successful dispatch and join using all of the
            // EnqueueAction() methods.

            ParallelActions parallel = new ParallelActions();
            bool task0_OK = false;
            bool task1_OK = false;
            bool task2_OK = false;
            bool task3_OK = false;
            bool task4_OK = false;

            parallel.EnqueueAction(() => task0_OK = true);
            parallel.EnqueueAction<int>(1, (p1) => task1_OK = p1 == 1);
            parallel.EnqueueAction<int, int>(1, 2, (p1, p2) => task2_OK = p1 == 1 && p2 == 2);
            parallel.EnqueueAction<int, int, int>(1, 2, 3, (p1, p2, p3) => task3_OK = p1 == 1 && p2 == 2 && p3 == 3);
            parallel.EnqueueAction<int, int, int, int>(1, 2, 3, 4, (p1, p2, p3, p4) => task4_OK = p1 == 1 && p2 == 2 && p3 == 3 && p4 == 4);
            parallel.Join(timeout);

            Assert.IsTrue(task0_OK);
            Assert.IsTrue(task1_OK);
            Assert.IsTrue(task2_OK);
            Assert.IsTrue(task3_OK);
            Assert.IsTrue(task4_OK);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_NoTasks()
        {
            // Verify that the class works when there are no tasks.

            ParallelActions parallel = new ParallelActions();

            parallel.Join(timeout);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_Timeout()
        {
            // Verify that a timeout is detected.

            ParallelActions parallel = new ParallelActions();

            parallel.EnqueueAction(() => Thread.Sleep(TimeSpan.FromSeconds(2)));

            try
            {
                parallel.Join(TimeSpan.FromSeconds(0.5));
                Assert.Fail("TimeoutException expected");
            }
            catch (TimeoutException)
            {
                // Expected
            }

            // Give the task a chance to complete before existing the test

            Thread.Sleep(TimeSpan.FromSeconds(2.25));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_TaskException()
        {
            // Queue a couple of tasks having one of them throw an exception.  Then
            // verify that Join() still works.

            ParallelActions parallel = new ParallelActions();
            bool isOK = false;

            parallel.EnqueueAction(() => { Thread.Sleep(250); isOK = true; });
            parallel.EnqueueAction(() => { throw new Exception(); });
            parallel.Join(timeout);

            Assert.IsTrue(isOK);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_InvalidOperation_EnqueueActionAfterJoin()
        {
            // Verify that invalid operations can be detected.

            ParallelActions parallel;

            parallel = new ParallelActions();
            parallel.Join();

            try
            {
                parallel.EnqueueAction(() => { Thread.Sleep(250); });
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            try
            {
                parallel.EnqueueAction<int>(1, (p1) => { Thread.Sleep(250); });
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            try
            {
                parallel.EnqueueAction<int, int>(1, 2, (p1, p2) => { Thread.Sleep(250); });
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            try
            {
                parallel.EnqueueAction<int, int, int>(1, 2, 3, (p1, p2, p3) => { Thread.Sleep(250); });
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            try
            {
                parallel.EnqueueAction<int, int, int, int>(1, 2, 3, 4, (p1, p2, p3, p4) => { Thread.Sleep(250); });
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ParallelActions_InvalidOperation_MultipleJoins()
        {
            // Verify that invalid operations can be detected.

            ParallelActions parallel;

            parallel = new ParallelActions();
            parallel.Join();

            try
            {
                parallel.Join();
                Assert.Fail("InvalidOperationException expected");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }
    }
}

