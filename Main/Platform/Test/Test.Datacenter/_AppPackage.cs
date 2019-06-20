//-----------------------------------------------------------------------------
// FILE:        _AppPackage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AppPackage
    {
        private string GetTempFileName()
        {
            //if (File.Exists("C:\\Temp\\Test.zip"))
            //    File.Delete("C:\\Temp\\Test.zip");

            //return "C:\\Temp\\Test.zip";

            return Path.GetTempFileName();
        }

        private void Delete(string tempPath)
        {
            if (File.Exists("C:\\Temp\\Test.zip"))
                File.Delete(tempPath);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackage_Basic()
        {
            string tempPath = GetTempFileName();
            string tempDir = Helper.AddTrailingSlash(Path.GetTempPath());
            string testPath = tempDir + "Test.txt";
            AppPackage package = null;
            MemoryStream ms;
            StreamReader reader;

            try
            {
                // Generate the package

                package = AppPackage.Create(tempPath, AppRef.Parse("appref://myapps/mypackage.zip?version=1.2.3.4"), @"
LaunchType   = Test.MyType:MyAssembly.dll;
LaunchMethod = Foo;
LaunchArgs   = Bar;
");

                using (StreamWriter writer = new StreamWriter(testPath))
                {
                    for (int i = 0; i < 4000; i++)
                        writer.WriteLine("Hello World!");
                }

                package.AddFile(testPath, tempDir);

                using (ms = new MemoryStream(4096))
                {
                    for (int i = 0; i < 4096; i++)
                        ms.WriteByte((byte)i);

                    ms.Position = 0;
                    package.AddFile("Test.dat", ms);
                }

                package.Close();
                package = null;

                // Verify that the package can be opened and the contents look good.

                package = AppPackage.Open(tempPath);

                Assert.AreEqual(new AppRef("appref://myapps/mypackage.zip?version=1.2.3.4"), package.AppRef);
                Assert.AreEqual(new Version(1, 2, 3, 4), package.Version);

                Assert.AreEqual("Test.MyType:MyAssembly.dll", package.Settings.Get("LaunchType"));
                Assert.AreEqual("Foo", package.Settings.Get("LaunchMethod"));
                Assert.AreEqual("Bar", package.Settings.Get("LaunchArgs"));

                Assert.IsTrue(package.ContainsFile("Test.txt"));
                Assert.IsTrue(package.ContainsFile("Test.dat"));

                ms = new MemoryStream();
                package.CopyFile("Test.txt", ms);
                ms.Position = 0;
                reader = new StreamReader(ms);
                for (int i = 0; i < 4000; i++)
                    Assert.AreEqual("Hello World!", reader.ReadLine());

                string tempPath2 = Path.GetTempFileName();

                try
                {
                    using (var fs = new FileStream(tempPath2, FileMode.Create, FileAccess.ReadWrite))
                    {
                        package.CopyFile("test.dat", fs);
                        Assert.AreEqual(4096, fs.Length);
                        fs.Position = 0;

                        for (int i = 0; i < 4096; i++)
                            Assert.AreEqual((byte)i, fs.ReadByte());
                    }
                }
                finally
                {
                    File.Delete(tempPath2);
                }

                package.Close();
                package = null;
            }
            finally
            {
                if (package != null)
                    package.Close();

                Delete(tempPath);
                Delete(testPath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackage_MD5()
        {
            string tempPath = GetTempFileName();
            string tempDir = Helper.AddTrailingSlash(Path.GetTempPath());
            AppPackage package = null;
            MemoryStream ms;
            byte[] md5;

            try
            {
                package = AppPackage.Create(tempPath, AppRef.Parse("appref://myapps/mypackage.zip?version=1.2.3.4"), @"
LaunchType   = Test.MyType:MyAssembly.dll;
LaunchMethod = Foo;
LaunchArgs   = Bar;
");
                using (ms = new MemoryStream(4096))
                {
                    for (int i = 0; i < 4096; i++)
                        ms.WriteByte((byte)i);

                    ms.Position = 0;
                    package.AddFile("Test.dat", ms);
                }

                package.Close();
                package = null;

                // Verify that a MD5 hash computed manually on the package file jibes
                // with what AppPackage computes.

                using (var fs = new EnhancedFileStream(tempPath, FileMode.Open, FileAccess.Read))
                {
                    md5 = MD5Hasher.Compute(fs, fs.Length);
                }

                package = AppPackage.Open(tempPath);
                CollectionAssert.AreEqual(md5, package.MD5);

                package.Close();
                package = null;
            }
            finally
            {
                if (package != null)
                    package.Close();

                Delete(tempPath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackage_ExtractTo()
        {
            string tempPath = GetTempFileName();
            string tempDir = Helper.AddTrailingSlash(Path.GetTempPath());
            AppPackage package = null;

            try
            {
                package = AppPackage.Create(tempPath, AppRef.Parse("appref://myapps/mypackage.zip?version=1.2.3.4"), @"
LaunchType   = Test.MyType:MyAssembly.dll;
LaunchMethod = Foo;
LaunchArgs   = Bar;
");
                package.AddFile("File1.txt", Helper.ToUTF8("Hello World! #1\r\n"));
                package.AddFile("File2.txt", Helper.ToUTF8("Hello World! #2\r\n"));
                package.Close();
                package = null;

                package = AppPackage.Open(tempPath);
                package.ExtractTo(tempDir);
                package.Close();
                package = null;

                Assert.IsTrue(File.Exists(tempDir + "Package.ini"));
                Assert.IsTrue(File.Exists(tempDir + "File1.txt"));
                Assert.IsTrue(File.Exists(tempDir + "File2.txt"));

                using (var reader = new StreamReader(tempDir + "File1.txt"))
                {
                    Assert.AreEqual("Hello World! #1", reader.ReadLine());
                }

                using (var reader = new StreamReader(tempDir + "File2.txt"))
                {

                    Assert.AreEqual("Hello World! #2", reader.ReadLine());
                }
            }
            finally
            {
                if (package != null)
                    package.Close();

                Delete(tempPath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppPackage_MultiRead()
        {
            // Verify that a package can be opened for reading by multiple streams.

            string tempPath = GetTempFileName();
            string tempDir = Helper.AddTrailingSlash(Path.GetTempPath());
            AppRef appRef = AppRef.Parse("appref://myapps/mypackage.zip?version=1.2.3.4");
            AppPackage pack1 = null;
            AppPackage pack2 = null;
            FileStream fs = null;
            MemoryStream ms;

            try
            {
                pack1 = AppPackage.Create(tempPath, appRef, @"
LaunchType   = Test.MyType:MyAssembly.dll;
LaunchMethod = Foo;
LaunchArgs   = Bar;
");
                using (ms = new MemoryStream(4096))
                {
                    for (int i = 0; i < 4096; i++)
                        ms.WriteByte((byte)i);

                    ms.Position = 0;
                    pack1.AddFile("Test.dat", ms);
                }

                pack1.Close();
                pack1 = null;

                pack1 = AppPackage.Open(tempPath);
                pack2 = AppPackage.Open(tempPath);
                fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            }
            finally
            {
                if (pack1 != null)
                    pack1.Close();

                if (pack2 != null)
                    pack2.Close();

                if (fs != null)
                    fs.Close();

                Delete(tempPath);
            }
        }
    }
}

