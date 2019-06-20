//-----------------------------------------------------------------------------
// FILE:        _TimesLRUCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Threading;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _TimedLRUCache
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_Basic()
        {
            TimedLRUCache<string, string> cache;
            string value;

            cache = new TimedLRUCache<string, string>(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(0, cache.Count);

            cache.Add("foo", "bar");
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual("bar", cache["foo"]);

            try
            {
                value = cache["xxx"];
                Assert.Fail("Expected a KeyNotFoundException");
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(KeyNotFoundException).Name, e.GetType().Name);
            }

            cache["foo"] = "foobar";
            Assert.AreEqual("foobar", cache["foo"]);

            cache["bar"] = "boobar";
            Assert.AreEqual("boobar", cache["bar"]);
            Assert.AreEqual("foobar", cache["foo"]);

            Assert.IsTrue(cache.TryGetValue("foo", out value));
            Assert.AreEqual("foobar", value);
            Assert.IsTrue(cache.TryGetValue("bar", out value));
            Assert.AreEqual("boobar", value);
            Assert.IsFalse(cache.TryGetValue("xxx", out value));

            Assert.IsTrue(cache.ContainsKey("foo"));
            Assert.IsTrue(cache.ContainsKey("bar"));
            Assert.IsFalse(cache.ContainsKey("xxx"));

            cache.Remove("foo");
            Assert.IsFalse(cache.ContainsKey("foo"));

            cache.Remove("bar");
            Assert.IsFalse(cache.ContainsKey("bar"));

            cache.Remove("xxx");
            Assert.IsFalse(cache.ContainsKey("xxx"));

            cache["foo"] = "foobar";
            cache["bar"] = "boobar";
            Assert.AreEqual(2, cache.Count);
            Assert.IsTrue(cache.ContainsKey("foo"));
            Assert.IsTrue(cache.ContainsKey("bar"));
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.ContainsKey("foo"));
            Assert.IsFalse(cache.ContainsKey("bar"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_MaxItems()
        {
            TimedLRUCache<int, int> cache;
            int value;

            cache = new TimedLRUCache<int, int>();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(int.MaxValue, cache.MaxItems);
            cache.MaxItems = 3;

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache.Add(i, i);

            Assert.AreEqual(3, cache.Count);
            cache.Add(3, 3);
            Assert.AreEqual(3, cache.Count);

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache[i] = i;

            Assert.AreEqual(3, cache.Count);
            cache[3] = 3;
            Assert.AreEqual(3, cache.Count);

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache[i] = i;

            Assert.AreEqual(3, cache.Count);
            cache.MaxItems = 2;
            Assert.AreEqual(2, cache.Count);

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));

            cache.MaxItems = 3;

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache[i] = i;

            Assert.IsTrue(cache.ContainsKey(0));

            Assert.AreEqual(3, cache.Count);
            cache[3] = 3;
            Assert.AreEqual(3, cache.Count);

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache[i] = i;

            cache.Touch(0);

            Assert.AreEqual(3, cache.Count);
            cache[3] = 3;
            Assert.AreEqual(3, cache.Count);

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 3; i++)
                cache[i] = i;

            cache.TryGetValue(0, out value);

            Assert.AreEqual(3, cache.Count);
            cache[3] = 3;
            Assert.AreEqual(3, cache.Count);

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            //---------------------------------------------

            cache.Clear();
            for (int i = 0; i < 6; i++)
                cache[i] = i;

            cache.TryGetValue(0, out value);

            Assert.AreEqual(3, cache.Count);
            cache[3] = 3;
            Assert.AreEqual(3, cache.Count);

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsFalse(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));
            Assert.IsTrue(cache.ContainsKey(4));
            Assert.IsTrue(cache.ContainsKey(5));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_Flush()
        {
            TimedLRUCache<int, int> cache;

            cache = new TimedLRUCache<int, int>();
            Assert.IsFalse(cache.RenewTTD);

            cache.Add(0, 0, TimeSpan.FromSeconds(1.0));
            cache.Add(1, 1, TimeSpan.FromSeconds(1.0));
            cache.Add(2, 2, TimeSpan.FromSeconds(2.0));
            cache.Add(3, 3, TimeSpan.FromSeconds(2.0));

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            Thread.Sleep(TimeSpan.FromSeconds(1) + SysTime.Resolution);
            cache.Flush();

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            Thread.Sleep(TimeSpan.FromSeconds(1) + SysTime.Resolution);
            cache.Flush();

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsFalse(cache.ContainsKey(2));
            Assert.IsFalse(cache.ContainsKey(3));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_FlushLRU()
        {
            TimedLRUCache<int, int> cache;

            cache = new TimedLRUCache<int, int>();
            Assert.IsFalse(cache.RenewTTD);

            cache.Add(0, 0, TimeSpan.FromSeconds(1.0));
            cache.Add(1, 1, TimeSpan.FromSeconds(1.0));
            cache.Add(2, 2, TimeSpan.FromSeconds(4.0));
            cache.Add(3, 3, TimeSpan.FromSeconds(1.0));
            cache.Touch(0);
            cache.Touch(1);
            cache.Touch(3);

            // Item 2 should now head the LRU list

            Thread.Sleep(TimeSpan.FromSeconds(1) + SysTime.Resolution);
            cache.FlushLRU();

            // All the items should still be present since
            // item 2 hasn't expired yet and will block any
            // other items from be purged

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));

            Thread.Sleep(TimeSpan.FromSeconds(3) + SysTime.Resolution);

            // All of the items should have expired by now

            cache.FlushLRU();

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsFalse(cache.ContainsKey(2));
            Assert.IsFalse(cache.ContainsKey(3));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_RenewTTD()
        {
            TimedLRUCache<int, int> cache;

            cache = new TimedLRUCache<int, int>();
            cache.RenewTTD = true;

            cache.Add(0, 0, TimeSpan.FromSeconds(1.0));
            cache.Add(1, 1, TimeSpan.FromSeconds(1.0));
            cache.Add(2, 2, TimeSpan.FromSeconds(1.0));
            cache.Add(3, 3, TimeSpan.FromSeconds(1.0));

            Thread.Sleep(TimeSpan.FromSeconds(1) + SysTime.Resolution);

            cache.Touch(0);
            cache.Flush();

            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsFalse(cache.ContainsKey(2));
            Assert.IsFalse(cache.ContainsKey(3));
        }

        private class DisposableItem : TestItem, IDisposable
        {
            public bool IsDisposed;

            public DisposableItem(int key)
                : base(key)
            {
                this.IsDisposed = false;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private class TestItem
        {
            public int Key;

            public TestItem(int key)
            {
                this.Key = key;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_AutoDispose()
        {
            TimedLRUCache<int, TestItem> cache;
            DisposableItem dItem0 = new DisposableItem(0);
            DisposableItem dItem1 = new DisposableItem(1);
            DisposableItem dItem2 = new DisposableItem(2);
            TestItem item0 = new TestItem(0);
            TestItem item1 = new TestItem(1);
            TestItem item2 = new TestItem(2);

            cache = new TimedLRUCache<int, TestItem>();
            cache.AutoDispose = true;
            cache.DefaultTTL = TimeSpan.FromMilliseconds(200);

            // Verify that disposable items are disposed when they
            // are implicitly removed from the cache when the maximum
            // number of items allowed has been exceeded.

            cache.MaxItems = 2;
            cache.Add(0, dItem0);
            cache.Add(1, dItem1);
            cache.Add(2, dItem2);

            Assert.AreEqual(2, cache.Count);
            Assert.IsTrue(dItem0.IsDisposed);
            Assert.IsFalse(dItem1.IsDisposed);
            Assert.IsFalse(dItem2.IsDisposed);

            // Verify that disposable items are disposed when the 
            // cache is cleared.

            cache.Clear();
            Assert.IsTrue(dItem1.IsDisposed);
            Assert.IsTrue(dItem2.IsDisposed);

            // Verify that disposable items are disposed when they
            // are explicitly removed.

            dItem0.IsDisposed = false;
            cache.Add(0, dItem0);
            cache.Remove(0);
            Assert.IsTrue(dItem0.IsDisposed);

            // Verify that disposable items are disposed when they
            // are replaced in the cache.

            cache.Clear();
            dItem0.IsDisposed = false;
            dItem1.IsDisposed = false;
            cache.Add(0, dItem0);
            cache[0] = dItem1;
            Assert.IsTrue(dItem0.IsDisposed);

            // Verify that replacing the same disposable item instance
            // doesn't dispose the object.

            cache.Clear();
            dItem0.IsDisposed = false;
            cache.Add(0, dItem0);
            cache[0] = dItem0;
            Assert.IsFalse(dItem0.IsDisposed);

            // Verify disposal after flushing.

            cache.Clear();
            dItem0.IsDisposed = false;
            dItem1.IsDisposed = false;
            dItem2.IsDisposed = false;
            cache.Add(0, dItem0);
            cache.Add(1, dItem1);
            cache.Add(2, dItem2);
            Thread.Sleep(250);
            cache.Flush();
            Assert.IsTrue(dItem0.IsDisposed);
            Assert.IsTrue(dItem1.IsDisposed);
            Assert.IsTrue(dItem2.IsDisposed);

            cache.Clear();
            dItem0.IsDisposed = false;
            dItem1.IsDisposed = false;
            dItem2.IsDisposed = false;
            cache.Add(0, dItem0);
            cache.Add(1, dItem1);
            cache.Add(2, dItem2);
            Thread.Sleep(250);
            cache.FlushLRU();
            Assert.IsTrue(dItem0.IsDisposed);
            Assert.IsTrue(dItem1.IsDisposed);
            Assert.IsTrue(dItem2.IsDisposed);

            // Verify that non-disposable items don't cause trouble 
            // when AutoDispose=true

            cache.Clear();
            cache.Add(0, item0);
            cache.Add(1, item1);
            cache.Add(2, item2);
            cache.Remove(1);
            cache[1] = new TestItem(3);
            cache[2] = cache[2];
            cache.Clear();

            cache.Add(0, item0);
            cache.Add(1, item1);
            cache.Add(2, item2);
            Thread.Sleep(250);
            cache.Flush();

            cache.Add(0, item0);
            cache.Add(1, item1);
            cache.Add(2, item2);
            Thread.Sleep(250);
            cache.FlushLRU();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void TimedLRUCache_GetHitStats()
        {
            TimedLRUCache<string, string> cache = new TimedLRUCache<string, string>();
            int cHits, cMisses;
            string found;

            cache.Add("a", "a");
            cache.Add("b", "b");
            cache.Add("c", "c");

            cache.GetHitStats(out cHits, out cMisses);
            Assert.AreEqual(0, cHits);
            Assert.AreEqual(0, cMisses);

            cache.TryGetValue("a", out found);
            cache.TryGetValue("b", out found);
            cache.TryGetValue("c", out found);
            cache.TryGetValue("d", out found);

            cache.GetHitStats(out cHits, out cMisses);
            Assert.AreEqual(3, cHits);
            Assert.AreEqual(1, cMisses);

            cache.GetHitStats(out cHits, out cMisses);
            Assert.AreEqual(0, cHits);
            Assert.AreEqual(0, cMisses);

            found = cache["a"];
            found = cache["b"];

            try
            {
                found = cache["d"];
            }
            catch
            {
                // Ignore
            }

            cache.GetHitStats(out cHits, out cMisses);
            Assert.AreEqual(2, cHits);
            Assert.AreEqual(1, cMisses);
        }
    }
}

