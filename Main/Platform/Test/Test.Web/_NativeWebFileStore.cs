//-----------------------------------------------------------------------------
// FILE:        _NativeWebFileStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Service;
using LillTek.Testing;

namespace LillTek.Web.Test
{

    [TestClass]
    public class _NativeWebFileStore
    {
        private string testPath = @"C:\Temp\Test";
        private string rootPath = @"C:\Temp\Store";
        private Uri rootUri = new Uri("http://lilltek.com");

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_CopyFrom()
        {
            try
            {
                Helper.CreateFolderTree(testPath);

                using (var store = new NativeWebFileStore(rootPath, rootUri))
                {
                    // Copy file from the root of the store

                    File.WriteAllText(Path.Combine(rootPath, "test.txt"), "Hello World!");
                    store.CopyFrom("/test.txt", Path.Combine(testPath, "test1.txt"));

                    Assert.IsTrue(File.Exists(Path.Combine(testPath, "test1.txt")));
                    Assert.AreEqual("Hello World!", File.ReadAllText(Path.Combine(testPath, "test1.txt")));

                    // Copy file from one folder beneath the store root.

                    Helper.CreateFolderTree(Path.Combine(rootPath, "folder"));

                    File.WriteAllText(Path.Combine(rootPath, @"folder\test.txt"), "Hello World!");
                    store.CopyFrom("/folder/test.txt", Path.Combine(testPath, "test2.txt"));

                    Assert.IsTrue(File.Exists(Path.Combine(testPath, "test2.txt")));
                    Assert.AreEqual("Hello World!", File.ReadAllText(Path.Combine(testPath, "test2.txt")));
                }
            }
            finally
            {
                Helper.DeleteFile(rootPath, true);
                Helper.DeleteFile(testPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_CopyTo()
        {
            try
            {
                Helper.CreateFolderTree(testPath);

                using (var store = new NativeWebFileStore(rootPath, rootUri))
                {
                    // Copy file to the root of the store

                    File.WriteAllText(Path.Combine(testPath, "test1.txt"), "Hello World! #1");
                    store.CopyTo(Path.Combine(testPath, "test1.txt"), "/test.txt");

                    Assert.IsTrue(File.Exists(Path.Combine(rootPath, "test.txt")));
                    Assert.AreEqual("Hello World! #1", File.ReadAllText(Path.Combine(rootPath, "test.txt")));

                    // Copy file to one folder beneath the store root.

                    File.WriteAllText(Path.Combine(testPath, "test2.txt"), "Hello World! #2");
                    store.CopyTo(Path.Combine(testPath, "test2.txt"), "/folder/test.txt");

                    Assert.IsTrue(File.Exists(Path.Combine(rootPath, @"folder\test.txt")));
                    Assert.AreEqual("Hello World! #2", File.ReadAllText(Path.Combine(rootPath, @"folder\test.txt")));
                }
            }
            finally
            {
                Helper.DeleteFile(rootPath, true);
                Helper.DeleteFile(testPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_Exists()
        {
            try
            {
                Helper.CreateFolderTree(testPath);

                using (var store = new NativeWebFileStore(rootPath, rootUri))
                {
                    // Copy file to the root of the store

                    File.WriteAllText(Path.Combine(testPath, "test1.txt"), "Hello World! #1");
                    store.CopyTo(Path.Combine(testPath, "test1.txt"), "/test.txt");

                    // Verify that it exists

                    Assert.IsTrue(store.Exists("/test.txt"));

                    // Verify that a non-existant file does not exist

                    Assert.IsFalse(store.Exists("/not-found.txt"));
                }
            }
            finally
            {
                Helper.DeleteFile(rootPath, true);
                Helper.DeleteFile(testPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_Delete()
        {
            try
            {
                Helper.CreateFolderTree(testPath);

                using (var store = new NativeWebFileStore(rootPath, rootUri))
                {
                    // Copy file to the root of the store

                    File.WriteAllText(Path.Combine(testPath, "test1.txt"), "Hello World! #1");
                    store.CopyTo(Path.Combine(testPath, "test1.txt"), "/test.txt");

                    // Verify that it exists

                    Assert.IsTrue(store.Exists("/test.txt"));

                    // Delete it and then verify that its gone.

                    store.Delete("/test.txt");
                    Assert.IsFalse(store.Exists("/test.txt"));

                    // Verify that deleting a non-existant file does not
                    // throw an exception.

                    store.Delete("/test.txt");
                }
            }
            finally
            {
                Helper.DeleteFile(rootPath, true);
                Helper.DeleteFile(testPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_EnumerateFiles()
        {
            try
            {
                Helper.CreateFolderTree(testPath);

                using (var store = new NativeWebFileStore(rootPath, rootUri))
                {
                    // Copy a couple files to the root

                    File.WriteAllText(Path.Combine(testPath, "test1.txt"), "Hello World! #1");
                    store.CopyTo(Path.Combine(testPath, "test1.txt"), "/test1.txt");

                    File.WriteAllText(Path.Combine(testPath, "test2.txt"), "Hello World! #2");
                    store.CopyTo(Path.Combine(testPath, "test2.txt"), "/test2.txt");

                    // Enumerate and verify the files.

                    Dictionary<string, bool> dic = new Dictionary<string, bool>();

                    foreach (var file in store.EnumerateFiles("/", "*.txt"))
                        dic.Add(file, true);

                    Assert.IsTrue(dic.ContainsKey("/test1.txt"));
                    Assert.IsTrue(dic.ContainsKey("/test2.txt"));
                }
            }
            finally
            {
                Helper.DeleteFile(rootPath, true);
                Helper.DeleteFile(testPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_GetUri()
        {
            using (var store = new NativeWebFileStore(rootPath, rootUri))
            {
                Assert.AreEqual("http://lilltek.com/test1.txt", store.GetUri("/test1.txt").ToString());
                Assert.AreEqual("http://lilltek.com/folder/test2.txt", store.GetUri("/folder/test2.txt").ToString());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void NativeWebFileStore_Close()
        {
            // Verify that we get an exception after closing the store.

            var store = new NativeWebFileStore(rootPath, rootUri);

            store.Close();

            try
            {
                store.GetUri("/test.txt");
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }
    }
}

