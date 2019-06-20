//-----------------------------------------------------------------------------
// FILE:        _AppRef.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AppRef
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppRef_Basic()
        {
            AppRef appRef;

            appRef = AppRef.Parse("appref://jeff/code/mycode.zip?version=1.2.3.4");
            Assert.AreEqual("appref://jeff/code/mycode.zip?version=1.2.3.4", appRef.ToString());
            Assert.AreEqual(new Uri("appref://jeff/code/mycode.zip?version=1.2.3.4"), appRef.Uri);
            Assert.AreEqual("jeff.code.mycode-0001.0002.0003.0004.zip", appRef.FileName);
            Assert.AreEqual(new Version(1, 2, 3, 4), appRef.Version);
            Assert.AreEqual("appref://jeff/code/mycode.zip?version=1.2.3.4", appRef.ToString());

            appRef = AppRef.Parse("APPREF://JEFF/CODE/MYCODE.ZIP?VERSION=1.2&extra=param");
            Assert.AreEqual("appref://jeff/code/mycode.zip?version=1.2", appRef.ToString());
            Assert.AreEqual(new Uri("appref://jeff/code/mycode.zip?version=1.2"), appRef.Uri);
            Assert.AreEqual("jeff.code.mycode-0001.0002.-1.-1.zip", appRef.FileName);
            Assert.AreEqual(new Version(1, 2), appRef.Version);
            Assert.AreEqual("appref://jeff/code/mycode.zip?version=1.2", appRef.ToString());
        }
    }
}

