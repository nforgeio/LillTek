//-----------------------------------------------------------------------------
// FILE:        _OsVersion.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _OsVersion
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void OsVersion_Basic()
        {
            // I'm not going to do much of a test here at this point.

            var ver = new OsVersion();

            Assert.AreEqual(Environment.OSVersion.Version.Major, ver.OSVersion.Major);
            Assert.AreEqual(Environment.OSVersion.Version.Minor, ver.OSVersion.Minor);
        }
    }
}

