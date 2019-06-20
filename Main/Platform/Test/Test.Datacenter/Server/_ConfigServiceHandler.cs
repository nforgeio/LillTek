//-----------------------------------------------------------------------------
// FILE:        _ConfigService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _ConfigServiceHandler
    {
        private List<string> files = null;
        private string folder;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        private void InitFiles()
        {
            ClearFiles();

            files = new List<string>();
            folder = Path.GetTempPath() + "Settings";

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private void WriteFile(string fname, string contents)
        {
            StreamWriter writer;

            fname = folder + "\\" + fname;
            writer = new StreamWriter(fname);
            writer.Write(contents.Replace('&', '#'));
            writer.Close();

            files.Add(fname);
        }

        private void ClearFiles()
        {
            if (files != null)
            {
                foreach (string fname in files)
                {
                    if (File.Exists(fname))
                        File.Delete(fname);
                }

                files = null;
            }

            if (folder != null && Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder);
                }
                catch
                {
                }
            }
        }

        private LeafRouter CreateRouter()
        {
            const string settings =
@"
&section LillTek.Datacenter.ConfigService

    SettingsFolder = {0}

&endsection

&section MsgRouter

    AppName                = Test
    AppDescription         = Test Description
    RouterEP		       = physical://detached/test/leaf
    CloudEP    			   = $(LillTek.DC.CloudEP)
    CloudAdapter    	   = ANY
    UdpEP				   = ANY:0
    TcpEP				   = ANY:0
    TcpBacklog			   = 100
    TcpDelay			   = off
    BkInterval			   = 1s
    MaxIdle				   = 5m
    EnableP2P              = yes
    AdvertiseTime		   = 1m
    DefMsgTTL			   = 5
    SharedKey		 	   = PLAINTEXT
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

&endsection
";
            LeafRouter router;
            ConfigServiceHandler configHandler;
            string folder;

            folder = Path.GetTempPath() + "Settings";
            Config.SetConfig(string.Format(settings, folder).Replace('&', '#'));
            MsgEP.ReloadAbstractMap();

            router = new LeafRouter();
            configHandler = new ConfigServiceHandler();

            configHandler.Start(router, null, null, null);
            router.Start();

            return router;
        }

        private Config Query(MsgRouter router, string machineName, string exeFile, Version exeVersion, string usage)
        {
            GetConfigMsg query;
            GetConfigAck ack;

            query = new GetConfigMsg(machineName, exeFile, exeVersion, usage);
            ack = (GetConfigAck)router.Query(ConfigServiceProvider.GetConfigEP, query);
            return new Config(null, new StringReader(ack.ConfigText));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ByExeFile()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.foo=foo
test.bar=foobar
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("foo", config.Get("test.foo"));
                Assert.AreEqual("foobar", config.Get("test.bar"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ByMachineName()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("svr-machine.ini",
@"
test.foo=foo
test.bar=foobar
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("foo", config.Get("test.foo"));
                Assert.AreEqual("foobar", config.Get("test.bar"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ByExeAndMachine()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.foo=foo
");
                WriteFile("svr-machine.ini",
@"
test.bar=foobar
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("foo", config.Get("test.foo"));
                Assert.AreEqual("foobar", config.Get("test.bar"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Include()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.foo=foo
&&include include.ini
");
                WriteFile("include.ini",
@"
test.bar=foobar
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("foo", config.Get("test.foo"));
                Assert.AreEqual("foobar", config.Get("test.bar"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnMachineName()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(MachineName)

    &&case foo

        test.value = 0

    &&case machine

        test.value = 1

    &&case bar

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("1", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnExeFile()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(EXEFILE)

    &&case foo

        test.value = 0

    &&case FOO.EXE

        test.value = 1

    &&case bar

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("1", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnVersion()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(ExeVersion)

    &&case 0.1

        test.value = 0

    &&case 1.0

        test.value = 1

    &&case 2.0

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "FOOBAR");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("1", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnUsage()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(usage)

    &&case foo

        test.value = 0

    &&case foobar

        test.value = 1

    &&case bar

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("1.0"), "FOOBAR");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("1", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnDefaultVer()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(ExeVersion)

    &&case 0.1

        test.value = 0

    &&case 1.0

        test.value = 1

    &&case 2.0

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("3.0"), "FOOBAR");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("3", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Switch_OnDefaultString()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
test.before = before
&&switch $(Usage)

    &&case foo

        test.value = 0

    &&case bar

        test.value = 1

    &&case foobar

        test.value = 2

    &&default

        test.value = 3

&&endswitch
test.after = after
");
                config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                Assert.AreEqual("before", config.Get("test.before"));
                Assert.AreEqual("after", config.Get("test.after"));
                Assert.AreEqual("3", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ConfigProvider()
        {
            MsgRouter router = null;
            Config config;
            string cfg;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini", "foo=bar");

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = RouterEP=physical://detached/test/$(Guid);CloudEP=$(LillTek.DC.CloudEP)
Config.ExeFile        = foo.exe
";
                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ConfigProvider_Cached()
        {
            MsgRouter router = null;
            Config config;
            string cfg;
            string cacheFile = Path.GetTempPath() + "cached.ini";

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini", "foo=bar");

                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = RouterEP=physical://detached/test/$(Guid);CloudEP=$(LillTek.DC.CloudEP)
Config.ExeFile        = foo.exe
Config.CacheFile      = $(Temp)\cached.ini
";
                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.AreEqual("bar", config.Get("foo"));
                Assert.IsTrue(File.Exists(cacheFile));

                // The settings should be cached now so I'm going to 
                // stop the router and verify that the provider fails
                // over to the cached settings.

                router.Stop();
                router = null;

                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();

                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);
            }
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_ConfigProvider_NoCache()
        {
            MsgRouter router = null;
            Config config;
            string cfg;
            string cacheFile = Path.GetTempPath() + "cached.ini";

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini", "foo=bar");

                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = RouterEP=physical://detached/test/$(Guid);CloudEP=$(LillTek.DC.CloudEP)
Config.ExeFile        = foo.exe
Config.CacheFile      = (no-cache)
";
                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.AreEqual("bar", config.Get("foo"));
                Assert.IsFalse(File.Exists(cacheFile));

                // The settings should not be cached now so I'm going to 
                // stop the router and verify that the provider fails.

                router.Stop();
                router = null;

                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.IsNull(config.Get("foo"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();

                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_IncludeInSwitch()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("app-foo.exe.ini",
@"
&&switch $(Usage)

    &&case foo

        &&include test1.ini

    &&case bar

        &&include test2.ini

    &&case foobar

        &&include test3.ini

    &&default

        &&include test4.ini

&&endswitch
");
                WriteFile("test1.ini", "test.value=1");
                WriteFile("test2.ini", "test.value=2");
                WriteFile("test3.ini", "test.value=3");
                WriteFile("test4.ini", "test.value=4");

                config = Query(router, "machine", "foo.exe", new Version("3.0"), "bar");
                Assert.AreEqual("2", config.Get("test.value"));
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_RecursiveIncludes()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("svr-machine.ini",
@"
&&include test1.ini
");

                WriteFile("test1.ini",
@"
&&include test2.ini
");

                WriteFile("test2.ini",
@"
&&include test1.ini
");

                config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                Assert.Fail("Expected a recursive include error");
            }
            catch (SessionException)
            {
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        public void Error_MaxIncludeDepth()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                WriteFile("svr-machine.ini", "##include test1.ini");
                for (int i = 0; i < 16; i++)
                    WriteFile(string.Format("test{0}.ini", i), string.Format("&&include test{0}.ini", i + 1));

                config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                Assert.Fail("Expected a max include depth error");
            }
            catch (SessionException)
            {
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_NoSettings()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                Assert.Fail("Expected a no settings available error");
            }
            catch (SessionException)
            {
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_NoSwitch()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                try
                {
                    WriteFile("svr-machine.ini", "##endswitch");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##switch error");
                }
                catch (SessionException)
                {
                }

                try
                {
                    WriteFile("svr-machine.ini", "##case foo");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##switch error");
                }
                catch (SessionException)
                {
                }

                try
                {
                    WriteFile("svr-machine.ini", "##default");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##switch error");
                }
                catch (SessionException)
                {
                }
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_MissingEndSwitch()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                try
                {
                    WriteFile("svr-machine.ini", "##switch $(usage)");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##endswitch error");
                }
                catch (SessionException)
                {
                }

                try
                {
                    WriteFile("svr-machine.ini", "##case foo");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##switch error");
                }
                catch (SessionException)
                {

                }

                try
                {
                    WriteFile("svr-machine.ini", "##default");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a missing ##switch error");
                }
                catch (SessionException)
                {
                }
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_MultipleDefault()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                try
                {
                    WriteFile("svr-machine.ini", "##switch $(usage)\r\n##default\r\n##default\r\n##endswitch");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a multiple ##default error");
                }
                catch (SessionException)
                {
                }
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void ConfigServiceHandler_Error_DefaultBeforeCase()
        {
            MsgRouter router = null;
            Config config;

            try
            {
                InitFiles();
                router = CreateRouter();

                try
                {
                    WriteFile("svr-machine.ini",
@"
&&switch $(usage)
    &&default
    &&case 1
&&endswitch");
                    config = Query(router, "machine", "foo.exe", new Version("3.0"), "other");
                    Assert.Fail("Expected a ##case must appear before ##default error");
                }
                catch (SessionException)
                {
                }
            }
            finally
            {
                if (router != null)
                {
                    router.Stop();
                    router = null;
                }

                ClearFiles();
            }
        }
    }
}

