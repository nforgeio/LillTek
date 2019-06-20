//-----------------------------------------------------------------------------
// FILE:        _TimeOfDay.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _TimeOfDay
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimeOfDay_Constructors()
        {
            TimeOfDay tod;

            tod = new TimeOfDay(new DateTime(2010, 10, 24, 12, 15, 30));
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(30, tod.Second);

            tod = new TimeOfDay("12:15");
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(00, tod.Second);

            tod = new TimeOfDay("12:15:30");
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(30, tod.Second);

            tod = new TimeOfDay(TimeSpan.Parse("12:15:30"));
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(30, tod.Second);

            tod = new TimeOfDay(12, 15);
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(00, tod.Second);

            tod = new TimeOfDay(12, 15, 30);
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(30, tod.Second);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimeOfDay_Errors()
        {
            try
            {
                new TimeOfDay(-1, 0);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay(24, 0);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay(23, 60);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay(23, 59, 60);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay(null);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay(string.Empty);
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay("11");
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {

                // Expected
            }

            try
            {
                new TimeOfDay("11:");
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay("11:xx");
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                new TimeOfDay("11:12:13:");
                Assert.Fail("ArgumentException expected");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimeOfDay_Parse()
        {
            TimeOfDay tod;

            Assert.IsTrue(TimeOfDay.TryParse("12:30", out tod));
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(30, tod.Minute);
            Assert.AreEqual(00, tod.Second);

            Assert.IsTrue(TimeOfDay.TryParse("12:15:30", out tod));
            Assert.AreEqual(12, tod.Hour);
            Assert.AreEqual(15, tod.Minute);
            Assert.AreEqual(30, tod.Second);

            // Parse failures

            Assert.IsFalse(TimeOfDay.TryParse(null, out tod));
            Assert.IsFalse(TimeOfDay.TryParse(string.Empty, out tod));
            Assert.IsFalse(TimeOfDay.TryParse("11", out tod));
            Assert.IsFalse(TimeOfDay.TryParse("11:", out tod));
            Assert.IsFalse(TimeOfDay.TryParse("11:xx", out tod));
            Assert.IsFalse(TimeOfDay.TryParse("11:12:13:", out tod));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TimeOfDay_ToStringOverride()
        {
            Assert.AreEqual("12:15:30", new TimeOfDay(12, 15, 30).ToString());
            Assert.AreEqual("01:02:03", new TimeOfDay(1, 2, 3).ToString());
        }
    }
}

