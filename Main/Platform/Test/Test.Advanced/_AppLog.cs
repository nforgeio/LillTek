//-----------------------------------------------------------------------------
// FILE:        _AppLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _AppLog
    {
        private string TestFolder
        {
            get
            {
                // return "c:\\temp\\LogTests";
                return Path.GetTempPath() + Helper.NewGuid().ToString();
            }
        }

        private void DeleteFolder(string path)
        {
            try
            {
                Helper.DeleteFile(path, true);
            }
            catch (IOException)
            {
                // Occasionally, we'll see an I/O exception here because
                // Windows still has a file locked.  Wait a bit and then
                // try again.

                Thread.Sleep(2000);
                Helper.DeleteFile(path, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_ReadWrite_DefaultLocation()
        {
            string          folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\AppLogs\Test");
            AppLogWriter    writer     = null;
            AppLogReader    reader     = null;
            AppLogRecord    rWrite, rRead;

            try
            {
                Helper.DeleteFile(folderPath, true);

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", "Bar");
                writer.Write(rWrite);
                writer.Close();
                writer = null;

                reader = AppLogReader.Open("Test");
                rRead  = reader.Read();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());
                reader.Close();
                reader = null;

                Assert.IsTrue(Directory.Exists(folderPath));
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();

                Helper.DeleteFile(folderPath, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_ReadWrite_SpecificLocation()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    rWrite, rRead;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", "Bar");
                writer.Write(rWrite);
                writer.Close();
                writer = null;

                reader = AppLogReader.Open("Test");
                rRead  = reader.Read();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());
                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_PersistReaderPos()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 5; i++)
                {
                    r = new AppLogRecord();
                    r.Add("index", i.ToString());
                    writer.Write(r);
                }

                writer.Commit();

                for (int i = 5; i < 10; i++)
                {
                    r = new AppLogRecord();
                    r.Add("index", i.ToString());
                    writer.Write(r);
                }

                writer.Commit();

                for (int i = 0; i < 10; i++)
                {
                    reader = AppLogReader.Open("Test");

                    r = reader.Read();
                    Assert.AreEqual(i.ToString(), r["index"]);

                    reader.Close();
                    reader = null;
                }

                r = new AppLogRecord();
                r.Add("index", "10");
                writer.Write(r);
                writer.Commit();

                reader = AppLogReader.Open("Test");

                r = reader.Read();
                Assert.AreEqual("10", r["index"]);

                reader.Close();
                reader = null;

                writer.Close();
                writer = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Peek()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                r = new AppLogRecord();
                r.Add("index", "0");
                writer.Write(r);

                r = new AppLogRecord();
                r.Add("index", "1");
                writer.Write(r);

                r = new AppLogRecord();
                r.Add("index", "2");
                writer.Write(r);

                writer.Close();
                writer = null;

                //-----------------------------------------

                reader = AppLogReader.Open("Test");

                r = reader.Peek();
                Assert.AreEqual("0", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("0", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("0", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("2", (string)r["index"]);

                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;

                //-----------------------------------------

                reader = AppLogReader.Open("Test");
                reader.Position = "BEGINNING";

                r = reader.ReadDelete();
                Assert.AreEqual("0", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("2", (string)r["index"]);

                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;

                //-----------------------------------------

                reader = AppLogReader.Open("Test");
                reader.Position = "BEGINNING";

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Read();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = reader.ReadDelete();
                Assert.AreEqual("2", (string)r["index"]);

                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.ReadDelete());
                Assert.IsNull(reader.ReadDelete());

                reader.Close();
                reader = null;

                //-----------------------------------------

                reader = AppLogReader.Open("Test");
                reader.Position = "BEGINNING";

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = reader.ReadDelete();
                Assert.AreEqual("1", (string)r["index"]);

                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.ReadDelete());
                Assert.IsNull(reader.ReadDelete());

                reader.Close();
                reader = null;

                //-----------------------------------------

                reader = AppLogReader.Open("Test");
                reader.Position = "BEGINNING";

                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Peek());
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.ReadDelete());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Commit()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                r = new AppLogRecord();
                r.Add("Index", "1");
                writer.Write(r);

                writer.Commit();
                Assert.AreEqual(1, Directory.GetFiles(folder, "*.log").Length);

                r = new AppLogRecord();
                r.Add("Index", "2");
                writer.Write(r);

                writer.Commit();
                Assert.AreEqual(2, Directory.GetFiles(folder, "*.log").Length);

                reader = AppLogReader.Open("Test");

                r = reader.Read();
                Assert.AreEqual("1", r["index"]);

                r = reader.Read();
                Assert.AreEqual("2", r["index"]);

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Idle_Commit()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 2s

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                r = new AppLogRecord();
                r.Add("Index", "1");
                writer.Write(r);

                Thread.Sleep(4000);     // Long enough for an idle commit
                Assert.AreEqual(1, Directory.GetFiles(folder, "*.log").Length);

                r = new AppLogRecord();
                r.Add("Index", "2");
                writer.Write(r);

                Thread.Sleep(4000);     // Long enough for an idle commit
                Assert.AreEqual(2, Directory.GetFiles(folder, "*.log").Length);

                reader = AppLogReader.Open("Test");

                r = reader.Read();
                Assert.AreEqual("1", r["index"]);

                r = reader.Read();
                Assert.AreEqual("2", r["index"]);

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Purge_Uncommitted()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                // Verify that the writer deletes any existing *.new files
                // before it gets started.

                Helper.CreateFolderTree(folder);

                new FileStream(folder + "\\test1.new", FileMode.Create).Close();
                new FileStream(folder + "\\test2.new", FileMode.Create).Close();
                new FileStream(folder + "\\test3.new", FileMode.Create).Close();
                Assert.AreEqual(3, Directory.GetFiles(folder, "*.new").Length);

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                Assert.AreEqual(1, Directory.GetFiles(folder, "*.new").Length);

                writer.Close();

                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Multiple_Logs()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    rWrite, rRead;
            byte[] data;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 9; i++)
                {
                    rWrite = new AppLogRecord();
                    rWrite.Add("Index", i.ToString());
                    rWrite.Add("Data", data);
                    writer.Write(rWrite);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");

                for (int i = 0; i < 9; i++)
                {
                    rRead = reader.Read();
                    Assert.AreEqual("my schema", rRead.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                    Assert.AreEqual(i.ToString(), rRead["index"]);
                    CollectionAssert.AreEqual(data, (byte[])rRead["data"]);
                }

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Multiple_ReadDelete()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;
            byte[]          data;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 9; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    r.Add("Data", data);
                    writer.Write(r);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");

                for (int i = 0; i < 9; i++)
                {
                    r = reader.ReadDelete();
                    Assert.AreEqual("my schema", r.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), r.SchemaVersion);

                    Assert.AreEqual(i.ToString(), r["index"]);
                    CollectionAssert.AreEqual(data, (byte[])r["data"]);
                }

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Position()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;
            byte[]          data;
            string[]        positions;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                reader = AppLogReader.Open("Test");
                Assert.AreEqual("BEGINNING", reader.Position);
                reader.Close();
                reader = null;

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 10; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    r.Add("Data", data);
                    writer.Write(r);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");
                positions = new string[10];

                for (int i = 0; i < 10; i++)
                {
                    positions[i] = reader.Position;

                    r = reader.Read();
                    Assert.AreEqual("my schema", r.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), r.SchemaVersion);

                    Assert.AreEqual(i.ToString(), r["index"]);
                    CollectionAssert.AreEqual(data, (byte[])r["data"]);
                }

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                for (int i = 9; i >= 0; i--)
                {
                    reader.Position = positions[i];

                    r = reader.Read();
                    Assert.AreEqual("my schema", r.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), r.SchemaVersion);

                    Assert.AreEqual(i.ToString(), r["index"]);
                    CollectionAssert.AreEqual(data, (byte[])r["data"]);
                }

                reader.Position = "BEGINNING";
                r = reader.Read();
                Assert.AreEqual("0", r["index"]);
                CollectionAssert.AreEqual(data, (byte[])r["data"]);
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Position_END()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;
            byte[]          data;
            string          posEnd;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 10; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    r.Add("Data", data);
                    writer.Write(r);
                }

                writer.Commit();

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(1, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");

                for (int i = 0; i < 10; i++)
                {
                    r = reader.ReadDelete();
                    Assert.AreEqual("my schema", r.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), r.SchemaVersion);

                    Assert.AreEqual(i.ToString(), r["index"]);
                    CollectionAssert.AreEqual(data, (byte[])r["data"]);
                }

                Assert.IsNull(reader.ReadDelete());
                Assert.IsNull(reader.ReadDelete());

                posEnd = reader.Position;

                r = new AppLogRecord();
                r.Add("Index", "10");
                r.Add("Data", data);
                writer.Write(r);
                writer.Commit();

                Assert.AreEqual(1, Directory.GetFiles(folder, "*.log").Length);

                reader.Position = posEnd;
                r = reader.Read();
                Assert.AreEqual("10", r["index"]);
                CollectionAssert.AreEqual(data, (byte[])r["data"]);
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Clear()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    rWrite, rRead;
            byte[]          data;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 9; i++)
                {
                    rWrite = new AppLogRecord();
                    rWrite.Add("Index", i.ToString());
                    rWrite.Add("Data", data);
                    writer.Write(rWrite);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");
                Assert.IsTrue(File.Exists(folder + "\\Reader.lock"));

                for (int i = 0; i < 9; i++)
                {
                    rRead = reader.Read();
                    Assert.AreEqual("my schema", rRead.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                    Assert.AreEqual(i.ToString(), rRead["index"]);
                    CollectionAssert.AreEqual(data, (byte[]) rRead["data"]);
                }

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Clear();
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Clear_In_Middle()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    rWrite, rRead;
            byte[] data;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 9; i++)
                {
                    rWrite = new AppLogRecord();
                    rWrite.Add("Index", i.ToString());
                    rWrite.Add("Data", data);
                    writer.Write(rWrite);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");
                Assert.IsTrue(File.Exists(folder + "\\Reader.lock"));

                for (int i = 0; i < 5; i++)
                {
                    rRead = reader.Read();
                    Assert.AreEqual("my schema", rRead.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                    Assert.AreEqual(i.ToString(), rRead["index"]);
                    CollectionAssert.AreEqual(data, (byte[])rRead["data"]);
                }

                reader.Clear();
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);
                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Multiple_Sessions()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    rWrite, rRead;
            byte[]          data;

            data = new byte[2048];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 0; i < 5; i++)
                {
                    rWrite = new AppLogRecord();
                    rWrite.Add("Index", i.ToString());
                    rWrite.Add("Data", data);
                    writer.Write(rWrite);
                }

                writer.Close();
                writer = null;

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                for (int i = 5; i < 9; i++)
                {
                    rWrite = new AppLogRecord();
                    rWrite.Add("Index", i.ToString());
                    rWrite.Add("Data", data);
                    writer.Write(rWrite);
                }

                writer.Close();
                writer = null;

                Assert.AreEqual(5, Directory.GetFiles(folder, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.IsTrue(File.Exists(folder + "\\Writer.lock"));

                reader = AppLogReader.Open("Test");
                Assert.IsTrue(File.Exists(folder + "\\Reader.lock"));

                for (int i = 0; i < 9; i++)
                {
                    rRead = reader.Read();
                    Assert.AreEqual("my schema", rRead.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                    Assert.AreEqual(i.ToString(), rRead["index"]);
                    CollectionAssert.AreEqual(data, (byte[])rRead["data"]);
                }

                Assert.IsNull(reader.Read());
                Assert.IsNull(reader.Read());

                reader.Close();
                reader = null;
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Multiple_Writers()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);

                try
                {
                    AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);
                    Assert.Fail("Expected a LogException indicating multiple writers.");
                }
                catch (Exception e)
                {
                    Assert.AreEqual(typeof(LogException).Name, e.GetType().Name);
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_Multiple_Readers()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogReader    reader = null;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                reader = AppLogReader.Open("Test");

                try
                {
                    AppLogReader.Open("Test");
                    Assert.Fail("Expected a LogException indicating multiple readers.");
                }
                catch (Exception e)
                {
                    Assert.AreEqual(typeof(LogException).Name, e.GetType().Name);
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        private bool newRecords = false;

        private void OnRecordAvailable()
        {
            newRecords = true;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_RecordAvailableEvent()
        {
            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 5m

&endsection
";

            string          root = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogReader    reader = null;
            AppLogRecord    r;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                newRecords = false;

                writer = AppLogWriter.Open("Test", "my schema", new Version("1.2.3.4"), 0);
                reader = AppLogReader.Open("Test");
                reader.RecordAvailable += new LogRecordAvailableHandler(OnRecordAvailable);

                Assert.IsNull(reader.Read());

                r = new AppLogRecord();
                r.Add("foo", "bar");
                writer.Write(r);
                writer.Commit();

                Thread.Sleep(4000);     // Give the record available event a chance to be raised
                Assert.IsTrue(newRecords);

                r = reader.Read();
                Assert.AreEqual("bar", r["foo"]);
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();
            }

            DeleteFolder(root);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLog_MaxLogSize()
        {
            // Verify that the log prunes log files to stay within the maximum log size.

            string config =
@"
&section LillTek.AppLog

RootFolder         = {0}
MaxFileSize        = 4096
BufferSize         = 128K
IdleCommitInterval = 2s
PurgeInterval      = 2s

&endsection
";
            const long MaxLogSize = 10 * 1024 * 1024;  // 10MB

            string          root   = TestFolder;
            string          folder = root + "\\AppLogs";
            AppLogWriter    writer = null;
            AppLogRecord    record;
            byte[]          data;
            long[]          fileSizes;
            long            totalSize;

            Helper.CreateFolderTree(root);

            try
            {
                Config.SetConfig(string.Format(config, folder).Replace('&', '#'));
                folder += "\\Test";

                writer = new AppLogWriter("Test", "TestSchema", new Version("1.2.3.4"), MaxLogSize);

                // Write out ~50MB worth of log records and then wait a bit for the log
                // to perform the purge, and then scan the folder to make sure the total
                // size of all of the log files, except for the newest does not exceed
                // the limit.

                data = new byte[1024 * 1024];

                for (int i = 0; i < 50; i++)
                {
                    record = new AppLogRecord();
                    record["data"] = data;

                    writer.Write(record);
                }

                Thread.Sleep(6000);

                var query =
                    from path in Directory.GetFiles(folder, "*.log", SearchOption.TopDirectoryOnly)
                    orderby path.ToLower() ascending
                    select new FileInfo(path).Length;

                fileSizes = query.ToArray();
                totalSize = 0;

                for (int i = 0; i < fileSizes.Length - 1; i++)
                    totalSize += fileSizes[i];

                Assert.IsTrue(totalSize <= MaxLogSize);
            }
            finally
            {
                Config.SetConfig(null);

                if (writer != null)
                    writer.Close();
            }

            DeleteFolder(root);
        }
    }
}

