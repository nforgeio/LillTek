//-----------------------------------------------------------------------------
// FILE:        _RegKey.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the RegKey class

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;
using LillTek.Windows;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _RegKey
    {
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            }
            catch
            {
            }
        }

        [ClassCleanup]
        public static void Cleanup()
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
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_Basic()
        {
            RegKey key;

            Assert.IsFalse(RegKey.Exists(@"HKEY_LOCAL_MACHINE\Software\RegTest"));
            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            Assert.IsTrue(RegKey.Exists(@"HKEY_LOCAL_MACHINE\Software\RegTest"));

            key.Set("Test1", "value1");
            key.Set("Test2", "value2");
            key.Set("Test3", 3);

            Assert.AreEqual("value1", key.Get("Test1"));
            Assert.AreEqual("value2", key.Get("Test2"));
            Assert.AreEqual("3", key.Get("Test3"));

            key.Set("Test1", "hello");
            Assert.AreEqual("hello", key.Get("Test1"));

            Assert.AreEqual("default", key.Get("foobar", "default"));

            key.Close();

            Assert.AreEqual("hello", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1"));
            Assert.AreEqual("value2", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test2"));
            Assert.AreEqual("3", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test3"));

            RegKey.SetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test4", "Hello");
            Assert.AreEqual("Hello", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test4"));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_Open()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            key.Set("Test1", "value1");
            key.Close();

            key = RegKey.Open(@"HKEY_LOCAL_MACHINE\Software\RegTest");
            Assert.AreEqual("value1", key.Get("Test1"));
            key.Close();

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            Assert.IsNull(RegKey.Open(@"HKEY_LOCAL_MACHINE\Software\RegTest"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_SetGet_Bool()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            key.Set("Bool1", true);
            key.Set("Bool2", false);
            key.Set("Bool3", "yes");
            key.Set("Bool4", "no");
            key.Set("Bool5", "on");
            key.Set("Bool6", "off");
            key.Set("Bool7", 1);
            key.Set("Bool8", 0);
            key.Set("Bool9", "what the hell?");

            Assert.IsTrue(key.Get("Bool1", false));
            Assert.IsFalse(key.Get("Bool2", true));
            Assert.IsTrue(key.Get("Bool3", false));
            Assert.IsFalse(key.Get("Bool4", true));
            Assert.IsTrue(key.Get("Bool5", false));
            Assert.IsFalse(key.Get("Bool6", true));
            Assert.IsTrue(key.Get("Bool7", false));
            Assert.IsFalse(key.Get("Bool8", true));
            Assert.IsTrue(key.Get("Bool9", true));
            Assert.IsFalse(key.Get("Bool9", false));
            Assert.IsTrue(key.Get("Bool10", true));
            Assert.IsFalse(key.Get("Bool10", false));

            key.Close();

            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool1", false));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool2", true));
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool3", false));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool4", true));
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool5", false));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool6", true));
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool7", false));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool8", true));
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool9", true));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool9", false));
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool10", true));
            Assert.IsFalse(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool10", false));

            RegKey.SetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool11", true);
            Assert.IsTrue(RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Bool11", false));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_SetGet_Int()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            key.Set("Int1", 0);
            key.Set("Int2", 100);
            key.Set("Int3", "-100");
            key.Set("Int4", "what???");

            Assert.AreEqual(0, key.Get("Int1", 55));
            Assert.AreEqual(100, key.Get("Int2", 55));
            Assert.AreEqual(-100, key.Get("Int3", 55));
            Assert.AreEqual(55, key.Get("Int4", 55));
            Assert.AreEqual(55, key.Get("Int5", 55));

            key.Close();

            Assert.AreEqual(0, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int1", 55));
            Assert.AreEqual(100, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int2", 55));
            Assert.AreEqual(-100, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int3", 55));
            Assert.AreEqual(55, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int4", 55));
            Assert.AreEqual(55, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int5", 55));

            RegKey.SetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int6", 1001);
            Assert.AreEqual(1001, RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Int6", 0));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_SetGet_TimeSpan()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            key.Set("TS1", "10");
            key.Set("TS2", "10ms");
            key.Set("TS3", "hello?");

            Assert.AreEqual(TimeSpan.FromSeconds(10), key.Get("TS1", TimeSpan.FromSeconds(55)));
            Assert.AreEqual(TimeSpan.FromMilliseconds(10), key.Get("TS2", TimeSpan.FromSeconds(55)));
            Assert.AreEqual(TimeSpan.FromSeconds(55), key.Get("TS3", TimeSpan.FromSeconds(55)));

            key.Close();

            Assert.AreEqual(TimeSpan.FromSeconds(10), RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS1", TimeSpan.FromSeconds(55)));
            Assert.AreEqual(TimeSpan.FromMilliseconds(10), RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS2", TimeSpan.FromSeconds(55)));
            Assert.AreEqual(TimeSpan.FromSeconds(55), RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS3", TimeSpan.FromSeconds(55)));
            Assert.AreEqual(TimeSpan.FromSeconds(55), RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS4", TimeSpan.FromSeconds(55)));

            RegKey.SetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS5", TimeSpan.FromSeconds(1001));
            Assert.AreEqual(TimeSpan.FromSeconds(1001), RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:TS5", TimeSpan.FromSeconds(1)));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_DeleteValue()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            key.Set("Test1", "10");
            key.Set("Test2", "20");

            Assert.AreEqual("20", key.Get("Test2"));
            key.DeleteValue("Test2");
            Assert.IsNull(key.Get("Test2"));

            key.Close();

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1");
            Assert.IsNull(key.Get("Test1"));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            RegKey.SetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Foo", "test");
            Assert.AreEqual("test", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Foo"));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_REG_DWORD()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            key.SetDWORD("Test1", 10);
            Assert.AreEqual("10", key.Get("Test1"));

            key.Close();

            RegKey.SetDWORDValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test2", 20);
            Assert.AreEqual("20", RegKey.GetValue(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test2"));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RegKey_GetTypeOf()
        {
            RegKey key;

            key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\RegTest");

            Assert.AreEqual(WinApi.REG_NONE, key.GetTypeOf("foo"));

            key.SetDWORD("Test1", 10);
            Assert.AreEqual(WinApi.REG_DWORD, key.GetTypeOf("Test1"));

            key.Set("Test2", "foobar");
            Assert.AreEqual(WinApi.REG_SZ, key.GetTypeOf("Test2"));

            key.Close();

            Assert.AreEqual(WinApi.REG_DWORD, RegKey.GetValueType(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test1"));
            Assert.AreEqual(WinApi.REG_SZ, RegKey.GetValueType(@"HKEY_LOCAL_MACHINE\Software\RegTest:Test2"));

            RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\RegTest");
        }
    }
}

