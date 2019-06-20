//-----------------------------------------------------------------------------
// FILE:        ExtendedAssert.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extended unit test assert validations.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Testing
{
    /// <summary>
    /// Extended unit test assert validations.
    /// </summary>
    public static class ExtendedAssert
    {
        /// <summary>
        /// Waits for a boolean delegate to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static void WaitFor(Func<bool> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (action())
                {
                    return;
                }

                Thread.Sleep(pollTime.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for a boolean delegate to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static async Task WaitForAsync(Func<Task<bool>> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (await action())
                {
                    return;
                }

                await Task.Delay(pollTime.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// Waits for a period of time for a task to complete.
        /// </summary>
        /// <param name="task">The <see cref="Task"/>.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <exception cref="TimeoutException">Thrown if the task did not complete within the specified time.</exception>
        public static void WaitFor(Task task, TimeSpan timeout)
        {
            if (!task.Wait(timeout))
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Waits for a period of time for all tasks in a set to complete.
        /// </summary>
        /// <param name="tasks">The <see cref="Task"/> sset.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <exception cref="TimeoutException">Thrown if the all of the tasks did not complete within the specified time.</exception>
        public static void WaitForAll(IEnumerable<Task> tasks, TimeSpan timeout)
        {
            if (!Task.WaitAll(tasks.ToArray(), timeout))
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Waits for a period of time for at least one of the tasks in a set to complete.
        /// </summary>
        /// <param name="tasks">The <see cref="Task"/> sset.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <exception cref="TimeoutException">Thrown if the at least one of the tasks did not complete within the specified time.</exception>
        public static void WaitForAny(IEnumerable<Task> tasks, TimeSpan timeout)
        {
            if (Task.WaitAny(tasks.ToArray(), timeout) == 0)
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Performs an action within an exception handler that verifies that
        /// an exception with the type specified is thrown.
        /// </summary>
        /// <typeparam name="TException">The required exception type.</typeparam>
        /// <param name="action">The test action to be performed.</param>
        public static void Throws<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected a [{0}] to be thrown.", typeof(TException).FullName);
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(AssertFailedException))
                    throw; // Let the test environment handle these.

                if (!typeof(TException).IsAssignableFrom(e.GetType()))
                    Assert.Fail("Expected a [{0}] to be thrown but got a [{1}] instead.", typeof(TException).FullName, e.GetType().FullName);
            }
        }

        /// <summary>
        /// Performs an asynchronous action within an exception handler that verifies that
        /// an exception with the type specified is thrown.
        /// </summary>
        /// <typeparam name="TException">The required exception type.</typeparam>
        /// <param name="action">The test action to be performed.</param>
        public static async Task ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
                Assert.Fail("Expected a [{0}] to be thrown.", typeof(TException).FullName);
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(AssertFailedException))
                    throw; // Let the test environment handle these.

                if (!typeof(TException).IsAssignableFrom(e.GetType()))
                {
                    Assert.Fail("Expected a [{0}] to be thrown but got a [{1}] instead.", typeof(TException).FullName, e.GetType().FullName);
                }
            }
        }

        /// <summary>
        /// Verifies that a collection is empty.
        /// </summary>
        /// <param name="collection">The test collection.</param>
        public static void IsEmpty(IEnumerable collection)
        {
            var count = 0;

            foreach (var item in collection)
            {
                count++;
            }
                
            if (count > 0)
                Assert.Fail("Expected an empty collection but it contains [{0}] items instead.", count);
        }

        /// <summary>
        /// Verifies that a collection is not empty.
        /// </summary>
        /// <param name="collection">The test collection.</param>
        public static void IsNotEmpty(IEnumerable collection)
        {
            foreach (var item in collection)
            {
                return;
            }

            Assert.Fail("Expected an non-empty collection.");
        }
    }
}
