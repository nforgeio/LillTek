//-----------------------------------------------------------------------------
// FILE:        _AsyncMutex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

#pragma warning disable 4014

namespace LillTek.Common.Test
{
    [TestClass]
    public class _AsyncMutex
    {
        private TimeSpan    defaultTimeout = TimeSpan.FromSeconds(15);  // Maximum time to wait for a test operation to complete.
        private const int   repeatCount    = 4;

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncMutex_Basic()
        {
            // Create a mutex and then several tasks that acquire the mutex for
            // varying periods of timev veryfying that each obtains exclusive
            // access.

            var refCount  = 0;
            var error     = false;
            var tasks     = new List<Task>();
            var stopwatch = new Stopwatch();
            var testTime  = defaultTimeout - TimeSpan.FromSeconds(2);

            stopwatch.Start();

            using (var mutex = new AsyncMutex())
            {
                for (int i = 0; i < 20; i++)
                {
                    tasks.Add(Task.Run(
                        async () =>
                        {
                            while (stopwatch.Elapsed < testTime)
                            {
                                using (await mutex.AcquireAsync())
                                {
                                    if (refCount > 0)
                                    {
                                        // This means that we don't have exclusive access indicating
                                        // that the mutex must be broken.

                                        error = true;
                                    }

                                    try
                                    {
                                        Interlocked.Increment(ref refCount);

                                        await Task.Delay(Helper.RandTimespan(TimeSpan.FromMilliseconds(250)));
                                    }
                                    finally
                                    {
                                        Interlocked.Decrement(ref refCount);
                                    }
                                }
                            }
                        }));

                    await Helper.WaitAllAsync(tasks, defaultTimeout);
                }

                Assert.IsFalse(error);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncMutex_Dispose()
        {
            // Create a mutex, acquire it, and then create another task that will
            // attempt to acquire it as well (and will fail because the mutex has
            // already been acquired).  Then dispose the mutex and verify that the
            // task saw the [ObjectDisposedException].

            var mutex    = new AsyncMutex();
            var inTask   = false;
            var acquired = false;
            var disposed = false;

            await mutex.AcquireAsync();

            var task = Task.Run(
                async () =>
                {
                    try
                    {
                        var acquireTask = mutex.AcquireAsync();

                        inTask = true;

                        await acquireTask;

                        acquired = true;
                    }
                    catch (ObjectDisposedException)
                    {
                        disposed = true;
                    }
                });

            // Wait for the task to have called [AcquireAsync()].

            Helper.WaitFor(() => inTask, defaultTimeout);

            // Dispose the mutex, wait for the task to exit and then verify
            // that it caught the [ObjectDisposedException].

            mutex.Dispose();
            task.Wait(defaultTimeout);

            Assert.IsFalse(acquired);
            Assert.IsTrue(disposed);
        }
    }
}