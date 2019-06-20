//-----------------------------------------------------------------------------
// FILE:        _SysTime.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

// $todo(jeff.lill): 
//
// Implement some tests to verify that wrap-around handling is
// working correctly.

namespace LillTek.Common.Test
{
    [TestClass]
    public class _SysTime
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SysTime_InitialValue()
        {
            SysTime.Reset();
            Assert.IsTrue(SysTime.Now >= DateTime.MinValue + TimeSpan.FromDays(365 / 2));
            Assert.IsTrue(SysTime.Now <= DateTime.MinValue + TimeSpan.FromDays(365 * 1.5));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SysTime_Delta()
        {
            DateTime start;
            DateTime end;
            TimeSpan delta;

            start = SysTime.Now;

            Thread.Sleep(1000);

            end = SysTime.Now;
            delta = end - start;
            Assert.IsTrue(delta >= TimeSpan.FromSeconds(1) - SysTime.Resolution);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SysTime_Infinite()
        {
            Assert.IsTrue(SysTime.Now + SysTime.Infinite >= DateTime.MaxValue - TimeSpan.FromDays(365));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void SysTime_Resolution()
        {
            Assert.IsTrue(SysTime.Resolution > TimeSpan.Zero);
            Assert.IsTrue(SysTime.Resolution <= TimeSpan.FromMilliseconds(100));
        }
    }
}

