//-----------------------------------------------------------------------------
// FILE:        _AppPackageInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AppPackageInfo
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageInfo_Serialize()
        {
            AppRef appRef = new AppRef("appref://myapps/app.zip?version=1.2.3.4");
            AppPackageInfo infoOut, infoIn;
            string s;

            infoIn = new AppPackageInfo(appRef, appRef.FileName, "test", new byte[] { 0, 1, 2, 3, 4 }, 77, DateTime.UtcNow);
            s = infoIn.ToString();
            infoOut = AppPackageInfo.Parse(s);

            Assert.AreEqual(infoIn.AppRef, infoOut.AppRef);
            CollectionAssert.AreEqual(infoIn.MD5, infoOut.MD5);
            Assert.AreEqual(infoIn.FileName, infoOut.FileName);
            Assert.AreEqual(infoIn.Size, infoOut.Size);

            Assert.IsNull(infoOut.FullPath);
            Assert.AreEqual(DateTime.MinValue, infoOut.WriteTimeUtc);
        }
    }
}

