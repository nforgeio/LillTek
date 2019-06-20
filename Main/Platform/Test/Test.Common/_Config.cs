//-----------------------------------------------------------------------------
// FILE:        _Config.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.set
// DESCRIPTION: UNIT tests for the Config class.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Config
    {
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            Helper.InitializeApp(Assembly.GetExecutingAssembly());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Basic()
        {
            try
            {
                var config = new Config(null, true);

                Assert.IsNull(config.Get("foo"));
                Assert.IsNull(config.Get("foo", (string)null));
                Assert.AreEqual("bar", config.Get("foo", "bar"));

                config.Add("foo", "bar");
                Assert.AreEqual("bar", config.Get("foo"));
                Assert.AreEqual("bar", config.Get("FOO"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_SetConfig()
        {
            try
            {
                Config config;

                Config.SetConfig("test.foo=bar");
                config = new Config("test");
                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_AppendConfig()
        {
            try
            {
                Config config;

                Config.SetConfig("test.foo=bar");
                Config.AppendConfig("test.hello=world!");
                config = new Config("test");
                Assert.AreEqual("bar", config.Get("foo"));
                Assert.AreEqual("world!", config.Get("hello"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Prefix()
        {
            try
            {
                var config = new Config("Test", true);

                config.Add("foo", "bar");
                Assert.IsNull(config.Get("Test.foo"));
                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_MultiLine()
        {
            try
            {
                string cfg;

                cfg = @"
v1 = value1
v2 = {{
        line1
        line2
        }}
v3 = value3
v4 = {{line1
        line2
        }}
v5 = {{
        line1
";
                Config.SetConfig(cfg);

                var config = new Config(null);
                Assert.AreEqual("value1", config.Get("v1"));
                Assert.AreEqual("line1\r\nline2", config.Get("v2"));
                Assert.AreEqual("value3", config.Get("v3"));
                Assert.AreEqual("line1\r\nline2", config.Get("v4"));
                Assert.AreEqual("line1", config.Get("v5"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Overwrite()
        {
            try
            {
                string cfg;

                cfg = @"
v1 = value1
v1 = value2
";
                Config.SetConfig(cfg);

                var config = new Config(null);
                Assert.AreEqual("value2", config.Get("v1"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetArray_Explicit()
        {
            try
            {
                var config = new Config(null, true);
                string[] arr;

                config.Add("a[0]", "0");
                config.Add("a[1]", "1");
                config.Add("a[2]", "2");
                config.Add("a[3]", "3");
                config.Add("a[5]", "5");

                config.Add("b[0]", "100");
                config.Add("b[1]", "200");

                arr = config.GetArray("a");
                Assert.AreEqual(4, arr.Length);
                Assert.AreEqual("0", arr[0]);
                Assert.AreEqual("1", arr[1]);
                Assert.AreEqual("2", arr[2]);
                Assert.AreEqual("3", arr[3]);

                arr = config.GetArray("b");
                Assert.AreEqual(2, arr.Length);
                Assert.AreEqual("100", arr[0]);
                Assert.AreEqual("200", arr[1]);

                arr = config.GetArray("not-found", new string[] { "a", "b" });
                CollectionAssert.AreEqual(new string[] { "a", "b" }, arr);

                arr = config.GetArray("test");
                Assert.AreEqual(0, arr.Length);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetArray_AutoIncrement()
        {
            const string cfg = @"

    a[-] = 0
    a[-] = 1
    a[-] = 2
    a[-] = 3
    a[-] = 4
    a[-] = 5

    b[-] = 100
    b[-] = 200
";
            Config.SetConfig(cfg);

            try
            {
                Config.SetConfig(cfg);

                var config = new Config();
                string[] arr;

                arr = config.GetArray("a");
                Assert.AreEqual(6, arr.Length);
                Assert.AreEqual("0", arr[0]);
                Assert.AreEqual("1", arr[1]);
                Assert.AreEqual("2", arr[2]);
                Assert.AreEqual("3", arr[3]);
                Assert.AreEqual("4", arr[4]);
                Assert.AreEqual("5", arr[5]);

                arr = config.GetArray("b");
                Assert.AreEqual(2, arr.Length);
                Assert.AreEqual("100", arr[0]);
                Assert.AreEqual("200", arr[1]);

                arr = config.GetArray("not-found", new string[] { "a", "b" });
                CollectionAssert.AreEqual(new string[] { "a", "b" }, arr);

                arr = config.GetArray("test");
                Assert.AreEqual(0, arr.Length);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetArray_Merged()
        {
            // Verify that explicit key array indexes "win" over the auto form regardless
            // of the position of the two settings in the configuration file.

            const string cfg = @"

    a[3] = xxx

    a[-] = 0
    a[-] = 1
    a[-] = 2
    a[-] = 3
    a[-] = 4
    a[-] = 5

    b[-] = 100
    b[-] = 200
";
            try
            {
                Config.SetConfig(cfg);

                var config = new Config();
                string[] arr;

                arr = config.GetArray("a");
                Assert.AreEqual(6, arr.Length);
                Assert.AreEqual("0", arr[0]);
                Assert.AreEqual("1", arr[1]);
                Assert.AreEqual("2", arr[2]);
                Assert.AreEqual("xxx", arr[3]);
                Assert.AreEqual("4", arr[4]);
                Assert.AreEqual("5", arr[5]);

                arr = config.GetArray("b");
                Assert.AreEqual(2, arr.Length);
                Assert.AreEqual("100", arr[0]);
                Assert.AreEqual("200", arr[1]);

                arr = config.GetArray("not-found", new string[] { "a", "b" });
                CollectionAssert.AreEqual(new string[] { "a", "b" }, arr);

                arr = config.GetArray("test");
                Assert.AreEqual(0, arr.Length);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetDictionary()
        {
            try
            {
                var config = new Config(null, true);
                Dictionary<string, string> ht;

                config.Add("a[foo]", "0");
                config.Add("a[bar]", "1");
                config.Add("a[foobar]", "2");

                config.Add("b[foo]", "100");
                config.Add("b[bar]", "200");
                config.Add("b[foobar]", "300");

                ht = config.GetDictionary("a");
                Assert.AreEqual("0", ht["foo"]);
                Assert.AreEqual("1", ht["bar"]);
                Assert.AreEqual("2", ht["foobar"]);

                ht = config.GetDictionary("b");
                Assert.AreEqual("100", ht["foo"]);
                Assert.AreEqual("200", ht["bar"]);
                Assert.AreEqual("300", ht["foobar"]);

                ht = config.GetDictionary("c");
                Assert.AreEqual(0, ht.Count);

                string settings =
@"
&set x foobar

config.a[foo] = bar
CONFIG.B[foo] = bar
CONFIG.B[bar] = $(x)
";
                Config.SetConfig(settings.Replace('&', '#'));
                config = new Config("config");
                ht = config.GetDictionary("a");
                Assert.AreEqual("bar", ht["foo"]);
                ht = config.GetDictionary("b");
                Assert.AreEqual("bar", ht["foo"]);
                Assert.AreEqual("foobar", ht["bar"]);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Boolean()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("bool:1", "1");
                config.Add("bool:0", "0");
                config.Add("bool:on", "on");
                config.Add("bool:off", "off");
                config.Add("bool:yes", "yes");
                config.Add("bool:no", "no");
                config.Add("bool:enable", "enable");
                config.Add("bool:disable", "disable");
                config.Add("bool:true", "true");
                config.Add("bool:false", "false");

                Assert.IsTrue(config.Get("bool:on", false));
                Assert.IsTrue(config.Get("bool:1", false));
                Assert.IsTrue(config.Get("bool:yes", false));
                Assert.IsTrue(config.Get("bool:enable", false));
                Assert.IsTrue(config.Get("bool:true", false));

                Assert.IsFalse(config.Get("bool:off", true));
                Assert.IsFalse(config.Get("bool:0", true));
                Assert.IsFalse(config.Get("bool:no", true));
                Assert.IsFalse(config.Get("bool:disable", true));
                Assert.IsFalse(config.Get("bool:false", true));

                config.Clear();
                config.Add("bool:on", " ON ");
                config.Add("bool:off", " OFF ");
                Assert.IsTrue(config.Get("bool:on", false));
                Assert.IsFalse(config.Get("bool:off", true));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Integer()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("100", "100");
                config.Add("-100", "-100");
                config.Add("2M", "2m");
                Assert.AreEqual(100, config.Get("100", 0));
                Assert.AreEqual(-100, config.Get("-100", 0));
                Assert.AreEqual(2 * 1024 * 1024, config.Get("2M", 0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Long()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("100.0", "100.0");
                config.Add("123.456", "123.456");
                config.Add("2T", "2T");
                Assert.AreEqual(100L, config.Get("100.0", 100.0));
                Assert.AreEqual(2 * 1024L * 1024L * 1024L * 1024L, config.Get("2T", 0L));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Double()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("100.0", "100.0");
                config.Add("123.456", "123.456");
                config.Add("2T", "2T");
                Assert.AreEqual(100, config.Get("100.0", 100.0));
                Assert.AreEqual(123.456, config.Get("123.456", 0.0));
                Assert.AreEqual((double)(2 * 1024L * 1024L * 1024L * 1024L), config.Get("2T", 0.0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        private void _TestTS(string value, TimeSpan tsCheck)
        {
            var config = new Config(null, true);
            TimeSpan ts;

            config.Add("test", value);
            ts = config.Get("test", TimeSpan.FromDays(55));
            Assert.AreEqual(ts, tsCheck);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_TimeSpan()
        {
            try
            {
                _TestTS("1", TimeSpan.FromSeconds(1.0));
                _TestTS("2.0", TimeSpan.FromSeconds(2.0));
                _TestTS("3.5s", TimeSpan.FromSeconds(3.5));
                _TestTS("0.5m", TimeSpan.FromMinutes(0.5));
                _TestTS("1000ms", TimeSpan.FromMilliseconds(1000.0));
                _TestTS("24.5h", TimeSpan.FromHours(24.5));
                _TestTS("5.25d", TimeSpan.FromDays(5.25));
                _TestTS("infinite", TimeSpan.MaxValue);

                _TestTS(" 3.5S ", TimeSpan.FromSeconds(3.5));
                _TestTS(" 0.5M ", TimeSpan.FromMinutes(0.5));
                _TestTS(" 1000MS ", TimeSpan.FromMilliseconds(1000.0));
                _TestTS(" 24.5H ", TimeSpan.FromHours(24.5));
                _TestTS(" 5.25d ", TimeSpan.FromDays(5.25));
                _TestTS(" INFINITE ", TimeSpan.MaxValue);

                _TestTS("1:30", new TimeSpan(0, 1, 30, 0, 0));
                _TestTS("00:1:30.050", new TimeSpan(0, 0, 1, 30, 50));
                _TestTS("1:2:3.4", new TimeSpan(0, 1, 2, 3, 400));
                _TestTS("5.1:2:3.4", new TimeSpan(5, 1, 2, 3, 400));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        private void _TestAddr(string value, IPAddress ipCheck)
        {
            var config = new Config(null, true);
            IPAddress addr;

            config.Add("test", value);
            addr = config.Get("test", (IPAddress)null);
            Assert.AreEqual(addr, ipCheck);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_IPAddress()
        {
            try
            {
                _TestAddr("0.0.0.0", IPAddress.Any);
                _TestAddr("127.0.0.1", IPAddress.Loopback);
                _TestAddr("1.2.3.4", IPAddress.Parse("1.2.3.4"));

                var config = new Config(null, true);

                Assert.IsNull(config.Get("test", (IPAddress)null));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        private void _TestEP(string value, NetworkBinding epCheck)
        {
            var config = new Config(null, true);
            NetworkBinding ep;

            config.Add("test", value);
            ep = config.Get("test", (NetworkBinding)null);
            Assert.AreEqual(ep, epCheck);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_NetworkBinding()
        {
            try
            {
                _TestEP("0.0.0.0:0", new IPEndPoint(IPAddress.Any, 0));
                _TestEP("127.0.0.1:80", new IPEndPoint(IPAddress.Loopback, 80));
                _TestEP("1.2.3.4:5", new IPEndPoint(IPAddress.Parse("1.2.3.4"), 5));

                var config = new Config(null, true);

                Assert.IsNull(config.Get("test", (NetworkBinding)null));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_NetworkBindingArray()
        {
            try
            {
                var config = new Config(null, true);
                NetworkBinding[] arr;

                config.Add("arr[0]", "0.0.0.0:0");
                config.Add("arr[1]", "127.0.0.1:80");
                config.Add("arr[2]", "1.2.3.4:5");
                config.Add("arr[3]", "bad:value");

                arr = config.GetNetworkBindingArray("arr");
                Assert.AreEqual(3, arr.Length);
                Assert.AreEqual(new NetworkBinding(IPAddress.Any, 0), arr[0]);
                Assert.AreEqual(new NetworkBinding(IPAddress.Loopback, 80), arr[1]);
                Assert.AreEqual(new NetworkBinding(IPAddress.Parse("1.2.3.4"), 5), arr[2]);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Guid()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("guid1", "{B86978CF-3B36-4e91-8955-146DDE3F8CFC}");
                config.Add("guid2", "bad");

                Assert.AreEqual(new Guid("{B86978CF-3B36-4e91-8955-146DDE3F8CFC}"), config.Get("guid1", Guid.Empty));
                Assert.AreEqual(Guid.Empty, config.Get("guid2", Guid.Empty));
                Assert.AreEqual(new Guid("{B86978CF-3B36-4e91-8955-146DDE3F8CFC}"), config.Get("guid2", new Guid("{B86978CF-3B36-4e91-8955-146DDE3F8CFC}")));
                Assert.AreEqual(new Guid("{B86978CF-3B36-4e91-8955-146DDE3F8CFC}"), config.Get("guid3", new Guid("{B86978CF-3B36-4e91-8955-146DDE3F8CFC}")));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Uri()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("uri1", "http://www.lilltek.com/");
                config.Add("uri2", "bad");

                Assert.AreEqual(new Uri("http://www.lilltek.com/"), config.Get("uri1", (Uri)null));
                Assert.IsNull(config.Get("uri2", (Uri)null));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Hex()
        {
            try
            {
                var config = new Config(null, true);
                byte[] arr;

                config.Add("arr1", "010203a1A2BB");
                config.Add("arr2", "");
                arr = config.Get("arr1", (byte[])null);
                CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0xA1, 0xA2, 0xBB }, arr);

                arr = config.Get("arr2", (byte[])null);
                Assert.AreEqual(0, arr.Length);

                arr = config.Get("xxx", new byte[] { 1, 2 });
                CollectionAssert.AreEqual(new byte[] { 1, 2 }, arr);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        private enum TestEnum
        {
            Unknown = -1,
            Zero,
            One,
            Two,
            Three
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Enum()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("test0", "Zero");
                config.Add("test1", "one");
                config.Add("test2", "TWO");
                config.Add("test3", "bad");

                Assert.AreEqual(TestEnum.Zero, config.Get("test0", typeof(TestEnum), TestEnum.Unknown));
                Assert.AreEqual(TestEnum.One, config.Get("test1", typeof(TestEnum), TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Two, config.Get("test2", typeof(TestEnum), TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Unknown, config.Get("test3", typeof(TestEnum), TestEnum.Unknown));

                Assert.AreEqual(TestEnum.Zero, config.Get<TestEnum>("test0", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.One, config.Get<TestEnum>("test1", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Two, config.Get<TestEnum>("test2", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Unknown, config.Get<TestEnum>("test3", TestEnum.Unknown));

                Assert.AreEqual(TestEnum.Zero, Config.Parse<TestEnum>("Zero", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.One, Config.Parse<TestEnum>("one", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Two, Config.Parse<TestEnum>("TWO", TestEnum.Unknown));
                Assert.AreEqual(TestEnum.Unknown, Config.Parse<TestEnum>("bad", TestEnum.Unknown));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Type()
        {
            try
            {
                var config = new Config(null, true);
                System.Type thisType = this.GetType();
                System.Type configType = typeof(Config);
                string dllPath;

                dllPath = Helper.StripFileScheme(Assembly.GetExecutingAssembly().GetName().CodeBase);

                config.Add("Found", thisType.FullName + ":" + dllPath);
                config.Add("NoAssembly", thisType.FullName + ":notfound.dll");
                config.Add("NoType", "notfound" + ":" + dllPath);

                Assert.AreEqual(thisType.FullName, config.Get("Found", configType).FullName);
                Assert.AreEqual(configType.FullName, config.Get("NotFound", configType).FullName);
                Assert.AreEqual(configType.FullName, config.Get("NoAssembly", configType).FullName);
                Assert.AreEqual(configType.FullName, config.Get("NoType", configType).FullName);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        private class Person : IParseable
        {
            public string Name;
            public int Age;

            public Person()
            {
            }

            public bool TryParse(string value)
            {
                if (value == null)
                    return false;

                var fields = value.Split(';');

                if (fields.Length != 2)
                    return false;

                this.Name = fields[0];
                this.Age = int.Parse(fields[1]);

                return true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Parse_Custom()
        {
            try
            {
                var config = new Config(null, true);
                Person def = new Person() { Name = "Default" };
                Person person;

                config.Add("Person", "Jeff;50");
                config.Add("Invalid", "");

                person = config.GetCustom<Person>("Person", def);
                Assert.AreEqual("Jeff", person.Name);
                Assert.AreEqual(50, person.Age);

                person = config.GetCustom<Person>("not-found", def);
                Assert.AreEqual("Default", person.Name);

                person = config.GetCustom<Person>("Invalid", def);
                Assert.AreEqual("Default", person.Name);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Defaults_Missing()
        {
            try
            {
                var config = new Config(null, true);

                Assert.AreEqual("hello", config.Get("world", "hello"));
                Assert.AreEqual(666, config.Get("test", 666));
                Assert.AreEqual(TimeSpan.FromSeconds(360), config.Get("test", TimeSpan.FromSeconds(360)));
                Assert.AreEqual(false, config.Get("test", false));
                Assert.AreEqual(true, config.Get("test", true));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Defaults_ParseError()
        {
            try
            {
                var config = new Config(null, true);

                config.Add("int:blank", "");
                Assert.AreEqual(-666, config.Get("int:blank", -666));
                config.Add("int:bad", "--xx");
                Assert.AreEqual(1000, config.Get("int:bad", 1000));

                config.Add("bool:blank", "");
                Assert.AreEqual(true, config.Get("bool:blank", true));
                Assert.AreEqual(false, config.Get("bool:blank", false));
                config.Add("bool:bad", "bad");
                Assert.AreEqual(true, config.Get("bool:bad", true));
                Assert.AreEqual(false, config.Get("bool:bad", false));

                config.Add("ts:blank", "");
                Assert.AreEqual(TimeSpan.FromSeconds(55), config.Get("ts:blank", TimeSpan.FromSeconds(55)));
                config.Add("ts:bad1", "145X");
                Assert.AreEqual(TimeSpan.FromSeconds(55), config.Get("ts:bad1", TimeSpan.FromSeconds(55)));
                config.Add("ts:bad2", "xxx22s");
                Assert.AreEqual(TimeSpan.FromSeconds(55), config.Get("ts:bad1", TimeSpan.FromSeconds(55)));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CrossPlatform()
        {
            string configFile =
@"
key1=test1
    key2=test2
// key3=test3
// key4=test4
    key5  = this is a test.
key6 =    

key7=77
";
            try
            {
                var config = new Config(null, configFile);
                Assert.AreEqual("test1", config.Get("key1"));
                Assert.AreEqual("test2", config.Get("key2"));
                Assert.IsNull(config.Get("key3"));
                Assert.IsNull(config.Get("key4"));
                Assert.AreEqual("this is a test.", config.Get("key5"));
                Assert.AreEqual("", config.Get("key6"));
                Assert.AreEqual(77, config.Get("key7", 0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CrossPlatform_Prefix()
        {
            string configFile =
@"
test.key1=test1
    test.key2=test2
// test.key3=test3
// test.key4=test4
    test.key5  = this is a test.
test.key6 =    

test.key7=77

key1=test1
    key2=test2
// key3=test3
// key4=test4
    key5  = this is a test.
key6 =    

key7=77
";
            try
            {
                var config = new Config("test", configFile);
                Assert.AreEqual("test1", config.Get("key1"));
                Assert.AreEqual("test2", config.Get("key2"));
                Assert.IsNull(config.Get("key3"));
                Assert.IsNull(config.Get("key4"));
                Assert.AreEqual("this is a test.", config.Get("key5"));
                Assert.AreEqual("", config.Get("key6"));
                Assert.AreEqual(77, config.Get("key7", 0));

                config = new Config("test.", configFile);
                Assert.AreEqual("test1", config.Get("key1"));
                Assert.AreEqual("test2", config.Get("key2"));
                Assert.IsNull(config.Get("key3"));
                Assert.IsNull(config.Get("key4"));
                Assert.AreEqual("this is a test.", config.Get("key5"));
                Assert.AreEqual("", config.Get("key6"));
                Assert.AreEqual(77, config.Get("key7", 0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_NonFileLoad()
        {
            try
            {
                Config.SetConfig("key1=1\r\nkey2=2\r\n");

                var config = new Config();
                Assert.AreEqual(1, config.Get("key1", -1));
                Assert.AreEqual(2, config.Get("key2", -1));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_EnvironmentVars()
        {
            try
            {
                Config.SetConfig("key1=%temp%\r\nkey2=$(systemroot)\r\n");

                var config = new Config();
                Assert.AreEqual(Environment.GetEnvironmentVariable("temp"), config.Get("key1"));
                Assert.AreEqual(Environment.GetEnvironmentVariable("systemroot"), config.Get("key2"));

                Config.ClearGlobal();
                Config.ProcessEnvironmentVars = false;

                config = new Config();
                Assert.AreEqual("%temp%", config.Get("key1"));
                Assert.AreEqual("$(systemroot)", config.Get("key2"));
                Assert.AreEqual(Environment.GetEnvironmentVariable("temp"), config.GetEnv("key1"));
                Assert.AreEqual(Environment.GetEnvironmentVariable("systemroot"), config.GetEnv("key2"));
            }
            finally
            {
                Config.ClearGlobal();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Sections()
        {
            string cfg =
@"
&section foo
key1 = 10
&endsection

key2 = 20

&section bar.
key3 = 30
&endsection

key4 = 40

&section foo.bar
key5 = 50
&endsection

&section joe
&section blo
key6 = 60
&endsection
key7 = 70
&endsection
key8 = 80
";
            try
            {
                var config = new Config(null, cfg.Replace('&', '#'));
                Assert.AreEqual("10", config.Get("foo.key1"));
                Assert.AreEqual("20", config.Get("key2"));
                Assert.AreEqual("30", config.Get("bar.key3"));
                Assert.AreEqual("40", config.Get("key4"));
                Assert.AreEqual("50", config.Get("foo.bar.key5"));
                Assert.AreEqual("60", config.Get("joe.blo.key6"));
                Assert.AreEqual("70", config.Get("joe.key7"));
                Assert.AreEqual("80", config.Get("key8"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetSection()
        {
            Config section;
            string cfg =
@"
test = 0

&section foo
key1 = 10
key2 = 20
&endsection

&section bar
key1 = 30
key2 = 40
&endsection

&section Root
    &section foo
        key1 = 50
        key2 = 60
    &endsection
&endsection
";
            try
            {
                var config = new Config(null, cfg.Replace('&', '#'));

                section = config.GetSection("foo");
                Assert.AreEqual(10, section.Get("key1", -1));
                Assert.AreEqual(20, section.Get("key2", -1));

                section = config.GetSection("bar.");
                Assert.AreEqual(30, section.Get("key1", -1));
                Assert.AreEqual(40, section.Get("key2", -1));

                section = config.GetSection("");
                Assert.AreEqual(0, section.Get("test", -1));
                Assert.AreEqual(10, section.Get("foo.key1", -1));
                Assert.AreEqual(20, section.Get("foo.key2", -1));
                Assert.AreEqual(30, section.Get("bar.key1", -1));
                Assert.AreEqual(40, section.Get("bar.key2", -1));

                section = config.GetSection(null);
                Assert.AreEqual(0, section.Get("test", -1));
                Assert.AreEqual(10, section.Get("foo.key1", -1));
                Assert.AreEqual(20, section.Get("foo.key2", -1));
                Assert.AreEqual(30, section.Get("bar.key1", -1));
                Assert.AreEqual(40, section.Get("bar.key2", -1));

                config = new Config("root", cfg.Replace('&', '#'));
                section = config.GetSection("foo");
                Assert.AreEqual(50, section.Get("key1", -1));
                Assert.AreEqual(60, section.Get("key2", -1));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Conditionals()
        {
            try
            {
                string cf1 =
@"
key0=0

&if WINCE
key1=10
key2=20
&endif

&if WINFULL
key1=100
key2=200
&endif

key3=30

&if winfull
key4=400
&else
key4=0
&endif
";
                var config = new Config(null, cf1.Replace('&', '#'));
                Assert.AreEqual("0", config.Get("key0"));
                Assert.AreEqual("100", config.Get("key1"));
                Assert.AreEqual("200", config.Get("key2"));
                Assert.AreEqual("30", config.Get("key3"));
                Assert.AreEqual("400", config.Get("key4"));

                string cf2 =
@"
key0=0

&if WINFULL
key1=10
key2=20
&endif

&if WINCE
key1=100
key2=200
&endif

key3=30
";
                config = new Config(null, cf2.Replace('&', '#'));
                Assert.AreEqual("0", config.Get("key0"));
                Assert.AreEqual("10", config.Get("key1"));
                Assert.AreEqual("20", config.Get("key2"));
                Assert.AreEqual("30", config.Get("key3"));

                string cf3 =
@"
&define bar

&if bar

    bar=mybar

    &if WINFULL
        key1=10
    &endif

    &if WINCE
        key1=20
    &endif

&endif

&if foo

    foo=myfoo

    &if WINFULL
    key2=100
    &endif

&endif

hello=world
";
                config = new Config(null, cf3.Replace('&', '#'));
                Assert.AreEqual("mybar", config.Get("bar"));
                Assert.IsNull(config.Get("foo"));
                Assert.AreEqual("10", config.Get("key1"));
                Assert.AreEqual("world", config.Get("hello"));
                Assert.IsNull(config.Get("key2"));

                string cf4 =
@"
&define bar

&if bar
    key1=1
&endif

&if !bar
    key2=2
&endif

&if foo
    key3=3
&endif

&if !foo
    key4=4
&endif
";
                config = new Config(null, cf4.Replace('&', '#'));
                Assert.AreEqual("1", config.Get("key1"));
                Assert.IsNull(config.Get("key2"));
                Assert.IsNull(config.Get("key3"));
                Assert.AreEqual("4", config.Get("key4"));

                string cf5 =
@"
&undef  bar
&define bar

&if bar
    key1=1
&else
    key1=2
&endif

&undef bar

&if bar
    key2=3
&else
    key2=4
&endif
";
                config = new Config(null, cf5.Replace('&', '#'));
                Assert.AreEqual("1", config.Get("key1"));
                Assert.AreEqual("4", config.Get("key2"));

                string cf6 =
@"
&if true
    key1 = 10
&endif

&if false
    key1 = 20
&endif
";
                config = new Config(null, cf6.Replace('&', '#'));
                Assert.AreEqual("10", config.Get("key1"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Macros()
        {
            try
            {
                string cf1 =
@"
&define guid1 $(Guid)
&set    guid2 $(Guid)

&define m1 10
&define m2 20
&define m3 $(m1)
&define m4 %m2%
&define m5 $(m6)
&define m6 %m2%
&define M7 70

&define site http://foo.com

key1=$(m0)
key2=%m0%
key3=$(m1)
key4=%m2%
key5=$(m3)
key6=%m4%
key7=$(m5)
key8=$(m7)
key9=$(M7)

gid1=$(guid1)
gid2=$(guid2)

uri = $(site)/test.txt

multi = {{
    $(site)/test.txt
}}
";
                var config = new Config(null, cf1.Replace('&', '#'));
                Assert.AreEqual("$(m0)", config.Get("key1"));
                Assert.AreEqual("%m0%", config.Get("key2"));
                Assert.AreEqual("10", config.Get("key3"));
                Assert.AreEqual("10", config.Get("key3", string.Empty));
                Assert.AreEqual("20", config.Get("key4"));
                Assert.AreEqual("10", config.Get("key5"));
                Assert.AreEqual("20", config.Get("key6"));
                Assert.AreEqual("20", config.Get("key7"));
                Assert.AreEqual("70", config.Get("key8"));
                Assert.AreEqual("70", config.Get("key9"));

                Assert.AreNotEqual(config.Get("gid1"), config.Get("gid1"));
                Assert.AreEqual(config.Get("gid2"), config.Get("gid2"));

                Assert.AreEqual(new Uri("http://foo.com/test.txt"), config.Get("uri", (Uri)null));
                Assert.IsTrue(config.Get("multi").Contains("http://foo.com/test.txt"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_EditMacro()
        {
            try
            {
                var es = new EnhancedMemoryStream();

                es.SetLength(0);
                es.WriteBytesNoLen(Helper.ToAnsi("#define key1 1"));
                Assert.IsTrue(Config.EditMacro(es, Helper.AnsiEncoding, "key1", "one"));
                es.Position = 0;
                Assert.AreEqual("#define key1 one\r\n", Helper.FromAnsi(es.ReadBytesToEnd()));

                es.SetLength(0);
                es.WriteBytesNoLen(Helper.ToAnsi("#define key1"));
                Assert.IsFalse(Config.EditMacro(es, Helper.AnsiEncoding, "key0", "zero"));
                es.Position = 0;
                Assert.AreEqual("#define key1\r\n", Helper.FromAnsi(es.ReadBytesToEnd()));

                es.SetLength(0);
                es.WriteBytesNoLen(Helper.ToAnsi("// #define key1 1"));
                Assert.IsFalse(Config.EditMacro(es, Helper.AnsiEncoding, "key1", "one"));
                es.Position = 0;
                Assert.AreEqual("// #define key1 1\r\n", Helper.FromAnsi(es.ReadBytesToEnd()));

                es.SetLength(0);
                es.WriteBytesNoLen(Helper.ToAnsi("#define key1 1\r\n\r\n#define key2 2\r\n"));
                Assert.IsTrue(Config.EditMacro(es, Helper.AnsiEncoding, "key2", "two"));
                es.Position = 0;
                Assert.AreEqual("#define key1 1\r\n\r\n#define key2 two\r\n", Helper.FromAnsi(es.ReadBytesToEnd()));

                es.SetLength(0);
                es.WriteBytesNoLen(Helper.ToAnsi("#define key1 1\r\n  \r\n#define key2 2\r\n"));
                Assert.IsFalse(Config.EditMacro(es, Helper.AnsiEncoding, "KEY2", "two"));
                es.Position = 0;
                Assert.AreEqual("#define key1 1\r\n  \r\n#define key2 2\r\n", Helper.FromAnsi(es.ReadBytesToEnd()));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_BuiltIn()
        {
            try
            {
                string cfg =
@"
&define GID1 $(Guid)
&set GUD2 $(Guid)

_TEMP=$(Temp)
_TMP=$(Tmp)
_SYSTEMROOT=$(SystemRoot)
_SYSTEMDIRECTORY=$(SystemDirectory)
_APPPATH=$(AppPath)
_OS=$(OS)
_WINFULL=$(WINFULL)
_GUID1=$(GID1)
_GUID2=$(GID2)
_MACHINENAME1=$(MachineName)
_MACHINENAME2=%MachineName%
_PROCESSORCOUNT=$(ProcessorCount)
";
                Config.ClearGlobal();
                var config = new Config(null, cfg.Replace('&', '#'));

                Assert.AreEqual(Environment.GetEnvironmentVariable("temp"), config.Get("_temp"));
                Assert.AreEqual(Environment.GetEnvironmentVariable("tmp"), config.Get("_tmp"));
                Assert.AreEqual(Environment.GetEnvironmentVariable("SystemRoot"), config.Get("_SystemRoot"));
                Assert.AreEqual((Environment.GetEnvironmentVariable("SystemRoot") + @"\system32").ToLowerInvariant(), config.Get("_SystemDirectory").ToLowerInvariant());
                Assert.AreEqual(Helper.EntryAssemblyFolder, config.Get("_AppPath"));
                Assert.AreNotEqual("$(OS)", config.Get("_OS"));
                Assert.AreNotEqual("$(WINFULL)", config.Get("_WINFULL"));
                Assert.AreNotEqual(config.Get("_guid1"), config.Get("_guid2"));
                Assert.AreNotEqual(config.Get("_guid1"), config.Get("_guid1"));
                Assert.AreEqual(config.Get("_guid2"), config.Get("_guid2"));
                Assert.AreEqual(Helper.MachineName, config.Get("_MachineName1"));
                Assert.AreEqual(Helper.MachineName, config.Get("_MachineName2"));
                Assert.AreEqual(Environment.ProcessorCount, config.Get("_PROCESSORCOUNT", 0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Global()
        {
            try
            {
                Config cfg1, cfg2;

                Config.SetConfig("a=b");
                cfg1 = Config.Global;
                Assert.AreEqual("b", cfg1.Get("a", ""));
                cfg2 = Config.Global;
                Assert.AreEqual("b", cfg1.Get("a", ""));
                Assert.AreSame(cfg1, cfg2);

                Config.ClearGlobal();
                Config.SetConfig("a=c");
                cfg1 = Config.Global;
                Assert.AreEqual("c", cfg1.Get("a", ""));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_GetConfigRef()
        {
            try
            {
                string key = "";
                string def = "";

                Assert.IsFalse(Config.GetConfigRef("", out key, out def));
                Assert.IsNull(key);
                Assert.IsNull(def);

                Assert.IsFalse(Config.GetConfigRef("foo", out key, out def));
                Assert.IsFalse(Config.GetConfigRef("[config:mykey", out key, out def));
                Assert.IsFalse(Config.GetConfigRef("config:mykey]", out key, out def));

                Assert.IsTrue(Config.GetConfigRef("[config:mykey]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.IsNull(def);

                Assert.IsTrue(Config.GetConfigRef("[config: mykey ]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.IsNull(def);

                Assert.IsTrue(Config.GetConfigRef("[config:mykey,default]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.AreEqual("default", def);

                Assert.IsTrue(Config.GetConfigRef("[config: mykey , default ]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.AreEqual("default", def);

                Assert.IsTrue(Config.GetConfigRef("[CONFIG:mykey,default]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.AreEqual("default", def);

                Assert.IsTrue(Config.GetConfigRef("[config:mykey,]", out key, out def));
                Assert.AreEqual("mykey", key);
                Assert.AreEqual("", def);

                try
                {
                    Config.GetConfigRef("[config:]", out key, out def);
                    Assert.Fail("ArgumentException expected");
                }
                catch (ArgumentException)
                {
                }

                try
                {
                    Config.GetConfigRef("[config:,]", out key, out def);
                    Assert.Fail("ArgumentException expected");
                }
                catch (ArgumentException)
                {
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_String()
        {
            try
            {
                Config.SetConfig("test=mystring");
                Config.ClearGlobal();

                Assert.AreEqual("test", Config.ParseValue("test", string.Empty));
                Assert.AreEqual("test", Config.ParseValue(null, "test"));
                Assert.AreEqual("mystring", Config.ParseValue("[config:test]", "bar"));
                Assert.AreEqual("default", Config.ParseValue("[config:bar,default]", "bar"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_Int()
        {
            try
            {
                Config.SetConfig("test=55\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(55, Config.ParseValue("55", 77));
                Assert.AreEqual(77, Config.ParseValue(null, 77));
                Assert.AreEqual(55, Config.ParseValue("[config:test]", 77));
                Assert.AreEqual(77, Config.ParseValue("[config:bar,77]", 88));
                Assert.AreEqual(88, Config.ParseValue("[config:foobar]", 88));
                Assert.AreEqual(88, Config.ParseValue("[config:bar,xx]", 88));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_Bool()
        {
            try
            {
                Config.SetConfig("test=yes\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(true, Config.ParseValue("true", false));
                Assert.AreEqual(false, Config.ParseValue(null, false));
                Assert.AreEqual(true, Config.ParseValue("[config:test]", false));
                Assert.AreEqual(true, Config.ParseValue("[config:bar,true]", false));
                Assert.AreEqual(false, Config.ParseValue("[config:foobar]", false));
                Assert.AreEqual(false, Config.ParseValue("[config:bar,xx]", false));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_Double()
        {
            try
            {
                Config.SetConfig("test=10.0\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(10.0, Config.ParseValue("10.0", 0.0));
                Assert.AreEqual(10.0, Config.ParseValue(null, 10.0));
                Assert.AreEqual(10.0, Config.ParseValue("[config:test]", 20.0));
                Assert.AreEqual(10.0, Config.ParseValue("[config:bar,10.0]", 20.0));
                Assert.AreEqual(20.0, Config.ParseValue("[config:foobar]", 20.0));
                Assert.AreEqual(20.0, Config.ParseValue("[config:bar,xx]", 20.0));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_TimeSpan()
        {
            try
            {
                Config.SetConfig("test=10s\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(TimeSpan.FromSeconds(10), Config.ParseValue("10s", TimeSpan.FromSeconds(100)));
                Assert.AreEqual(TimeSpan.FromSeconds(10), Config.ParseValue(null, TimeSpan.FromSeconds(10)));
                Assert.AreEqual(TimeSpan.FromSeconds(10), Config.ParseValue("[config:test]", TimeSpan.FromSeconds(20)));
                Assert.AreEqual(TimeSpan.FromSeconds(10), Config.ParseValue("[config:bar,10s]", TimeSpan.FromSeconds(20)));
                Assert.AreEqual(TimeSpan.FromSeconds(20), Config.ParseValue("[config:foobar]", TimeSpan.FromSeconds(20)));
                Assert.AreEqual(TimeSpan.FromSeconds(20), Config.ParseValue("[config:bar,xx]", TimeSpan.FromSeconds(20)));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_IPAddress()
        {
            try
            {
                Config.SetConfig("test=10.10.10.10\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(IPAddress.Parse("10.10.10.10"), Config.ParseValue("10.10.10.10", IPAddress.Parse("20.20.20.20")));
                Assert.AreEqual(IPAddress.Parse("10.10.10.10"), Config.ParseValue(null, IPAddress.Parse("10.10.10.10")));
                Assert.AreEqual(IPAddress.Parse("10.10.10.10"), Config.ParseValue("[config:test]", IPAddress.Parse("20.20.20.20")));
                Assert.AreEqual(IPAddress.Parse("10.10.10.10"), Config.ParseValue("[config:bar,10.10.10.10]", IPAddress.Parse("20.20.20.20")));
                Assert.AreEqual(IPAddress.Parse("20.20.20.20"), Config.ParseValue("[config:foobar]", IPAddress.Parse("20.20.20.20")));
                Assert.AreEqual(IPAddress.Parse("20.20.20.20"), Config.ParseValue("[config:bar,xx]", IPAddress.Parse("20.20.20.20")));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_NetworkBinding()
        {
            try
            {
                NetworkBinding ep10 = new IPEndPoint(IPAddress.Parse("10.10.10.10"), 10);
                NetworkBinding ep20 = new IPEndPoint(IPAddress.Parse("20.20.20.20"), 20);

                Config.SetConfig("test=10.10.10.10:10\r\nfoobar=xx");
                Config.ClearGlobal();

                Assert.AreEqual(ep10, Config.ParseValue("10.10.10.10:10", ep20));
                Assert.AreEqual(ep10, Config.ParseValue(null, ep10));
                Assert.AreEqual(ep10, Config.ParseValue("[config:test]", ep20));
                Assert.AreEqual(ep10, Config.ParseValue("[config:bar,10.10.10.10:10]", ep20));
                Assert.AreEqual(ep20, Config.ParseValue("[config:foobar]", ep20));
                Assert.AreEqual(ep20, Config.ParseValue("[config:bar,xx]", ep20));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        public class Class10
        {
        }

        public class Class20
        {
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_ParseValue_Type()
        {
            try
            {
                var assemblyPath = Assembly.GetExecutingAssembly().Location;

                Config.SetConfig(string.Format("test={0}:{1}\r\nfoobar=xx", typeof(Class10).FullName, assemblyPath));
                Config.ClearGlobal();

                Assert.AreEqual(typeof(Class10), Config.ParseValue(string.Format("{0}:{1}", typeof(Class10).FullName, assemblyPath), typeof(Class20)));
                Assert.AreEqual(typeof(Class10), Config.ParseValue(null, typeof(Class10)));
                Assert.AreEqual(typeof(Class10), Config.ParseValue("[config:test]", typeof(Class20)));
                Assert.AreEqual(typeof(Class10), Config.ParseValue("[config:bar," + typeof(Class10).FullName + ":" + assemblyPath + "]", typeof(Class20)));
                Assert.AreEqual(typeof(Class20), Config.ParseValue("[config:foobar]", typeof(Class20)));
                Assert.AreEqual(typeof(Class20), Config.ParseValue("[config:bar,xx]", typeof(Class20)));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        public class Provider : IConfigProvider
        {
            public static string Result = null;
            public static bool CacheEnable = false;

            public static void Reset()
            {

                Result = null;
                CacheEnable = false;
            }

            public static ArgCollection Settings;
            public static string CacheFile;
            public static string MachineName;
            public static string ExeFile;
            public static Version ExeVersion;
            public static String Usage;

            public string GetConfig(ArgCollection settings, string cacheFile, string machineName, string exeFile, Version exeVersion, string usage)
            {
                Settings = settings;
                CacheFile = cacheFile;
                MachineName = machineName;
                ExeFile = exeFile;
                ExeVersion = exeVersion;
                Usage = usage;

                if (CacheEnable)
                {

                    if (Result == null)
                    {

                        StreamReader input;
                        string cached;

                        input = new StreamReader(CacheFile);
                        cached = input.ReadToEnd();
                        input.Close();

                        return cached;
                    }
                    else
                    {

                        StreamWriter output;

                        output = new StreamWriter(CacheFile);
                        output.Write(Result);
                        output.Close();

                        return Result;
                    }
                }
                else
                    return Result;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CustomProvider()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tempDir = Path.GetTempPath();
                Config config;
                string cfg;

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = MySetting=Foo
Config.CacheFile      = {2}Test.cache.ini
Config.MachineName    = $(MachineName)
Config.ExeFile        = Foo.exe
Config.ExeVersion     = 1.0
Config.Usage          = Test
";

                Provider.Reset();
                if (File.Exists(tempDir + "Test.cache.ini"))
                    File.Delete(tempDir + "Test.cache.ini");

                cfg = string.Format(cfg, typeof(Provider).FullName, typeof(Provider).Assembly.Location, tempDir);

                Provider.Result = "foo=bar\r\nhost=$(MachineName)";
                Config.SetConfig(cfg);

                config = new Config();
                Assert.AreEqual("Foo", Provider.Settings["MySetting"]);
                Assert.AreEqual(tempDir + "Test.cache.ini", Provider.CacheFile);
                Assert.AreEqual(Helper.MachineName, Provider.MachineName);
                Assert.AreEqual("Foo.exe", Provider.ExeFile);
                Assert.AreEqual(new Version("1.0"), Provider.ExeVersion);
                Assert.AreEqual("Test", Provider.Usage);

                Assert.AreEqual("bar", config.Get("foo"));
                Assert.AreEqual(Helper.MachineName, config.Get("host"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CustomProvider_Cached()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tempDir = Path.GetTempPath();
                Config config;
                string cfg;

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = MySetting=Foo
Config.CacheFile      = {2}Test.cache.ini
Config.MachineName    = $(MachineName)
Config.ExeFile        = Foo.exe
Config.ExeVersion     = 1.0
Config.Usage          = Test
";
                try
                {
                    Provider.Reset();
                    if (File.Exists(tempDir + "Test.cache.ini"))
                        File.Delete(tempDir + "Test.cache.ini");

                    cfg = string.Format(cfg, typeof(Provider).FullName, typeof(Provider).Assembly.Location, tempDir);

                    Provider.Result = "foo=bar";
                    Provider.CacheEnable = true;
                    Config.SetConfig(cfg);

                    config = new Config();
                    Assert.AreEqual("bar", config.Get("foo"));

                    Provider.Result = null;
                    Config.SetConfig(cfg);

                    config = new Config();
                    Assert.AreEqual("bar", config.Get("foo"));
                }
                finally
                {
                    if (File.Exists(tempDir + "Test.cache.ini"))
                        File.Delete(tempDir + "Test.cache.ini");
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Override()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                string path = tempDir + "Override.ini";
                StreamWriter writer = null;
                Config config;

                try
                {
                    writer = new StreamWriter(path);
                    writer.Write("foo=foo\r\nbar=foobar\r\n");
                    writer.Close();
                    writer = null;

                    Config.SetConfig("foo=bar");
                    config = new Config();
                    Assert.AreEqual("bar", config.Get("foo"));
                    Assert.IsNull(config.Get("bar"));

                    LillTek.Common.EnvironmentVars.Load("LillTek.ConfigOverride=" + path);

                    Config.SetConfig("foo=bar");
                    config = new Config();
                    Assert.AreEqual("foo", config.Get("foo"));
                    Assert.AreEqual("foobar", config.Get("bar"));
                }
                finally
                {
                    if (writer != null)
                        writer.Close();

                    if (File.Exists(path))
                        File.Delete(path);

                    LillTek.Common.EnvironmentVars.Load(string.Empty);
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_SubSections()
        {
            try
            {
                Config root;
                Config[] sections;
                string[] sectionKeys;

                string cfg = @"

&section Root

    &section Folder[0]
        Test = value1
    &endsection

    &section Folder[1]
        Test = value2
    &endsection

&endsection
";

                Config.SetConfig(cfg.Replace('&', '#'));

                root = new Config("Root");
                sectionKeys = root.GetSectionKeyArray("Folder");
                CollectionAssert.AreEqual(new string[] { "Root.Folder[0]", "Root.Folder[1]" }, sectionKeys);

                sections = root.GetSectionConfigArray("Folder");
                Assert.AreEqual("value1", sections[0].Get("Test"));
                Assert.AreEqual("value2", sections[1].Get("Test"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Switch()
        {
            try
            {
                Config config;

                string cfg =
@"
&define v1 hello

&switch v1

    &case test1

        key1 = 10

    &case hello

        key1 = 20

    &default

        key1 = 30

&endswitch

&switch v1

    &case TEST1

        key2 = 10

    &case HELLO

        key2 = 20

    &default

        key2 = 30

&endswitch

&switch v1

    &case TEST1

        key3 = 10

    &case TEST2

        key3 = 20

    &default

        key3 = 30

&endswitch

&switch v1

    &case TEST1

        key4 = 10

    &case TEST2

        key4 = 20

&endswitch
";

                Config.SetConfig(cfg.Replace('&', '#'));
                config = new Config();
                Assert.AreEqual("20", config.Get("key1"));
                Assert.AreEqual("20", config.Get("key2"));
                Assert.AreEqual("30", config.Get("key3"));
                Assert.IsNull(config.Get("key4"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_SwitchNested()
        {
            try
            {
                Config config;

                string cfg =
@"
&define v1 hello
&define v2 foo

&switch v1

    &case test1

        key1 = 10
        key2 = TEST1

    &case hello

        &switch v2

            &case hello

                key1 = 20HELLO

            &case foo

                key1 = FOO

            &default

                key1 = 20DEF

        &endswitch

        key2 = HELLO

    &default

        key1 = 30
        key2 = DEFAULT

&endswitch

key3 = 30
";

                Config.SetConfig(cfg.Replace('&', '#'));
                config = new Config();
                Assert.AreEqual("FOO", config.Get("key1"));
                Assert.AreEqual("HELLO", config.Get("key2"));
                Assert.AreEqual("30", config.Get("key3"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CombineKeys()
        {
            try
            {
                Assert.IsNull(Config.CombineKeys(null, null));

                Assert.AreEqual("key2", Config.CombineKeys(null, "key2"));
                Assert.AreEqual("key2", Config.CombineKeys(" ", "key2"));
                Assert.AreEqual("key2", Config.CombineKeys(".", "key2"));
                Assert.AreEqual("key2", Config.CombineKeys(null, ".key2"));

                Assert.AreEqual("key1", Config.CombineKeys("key1", null));
                Assert.AreEqual("key1", Config.CombineKeys("key1", " "));
                Assert.AreEqual("key1", Config.CombineKeys("key1.", ""));

                Assert.AreEqual("key1.key2", Config.CombineKeys("key1", "key2"));
                Assert.AreEqual("key1.key2", Config.CombineKeys("key1.", "key2"));
                Assert.AreEqual("key1.key2", Config.CombineKeys("key1", ".key2"));
                Assert.AreEqual("key1.key2", Config.CombineKeys("key1.", ".key2"));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Set_Remove()
        {
            try
            {
                var config = new Config(true);

                Assert.AreEqual(0, config.Count);
                config.Set("Test1", "Hello");
                Assert.AreEqual(1, config.Count);
                Assert.AreEqual("Hello", config.Get("Test1"));

                config.Set("Test2", "World!");
                Assert.AreEqual(2, config.Count);
                Assert.AreEqual("Hello", config.Get("Test1"));
                Assert.AreEqual("World!", config.Get("Test2"));

                config.Set("Test1", "Hello World!");
                Assert.AreEqual(2, config.Count);
                Assert.AreEqual("Hello World!", config.Get("Test1"));

                config.Remove("Test1");
                Assert.AreEqual(1, config.Count);
                Assert.IsNull(config.Get("Test1"));

                config.Remove("Test1"); // Shouldn't throw an exception
                Assert.IsNull(config.Get("Test1"));

                config.Remove("Test2");
                Assert.AreEqual(0, config.Count);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Enumerate()
        {
            try
            {
                var config = new Config(true);
                var values = new Dictionary<string, string>();

                foreach (var key in config)
                    values[key.Key] = key.Value;

                Assert.AreEqual(0, values.Count);

                config.Set("Test1", "Hello");
                config.Set("Test2", "World!");

                foreach (var key in config)
                    values[key.Key] = key.Value;

                Assert.AreEqual(2, config.Count);

                Assert.IsTrue(values.ContainsKey("Test1"));
                Assert.AreEqual(values["Test1"], "Hello");

                Assert.IsTrue(values.ContainsKey("Test2"));
                Assert.AreEqual(values["Test2"], "World!");
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_CreateEmpty()
        {
            try
            {
                var config = new Config(true);
                Assert.AreEqual(0, config.Count);

                config = Config.CreateEmpty();
                Assert.AreEqual(0, config.Count);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Save()
        {
            string path = Path.GetTempFileName();
            // string      path = @"C:\temp\config.txt";
            Config config;

            try
            {

                Config.SetConfigPath(path);

                config = new Config(null);
                config.Set("hello", "world");
                config.Set("foo", "bar");

                config.Save();
                Config.ClearGlobal();

                Config.SetConfigPath(path);

                config = new Config(null);
                Assert.AreEqual("world", config.Get("hello"));
                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {

                Config.SetConfigPath((string)null);
                Config.SetConfig(null);

                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Config_Set()
        {
            Guid guid = Guid.NewGuid();
            Config config;

            try
            {
                Config.SetConfig(string.Empty);

                config = new Config();

                config.Set("bool", true);
                config.Set("double", 123.4);
                config.Set("enum", TestEnum.Three);
                config.Set("guid", guid);
                config.Set("timespan", TimeSpan.FromMilliseconds(1234));
                config.Set("long", 12345670000000L);
                config.Set("ip", Helper.ParseIPAddress("10.1.1.3"));
                config.Set("binding", new NetworkBinding("1.2.3.4:80"));

                Assert.AreEqual(true, config.Get("bool", false));
                Assert.AreEqual(123.4, config.Get("double", 0.0));
                Assert.AreEqual(TestEnum.Three, config.Get<TestEnum>("enum", TestEnum.Unknown));
                Assert.AreEqual(guid, config.Get("guid", Guid.Empty));
                Assert.AreEqual(TimeSpan.FromMilliseconds(1234), config.Get("timespan", TimeSpan.Zero));
                Assert.AreEqual(Helper.ParseIPAddress("10.1.1.3"), config.Get("ip", IPAddress.Loopback));
                Assert.AreEqual(new NetworkBinding("1.2.3.4:80"), config.Get("binding", NetworkBinding.Any));
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

