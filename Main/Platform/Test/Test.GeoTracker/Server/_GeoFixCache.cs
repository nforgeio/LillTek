//-----------------------------------------------------------------------------
// FILE:        _GeoFixCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _GeoFixCache
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Basic()
        {
            // Verify that we can add an entity fix without a group and
            // on with a group.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {
                DateTime time0 = DateTime.UtcNow + TimeSpan.FromSeconds(0);
                DateTime time1 = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                GeoFix[] fixes;
                GeoFix fix;

                Assert.AreEqual(0, cache.EntityCount);
                Assert.AreEqual(0, cache.GroupCount);

                cache.AddEntityFix("Jeff", null, new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 });

                Assert.AreEqual(1, cache.EntityCount);
                Assert.AreEqual(0, cache.GroupCount);

                fixes = cache.GetEntityFixes("Jeff");
                Assert.IsNotNull(fixes);
                Assert.AreEqual(1, fixes.Length);
                Assert.AreEqual(time0, fixes[0].TimeUtc.Value);

                fixes = cache.GetEntityFixes("JEFF");
                Assert.IsNull(fixes);   // Entity IDs are case sensitive

                cache.AddEntityFix("Jeff", "Lill-Family", new GeoFix() { TimeUtc = time1, Latitude = 20, Longitude = 30 });

                Assert.AreEqual(1, cache.EntityCount);
                Assert.AreEqual(1, cache.GroupCount);

                fixes = cache.GetEntityFixes("Jeff");
                Assert.IsNotNull(fixes);
                Assert.AreEqual(2, fixes.Length);
                Assert.AreEqual(time1, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time0, fixes[1].TimeUtc.Value);

                fix = cache.GetCurrentEntityFix("Jeff");
                Assert.IsNotNull(fix);
                Assert.AreEqual(time1, fix.TimeUtc.Value);

                // Verify that entityIDs are case sensitive.

                Assert.IsNotNull(cache.GetCurrentEntityFix("Jeff"));
                Assert.IsNull(cache.GetCurrentEntityFix("JEFF"));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Add_OldReject()
        {
            // Verify that the cache won't add fixes older than the retention interval.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {

                DateTime time0 = DateTime.UtcNow;
                DateTime time1 = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                DateTime tooTime = DateTime.UtcNow - (settings.GeoFixRetentionInterval + TimeSpan.FromSeconds(1));

                Assert.AreEqual(0, cache.EntityCount);

                cache.AddEntityFix("Jeff", null, new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Bob", null, new GeoFix() { TimeUtc = time1, Latitude = 20, Longitude = 30 });
                cache.AddEntityFix("Judy", null, new GeoFix() { TimeUtc = tooTime, Latitude = 40, Longitude = 50 });

                Assert.AreEqual(2, cache.EntityCount);

                Assert.IsNotNull(cache.GetCurrentEntityFix("Jeff"));
                Assert.IsNotNull(cache.GetCurrentEntityFix("Bob"));
                Assert.IsNull(cache.GetCurrentEntityFix("Judy"));       // Fix was too old
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Group_Basic()
        {
            // Verify that we can query for groups.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {
                Assert.AreEqual(0, cache.EntityCount);
                Assert.AreEqual(0, cache.GroupCount);

                cache.AddEntityFix("Jeff", null, new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Eddie", null, new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Jeff", "Group-1", new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Bob", "Group-1", new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Joe", "Group-1", new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Mary", "Group-2", new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Angie", "Group-2", new GeoFix() { Latitude = 10, Longitude = 20 });
                cache.AddEntityFix("Jeff", "Group-2", new GeoFix() { Latitude = 10, Longitude = 20 });

                Assert.AreEqual(6, cache.EntityCount);
                Assert.AreEqual(2, cache.GroupCount);

                // Verify that NULL and non-existing groups return empty arrays.

                Assert.AreEqual(0, cache.GetGroupEntities(null).Length);
                Assert.AreEqual(0, cache.GetGroupEntities("DOES NOT EXIST").Length);

                // Verify the group membership.  Note that "Jeff" is a member of both groups.

                EntityState[] entities;

                entities = cache.GetGroupEntities("Group-1");
                Assert.AreEqual(3, entities.Length);
                Assert.IsTrue(entities.Any(e => e.EntityID == "Jeff"));
                Assert.IsTrue(entities.Any(e => e.EntityID == "Bob"));
                Assert.IsTrue(entities.Any(e => e.EntityID == "Joe"));

                entities = cache.GetGroupEntities("Group-2");
                Assert.AreEqual(3, entities.Length);
                Assert.IsTrue(entities.Any(e => e.EntityID == "Mary"));
                Assert.IsTrue(entities.Any(e => e.EntityID == "Angie"));
                Assert.IsTrue(entities.Any(e => e.EntityID == "Jeff"));

                // Verify the case sensitivity of group IDs.

                Assert.AreEqual(3, cache.GetGroupEntities("Group-1").Length);
                Assert.AreEqual(0, cache.GetGroupEntities("GROUP-1").Length);
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Purge_EntityFixes()
        {
            var settings = new GeoTrackerServerSettings()
            {
                GeoFixRetentionInterval = TimeSpan.FromSeconds(1),
                GeoFixPurgeInterval = TimeSpan.FromSeconds(0.25),
                BkInterval = TimeSpan.FromSeconds(1)
            };

            var cache = new GeoFixCache(settings);

            try
            {
                DateTime timeOld = DateTime.UtcNow;
                DateTime timeNew = timeOld + TimeSpan.FromSeconds(5);    // Actually, 5 seconds in the future
                GeoFix[] fixes;

                // Verify that old fixes are purged from entities.

                cache.AddEntityFix("test", null, new GeoFix() { TimeUtc = timeOld, Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("test", null, new GeoFix() { TimeUtc = timeNew, Latitude = 10, Longitude = 10 });

                fixes = cache.GetEntityFixes("test");
                Assert.AreEqual(2, fixes.Length);
                Assert.IsTrue(fixes.Any(f => f.TimeUtc == timeOld));
                Assert.IsTrue(fixes.Any(f => f.TimeUtc == timeNew));

                Thread.Sleep(TimeSpan.FromSeconds(3));  // Wait long enough for the purge thread to do its thing.

                fixes = cache.GetEntityFixes("test");
                Assert.AreEqual(1, fixes.Length);
                Assert.IsFalse(fixes.Any(f => f.TimeUtc == timeOld));
                Assert.IsTrue(fixes.Any(f => f.TimeUtc == timeNew));

                // Wait long enough for the second fix to expire and verify that
                // the entire entity is purged.

                Thread.Sleep(TimeSpan.FromSeconds(4));

                Assert.IsNull(cache.GetEntityFixes("test"));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Purge_Groups()
        {
            var settings = new GeoTrackerServerSettings()
            {
                GeoFixRetentionInterval = TimeSpan.FromSeconds(1),
                GeoFixPurgeInterval = TimeSpan.FromSeconds(0.25),
                BkInterval = TimeSpan.FromSeconds(1)
            };

            var cache = new GeoFixCache(settings);

            try
            {
                DateTime timeOld = DateTime.UtcNow;
                DateTime timeNew = timeOld + TimeSpan.FromSeconds(5); // Actually, 5 seconds in the future
                DateTime timeFuture = timeOld + TimeSpan.FromSeconds(10);
                EntityState[] groupEntities;
                EntityState entity;

                // Verify that purged fixes are reflected in a group's entities.

                cache.AddEntityFix("test1", "group", new GeoFix() { TimeUtc = timeOld, Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("test1", "group", new GeoFix() { TimeUtc = timeNew, Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("test2", "group", new GeoFix() { TimeUtc = timeFuture, Latitude = 10, Longitude = 10 });

                groupEntities = cache.GetGroupEntities("group");
                Assert.AreEqual(2, groupEntities.Length);
                Assert.IsTrue(groupEntities.Any(ef => ef.EntityID == "test1"));
                Assert.IsTrue(groupEntities.Any(ef => ef.EntityID == "test2"));

                entity = groupEntities.Where(ef => ef.EntityID == "test1").First();
                Assert.AreEqual(2, entity.GetFixes().Length);
                Assert.IsTrue(entity.GetFixes().Any(ef => ef.TimeUtc == timeOld));
                Assert.IsTrue(entity.GetFixes().Any(ef => ef.TimeUtc == timeNew));

                entity = groupEntities.Where(ef => ef.EntityID == "test2").First();
                Assert.AreEqual(1, entity.GetFixes().Length);
                Assert.IsTrue(entity.GetFixes().Any(ef => ef.TimeUtc == timeFuture));

                Thread.Sleep(TimeSpan.FromSeconds(3));  // Wait long enough for the purge thread to purge
                // the first fix for "test1".

                groupEntities = cache.GetGroupEntities("group");
                Assert.AreEqual(2, groupEntities.Length);
                Assert.IsTrue(groupEntities.Any(ef => ef.EntityID == "test1"));
                Assert.IsTrue(groupEntities.Any(ef => ef.EntityID == "test2"));

                entity = groupEntities.Where(ef => ef.EntityID == "test1").First();
                Assert.AreEqual(1, entity.GetFixes().Length);
                Assert.IsFalse(entity.GetFixes().Any(ef => ef.TimeUtc == timeOld));
                Assert.IsTrue(entity.GetFixes().Any(ef => ef.TimeUtc == timeNew));

                entity = groupEntities.Where(ef => ef.EntityID == "test2").First();
                Assert.AreEqual(1, entity.GetFixes().Length);
                Assert.IsTrue(entity.GetFixes().Any(ef => ef.TimeUtc == timeFuture));

                // Wait long enough for the "test2" fix to expire and verify that
                // group no longer exists.

                Thread.Sleep(TimeSpan.FromSeconds(9));

                groupEntities = cache.GetGroupEntities("group");
                Assert.AreEqual(0, groupEntities.Length);
                Assert.IsFalse(cache.GroupExists("group"));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Group_GetFixes()
        {
            // Verify that we can get the set of entities in a group along with
            // all of their cached location fixes.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 20, Longitude = 10 });
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 30, Longitude = 10 });

                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 40, Longitude = 10 });
                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 50, Longitude = 10 });
                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 60, Longitude = 10 });

                EntityState[] groupEntities = cache.GetGroupEntities("group");
                EntityState entity;

                Assert.AreEqual(2, groupEntities.Length);

                entity = groupEntities.Where(ef => ef.EntityID == "jeff").First();
                Assert.AreEqual(3, entity.GetFixes().Length);
                Assert.AreEqual(30, entity.CurrentFix.Latitude);

                entity = groupEntities.Where(ef => ef.EntityID == "bob").First();
                Assert.AreEqual(3, entity.GetFixes().Length);
                Assert.AreEqual(60, entity.CurrentFix.Latitude);
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Group_GetCurrentFixes()
        {
            // Verify that we can get the set of entities in a group along with
            // all of their cached location fixes.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 20, Longitude = 10 });
                cache.AddEntityFix("jeff", "group", new GeoFix() { Latitude = 30, Longitude = 10 });

                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 40, Longitude = 10 });
                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 50, Longitude = 10 });
                cache.AddEntityFix("bob", "group", new GeoFix() { Latitude = 60, Longitude = 10 });

                EntityFix[] entityFixes = cache.GetGroupCurrentEntityFixes("group");
                EntityFix entity;

                Assert.AreEqual(2, entityFixes.Length);

                entity = entityFixes.Where(ef => ef.EntityID == "jeff").First();
                Assert.AreEqual(30, entity.Fix.Latitude);

                entity = entityFixes.Where(ef => ef.EntityID == "bob").First();
                Assert.AreEqual(60, entity.Fix.Latitude);
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Group_Multiple()
        {
            // Verify that multiple groups and entities belonging to multiple groups works properly.

            var settings = new GeoTrackerServerSettings();
            var cache = new GeoFixCache(settings);

            try
            {
                cache.AddEntityFix("jeff", "group1", new GeoFix() { Latitude = 10, Longitude = 10 });
                cache.AddEntityFix("bob", "group1", new GeoFix() { Latitude = 60, Longitude = 10 });

                cache.AddEntityFix("jeff", "group2", new GeoFix() { Latitude = 20, Longitude = 10 });
                cache.AddEntityFix("andy", "group2", new GeoFix() { Latitude = 50, Longitude = 10 });

                EntityFix[] entityFixes;
                EntityFix entity;

                entityFixes = cache.GetGroupCurrentEntityFixes("group1");
                Assert.AreEqual(2, entityFixes.Length);

                entity = entityFixes.Where(ef => ef.EntityID == "jeff").First();
                Assert.AreEqual(20, entity.Fix.Latitude);

                entity = entityFixes.Where(ef => ef.EntityID == "bob").First();
                Assert.AreEqual(60, entity.Fix.Latitude);

                entityFixes = cache.GetGroupCurrentEntityFixes("group2");
                Assert.AreEqual(2, entityFixes.Length);

                entity = entityFixes.Where(ef => ef.EntityID == "jeff").First();
                Assert.AreEqual(20, entity.Fix.Latitude);

                entity = entityFixes.Where(ef => ef.EntityID == "andy").First();
                Assert.AreEqual(50, entity.Fix.Latitude);
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoFixCache_Load()
        {
            // Run the cache under significant load and with continuous purging for a couple minutes
            // to try to cause thred synchronization issues.

            var settings = new GeoTrackerServerSettings()
            {

                GeoFixPurgeInterval = TimeSpan.Zero,
                GeoFixRetentionInterval = TimeSpan.FromSeconds(1),
                BkInterval = TimeSpan.Zero
            };

            var cache = new GeoFixCache(settings);

            try
            {
                const int EntityCount = 100000;
                const int GroupCount = 20;

                bool stopThreads = false;
                bool threadError = false;
                Thread add1Thread;
                Thread add2Thread;
                Thread query1Thread;
                Thread query2Thread;

                add1Thread = new Thread(new ThreadStart(
                    () =>
                    {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);

                        while (!stopThreads)
                        {
                            try
                            {
                                string entityID = string.Format("entity-{0}", rand.Next(EntityCount));
                                string groupID = string.Format("group-{0}", rand.Next(GroupCount));

                                cache.AddEntityFix(entityID, groupID, new GeoFix() { Latitude = 10, Longitude = 20 });
                            }
                            catch (Exception e)
                            {
                                threadError = true;
                                SysLog.LogException(e);
                            }
                        }
                    }));

                add2Thread = new Thread(new ThreadStart(
                    () =>
                    {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);

                        while (!stopThreads)
                        {
                            try
                            {
                                string entityID = string.Format("entity-{0}", rand.Next(EntityCount));
                                string groupID = string.Format("group-{0}", rand.Next(GroupCount));

                                cache.AddEntityFix(entityID, groupID, new GeoFix() { Latitude = 10, Longitude = 20 });
                            }
                            catch (Exception e)
                            {
                                threadError = true;
                                SysLog.LogException(e);
                            }
                        }
                    }));

                query1Thread = new Thread(new ThreadStart(
                    () =>
                    {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);

                        while (!stopThreads)
                        {
                            try
                            {
                                string entityID = string.Format("entity-{0}", rand.Next(EntityCount));
                                string groupID = string.Format("group-{0}", rand.Next(GroupCount));
                                int cEntities = cache.EntityCount;
                                int cGroups = cache.GroupCount;

                                cache.GetEntityFixes(entityID);
                                cache.GetCurrentEntityFix(entityID);

                                var groupFixes = cache.GetGroupEntities(groupID);

                                foreach (var entity in groupFixes)
                                    entity.GetFixes();

                                var entityFixes = cache.GetGroupCurrentEntityFixes(groupID);
                                int c = 0;

                                foreach (var fix in entityFixes)
                                    c++;
                            }
                            catch (Exception e)
                            {
                                threadError = true;
                                SysLog.LogException(e);
                            }
                        }
                    }));

                query2Thread = new Thread(new ThreadStart(
                    () =>
                    {
                        Random rand = new Random(Thread.CurrentThread.ManagedThreadId);

                        while (!stopThreads)
                        {
                            try
                            {
                                string entityID = string.Format("entity-{0}", rand.Next(EntityCount));
                                string groupID = string.Format("group-{0}", rand.Next(GroupCount));
                                int cEntities = cache.EntityCount;
                                int cGroups = cache.GroupCount;

                                cache.GetEntityFixes(entityID);
                                cache.GetCurrentEntityFix(entityID);

                                var groupFixes = cache.GetGroupEntities(groupID);

                                foreach (var entity in groupFixes)
                                    entity.GetFixes();

                                var entityFixes = cache.GetGroupCurrentEntityFixes(groupID);
                                int c = 0;

                                foreach (var fix in entityFixes)
                                    c++;
                            }
                            catch (Exception e)
                            {
                                threadError = true;
                                SysLog.LogException(e);
                            }
                        }
                    }));

                add1Thread.Start();
                add2Thread.Start();
                query1Thread.Start();
                query2Thread.Start();

                // Let the threads pound on the cache for a while.

                Thread.Sleep(TimeSpan.FromMinutes(5));

                stopThreads = true;
                add1Thread.Join();
                add2Thread.Join();
                query1Thread.Join();
                query2Thread.Join();

                Assert.IsFalse(threadError);
            }
            finally
            {
                cache.Stop();
            }
        }
    }
}


