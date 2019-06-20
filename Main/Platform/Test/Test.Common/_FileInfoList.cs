//-----------------------------------------------------------------------------
// FILE:        _FileInfoList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _FileInfoList
    {
        private static string folderPath;
        private static string[] files;

        private static void InitFile(string fileName, int size, DateTime createTime, DateTime accessTime, DateTime writeTime)
        {
            FileInfo f = new FileInfo(folderPath + "\\" + fileName);
            FileStream fs;

            using (fs = f.OpenWrite())
            {

                byte[] buf = new byte[4096];
                int cbRemain;
                int cb;

                cbRemain = size;
                while (cbRemain > 0)
                {

                    cb = Math.Min(buf.Length, cbRemain);
                    fs.Write(buf, 0, cb);
                    cbRemain -= cb;
                }
            }

            f.CreationTime = createTime;
            f.LastAccessTime = accessTime;
            f.LastWriteTime = writeTime;
        }

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            DateTime now = DateTime.Now;

            folderPath = Helper.StripTrailingSlash(Helper.GetAssemblyFolder(Assembly.GetExecutingAssembly()));

            InitFile("Test01", 1000, now - TimeSpan.FromDays(100), now - TimeSpan.FromDays(99), now - TimeSpan.FromDays(98));
            InitFile("Test02", 2000, now - TimeSpan.FromDays(50), now - TimeSpan.FromDays(49), now - TimeSpan.FromDays(48));
            InitFile("Test03", 3000, now - TimeSpan.FromDays(25), now - TimeSpan.FromDays(24), now - TimeSpan.FromDays(23));

            files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_Basic()
        {
            FileInfoList list = new FileInfoList(files);

            Assert.AreEqual(files.Length, list.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_SortByPath()
        {
            FileInfoList list = new FileInfoList(files);

            list.SortByPath(false);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(String.Compare(list[i].FullName, list[i + 1].FullName, true) >= 0);

            list.SortByPath(true);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(String.Compare(list[i].FullName, list[i + 1].FullName, true) <= 0);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_SortBySize()
        {
            FileInfoList list = new FileInfoList(files);

            list.SortByLength(false);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].Length >= list[i + 1].Length);

            list.SortByLength(true);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].Length <= list[i + 1].Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_SortByCreationTime()
        {
            FileInfoList list = new FileInfoList(files);

            list.SortByCreationTime(false);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].CreationTime >= list[i + 1].CreationTime);

            list.SortByCreationTime(true);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].CreationTime <= list[i + 1].CreationTime);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_SortByLastAccessTime()
        {
            FileInfoList list = new FileInfoList(files);

            list.SortByLastAccessTime(false);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].LastAccessTime >= list[i + 1].LastAccessTime);

            list.SortByLastAccessTime(true);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].LastAccessTime <= list[i + 1].LastAccessTime);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void FileInfoList_SortByLastWriteTime()
        {
            FileInfoList list = new FileInfoList(files);

            list.SortByLastWriteTime(false);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].LastWriteTime >= list[i + 1].LastWriteTime);

            list.SortByLastWriteTime(true);

            for (int i = 0; i < list.Count - 1; i++)
                Assert.IsTrue(list[i].LastWriteTime <= list[i + 1].LastWriteTime);
        }
    }
}

