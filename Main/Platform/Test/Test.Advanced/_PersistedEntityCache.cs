//-----------------------------------------------------------------------------
// FILE:        _PersistedEntityCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _PersistedEntityCache
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PersistedEntityCache_Basic()
        {
            var cache = new PersistedEntityCache<string, string>(TimeSpan.MaxValue, TimeSpan.MaxValue, StringComparer.OrdinalIgnoreCase);
            try
            {
                // Test item doesn't exist.

                Assert.IsNull(cache.Get("hello"));

                // Test cache clear.

                cache.Add("hello", "world", TimeSpan.FromSeconds(1));
                Assert.AreEqual("world", cache.Get("hello"));
                cache.Clear();
                Assert.IsNull(cache.Get("hello"));

                // Test item TTL/Flush.

                cache.Clear();
                cache.Add("hello", "world", TimeSpan.FromSeconds(1));
                Assert.AreEqual("world", cache.Get("hello"));
                Thread.Sleep(TimeSpan.FromSeconds(1.5));
                Assert.AreEqual("world", cache.Get("hello"));
                cache.Flush();
                Assert.IsNull(cache.Get("hello"));

                // Test item remove.

                cache.Clear();
                cache.Add("hello", "world", TimeSpan.FromSeconds(1));
                Assert.AreEqual("world", cache.Get("hello"));
                cache.Remove("hello");
                Assert.IsNull(cache.Get("hello"));

                // Test item retrieval.

                cache.Clear();
                Assert.AreEqual("world", cache.Get("hello", () => "world", TimeSpan.FromSeconds(1)));

                // Test with a [null] retriever delegate.

                cache.Clear();
                Assert.IsNull(cache.Get("hello", null, TimeSpan.FromSeconds(1)));

                // Verify that the TTL is set when the retriever is used.

                cache.Clear();
                Assert.AreEqual("world", cache.Get("hello", () => "world", TimeSpan.FromSeconds(1)));
                Assert.AreEqual("world", cache.Get("hello"));
                Thread.Sleep(TimeSpan.FromSeconds(1.5));
                Assert.AreEqual("world", cache.Get("hello"));
                cache.Flush();
                Assert.IsNull(cache.Get("hello"));

                // Blast traffic at the class from multiple threads to verify that there
                // are no threading issues.

                var testEndTime = SysTime.Now + TimeSpan.FromSeconds(10);
                var failed = false;

                for (int i = 0; i < 4; i++)
                {
                    Helper.EnqueueAction<int>(
                        i,
                        index =>
                        {
                            try
                            {
                                var key = string.Format("{0}-hello", index);

                                while (SysTime.Now < testEndTime)
                                {
                                    Assert.AreEqual("world", cache.Get(key, () => "world", TimeSpan.FromSeconds(1)));
                                    Assert.AreEqual("world", cache.Get(key));
                                    cache.Flush();
                                    Assert.AreEqual("world", cache.Get(key));
                                }
                            }
                            catch
                            {
                                failed = true;
                            }
                        });

                    Assert.IsFalse(failed);
                }

                // Test integrated chat purging.

                cache.Stop();

                cache = new PersistedEntityCache<string, string>(TimeSpan.FromSeconds(1), TimeSpan.MaxValue);
                Assert.AreEqual("world", cache.Get("hello", () => "world", TimeSpan.FromSeconds(0.5)));
                Assert.AreEqual("world", cache.Get("hello"));
                Thread.Sleep(TimeSpan.FromSeconds(1.5));
                Assert.IsNull(cache.Get("hello"));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void PersistedEntityCache_SimultaneousGet()
        {
            var cache = new PersistedEntityCache<string, string>(TimeSpan.MaxValue, TimeSpan.MaxValue, StringComparer.OrdinalIgnoreCase);

            try
            {
                // Verify that the cache doesn't barf if two GETs for the same item happen
                // at the same time.

                string t1, t2;

                t1 = cache.Get("test",
                    () =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        return "hello";
                    });

                t2 = cache.Get("test",
                    () =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        return "hello";
                    });

                Assert.AreEqual(1, cache.Count);
                Assert.AreEqual("hello", t1);
                Assert.AreEqual("hello", t2);

                t1 = cache.Get("test",
                    () =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        return "hello";
                    });

                Assert.AreEqual("hello", t1);
            }
            finally
            {
                cache.Stop();
            }
        }
    }
}


