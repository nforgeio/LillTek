//-----------------------------------------------------------------------------
// FILE:        _AppPackageFolder.cs
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
    public class _AppPackageFolder : ILockable
    {
        string tempFolder;
        TimeSpan changeDetectTime = AppPackageFolder.RetryTime + TimeSpan.FromSeconds(1);

        [TestInitialize]
        public void Initialize()
        {
            tempFolder = Helper.AddTrailingSlash(Path.GetTempPath()) + Guid.NewGuid().ToString();
            // tempFolder = Helper.AddTrailingSlash("c:\\temp") + "test";

            Helper.CreateFolderTree(tempFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Helper.DeleteFile(tempFolder, true);
        }

        private void CreatePackage(AppRef appRef)
        {
            AppPackage package;

            package = AppPackage.Create(tempFolder + "\\" + appRef.FileName, appRef, @"
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

        private void CreatePackageAt(string path, AppRef appRef)
        {
            AppPackage package;

            package = AppPackage.Create(path, appRef, @"
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

        private void Delete(string fileName)
        {
            Thread.Sleep(1000);     // Wait a bit to ensure that a folder from
                                    // a previous test has stopped scanning.

            File.Delete(tempFolder + "\\" + fileName);
        }

        private void DeleteAll()
        {
            Thread.Sleep(1000);     // Wait a bit to ensure that a folder from
                                    // a previous test has stopped scanning.

            Helper.DeleteFile(tempFolder + "\\*.*");
        }

        private void Touch(string fileName)
        {
            File.SetLastWriteTimeUtc(tempFolder + "\\" + fileName, DateTime.UtcNow);
        }

        private void GetFileInfo(string fileName, out int size, out byte[] md5)
        {
            using (var es = new EnhancedFileStream(tempFolder + "\\" + fileName, FileMode.Open, FileAccess.Read))
            {
                size = (int)es.Length;
                md5 = MD5Hasher.Compute(es, size);
            }
        }

        private AppPackageInfo FindFile(AppPackageInfo[] infoArr, string fileName)
        {
            foreach (AppPackageInfo info in infoArr)
                if (String.Compare(info.FileName, fileName, true) == 0)
                    return info;

            return null;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Scan_Empty()
        {
            DeleteAll();

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                Assert.AreEqual(0, folder.GetPackages().Length);
                folder.Scan();
                Assert.AreEqual(0, folder.GetPackages().Length);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Scan_Two()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;
                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Scan_IgnoreBad()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var fs = new FileStream(tempFolder + "\\BadFile.zip", FileMode.Create, FileAccess.ReadWrite))
            {
                fs.WriteByte(10);
            }

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Detect_Add()
        {
            DeleteAll();

            using (var folder = new AppPackageFolder(this, tempFolder))
            {

                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;

                folder.Scan();
                Assert.AreEqual(0, folder.GetPackages().Length);

                CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
                Thread.Sleep(changeDetectTime);

                infoArr = folder.GetPackages();
                Assert.AreEqual(1, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));
                Thread.Sleep(changeDetectTime);

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Detect_Delete()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                Thread.Sleep(changeDetectTime);
                Delete("myapps.test1-0001.0002.0003.0004.zip");
                Thread.Sleep(changeDetectTime);

                infoArr = folder.GetPackages();
                Assert.AreEqual(1, infoArr.Length);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                Delete(info.FileName);
                Thread.Sleep(changeDetectTime);
                Assert.AreEqual(0, folder.GetPackages().Length);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Detect_Change()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;

                Thread.Sleep(2000);
                using (var fs = new FileStream(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", FileMode.Open, FileAccess.ReadWrite))
                {
                    byte b;

                    b = (byte)fs.ReadByte();
                    fs.Position = 0;
                    fs.WriteByte((byte)~b);
                    fs.Position = 0;
                    fs.WriteByte(b);
                }

                Thread.Sleep(changeDetectTime);

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Detect_Rename()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                int size;
                byte[] md5;

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                Thread.Sleep(changeDetectTime);
                File.Move(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", tempFolder + "\\hello.zip");
                Thread.Sleep(changeDetectTime);

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                Assert.IsNull(FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip"));

                info = FindFile(infoArr, "hello.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("hello.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\hello.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                info = FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip");
                Assert.IsNotNull(info);
                Assert.AreEqual("myapps.test2-0005.0006.0007.0008.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test2-0005.0006.0007.0008.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Remove()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                Assert.IsNotNull(FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip"));
                Assert.IsNotNull(FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip"));

                folder.Remove(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

                infoArr = folder.GetPackages();
                Assert.AreEqual(1, infoArr.Length);

                Assert.IsNotNull(FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip"));
                Assert.IsNull(FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip"));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Clear()
        {
            DeleteAll();

            CreatePackage(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"));
            CreatePackage(new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8"));

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppPackageInfo[] infoArr;

                infoArr = folder.GetPackages();
                Assert.AreEqual(2, infoArr.Length);

                Assert.IsNotNull(FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip"));
                Assert.IsNotNull(FindFile(infoArr, "myapps.test2-0005.0006.0007.0008.zip"));

                folder.Clear();

                infoArr = folder.GetPackages();
                Assert.AreEqual(0, infoArr.Length);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackageFolder_Transit()
        {
            DeleteAll();

            using (var folder = new AppPackageFolder(this, tempFolder))
            {
                AppRef appRef;
                AppPackageInfo[] infoArr;
                AppPackageInfo info;
                string path;
                int size;
                byte[] md5;

                Assert.AreEqual(0, folder.GetPackages().Length);

                appRef = new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4");
                path = folder.BeginTransit(appRef);
                CreatePackageAt(path, appRef);

                Assert.AreEqual(0, folder.GetPackages().Length);
                folder.Scan();
                infoArr = folder.GetPackages();
                Assert.AreEqual(0, folder.GetPackages().Length);

                folder.EndTransit(path, true);

                infoArr = folder.GetPackages();
                Assert.AreEqual(1, infoArr.Length);

                info = FindFile(infoArr, "myapps.test1-0001.0002.0003.0004.zip");
                Assert.IsNotNull(infoArr);
                Assert.AreEqual("myapps.test1-0001.0002.0003.0004.zip", info.FileName);
                Assert.AreEqual(tempFolder + "\\myapps.test1-0001.0002.0003.0004.zip", info.FullPath);
                Assert.AreEqual(new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4"), info.AppRef);
                GetFileInfo(info.FileName, out size, out md5);
                Assert.AreEqual(size, info.Size);
                CollectionAssert.AreEqual(md5, info.MD5);

                // Try to overwrite an existing package

                appRef = new AppRef("appref://MyApps/Test1.zip?version=1.2.3.4");
                path = folder.BeginTransit(appRef);
                CreatePackageAt(path, appRef);
                folder.EndTransit(path, true);

                // Try cancelling a commit

                appRef = new AppRef("appref://MyApps/Test2.zip?version=5.6.7.8");
                path = folder.BeginTransit(appRef);
                CreatePackageAt(path, appRef);
                folder.EndTransit(path, false);

                Assert.IsFalse(File.Exists(path));

                Assert.IsNull(FindFile(folder.GetPackages(), "myapps.test2-0005.0006.0007.0008.zip"));
                folder.Scan();
                Assert.IsNull(FindFile(folder.GetPackages(), "myapps.test2-0005.0006.0007.0008.zip"));
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}

