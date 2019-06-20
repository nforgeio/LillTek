//-----------------------------------------------------------------------------
// FILE:        _EntityState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _EntityState
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Basic()
        {
            var entity = new EntityState("1234");

            Assert.AreEqual("1234", entity.EntityID);
            Assert.AreEqual(0, entity.GetFixes().Length);
            Assert.IsNull(entity.CurrentFix);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Add_One()
        {
            var entity = new EntityState("1234");
            var time1 = new DateTime(2011, 4, 21, 18, 10, 0);
            GeoFix fix;

            entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, null);

            fix = entity.GetFixes()[0];
            Assert.AreEqual(time1, fix.TimeUtc.Value);
            Assert.AreEqual(10, fix.Latitude);
            Assert.AreEqual(20, fix.Longitude);

            fix = entity.CurrentFix;
            Assert.AreEqual(time1, fix.TimeUtc.Value);
            Assert.AreEqual(10, fix.Latitude);
            Assert.AreEqual(20, fix.Longitude);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Add_NoTimestamp()
        {
            var entity = new EntityState("1234");
            GeoFix fix;

            entity.AddFix(new GeoFix() { Latitude = 10, Longitude = 20 }, null);

            fix = entity.GetFixes()[0];
            Assert.IsNotNull(fix.TimeUtc);
            Assert.AreEqual(10, fix.Latitude);
            Assert.AreEqual(20, fix.Longitude);

            fix = entity.CurrentFix;
            Assert.IsNotNull(fix.TimeUtc);
            Assert.AreEqual(10, fix.Latitude);
            Assert.AreEqual(20, fix.Longitude);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Add_PastLimit()
        {
            try
            {
                EntityState.MaxEntityFixes = 3;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);
                var time2 = new DateTime(2011, 4, 21, 18, 10, 2);
                var time3 = new DateTime(2011, 4, 21, 18, 10, 3);
                GeoFix[] fixes;

                fixes = entity.GetFixes();
                Assert.AreEqual(0, fixes.Length);

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, null);
                fixes = entity.GetFixes();
                Assert.AreEqual(1, fixes.Length);
                Assert.AreEqual(time0, fixes[0].TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, null);
                fixes = entity.GetFixes();
                Assert.AreEqual(2, fixes.Length);
                Assert.AreEqual(time1, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time0, fixes[1].TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time2, Latitude = 10, Longitude = 20 }, null);
                fixes = entity.GetFixes();
                Assert.AreEqual(3, fixes.Length);
                Assert.AreEqual(time2, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time0, fixes[2].TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time3, Latitude = 10, Longitude = 20 }, null);
                fixes = entity.GetFixes();
                Assert.AreEqual(3, fixes.Length);
                Assert.AreEqual(time3, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time2, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[2].TimeUtc.Value);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Purge()
        {
            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);
                var time2 = new DateTime(2011, 4, 21, 18, 10, 2);
                var time3 = new DateTime(2011, 4, 21, 18, 10, 3);
                GeoFix[] fixes;

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time2, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time3, Latitude = 10, Longitude = 20 }, null);

                fixes = entity.GetFixes();
                Assert.AreEqual(4, fixes.Length);
                Assert.AreEqual(time3, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time2, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[2].TimeUtc.Value);
                Assert.AreEqual(time0, fixes[3].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 1));

                fixes = entity.GetFixes();
                Assert.AreEqual(3, fixes.Length);
                Assert.AreEqual(time3, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time2, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[2].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 3));

                fixes = entity.GetFixes();
                Assert.AreEqual(1, fixes.Length);
                Assert.AreEqual(time3, fixes[0].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 4));

                fixes = entity.GetFixes();
                Assert.AreEqual(0, fixes.Length);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Purge_OutOfOrder()
        {
            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);
                var time2 = new DateTime(2011, 4, 21, 18, 10, 2);
                var time3 = new DateTime(2011, 4, 21, 18, 10, 3);
                GeoFix[] fixes;

                entity.AddFix(new GeoFix() { TimeUtc = time3, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, null);
                entity.AddFix(new GeoFix() { TimeUtc = time2, Latitude = 10, Longitude = 20 }, null);

                fixes = entity.GetFixes();
                Assert.AreEqual(4, fixes.Length);
                Assert.AreEqual(time2, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time0, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[2].TimeUtc.Value);
                Assert.AreEqual(time3, fixes[3].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 1));

                fixes = entity.GetFixes();
                Assert.AreEqual(3, fixes.Length);
                Assert.AreEqual(time2, fixes[0].TimeUtc.Value);
                Assert.AreEqual(time1, fixes[1].TimeUtc.Value);
                Assert.AreEqual(time3, fixes[2].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 3));

                fixes = entity.GetFixes();
                Assert.AreEqual(1, fixes.Length);
                Assert.AreEqual(time3, fixes[0].TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 4));

                fixes = entity.GetFixes();
                Assert.AreEqual(0, fixes.Length);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_CurrentFix()
        {
            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);
                var time2 = new DateTime(2011, 4, 21, 18, 10, 2);
                var time3 = new DateTime(2011, 4, 21, 18, 10, 3);

                Assert.IsNull(entity.CurrentFix);

                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, null);
                Assert.AreEqual(time1, entity.CurrentFix.TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time3, Latitude = 10, Longitude = 20 }, null);
                Assert.AreEqual(time3, entity.CurrentFix.TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, null);
                Assert.AreEqual(time3, entity.CurrentFix.TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = time2, Latitude = 10, Longitude = 20 }, null);
                Assert.AreEqual(time3, entity.CurrentFix.TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 1));
                Assert.AreEqual(time3, entity.CurrentFix.TimeUtc.Value);

                entity.Purge(new DateTime(2011, 4, 21, 18, 10, 4));
                Assert.IsNull(entity.CurrentFix);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_FutureFixes()
        {
            // Verify that figures with future dates are recorded with the current time instead.

            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var timeNow = DateTime.UtcNow;
                var timeFuture = timeNow + TimeSpan.FromDays(100);

                Assert.IsNull(entity.CurrentFix);

                entity.AddFix(new GeoFix() { TimeUtc = timeNow, Latitude = 10, Longitude = 20 }, null);
                Assert.AreEqual(timeNow, entity.CurrentFix.TimeUtc.Value);

                entity.AddFix(new GeoFix() { TimeUtc = timeFuture, Latitude = 10, Longitude = 20 }, null);
                Assert.IsTrue(Helper.Within(timeNow, entity.CurrentFix.TimeUtc.Value, TimeSpan.FromSeconds(1)));
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_NoGroups()
        {
            // Verify that entities that don't belong to a group work properly.

            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = DateTime.UtcNow;

                Assert.IsNull(entity.CurrentFix);

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, null);
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(0, entity.GetGroups().Length);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Group_Add()
        {
            // Verify that entities group membership is recorded.

            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);

                Assert.IsNull(entity.CurrentFix);

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, "Hello");
                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(1, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[0]);

                // Note that this next group should be recorded even though the time
                // is the same as the last fix.

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, "World");
                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(2, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[1]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[0]);

                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, "Now");
                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(3, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[2]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[1]);
                Assert.AreEqual("Now", entity.GetGroups()[0]);
            }
            finally
            {
                EntityState.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void EntityState_Group_Purge()
        {
            // Verify that entities group membership is purged when fixes are purged.

            try
            {
                EntityState.MaxEntityFixes = 4;

                var entity = new EntityState("1234");
                var time0 = new DateTime(2011, 4, 21, 18, 10, 0);
                var time1 = new DateTime(2011, 4, 21, 18, 10, 1);
                var time2 = new DateTime(2011, 4, 21, 18, 10, 2);

                Assert.IsNull(entity.CurrentFix);

                entity.AddFix(new GeoFix() { TimeUtc = time0, Latitude = 10, Longitude = 20 }, "Hello");

                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(1, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[0]);

                entity.AddFix(new GeoFix() { TimeUtc = time1, Latitude = 10, Longitude = 20 }, "World");

                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(2, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[1]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[0]);

                entity.AddFix(new GeoFix() { TimeUtc = time2, Latitude = 10, Longitude = 20 }, "Now");

                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(3, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[2]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[1]);
                Assert.AreEqual("Now", entity.GetGroups()[0]);

                // This shouldn't purge any groups because the time is earlier than any of the fixes.

                entity.Purge(time0 - TimeSpan.FromMilliseconds(1));

                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(3, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[2]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[1]);
                Assert.AreEqual("Now", entity.GetGroups()[0]);

                // This also shouldn't purge any groups because the time is equal to the earliest fix.

                entity.Purge(time0);

                Assert.IsTrue(entity.IsMemberOf("Hello"));
                Assert.IsTrue(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(3, entity.GetGroups().Length);
                Assert.AreEqual("Hello", entity.GetGroups()[2]);     // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[1]);
                Assert.AreEqual("Now", entity.GetGroups()[0]);

                // This should purge the "Hello" group membership.

                entity.Purge(time0 + TimeSpan.FromMilliseconds(1));

                Assert.IsFalse(entity.IsMemberOf("Hello"));
                Assert.IsFalse(entity.IsMemberOf("HELLO"));
                Assert.IsTrue(entity.IsMemberOf("World"));
                Assert.IsTrue(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(2, entity.GetGroups().Length);
                Assert.AreEqual("Now", entity.GetGroups()[0]);       // Verify that new groups are at front
                Assert.AreEqual("World", entity.GetGroups()[1]);

                // This should purge the "World" group membership.

                entity.Purge(time1 + TimeSpan.FromMilliseconds(1));

                Assert.IsFalse(entity.IsMemberOf("Hello"));
                Assert.IsFalse(entity.IsMemberOf("HELLO"));
                Assert.IsFalse(entity.IsMemberOf("World"));
                Assert.IsFalse(entity.IsMemberOf("WORLD"));
                Assert.IsTrue(entity.IsMemberOf("Now"));
                Assert.IsTrue(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(1, entity.GetGroups().Length);
                Assert.AreEqual("Now", entity.GetGroups()[0]);

                // This should purge the "Now" group membership.

                entity.Purge(time2 + TimeSpan.FromMilliseconds(1));

                Assert.IsFalse(entity.IsMemberOf("Hello"));
                Assert.IsFalse(entity.IsMemberOf("HELLO"));
                Assert.IsFalse(entity.IsMemberOf("World"));
                Assert.IsFalse(entity.IsMemberOf("WORLD"));
                Assert.IsFalse(entity.IsMemberOf("Now"));
                Assert.IsFalse(entity.IsMemberOf("NOW"));
                Assert.IsFalse(entity.IsMemberOf("foo"));
                Assert.AreEqual(0, entity.GetGroups().Length);
            }
            finally
            {
                EntityState.Reset();
            }
        }
    }
}

