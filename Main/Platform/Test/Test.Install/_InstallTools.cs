//-----------------------------------------------------------------------------
// FILE:        _InstallTools.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 
 
using System;
using System.Threading;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;
using LillTek.Windows;

namespace LillTek.Install.Test 
{
    [TestClass]
    public class _InstallTools 
    {
        [TestInitialize]
        public void Initialize() 
        {
            try 
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            }
            catch 
            {
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            }
            catch
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void InstallTools_RegInstaller() 
        {
            // $todo(jeff.lill): I need to come up with a better test harness for this
#if FALSE
            Hashtable       state = new Hashtable();
            RegInstaller    installer;
            
            installer = new RegInstaller(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1",10);
            installer.Install(state);
            Assert.AreEqual(10,RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1",0));
            installer.Commit(state);
            Assert.AreEqual(10,RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1",0));
            installer.Uninstall(state);
            Assert.AreEqual(WinApi.REG_NONE,RegKey.GetValueType(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1"));

            installer = new RegInstaller(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1",10);
            installer.Install(state);
            Assert.AreEqual(10,RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1",0));
            installer.Rollback(state);
            Assert.AreEqual(WinApi.REG_NONE,RegKey.GetValueType(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1"));
#else
            Assert.Inconclusive("Test Disabled");
#endif
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void InstallTools_RegUnregDLL() 
        {
#if FALSE
            // Hardcoded tests for dynamically registering and unregistering
            // a DLL.

            InstallTools.RegisterDLL(@"C:\Program Files\NCT\AudioStudio2\Redist\NCTAudioFile2.dll");
            InstallTools.UnregisterDLL(@"C:\Program Files\NCT\AudioStudio2\Redist\NCTAudioFile2.dll");
            InstallTools.RegisterDLL(@"C:\Program Files\NCT\AudioStudio2\Redist\NCTAudioFile2.dll");
#else
            Assert.Inconclusive("Test Disabled");
#endif
        }
    }
}

