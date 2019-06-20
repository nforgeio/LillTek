//-----------------------------------------------------------------------------
// FILE:        _AuthServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _AppStoreHandler
    {
        private LeafRouter router = null;
        private string tempFolder = Helper.AddTrailingSlash(Path.GetTempPath()) + "Test\\";
        private string primaryFolder = Helper.AddTrailingSlash(Path.GetTempPath()) + "Test\\Primary\\";
        private string cacheFolder = Helper.AddTrailingSlash(Path.GetTempPath()) + "Test\\Cache\\";
        private string clientFolder = Helper.AddTrailingSlash(Path.GetTempPath()) + "Test\\Client\\";
        private TimeSpan waitTime = TimeSpan.FromSeconds(15);

        [TestInitialize]
        public void Initialize()
        {
            //NetTrace.Start();
            //NetTrace.Enable(MsgRouter.TraceSubsystem,255);
            //NetTrace.Enable(ReliableTransferSession.TraceSubsystem,255);
            //NetTrace.Enable(ClusterMember.TraceSubsystem,255);

            const string settings =
@"
&section MsgRouter

    AppName                = Test
    AppDescription         = Test Description
    RouterEP			   = physical://detached/test/leaf
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

    AbstractMap[abstract://LillTek/DataCenter/AppStore]   = logical://LillTek/DataCenter/AppStore
    AbstractMap[abstract://LillTek/DataCenter/AppStore/*] = logical://LillTek/DataCenter/AppStore/*

&endsection

// Use this section to initialize the primary application store

&section AppStore.Primary

    Mode                = Primary
    PackageFolder       = $(temp)\Test\Primary
    PackageScanInterval = 1s
    PrimaryPollInterval = 0
    PrimaryBroadcast    = no
    BkTaskInterval      = 1s

    &section Cluster

        ClusterBaseEP           = abstract://LillTek/DataCenter/AppStore
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 5s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection

// Use this section to initialize caching application stores

&section AppStore.Cache

    Mode                = Cache
    PackageFolder       = $(temp)\Test\Cache
    PackageScanInterval = 1s
    PrimaryPollInterval = 0
    PrimaryBroadcast    = no
    BkTaskInterval      = 1s

    &section Cluster

        ClusterBaseEP           = abstract://LillTek/DataCenter/AppStore
        Mode                    = Normal
        MasterBroadcastInterval = 1s
        SlaveUpdateInterval     = 1s
        ElectionInterval        = 5s
        MissingMasterCount      = 3
        MissingSlaveCount       = 3
        MasterBkInterval        = 1s
        SlaveBkInterval         = 1s
        BkInterval              = 1s

    &endsection

&endsection
";
            Config.SetConfig(settings.Replace('&', '#'));

            router = new LeafRouter();
            router.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Config.SetConfig(null);

            if (router != null)
                router.Stop();

            NetTrace.Stop();

            //Helper.DeleteFile(tempFolder + "*.*",true);
            //Directory.Delete(tempFolder.Substring(0,tempFolder.Length-1));
        }

        private void InitFolders()
        {
            Helper.CreateFileTree(tempFolder + "test.txt");

            Helper.CreateFileTree(primaryFolder + "Transit\\test.txt");
            Helper.DeleteFile(primaryFolder + "*.*");
            Helper.DeleteFile(primaryFolder + "Transit\\*.*");

            Helper.CreateFileTree(cacheFolder + "Transit\\test.txt");
            Helper.DeleteFile(cacheFolder + "*.*");
            Helper.DeleteFile(cacheFolder + "Transit\\*.*");

            Helper.CreateFileTree(clientFolder + "Transit\\test.txt");
            Helper.CreateFileTree(clientFolder + "Cache\\test.txt");

            Helper.DeleteFile(clientFolder + "*.*");
            Helper.DeleteFile(clientFolder + "Transit\\*.*");
            Helper.DeleteFile(clientFolder + "Cache\\*.*");
        }

        private void WaitForOnline(AppStoreHandler handler, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + TimeSpan.FromSeconds(2);

            while (SysTime.Now < exitTime)
            {
                if (handler.Cluster.IsOnline)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for [IsOnline]");
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

        private AppPackageInfo FindPackage(AppPackageInfo[] infoArr, AppRef appRef)
        {
            foreach (AppPackageInfo info in infoArr)
                if (info.AppRef.Equals(appRef))
                    return info;

            return null;
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
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Primary_List()
        {
            AppStoreHandler primary = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            AppPackageInfo[] infoArr;
            MsgEP instanceEP;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = cacheFolder;
                client.Open(router, settings);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);

                // Verify that GetPrimaryStoreEP() works and that ListRemotePackages()
                // returns an empty list.

                instanceEP = client.GetPrimaryStoreEP(null);
                Assert.AreEqual(primary.Cluster.InstanceEP, instanceEP);
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);

                // Add a couple of packages to the primary store and the verify that ListRemotePackages() returns
                // information about the new packages.

                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4"));
                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8"));
                Thread.Sleep(1000);
                primary.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8")));

                // Delete one of the packages and reverify the list.

                File.Delete(primaryFolder + AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4").FileName);
                Thread.Sleep(1000);
                primary.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.AreEqual(1, infoArr.Length);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8")));
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Primary_Upload()
        {
            AppStoreHandler primary = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            AppPackageInfo[] infoArr;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = cacheFolder;
                client.Open(router, settings);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);

                instanceEP = primary.Cluster.InstanceEP;
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);

                // Upload a package to the primary store and then verify
                // that it got there.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(tempFolder, appRef);

                client.UploadPackage(instanceEP, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                primary.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.IsNotNull(FindPackage(infoArr, appRef));

                EnhancedFileStream es1 = null;
                EnhancedFileStream es2 = null;

                try
                {
                    es1 = new EnhancedFileStream(tempFolder + appRef.FileName, FileMode.Open, FileAccess.Read);
                    es2 = new EnhancedFileStream(primaryFolder + appRef.FileName, FileMode.Open, FileAccess.Read);

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
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Primary_Download()
        {
            AppStoreHandler primary = null;
            AppStoreClient client = new AppStoreClient();
            string tempFile = Path.GetTempFileName();
            AppStoreClientSettings settings;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = cacheFolder;
                client.Open(router, settings);

                // Add a package to the primary store, download it, and then
                // verify that it is valid.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(primaryFolder, appRef);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);

                instanceEP = primary.Cluster.InstanceEP;
                Assert.AreEqual(1, client.ListRemotePackages(instanceEP).Length);

                client.DownloadPackage(instanceEP, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                primary.Scan();

                EnhancedFileStream es1 = null;
                EnhancedFileStream es2 = null;

                try
                {
                    es1 = new EnhancedFileStream(tempFolder + appRef.FileName, FileMode.Open, FileAccess.Read);
                    es2 = new EnhancedFileStream(primaryFolder + appRef.FileName, FileMode.Open, FileAccess.Read);

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
            finally
            {
                Helper.DeleteFile(tempFile);

                if (primary != null)
                    primary.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Primary_Remove()
        {
            AppStoreHandler primary = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = cacheFolder;
                client.Open(router, settings);

                // Add a package to the primary store then remove it and
                // verify that it's gone.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(primaryFolder, appRef);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);

                instanceEP = primary.Cluster.InstanceEP;
                Assert.AreEqual(1, client.ListRemotePackages(instanceEP).Length);

                client.RemoveRemotePackage(instanceEP, appRef);
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_List()
        {
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            AppPackageInfo[] infoArr;
            MsgEP instanceEP;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);

                // Verify that GetPrimaryStoreEP() works with no and that ListRemotePackages()
                // returns an empty list.

                instanceEP = cache.Cluster.InstanceEP;
                Assert.IsNull(client.GetPrimaryStoreEP(null));
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);

                // Add a couple of packages to the store and the verify that ListRemotePackages() returns
                // information about the new packages.

                CreatePackage(cacheFolder, AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4"));
                CreatePackage(cacheFolder, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8"));
                Thread.Sleep(1000);
                cache.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8")));

                // Delete one of the packages and reverify the list.

                File.Delete(cacheFolder + AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4").FileName);
                Thread.Sleep(1000);
                cache.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.AreEqual(1, infoArr.Length);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8")));
            }
            finally
            {
                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_Upload()
        {
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            AppPackageInfo[] infoArr;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);

                instanceEP = cache.Cluster.InstanceEP;
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);

                // Upload a package to the store and then verify
                // that it got there.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(tempFolder, appRef);

                client.UploadPackage(instanceEP, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                cache.Scan();

                infoArr = client.ListRemotePackages(instanceEP);
                Assert.IsNotNull(FindPackage(infoArr, appRef));

                CompareFiles(tempFolder + appRef.FileName, cacheFolder + appRef.FileName);
            }
            finally
            {
                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_Download()
        {
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            string tempFile = Path.GetTempFileName();
            AppStoreClientSettings settings;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add a package to the store, download it, and then
                // verify that it is valid.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(cacheFolder, appRef);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);

                instanceEP = cache.Cluster.InstanceEP;
                Assert.AreEqual(1, client.ListRemotePackages(instanceEP).Length);

                client.DownloadPackage(instanceEP, appRef, tempFolder + appRef.FileName);
                Thread.Sleep(1000);
                cache.Scan();

                CompareFiles(tempFolder + appRef.FileName, cacheFolder + appRef.FileName);
            }
            finally
            {
                Helper.DeleteFile(tempFile);

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_Remove()
        {
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add a package to the store then remove it and
                // verify that it's gone.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(cacheFolder, appRef);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);

                instanceEP = cache.Cluster.InstanceEP;
                Assert.AreEqual(1, client.ListRemotePackages(instanceEP).Length);

                client.RemoveRemotePackage(instanceEP, appRef);
                Assert.AreEqual(0, client.ListRemotePackages(instanceEP).Length);
            }
            finally
            {
                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_DemandLoad()
        {
            // Start primary and cache application store instances.  Disable
            // primary broadcasting and cache sync.  Then add a package to the
            // primary and use an AppStoreClient to download the package
            // from the caching instance, which will need to download the
            // package from the primary first.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP primaryEP;
            MsgEP cacheEP;
            AppRef appRef;
            AppPackageInfo[] infoArr;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add a package to the primary store.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(primaryFolder, appRef);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);
                primaryEP = primary.Cluster.InstanceEP;

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Verify that the appstore instances both return the
                // correct primary endpoint.

                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(primaryEP));
                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(cacheEP));

                // Make sure there are no packages in the cache.

                infoArr = cache.PackageFolder.GetPackages();
                foreach (AppPackageInfo info in infoArr)
                    cache.PackageFolder.Remove(info.AppRef);

                // Download the package from the cache.

                client.DownloadPackage(cacheEP, appRef, tempFolder + "Test.zip");

                // Verify that the cached and downloaded files match the primary file.

                CompareFiles(primaryFolder + appRef.FileName, cacheFolder + appRef.FileName);
                CompareFiles(primaryFolder + appRef.FileName, tempFolder + "Test.zip");
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_DemandLoad_Multiple()
        {
            // Start primary and cache application store instances.  Disable
            // primary broadcasting and cache sync.  Then add a package to the
            // primary and use an AppStoreClient to simultaneously download 
            // the package three times from the caching instance.  Then verify that
            // all packages downloaded OK and also that only one copy of the
            // package was downloaded from the primary by the cache.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP primaryEP;
            MsgEP cacheEP;
            AppRef appRef;
            AppPackageInfo[] infoArr;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add a package to the primary store.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(primaryFolder, appRef);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);
                primaryEP = primary.Cluster.InstanceEP;

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Verify that the appstore instances both return the
                // correct primary endpoint.

                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(primaryEP));
                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(cacheEP));

                // Make sure there are no packages in the cache.

                infoArr = cache.PackageFolder.GetPackages();
                foreach (AppPackageInfo info in infoArr)
                    cache.PackageFolder.Remove(info.AppRef);

                // Download the package three times on parallel threads.

                Assert.AreEqual(0, primary.DownloadCount);
                Assert.AreEqual(0, cache.DownloadCount);

                Thread[] threads = new Thread[3];

                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(new ParameterizedThreadStart(delegate(object path)
                    {
                        client.DownloadPackage(cacheEP, appRef, (string)path);

                    }));

                    threads[i].Start(tempFolder + string.Format("Test{0}.zip", i));
                }

                for (int i = 0; i < threads.Length; i++)
                    threads[i].Join();

                // Verify that the cached and downloaded files match the primary file.

                CompareFiles(primaryFolder + appRef.FileName, cacheFolder + appRef.FileName);

                for (int i = 0; i < threads.Length; i++)
                    CompareFiles(primaryFolder + appRef.FileName, tempFolder + string.Format("Test{0}.zip", i));

                Assert.AreEqual(1, primary.DownloadCount);
                Assert.AreEqual(3, cache.DownloadCount);
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_DemandLoad_NoFile()
        {
            // Start primary and cache application store instances.  Then 
            // attempt to download a package via a cache that doesn't
            // exist and verify that we get an error.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP primaryEP;
            MsgEP cacheEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add a package to the primary store.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);
                primaryEP = primary.Cluster.InstanceEP;

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Verify that the appstore instances both return the
                // correct primary endpoint.

                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(primaryEP));
                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(cacheEP));

                // Make sure there are no packages in either store.

                Assert.AreEqual(0, primary.PackageFolder.GetPackages().Length);
                Assert.AreEqual(0, cache.PackageFolder.GetPackages().Length);

                // Attempt to download a package that doesn't exist.

                try
                {
                    client.DownloadPackage(cacheEP, appRef, tempFolder + "Test.zip");
                    Assert.Fail("Expected a SessionException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(SessionException));
                }
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_DemandLoad_NoPrimary()
        {
            // Start a cache store and the request a non-existent file from
            // the cache without a primary server around.

            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP cacheEP;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                Assert.IsNull(client.GetPrimaryStoreEP(cacheEP));

                // Make sure there are no packages in the cache.

                Assert.AreEqual(0, cache.PackageFolder.GetPackages().Length);

                // Attempt to download a package that doesn't exist.

                try
                {
                    client.DownloadPackage(cacheEP, AppRef.Parse("appref://myapp/test.zip?version=1.0"), tempFolder + "Test.zip");
                    Assert.Fail("Expected a SessionException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(SessionException));
                }
            }
            finally
            {
                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_DemandLoad_PrimaryTimeout()
        {
            // Start a cache store and the request a non-existent file from
            // the cache without a primary server configured to timeout.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP cacheEP;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Wait for the cache to discover the primary and then
                // simulate a primary failure.

                while (cache.PrimaryEP == null)
                    Thread.Sleep(100);

                primary.NetFail = true;

                // Make sure there are no packages in the cache.

                Assert.AreEqual(0, cache.PackageFolder.GetPackages().Length);

                // Attempt to download a package that doesn't exist.

                try
                {
                    client.DownloadPackage(cacheEP, AppRef.Parse("appref://myapp/test.zip?version=1.0"), tempFolder + "Test.zip");
                    Assert.Fail("Expected a SessionException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(SessionException));
                }
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Cache_Sync()
        {
            // Pre-populate the primary and and cache application stores
            // with some packages.  Then start the stores and force the
            // cache to synchronize with the server and verify the result.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP primaryEP;
            MsgEP cacheEP;
            AppPackageInfo[] infoArr;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                // Add packages to the primary store.

                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app01.zip?version=1.0"));
                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app01.zip?version=2.0"));
                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app02.zip?version=1.0"));
                CreatePackage(primaryFolder, AppRef.Parse("appref://myapps/app03.zip?version=1.0"));

                // Add a packages to the caching store, taking care to copy the app02.zip file
                // from the primary folder so the MD5 hashes will match.

                string fileName = AppRef.Parse("appref://myapps/app02.zip?version=1.0").FileName;

                File.Copy(primaryFolder + fileName, cacheFolder + fileName);                       // This shouldn't be downloaded again
                CreatePackage(cacheFolder, AppRef.Parse("appref://myapps/app04.zip?version=1.0")); // This should be deleted

                // Start the stores

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);
                primaryEP = primary.Cluster.InstanceEP;

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Verify the package count before the sync

                Assert.AreEqual(4, primary.PackageFolder.GetPackages().Length);
                Assert.AreEqual(2, cache.PackageFolder.GetPackages().Length);

                // Wait for the cache to discover the primary

                while (cache.PrimaryEP == null)
                    Thread.Sleep(100);

                // Force the sync

                primary.DownloadCount = 0;
                cache.Sync();

                // Validate what happened.

                Assert.AreEqual(3, primary.DownloadCount);   // Only three downloads should have been done since 
                // one package was already present
                infoArr = primary.PackageFolder.GetPackages();
                Assert.AreEqual(4, infoArr.Length);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=2.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app03.zip?version=1.0")));

                infoArr = cache.PackageFolder.GetPackages();
                Assert.AreEqual(4, infoArr.Length);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=2.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app03.zip?version=1.0")));

                // Sync again.  This time no packages should be downloaded because
                // we're already in sync.

                primary.DownloadCount = 0;
                cache.Sync();

                Assert.AreEqual(0, primary.DownloadCount);

                infoArr = cache.PackageFolder.GetPackages();
                Assert.AreEqual(4, infoArr.Length);
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app01.zip?version=2.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app02.zip?version=1.0")));
                Assert.IsNotNull(FindPackage(infoArr, AppRef.Parse("appref://myapps/app03.zip?version=1.0")));

                // One last sync test: I'm going to create new file on the cache
                // with the same appref of an existing file.  The file will
                // have a different MD5 signature due to use of a random number 
                // during the package creation.  I'm going to verify that the
                // modified package is overwritten with the primary file
                // after a sync.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.0");
                CreatePackage(cacheFolder, appRef);
                cache.Scan();

                primary.DownloadCount = 0;
                cache.Sync();

                Assert.AreEqual(1, primary.DownloadCount);
                CollectionAssert.AreEqual(primary.PackageFolder.GetPackageInfo(appRef).MD5, cache.PackageFolder.GetPackageInfo(appRef).MD5);
                CompareFiles(primaryFolder + appRef.FileName, cacheFolder + appRef.FileName);

                // Just for fun, we'll download a package from the cache.

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=2.0");
                client.DownloadPackage(cacheEP, appRef, tempFolder + "Test.zip");

                // Verify that the cached and downloaded files match the primary file.

                CompareFiles(primaryFolder + appRef.FileName, cacheFolder + appRef.FileName);
                CompareFiles(primaryFolder + appRef.FileName, tempFolder + "Test.zip");
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Primary_Broadcast()
        {
            // Start primary and cache application store instances with
            // no packages.  Enable primary broadcasting and then add
            // a package to the primary, and confirm that the cache
            // picks it up.

            AppStoreHandler primary = null;
            AppStoreHandler cache = null;
            AppStoreClient client = new AppStoreClient();
            AppStoreClientSettings settings;
            MsgEP primaryEP;
            MsgEP cacheEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder;
                client.Open(router, settings);

                primary = new AppStoreHandler();
                primary.Start(router, "AppStore.Primary", null, null);
                WaitForOnline(primary, waitTime);
                primaryEP = primary.Cluster.InstanceEP;

                cache = new AppStoreHandler();
                cache.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(cache, waitTime);
                cacheEP = cache.Cluster.InstanceEP;

                Thread.Sleep(2000);

                // Verify that the appstore instances both return the
                // correct primary endpoint.

                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(primaryEP));
                Assert.AreEqual(primaryEP, client.GetPrimaryStoreEP(cacheEP));

                // Verify that both stores are empty

                Assert.AreEqual(0, primary.PackageFolder.GetPackages().Length);
                Assert.AreEqual(0, cache.PackageFolder.GetPackages().Length);

                // Add a package to the primary and then wait a bit for the
                // the cache to handle the sync broadcast.

                primary.PrimaryBroadcast = true;
                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(primaryFolder, appRef);
                primary.Scan();

                // Wait up to a minute

                DateTime waitLimit = SysTime.Now + TimeSpan.FromMinutes(0.25);

                while (SysTime.Now < waitLimit)
                {
                    if (cache.PackageFolder.GetPackages().Length > 0)
                        break;

                    Thread.Sleep(100);
                }

                Assert.AreEqual(1, primary.PackageFolder.GetPackages().Length);
                Assert.AreEqual(1, cache.PackageFolder.GetPackages().Length);

                CompareFiles(primaryFolder + appRef.FileName, cacheFolder + appRef.FileName);
            }
            finally
            {
                if (primary != null)
                    primary.Stop();

                if (cache != null)
                    cache.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AppStoreHandler_Client_Cache()
        {
            // Test the AppStoreClient caching behavior

            AppStoreHandler store = null;
            AppStoreClient client = new AppStoreClient();
            string tempFile = Path.GetTempFileName();
            AppPackage package = null;
            AppStoreClientSettings settings;
            MsgEP instanceEP;
            AppRef appRef;

            try
            {
                InitFolders();

                settings = new AppStoreClientSettings();
                settings.PackageFolder = clientFolder + "Cache";
                settings.LocalCache = true;
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.PurgeInterval = TimeSpan.FromSeconds(1);
                settings.PackageTTL = TimeSpan.FromSeconds(10);
                client.Open(router, settings);

                appRef = AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4");
                CreatePackage(cacheFolder, appRef);

                store = new AppStoreHandler();
                store.Start(router, "AppStore.Cache", null, null);
                WaitForOnline(store, waitTime);

                instanceEP = store.Cluster.InstanceEP;
                Assert.AreEqual(1, client.ListRemotePackages(instanceEP).Length);

                // Download a package for the first time.

                Assert.IsFalse(client.IsCached(appRef));
                package = client.GetPackage(null, appRef);
                Assert.IsNotNull(package);
                Assert.AreEqual(appRef, package.AppRef);
                package.Close();
                package = null;

                // Verify that the next reference comes from the local cache.

                Assert.IsTrue(client.IsCached(appRef));
                store.DownloadCount = 0;
                package = client.GetPackage(null, appRef);
                Assert.AreEqual(0, store.DownloadCount);
                Assert.IsNotNull(package);
                Assert.AreEqual(appRef, package.AppRef);
                package.Close();
                package = null;

                // Wait long enough for the cached package to be purged
                // and then verify that it is gone.

                Thread.Sleep(TimeSpan.FromSeconds(15));
                Assert.IsFalse(client.IsCached(appRef));
                Assert.IsFalse(File.Exists(clientFolder + "Cache\\" + appRef.FileName));
            }
            finally
            {
                Helper.DeleteFile(tempFile);

                if (package != null)
                    package.Close();

                if (store != null)
                    store.Stop();

                if (client != null)
                    client.Close();
            }
        }
    }
}

