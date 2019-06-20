//-----------------------------------------------------------------------------
// FILE:        App_ConfigService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Messaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.ConfigService.Test
{
    [TestClass]
    public class App_ConfigService
    {
        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void ConfigService_EndToEnd()
        {
            // This test peforms an end-to-end test of the Config Service
            // by writing a config file to the application folder and then
            // verifying that a ConfigServiceProvider can retrieve the 
            // settings.

            Process svcProcess = null;
            string cfg;
            Assembly assembly;
            string configFile;
            StreamWriter writer;
            Config config;

            assembly = typeof(LillTek.Datacenter.ConfigService.Program).Assembly;
            configFile = Helper.GetAssemblyFolder(assembly) + "svr-machine.ini";

            try
            {
                writer = new StreamWriter(configFile);
                writer.Write("foo=bar");
                writer.Close();

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                cfg =
@"
Config.CustomProvider = {0}:{1}
Config.Settings       = RouterEP=physical://detached/$(LillTek.DC.DefHubName)/$(Guid);CloudEP=$(LillTek.DC.CloudEP)
Config.MachineName    = machine
Config.CacheFile      = (no-cache)
";
                Helper.InitializeApp(Assembly.GetExecutingAssembly());
                Config.SetConfig(string.Format(cfg, typeof(ConfigServiceProvider).FullName, typeof(ConfigServiceProvider).Assembly.Location));
                config = new Config();

                Assert.AreEqual("bar", config.Get("foo"));
            }
            finally
            {
                if (File.Exists(configFile))
                    File.Delete(configFile);

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }
            }
        }
    }
}

