//-----------------------------------------------------------------------------
// FILE:        App_AppStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Datacenter.AppStore;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.AppStore.Test
{
    [TestClass]
    public class App_AppStore
    {
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

        private void CreatePackage(string folder, AppRef appRef)
        {
            AppPackage package;

            package = AppPackage.Create(Helper.AddTrailingSlash(folder) + appRef.FileName, appRef, @"
LaunchType   = Test.MyType:MyAssembly.dll;
LaunchMethod = Foo;
LaunchArgs   = Bar;
");
            package.AddFile("Test1.txt", Helper.ToUTF8("Hello World!\r\n"));

            byte[] buf = new byte[1000000];

            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)Helper.Rand();  // Use Rand() to disable compression

            package.AddFile("Test2.dat", buf);
            package.Close();
        }

        private void CompareFiles(string path1, string path2)
        {
            EnhancedFileStream es1 = null;
            EnhancedFileStream es2 = null;

            try
            {
                es1 = new EnhancedFileStream(path1, FileMode.Open, FileAccess.Read);
                es2 = new EnhancedFileStream(path2, FileMode.Open, FileAccess.Read);

                Assert.AreEqual(es1.Length, es2.Length);
                CollectionAssert.AreEqual(MD5Hasher.Compute(es1, es1.Length), MD5Hasher.Compute(es2, es2.Length));
            }
            finally
            {
                if (es1 != null)
                    es1.Close();

                if (es2 != null)
                    es2.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void AppStore_EndToEnd()
        {
            // This test peforms a simple end-to-end test of the AppStore
            // service by starting the service, uploading, downloading
            // and then deleting a package file.

            Process         svcProcess = null;
            LeafRouter      router     = null;
            AppStoreClient  client     = null;
            Assembly        assembly;
            string          packageFolder;
            string          tempFolder;
            AppRef          appRef;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            assembly      = typeof(LillTek.Datacenter.AppStore.Program).Assembly;
            tempFolder    = Helper.AddTrailingSlash(Path.GetTempPath());
            packageFolder = Helper.GetAssemblyFolder(assembly) + "packages\\";
            appRef        = AppRef.Parse("appref://MyApps/App00.zip?version=1.2.0.0");

            try
            {
                // Start a local router and open a client.

                Config.SetConfig(@"

//-----------------------------------------------------------------------------
// LeafRouter Settings

&section MsgRouter

    AppName                = LillTek.Test Router
    AppDescription         = Unit Test
    RouterEP			   = physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
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
    Encryption		 	   = PLAINTEXT
    Key					   = 00
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s
    
    // This maps the abstract AppStore endpoints to their default logical endpoints.

    AbstractMap[abstract://LillTek/DataCenter/AppStore]   = logical://LillTek/DataCenter/AppStore
    AbstractMap[abstract://LillTek/DataCenter/AppStore/*] = logical://LillTek/DataCenter/AppStore/*

&endsection

".Replace('&', '#'));

                router = new LeafRouter();
                router.Start();

                client = new AppStoreClient();
                client.Open(router, AppStoreClientSettings.LoadConfig("AppStore.Client"));

                // Start the application store service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

                // Upload a package to the server

                CreatePackage(tempFolder, appRef);
                client.UploadPackage(null, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                CompareFiles(tempFolder + appRef.FileName, packageFolder + appRef.FileName);

                // Download the package

                File.Delete(tempFolder + appRef.FileName);
                client.DownloadPackage(null, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                CompareFiles(tempFolder + appRef.FileName, packageFolder + appRef.FileName);

                // Delete the package

                client.RemoveRemotePackage(null, appRef);
                Thread.Sleep(1000);
                Assert.IsFalse(File.Exists(packageFolder + appRef.FileName));
            }
            finally
            {
                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (client != null)
                    client.Close();

                if (router != null)
                    router.Stop();

                if (File.Exists(tempFolder + appRef.FileName))
                    File.Delete(tempFolder + appRef.FileName);

                Config.SetConfig(null);
            }
        }
    }
}

