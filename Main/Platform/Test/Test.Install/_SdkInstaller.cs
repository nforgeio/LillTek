//-----------------------------------------------------------------------------
// FILE:        _SdkInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 
 
using System;
using System.IO;
using System.Threading;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;
using LillTek.Windows;

namespace LillTek.Install.Test 
{
    [TestClass]
    public class _SdkInstaller
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void SdkInstaller_Test() 
        {
            Assert.Inconclusive("Manual verification required.");

            SdkInstaller    installer = new SdkInstaller();

            installer.Install(null);
            installer.Uninstall(null);
        }
    }
}

