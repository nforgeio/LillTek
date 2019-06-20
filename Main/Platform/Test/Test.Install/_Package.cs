//-----------------------------------------------------------------------------
// FILE:        _Package.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 
 
using System;
using System.IO;
using System.Threading;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;
using LillTek.Windows;

namespace LillTek.Install.Test 
{
    [TestClass]
    public class _Package
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_Empty() 
        {
            Package                 package;
            PackageEntry            root;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();

            //-------------------------

            package = new Package();
            package.Create(es);

            root    = package.RootFolder;
            Assert.IsNotNull(root);
            Assert.IsNull(root.Parent);
            Assert.AreSame(root,package["/"]);
            Assert.IsNull(root["foo"]);
            Assert.IsTrue(root.IsFolder);
            Assert.AreEqual(0,root.Children.Length);
            Assert.AreEqual("/",root.FullName);
            Assert.AreEqual("",root.Name);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);
            Assert.IsNotNull(root);
            root = package.RootFolder;
            Assert.AreSame(root,package["/"]);
            Assert.IsNull(root["foo"]);
            Assert.AreEqual(0,root.Children.Length);

            package.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_Root_Relative()
        {
            Package                 package;
            PackageEntry            root;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            test1,test2;
            bool                    ok;

            //-------------------------

            package = new Package();
            package.Create(es);

            root  = package.RootFolder;
            test1 = root.AddFile("Test1.dat",new byte[] {1,1,1,1});
            test2 = root.AddFile("Test2.dat",new byte[] {2,2,2,2});
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,test1.Parent);
            Assert.AreSame(root,test2.Parent);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Test1.dat",test1.FullName);
            Assert.AreEqual("/Test2.dat",test2.FullName);
            Assert.AreSame(test1,package["/Test1.dat"]);
            Assert.AreSame(test2,package["/Test2.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            root  = package.RootFolder;
            test1 = root["Test1.dat"];
            test2 = root["Test2.dat"];
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,test1.Parent);
            Assert.AreSame(root,test2.Parent);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Test1.dat",test1.FullName);
            Assert.AreEqual("/Test2.dat",test2.FullName);
            Assert.AreSame(test1,package["/Test1.dat"]);
            Assert.AreSame(test2,package["/Test2.dat"]);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, test1.GetContents());
            CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, test2.GetContents());

            package.Close();
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_Root_Absolute() 
        {
            Package                 package;
            PackageEntry            root;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            test1,test2;
            bool                    ok;

            //-------------------------

            package = new Package();
            package.Create(es);

            root  = package.RootFolder;
            test1 = package.AddFile("/Test1.dat",new byte[] {1,1,1,1});
            test2 = package.AddFile("/Test2.dat",new byte[] {2,2,2,2});
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,test1.Parent);
            Assert.AreSame(root,test2.Parent);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Test1.dat",test1.FullName);
            Assert.AreEqual("/Test2.dat",test2.FullName);
            Assert.AreSame(test1,package["/Test1.dat"]);
            Assert.AreSame(test2,package["/Test2.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            root  = package.RootFolder;
            test1 = package["/Test1.dat"];
            test2 = package["/Test2.dat"];
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,test1.Parent);
            Assert.AreSame(root,test2.Parent);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Test1.dat",test1.FullName);
            Assert.AreEqual("/Test2.dat",test2.FullName);
            Assert.AreSame(test1,package["/Test1.dat"]);
            Assert.AreSame(test2,package["/Test2.dat"]);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, test1.GetContents());
            CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, test2.GetContents());

            package.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_Folders_Relative() 
        {
            Package                 package;
            PackageEntry            root;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            folder1,folder2,folder3;
            PackageEntry            test1,test2,test3;
            bool                    ok;

            //-------------------------

            package = new Package();
            package.Create(es);

            root    = package.RootFolder;
            folder1 = root.AddFolder("Folder1");
            folder2 = root.AddFolder("Folder2");
            folder3 = folder2.AddFolder("Folder3");
            test1   = folder1.AddFile("Test1.dat",new byte[] {1,1,1,1});
            test2   = folder2.AddFile("Test2.dat",new byte[] {2,2,2,2});
            test3   = folder3.AddFile("Test3.dat",new byte[] {3,3,3,3});
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,folder1.Parent);
            Assert.AreSame(root,folder2.Parent);
            Assert.AreSame(folder1,test1.Parent);
            Assert.AreSame(folder2,test2.Parent);
            Assert.AreSame(folder3,test3.Parent);
            Assert.AreEqual("Folder1",folder1.Name);
            Assert.AreEqual("Folder2",folder2.Name);
            Assert.AreEqual("Folder3",folder3.Name);
            Assert.AreEqual("/Folder1",folder1.FullName);
            Assert.AreEqual("/Folder2",folder2.FullName);
            Assert.AreEqual("/Folder2/Folder3",folder3.FullName);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Folder1/Test1.dat",test1.FullName);
            Assert.AreEqual("/Folder2/Test2.dat",test2.FullName);
            Assert.AreSame(test1,folder1["Test1.dat"]);
            Assert.AreSame(test2,folder2["Test2.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder1.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder2.Children)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            root    = package.RootFolder;
            root    = package.RootFolder;
            folder1 = root["Folder1"];
            folder2 = root["Folder2"];
            folder3 = folder2["Folder3"];
            test1   = folder1["Test1.dat"];
            test2   = folder2["Test2.dat"];
            test3   = folder3["Test3.dat"];
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,folder1.Parent);
            Assert.AreSame(root,folder2.Parent);
            Assert.AreSame(folder1,test1.Parent);
            Assert.AreSame(folder2,test2.Parent);
            Assert.AreSame(folder3,test3.Parent);
            Assert.AreEqual("Folder1",folder1.Name);
            Assert.AreEqual("Folder2",folder2.Name);
            Assert.AreEqual("Folder3",folder3.Name);
            Assert.AreEqual("/Folder1",folder1.FullName);
            Assert.AreEqual("/Folder2",folder2.FullName);
            Assert.AreEqual("/Folder2/Folder3",folder3.FullName);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("/Folder1/Test1.dat",test1.FullName);
            Assert.AreEqual("/Folder2/Test2.dat",test2.FullName);
            Assert.AreSame(test1,folder1["Test1.dat"]);
            Assert.AreSame(test2,folder2["Test2.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder1.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder2.Children)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, test1.GetContents());
            CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, test2.GetContents());
            CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, test3.GetContents());

            package.Close();
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_Folders_Absolute() 
        {
            Package                 package;
            PackageEntry            root;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            folder1,folder2,folder3;
            PackageEntry            test1,test2,test3;
            bool                    ok;

            //-------------------------

            package = new Package();
            package.Create(es);

            root    = package.RootFolder;
            folder1 = package.AddFolder("/Folder1");
            folder2 = package.AddFolder("/Folder2");
            folder3 = package.AddFolder("/Folder2/Folder3");
            test1   = package.AddFile("/Folder1/Test1.dat",new byte[] {1,1,1,1});
            test2   = package.AddFile("/Folder2/Test2.dat",new byte[] {2,2,2,2});
            test3   = package.AddFile("/Folder2/Folder3/Test3.dat",new byte[] {3,3,3,3});
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,folder1.Parent);
            Assert.AreSame(root,folder2.Parent);
            Assert.AreSame(folder1,test1.Parent);
            Assert.AreSame(folder2,test2.Parent);
            Assert.AreSame(folder3,test3.Parent);
            Assert.AreEqual("Folder1",folder1.Name);
            Assert.AreEqual("Folder2",folder2.Name);
            Assert.AreEqual("Folder3",folder3.Name);
            Assert.AreEqual("/Folder1",folder1.FullName);
            Assert.AreEqual("/Folder2",folder2.FullName);
            Assert.AreEqual("/Folder2/Folder3",folder3.FullName);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("Test3.dat",test3.Name);
            Assert.AreEqual("/Folder1/Test1.dat",test1.FullName);
            Assert.AreEqual("/Folder2/Test2.dat",test2.FullName);
            Assert.AreEqual("/Folder2/Folder3/Test3.dat",test3.FullName);
            Assert.AreSame(test1,folder1["Test1.dat"]);
            Assert.AreSame(test2,folder2["Test2.dat"]);
            Assert.AreSame(test3,folder3["Test3.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder1.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder2.Children)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            root    = package.RootFolder;
            root    = package.RootFolder;
            folder1 = root["Folder1"];
            folder2 = root["Folder2"];
            folder3 = folder2["Folder3"];
            test1   = folder1["Test1.dat"];
            test2   = folder2["Test2.dat"];
            test3   = folder3["Test3.dat"];
            Assert.AreEqual(2,root.Children.Length);
            Assert.AreSame(root,folder1.Parent);
            Assert.AreSame(root,folder2.Parent);
            Assert.AreSame(folder1,test1.Parent);
            Assert.AreSame(folder2,test2.Parent);
            Assert.AreSame(folder3,test3.Parent);
            Assert.AreEqual("Folder1",folder1.Name);
            Assert.AreEqual("Folder2",folder2.Name);
            Assert.AreEqual("Folder3",folder3.Name);
            Assert.AreEqual("/Folder1",folder1.FullName);
            Assert.AreEqual("/Folder2",folder2.FullName);
            Assert.AreEqual("/Folder2/Folder3",folder3.FullName);
            Assert.AreEqual("Test1.dat",test1.Name);
            Assert.AreEqual("Test2.dat",test2.Name);
            Assert.AreEqual("Test3.dat",test3.Name);
            Assert.AreEqual("/Folder1/Test1.dat",test1.FullName);
            Assert.AreEqual("/Folder2/Test2.dat",test2.FullName);
            Assert.AreEqual("/Folder2/Folder3/Test3.dat",test3.FullName);
            Assert.AreSame(test1,folder1["Test1.dat"]);
            Assert.AreSame(test2,folder2["Test2.dat"]);
            Assert.AreSame(test3,folder3["Test3.dat"]);
            
            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root)
                if (entry == folder2)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in root.Children)
                if (entry == folder1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder1.Children)
                if (entry == test1)
                    ok = true;
            
            Assert.IsTrue(ok);

            ok = false;
            foreach (PackageEntry entry in folder2.Children)
                if (entry == test2)
                    ok = true;
            
            Assert.IsTrue(ok);

            CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, test1.GetContents());
            CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, test2.GetContents());
            CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, test3.GetContents());

            package.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_AutoAddFolders()
        {
            Package                 package;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            entry;

            //-------------------------

            package = new Package();
            package.Create(es);

            entry = package.AddFile("/Foo/Bar/Test1.dat",new byte[] {1,1,1,1});
            Assert.IsTrue(entry.IsFile);
            Assert.IsTrue(package["/Foo"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            Assert.IsTrue(entry.IsFile);
            Assert.IsTrue(package["/Foo"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);
            CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, package["/Foo/Bar/Test1.dat"].GetContents());

            package.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_AddStream() 
        {
            Package                 package;
            EnhancedBlockStream     bs = new EnhancedBlockStream();
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            entry;
            byte[]                  buf;

            buf = new byte[37000];
            for (int i=0;i<buf.Length;i++)
                buf[i] = (byte) i;

            //-------------------------

            package = new Package();
            package.Create(es);

            bs.WriteBytes32(buf);
            bs.Position = 0;

            entry = package.AddFile("/Foo/Bar/Test1.dat",bs,(int) bs.Length);
            Assert.IsTrue(bs.Eof);
            Assert.IsTrue(entry.IsFile);
            Assert.IsTrue(package["/Foo"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

            package.Close(true);

            //-------------------------

            es.Position = 0;
            package = new Package(es);

            Assert.IsTrue(entry.IsFile);
            Assert.IsTrue(package["/Foo"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

            bs.SetLength(0);
            package["/Foo/Bar/Test1.dat"].GetContents(bs);

            bs.Position = 0;
            CollectionAssert.AreEqual(buf, bs.ReadBytes32());

            package.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_AddFile() 
        {
            Package                 package;
            EnhancedStream          fs;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            entry;
            byte[]                  buf;
            string                  inputFile;
            string                  outputFile;

            inputFile  = Path.GetTempFileName();
            outputFile = Path.GetTempFileName();
            try 
            {
                buf = new byte[37000];
                for (int i=0;i<buf.Length;i++)
                    buf[i] = (byte) i;

                fs = new EnhancedFileStream(inputFile,FileMode.OpenOrCreate);
                fs.WriteBytes32(buf);
                fs.Close();

                //-------------------------

                package = new Package();
                package.Create(es);

                entry = package.AddFile("/Foo/Bar/Test1.dat",inputFile);
                Assert.IsTrue(entry.IsFile);
                Assert.IsTrue(package["/Foo"].IsFolder);
                Assert.IsTrue(package["/Foo/Bar"].IsFolder);
                Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

                package.Close(true);

                //-------------------------

                package = new Package(es);

                Assert.IsTrue(entry.IsFile);
                Assert.IsTrue(package["/Foo"].IsFolder);
                Assert.IsTrue(package["/Foo/Bar"].IsFolder);
                Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

                package["/Foo/Bar/Test1.dat"].GetContents(outputFile);

                fs = new EnhancedFileStream(outputFile,FileMode.Open);
                try
                {
                    CollectionAssert.AreEqual(buf,fs.ReadBytes32());
                }
                finally 
                {
                    fs.Close();
                }

                package.Close();
            }
            finally
            {
                File.Delete(inputFile);
                File.Delete(outputFile);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_HashVerify()
        {
            Package                 package;
            EnhancedBlockStream     bs = new EnhancedBlockStream();
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            PackageEntry            entry;
            byte[]                  buf;
            byte                    v;

            buf = new byte[37000];
            for (int i=0;i<buf.Length;i++)
                buf[i] = (byte) i;

            //-------------------------

            package = new Package();
            package.Create(es);

            bs.WriteBytes32(buf);
            bs.Position = 0;

            entry = package.AddFile("/Foo/Bar/Test1.dat",bs,(int) bs.Length);
            Assert.IsTrue(bs.Eof);
            Assert.IsTrue(entry.IsFile);
            Assert.IsTrue(package["/Foo"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar"].IsFolder);
            Assert.IsTrue(package["/Foo/Bar/Test1.dat"].IsFile);

            package.Close(true);

            es.Position = es.Length/2;  // Corrupt a byte in the middle of the stream
                                        // to verify that the MD5 hash comparision
                                        // catches it.
            v = (byte) es.ReadByte();
            es.Seek(-1,SeekOrigin.Current);
            es.WriteByte((byte) ~v);

            es.Position = 0;

            try
            {
                package = new Package(es);
                Assert.Fail();
            }
            catch (PackageException)
            {
            }
        }

        private void WriteFile(string path, byte[] data) 
        {
            var es = new EnhancedFileStream(path,FileMode.Create);

            es.WriteBytesNoLen(data);
            es.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_AddFiles_Absolute() 
        {
            Package                 package;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            string                  tempDir;
            string                  tempPath;

            tempDir  = Path.GetTempPath() + Helper.NewGuid().ToString();
            tempPath = tempDir + "\\";
            Directory.CreateDirectory(tempDir);

            WriteFile(tempPath + "Test1.dat",new byte[] {1,1,1,1});
            WriteFile(tempPath + "Test2.dat",new byte[] {2,2,2,2});
            WriteFile(tempPath + "Test3.bin",new byte[] {3,3,3,3});
            WriteFile(tempPath + "Test4.bin",new byte[] {4,4,4,4});

            try
            {
                package = new Package();
                package.Create(es);

                package.AddFiles("/",tempDir,null);

                Assert.IsNotNull(package["/Test1.dat"]);
                Assert.IsNotNull(package["/Test2.dat"]);
                Assert.IsNotNull(package["/Test3.bin"]);
                Assert.IsNotNull(package["/Test4.bin"]);

                package.AddFiles("/Folder1/",tempDir,"*.bin");

                Assert.IsNull(package["/Folder1/Test1.dat"]);
                Assert.IsNull(package["/Folder1/Test2.dat"]);
                Assert.IsNotNull(package["/Folder1/Test3.bin"]);
                Assert.IsNotNull(package["/Folder1/Test4.bin"]);

                package.Close(true);

                //-------------------------

                package = new Package(es);

                CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, package["/Test1.dat"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, package["/Test2.dat"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, package["/Test3.bin"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 4, 4, 4, 4 }, package["/Test4.bin"].GetContents());

                Assert.IsNull(package["/Folder1/Test1.dat"]);
                Assert.IsNull(package["/Folder1/Test2.dat"]);
                CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, package["/Folder1/Test3.bin"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 4, 4, 4, 4 }, package["/Folder1/Test4.bin"].GetContents());

                package.Close();
            }
            finally
            {
                File.Delete(tempPath + "Test1.dat");
                File.Delete(tempPath + "Test2.dat");
                File.Delete(tempPath + "Test3.bin");
                File.Delete(tempPath + "Test4.bin");
                Directory.CreateDirectory(tempDir);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Install")]
        public void Package_AddFiles_Relative() 
        {
            Package                 package;
            EnhancedMemoryStream    es = new EnhancedMemoryStream();
            string                  tempDir;
            string                  tempPath;
            PackageEntry            folder1;

            tempDir  = Path.GetTempPath() + Helper.NewGuid().ToString();
            tempPath = tempDir + "\\";
            Directory.CreateDirectory(tempDir);

            WriteFile(tempPath + "Test1.dat",new byte[] {1,1,1,1});
            WriteFile(tempPath + "Test2.dat",new byte[] {2,2,2,2});
            WriteFile(tempPath + "Test3.bin",new byte[] {3,3,3,3});
            WriteFile(tempPath + "Test4.bin",new byte[] {4,4,4,4});

            try
            {
                package = new Package();
                package.Create(es);

                package.RootFolder.AddFiles(tempDir,null);

                Assert.IsNotNull(package["/Test1.dat"]);
                Assert.IsNotNull(package["/Test2.dat"]);
                Assert.IsNotNull(package["/Test3.bin"]);
                Assert.IsNotNull(package["/Test4.bin"]);

                folder1 = package.AddFolder("/Folder1");
                folder1.AddFiles(tempDir,"*.bin");

                Assert.IsNull(package["/Folder1/Test1.dat"]);
                Assert.IsNull(package["/Folder1/Test2.dat"]);
                Assert.IsNotNull(package["/Folder1/Test3.bin"]);
                Assert.IsNotNull(package["/Folder1/Test4.bin"]);

                package.Close(true);

                //-------------------------

                package = new Package(es);

                CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, package["/Test1.dat"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 2, 2, 2, 2 }, package["/Test2.dat"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, package["/Test3.bin"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 4, 4, 4, 4 }, package["/Test4.bin"].GetContents());

                Assert.IsNull(package["/Folder1/Test1.dat"]);
                Assert.IsNull(package["/Folder1/Test2.dat"]);
                CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3 }, package["/Folder1/Test3.bin"].GetContents());
                CollectionAssert.AreEqual(new byte[] { 4, 4, 4, 4 }, package["/Folder1/Test4.bin"].GetContents());

                package.Close();
            }
            finally 
            {
                File.Delete(tempPath + "Test1.dat");
                File.Delete(tempPath + "Test2.dat");
                File.Delete(tempPath + "Test3.bin");
                File.Delete(tempPath + "Test4.bin");
                Directory.CreateDirectory(tempDir);
            }
        }
    }
}

