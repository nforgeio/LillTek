﻿//-----------------------------------------------------------------------------
// FILE:        _AsyncReaderWriterLock.cs
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

#pragma warning disable 4014

namespace LillTek.Common.Test
{
    [TestClass]
    public class _AsyncReaderWriterLock
    {
        private TimeSpan    defaultTimeout = TimeSpan.FromSeconds(15);  // Maximum time to wait for a test operation to complete.
        private TimeSpan    taskWait       = TimeSpan.FromSeconds(2);   // Delay time to allow a task to start (fragile)
        private const int   repeatCount    = 4;

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_Basic()
        {
            var rwLock   = (AsyncReaderWriterLock)null;
            var haveLock = false;

            // Verify that we can obtain a reader lock.

            rwLock   = new AsyncReaderWriterLock();
            haveLock = false;

            using (await rwLock.GetReadLockAsync())
            {
                haveLock = true;
            }

            Assert.IsTrue(haveLock);
            rwLock.Dispose();

            // Verify that we can obtain a writer lock.

            rwLock   = new AsyncReaderWriterLock();
            haveLock = false;

            using (await rwLock.GetWriteLockAsync())
            {
                haveLock = true;
            }

            Assert.IsTrue(haveLock);
            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenWriter()
        {
            // Verify that obtaining a reader lock blocks a writer lock and then
            // that releasing the read lock, unblocks the writer.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var readLock = await rwLock.GetReadLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetWriteLockAsync())
                    {
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsFalse(haveLock);

            readLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenReader()
        {
            // Verify that obtaining a reader lock does not block
            // another reader.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var readLock = await rwLock.GetReadLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetReadLockAsync())
                    {
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsTrue(haveLock);

            readLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenReader()
        {
            // Verify that obtaining a writer lock blocks a reader lock and then
            // that releasing the write lock, unblocks the reader.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var writeLock = await rwLock.GetWriteLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetReadLockAsync())
                    {
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsFalse(haveLock);

            writeLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleReaders()
        {
            // Verify that obtaining a writer lock blocks multiple readers and then
            // that releasing the write lock, unblocks all of the readers.

            const int readerCount = 10;

            var waitCount    = 0;
            var acquireCount = 0;
            var rwLock       = new AsyncReaderWriterLock();
            var writeLock    = await rwLock.GetWriteLockAsync();

            for (int i = 0; i < readerCount; i++)
            {
                Task.Run(
                    async () =>
                    {
                        Interlocked.Increment(ref waitCount);

                        using (await rwLock.GetReadLockAsync())
                        {
                            Interlocked.Increment(ref acquireCount);
                        }
                    });
            }

            Helper.WaitFor(() => waitCount == readerCount, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.AreEqual(0, acquireCount);

            writeLock.Dispose();
            Helper.WaitFor(() => acquireCount == readerCount, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleWriters()
        {
            // Verify that obtaining a writer lock blocks multiple writers and then
            // that releasing the write lock, unblocks all of the other writers.

            const int writerCount = 10;

            var waitCount    = 0;
            var acquireCount = 0;
            var rwLock       = new AsyncReaderWriterLock();
            var writeLock    = await rwLock.GetWriteLockAsync();

            for (int i = 0; i < writerCount; i++)
            {
                Task.Run(
                    async () =>
                    {
                        Interlocked.Increment(ref waitCount);

                        //using (await rwLock.GetWriteLockAsync())
                        //{
                        //    Interlocked.Increment(ref acquireCount);
                        //}

                        var testLock = await rwLock.GetWriteLockAsync();

                        Interlocked.Increment(ref acquireCount);
                        testLock.Dispose();
                    });
            }

            Helper.WaitFor(() => waitCount == writerCount, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.AreEqual(0, acquireCount);

            writeLock.Dispose();
            Helper.WaitFor(() => acquireCount == writerCount, defaultTimeout);

            rwLock.Dispose();
        }

        //=====================================================================

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_Basic_Repeat()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
                var haveLock = false;

                for (int i = 0; i < repeatCount; i++)
                {
                    // Verify that we can obtain a reader lock.

                    using (await rwLock.GetReadLockAsync())
                    {
                        haveLock = true;
                    }

                    Assert.IsTrue(haveLock);
                    haveLock = false;

                    // Verify that we can obtain a writer lock.

                    using (await rwLock.GetWriteLockAsync())
                    {
                        haveLock = true;
                    }

                    Assert.IsTrue(haveLock);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenWriter_Repeat()
        {
            // Verify that obtaining a reader lock blocks a writer lock and then
            // that releasing the read lock, unblocks the writer.

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock = false;
                    var inTask   = false;
                    var readLock = await rwLock.GetReadLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            var writeLock = await rwLock.GetWriteLockAsync();

                            haveLock = true;

                            writeLock.Dispose();
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsFalse(haveLock);

                    readLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenReader_Repeat()
        {
            // Verify that obtaining a reader lock does not block
            // another reader.

            using (var rwLock   = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock = false;
                    var inTask   = false;
                    var readLock = await rwLock.GetReadLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            using (await rwLock.GetReadLockAsync())
                            {
                                haveLock = true;
                            }
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsTrue(haveLock);

                    readLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenReader_Repeat()
        {
            // Verify that obtaining a writer lock blocks a reader lock and then
            // that releasing the write lock, unblocks the reader.

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock  = false;
                    var inTask    = false;
                    var writeLock = await rwLock.GetWriteLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            using (await rwLock.GetReadLockAsync())
                            {
                                haveLock = true;
                            }
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsFalse(haveLock);

                    writeLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleReaders_Repeat()
        {
            // Verify that obtaining a writer lock blocks multiple readers and then
            // that releasing the write lock, unblocks all of the readers.

            const int readerCount = 10;

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var waitCount    = 0;
                    var acquireCount = 0;
                    var writeLock    = await rwLock.GetWriteLockAsync();

                    for (int j = 0; j < readerCount; j++)
                    {
                        Task.Run(
                            async () =>
                            {
                                Interlocked.Increment(ref waitCount);

                                using (await rwLock.GetReadLockAsync())
                                {
                                    Interlocked.Increment(ref acquireCount);
                                }
                            });
                    }

                    Helper.WaitFor(() => waitCount == readerCount, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.AreEqual(0, acquireCount);

                    writeLock.Dispose();
                    Helper.WaitFor(() => acquireCount == readerCount, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleWriters_Repeat()
        {
            // Verify that obtaining a writer lock blocks multiple writers and then
            // that releasing the write lock, unblocks all of the other writers.

            const int writerCount = 10;

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var waitCount    = 0;
                    var acquireCount = 0;
                    var writeLock    = await rwLock.GetWriteLockAsync();

                    for (int j = 0; j < writerCount; j++)
                    {
                        Task.Run(
                            async () =>
                            {
                                Interlocked.Increment(ref waitCount);

                                using (await rwLock.GetWriteLockAsync())
                                {
                                    Interlocked.Increment(ref acquireCount);
                                }
                            });
                    }

                    Helper.WaitFor(() => waitCount == writerCount, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.AreEqual(0, acquireCount);

                    writeLock.Dispose();
                    Helper.WaitFor(() => acquireCount == writerCount, defaultTimeout);
                }
            }
        }

        //=====================================================================

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_Basic_Delay()
        {
            var rwLock   = (AsyncReaderWriterLock)null;
            var haveLock = false;

            // Verify that we can obtain a reader lock.  We'll add delay
            // to mix things up.

            rwLock   = new AsyncReaderWriterLock();
            haveLock = false;

            using (await rwLock.GetReadLockAsync())
            {
                await Task.Delay(250);
                haveLock = true;
            }

            Assert.IsTrue(haveLock);
            rwLock.Dispose();

            // Verify that we can obtain a writer lock.

            rwLock   = new AsyncReaderWriterLock();
            haveLock = false;

            using (await rwLock.GetWriteLockAsync())
            {
                await Task.Delay(250);
                haveLock = true;
            }

            Assert.IsTrue(haveLock);
            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenWriter_Delay()
        {
            // Verify that obtaining a reader lock blocks a writer lock and then
            // that releasing the read lock, unblocks the writer.    We'll add delay
            // to mix things up.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var readLock = await rwLock.GetReadLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetWriteLockAsync())
                    {
                        await Task.Delay(250);
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsFalse(haveLock);

            readLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenReader_Delay()
        {
            // Verify that obtaining a reader lock does not block
            // another reader.  We'll add delay to mix things up.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var readLock = await rwLock.GetReadLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetReadLockAsync())
                    {
                        await Task.Delay(250);
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsTrue(haveLock);

            readLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenReader_Delay()
        {
            // Verify that obtaining a writer lock blocks a reader lock and then
            // that releasing the write lock, unblocks the reader.  We'll add delay
            // to mix things up.

            var rwLock   = new AsyncReaderWriterLock();
            var haveLock = false;
            var inTask   = false;

            var writeLock = await rwLock.GetWriteLockAsync();

            Task.Run(
                async () =>
                {
                    inTask = true;

                    using (await rwLock.GetReadLockAsync())
                    {
                        await Task.Delay(250);
                        haveLock = true;
                    }
                });

            Helper.WaitFor(() => inTask, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.IsFalse(haveLock);

            writeLock.Dispose();
            Helper.WaitFor(() => haveLock, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleReaders_Delay()
        {
            // Verify that obtaining a writer lock blocks multiple readers and then
            // that releasing the write lock, unblocks all of the readers.  We'll 
            // add delay to mix things up.

            const int readerCount = 10;

            var waitCount    = 0;
            var acquireCount = 0;
            var rwLock       = new AsyncReaderWriterLock();
            var writeLock    = await rwLock.GetWriteLockAsync();

            for (int i = 0; i < readerCount; i++)
            {
                Task.Run(
                    async () =>
                    {
                        Interlocked.Increment(ref waitCount);

                        using (await rwLock.GetReadLockAsync())
                        {
                            await Task.Delay(100);
                            Interlocked.Increment(ref acquireCount);
                        }
                    });
            }

            Helper.WaitFor(() => waitCount == readerCount, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.AreEqual(0, acquireCount);

            writeLock.Dispose();
            Helper.WaitFor(() => acquireCount == readerCount, defaultTimeout);

            rwLock.Dispose();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleWriters_Delay()
        {
            // Verify that obtaining a writer lock blocks multiple writers and then
            // that releasing the write lock, unblocks all of the other writers.
            // We'll add delay to mix things up.

            const int writerCount = 10;

            var waitCount    = 0;
            var acquireCount = 0;
            var rwLock       = new AsyncReaderWriterLock();
            var writeLock    = await rwLock.GetWriteLockAsync();

            for (int i = 0; i < writerCount; i++)
            {
                Task.Run(
                    async () =>
                    {
                        Interlocked.Increment(ref waitCount);

                        using (await rwLock.GetWriteLockAsync())
                        {
                            await Task.Delay(100);
                            Interlocked.Increment(ref acquireCount);
                        }
                    });
            }

            Helper.WaitFor(() => waitCount == writerCount, defaultTimeout);
            await Task.Delay(taskWait);
            Assert.AreEqual(0, acquireCount);

            writeLock.Dispose();
            Helper.WaitFor(() => acquireCount == writerCount, defaultTimeout);

            rwLock.Dispose();
        }

        //=====================================================================

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_Basic_Repeat_Delay()
        {
            using (var rwLock = new AsyncReaderWriterLock())
            {
                var haveLock = false;

                for (int i = 0; i < repeatCount; i++)
                {
                    // Verify that we can obtain a reader lock.
                    // We'll add delay to mix things up.

                    using (await rwLock.GetReadLockAsync())
                    {
                        await Task.Delay(100);
                        haveLock = true;
                    }

                    Assert.IsTrue(haveLock);
                    haveLock = false;

                    // Verify that we can obtain a writer lock.
                    // We'll add delay to mix things up.

                    using (await rwLock.GetWriteLockAsync())
                    {
                        await Task.Delay(100);
                        haveLock = true;
                    }

                    Assert.IsTrue(haveLock);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenWriter_Repeat_Delay()
        {
            // Verify that obtaining a reader lock blocks a writer lock and then
            // that releasing the read lock, unblocks the writer.  We'll add delay
            // to mix things up.

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock = false;
                    var inTask   = false;
                    var readLock = await rwLock.GetReadLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            using (await rwLock.GetWriteLockAsync())
                            {
                                await Task.Delay(100);
                                haveLock = true;
                            }
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsFalse(haveLock);

                    readLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_ReaderThenReader_Repeat_Delay()
        {
            // Verify that obtaining a reader lock does not block
            // another reader.  We'll add delay to mix things up.

            using (var rwLock   = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock = false;
                    var inTask   = false;
                    var readLock = await rwLock.GetReadLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            using (await rwLock.GetReadLockAsync())
                            {
                                await Task.Delay(100);
                                haveLock = true;
                            }
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsTrue(haveLock);

                    readLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenReader_Repeat_Delay()
        {
            // Verify that obtaining a writer lock blocks a reader lock and then
            // that releasing the write lock, unblocks the reader.  We'll add 
            // delay to mix things up.

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var haveLock  = false;
                    var inTask    = false;
                    var writeLock = await rwLock.GetWriteLockAsync();

                    Task.Run(
                        async () =>
                        {
                            inTask = true;

                            using (await rwLock.GetReadLockAsync())
                            {
                                await Task.Delay(100);
                                haveLock = true;
                            }
                        });

                    Helper.WaitFor(() => inTask, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.IsFalse(haveLock);

                    writeLock.Dispose();
                    Helper.WaitFor(() => haveLock, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleReaders_Repeat_Delay()
        {
            // Verify that obtaining a writer lock blocks multiple readers and then
            // that releasing the write lock, unblocks all of the readers.  We'll add 
            // delay to mix things up.

            const int readerCount = 10;

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var waitCount    = 0;
                    var acquireCount = 0;
                    var writeLock    = await rwLock.GetWriteLockAsync();

                    for (int j = 0; j < readerCount; j++)
                    {
                        Task.Run(
                            async () =>
                            {
                                Interlocked.Increment(ref waitCount);

                                using (await rwLock.GetReadLockAsync())
                                {
                                    await Task.Delay(100);
                                    Interlocked.Increment(ref acquireCount);
                                }
                            });
                    }

                    Helper.WaitFor(() => waitCount == readerCount, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.AreEqual(0, acquireCount);

                    writeLock.Dispose();
                    Helper.WaitFor(() => acquireCount == readerCount, defaultTimeout);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_WriterThenMultipleWriters_Repeat_Delay()
        {
            // Verify that obtaining a writer lock blocks multiple writers and then
            // that releasing the write lock, unblocks all of the other writers.
            // We'll add delay to mix things up.

            const int writerCount = 10;

            using (var rwLock = new AsyncReaderWriterLock())
            {
                for (int i = 0; i < repeatCount; i++)
                {
                    var waitCount    = 0;
                    var acquireCount = 0;
                    var writeLock    = await rwLock.GetWriteLockAsync();

                    for (int j = 0; j < writerCount; j++)
                    {
                        Task.Run(
                            async () =>
                            {
                                Interlocked.Increment(ref waitCount);

                                using (await rwLock.GetWriteLockAsync())
                                {
                                    await Task.Delay(100);
                                    Interlocked.Increment(ref acquireCount);
                                }
                            });
                    }

                    Helper.WaitFor(() => waitCount == writerCount, defaultTimeout);
                    await Task.Delay(taskWait);
                    Assert.AreEqual(0, acquireCount);

                    writeLock.Dispose();
                    Helper.WaitFor(() => acquireCount == writerCount, defaultTimeout);
                }
            }
        }

        //=====================================================================

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void AsyncReaderWriterLock_ReaderWriters()
        {
            // Verify that multiple pending locks work.

            using (var rwLock = new AsyncReaderWriterLock())
            {
                var     read1LockTask  = rwLock.GetReadLockAsync();
                var     write1LockTask = rwLock.GetWriteLockAsync();
                var     read2LockTask  = rwLock.GetReadLockAsync();
                var     write2LockTask = rwLock.GetWriteLockAsync();
                var     haveLock       = false;
                Task    t;

                // Wait for the first read lock.

                haveLock = false;
                t = Task.Run(async () =>
                    {
                        var lk = await read1LockTask;

                        haveLock = true;

                        lk.Dispose();
                    });

                Helper.WaitFor(() => haveLock, defaultTimeout);

                // Wait for the first write lock.

                haveLock = false;
                t = Task.Run(async () =>
                {
                    var lk = await write1LockTask;

                    haveLock = true;

                    lk.Dispose();
                });

                Helper.WaitFor(() => haveLock, defaultTimeout);

                // Wait for the second write lock.  Not that write lock 2
                // is favored over read lock 2.

                haveLock = false;
                t = Task.Run(async () =>
                {
                    var lk = await write2LockTask;

                    haveLock = true;

                    lk.Dispose();
                });

                Helper.WaitFor(() => haveLock, defaultTimeout);

                // Wait for the second read lock.

                haveLock = false;
                t = Task.Run(async () =>
                {
                    var lk = await read2LockTask;

                    haveLock = true;

                    lk.Dispose();
                });

                Helper.WaitFor(() => haveLock, defaultTimeout);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task AsyncReaderWriterLock_MultipleTasks()
        {
            // Verify that we can acquire and release both read and write
            // locks over an extended period of time from several parallel
            // tasks.  We're also going to verify that only one writer is
            // allowed at a time and that readers aren't allowed when a writer
            // has the lock.

            const int readTaskCount  = 50;
            const int writeTaskCount = 10;

            var readers = 0;
            var writers = 0;
            var delay   = TimeSpan.FromMilliseconds(50);

            using (var rwLock = new AsyncReaderWriterLock())
            {
                var exit  = false;
                var tasks = new List<Task>();
                var error = false;

                for (int i = 0; i < readTaskCount; i++)
                {
                    tasks.Add(Task.Run(
                        async () =>
                        {
                            while (!exit)
                            {
                                using (await rwLock.GetReadLockAsync())
                                {
                                    if (writers > 0)
                                    {
                                        error = true;
                                    }

                                    Interlocked.Increment(ref readers);
                                    await Task.Delay(delay);
                                    Interlocked.Decrement(ref readers);
                                }
                            }
                        }));
                }

                for (int i = 0; i < writeTaskCount; i++)
                {
                    tasks.Add(Task.Run(
                        async () =>
                        {
                            while (!exit)
                            {
                                using (await rwLock.GetWriteLockAsync())
                                {
                                    if (writers > 0)
                                    {
                                        error = true;
                                    }

                                    if (readers > 0)
                                    {
                                        error = true;
                                    }

                                    Interlocked.Increment(ref writers);
                                    await Task.Delay(delay);
                                    Interlocked.Decrement(ref writers);
                                }
                            }
                        }));
                }

                await Task.Delay(TimeSpan.FromSeconds(60));
                exit = true;
                await Helper.WaitAllAsync(tasks.ToArray(), defaultTimeout);
                Assert.IsFalse(error);
            }
        }
    }
}
