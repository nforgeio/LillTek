//-----------------------------------------------------------------------------
// FILE:        _Test.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Data;
using LillTek.Data.Install;
using LillTek.Testing;

namespace LillTek.Data.Install.Test
{
    [TestClass]
    public class _InstallTest
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data.Install")]
        public void InstallTest_InstallParams()
        {
            DBInstallParams     dbParams;
            string              sysDrive;
            int                 pos;

            sysDrive = Environment.SystemDirectory;
            pos      = sysDrive.IndexOf(':');
            sysDrive = sysDrive.Substring(0, pos + 1);

            dbParams = new DBInstallParams("App", "Foo");
            Assert.AreEqual("Foo", dbParams.Database);
            Assert.AreEqual(sysDrive + @"\LillTek\Data\Foo.mdf", dbParams.DBPath);
            Assert.AreEqual(sysDrive + @"\LillTek\Data\Foo.ldf", dbParams.LogPath);
        }
    }
}

