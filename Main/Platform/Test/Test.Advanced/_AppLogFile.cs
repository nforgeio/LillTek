//-----------------------------------------------------------------------------
// FILE:        _AppLogFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _AppLogFile
    {
        private string TestFolder
        {
            get
            {
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
        public void AppLogFile_ReadWrite_OneField()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", "Bar");
                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_LargeField()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;
            byte[] data;

            data = new byte[2 * 1024 * 1024];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)i;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                rWrite.Add("data", data);
                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.IsTrue(rWrite.Equals(rRead));
                CollectionAssert.AreEqual(data, (byte[])rRead["data"]);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_CreateDate()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;
            DateTime now;

            Helper.CreateFolderTree(folder);

            try
            {
                now = DateTime.UtcNow;
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                r = new AppLogRecord();
                r.Add("Foo", "Bar");
                logFile.Write(r);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                Assert.IsTrue(now - TimeSpan.FromSeconds(2) <= logFile.CreateDate && logFile.CreateDate <= now + TimeSpan.FromSeconds(2));

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadWrite_TwoFields()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", "Bar");
                rWrite.Add("Bar", "Foo");
                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadWrite_4096Fields()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                for (int i = 0; i < 4096; i++)
                    rWrite.Add(i.ToString(), i.ToString());

                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.IsTrue(rWrite.Equals(rRead));
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadWrite_ByteArray()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", new byte[] { 0, 1, 2, 3 });
                rWrite.Add("Bar", new byte[] { 5, 6, 7, 8 });
                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.IsTrue(rWrite.Equals(rRead));
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadWrite_LargeFields()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rWrite, rRead;
            byte[] arr;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                arr = new byte[4096];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = (byte)i;

                rWrite = new AppLogRecord();
                rWrite.Add("Foo", new string('x', 4096));
                rWrite.Add("Bar", arr);
                logFile.Write(rWrite);
                Assert.AreEqual(1, logFile.WriteCount);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);
                rRead = logFile.Read();
                Assert.IsTrue(rWrite.Equals(rRead));
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);
                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());
                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadWrite_50()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                for (int i = 0; i < 50; i++)
                {

                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    r.Add("Foo", "Bar: " + i.ToString());
                    logFile.Write(r);
                }

                Assert.AreEqual(50, logFile.WriteCount);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                for (int i = 0; i < 50; i++)
                {
                    r = logFile.Read();
                    Assert.AreEqual(i, int.Parse((string)r["index"]));
                    Assert.AreEqual("Bar: " + i.ToString(), (string)r["FOO"]);
                    Assert.AreEqual("my schema", r.SchemaName);
                    Assert.AreEqual(new Version("1.2.3.4"), r.SchemaVersion);
                }

                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Write_Zero()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;

            Helper.CreateFolderTree(folder);

            try
            {
                // Verify that committing a log with no records deletes
                // the file.

                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));
                logFile.Close();
                logFile = null;

                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadDelete()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                for (int i = 0; i < 2; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    logFile.Write(r);
                }

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.ReadDelete();
                Assert.AreEqual(0, int.Parse((string)r["index"]));

                r = logFile.Read();
                Assert.AreEqual(1, int.Parse((string)r["index"]));

                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.Read();
                Assert.AreEqual(1, int.Parse((string)r["index"]));

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_ReadDelete_All()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                for (int i = 0; i < 2; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    logFile.Write(r);
                }

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.ReadDelete();
                Assert.AreEqual(0, int.Parse((string)r["index"]));

                r = logFile.Read();
                Assert.AreEqual(1, int.Parse((string)r["index"]));

                Assert.IsNull(logFile.Read());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.ReadDelete();
                Assert.AreEqual(1, int.Parse((string)r["index"]));

                logFile.Close();
                logFile = null;

                // Verify that the log file was deleted when all all
                // records were marked for deletion.

                Assert.AreEqual(0, Directory.GetFiles(folder, "*.new").Length);
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.log").Length);
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Position()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            string[] positions;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                positions = new string[50];
                for (int i = 0; i < positions.Length; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    logFile.Write(r);
                }

                Assert.AreEqual(50, logFile.WriteCount);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                // Verify that we can seek to all records.

                for (int i = 0; i < 50; i++)
                {
                    positions[i] = logFile.Position;
                    r = logFile.Read();
                    Assert.AreEqual(i, int.Parse((string)r["index"]));
                }

                for (int i = 49; i >= 0; i--)
                {
                    logFile.Position = positions[i];
                    r = logFile.Read();
                    Assert.AreEqual(i, int.Parse((string)r["index"]));
                }

                // Verify that read after seek to a deleted record 
                // returns the record after the deleted one.

                logFile.Position = positions[25];
                r = logFile.ReadDelete();
                Assert.AreEqual(25, int.Parse((string)r["index"]));

                logFile.Position = positions[25];
                r = logFile.ReadDelete();
                Assert.AreEqual(26, int.Parse((string)r["index"]));

                // Verify that seek("END") actually goes to the end

                logFile.Position = "END";
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Position_Bad()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            string[] positions;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                positions = new string[50];
                for (int i = 0; i < positions.Length; i++)
                {
                    r = new AppLogRecord();
                    r.Add("Index", i.ToString());
                    logFile.Write(r);
                }

                Assert.AreEqual(50, logFile.WriteCount);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                for (int i = 0; i < 50; i++)
                {
                    positions[i] = logFile.Position;
                    r = logFile.Read();
                    Assert.AreEqual(i, int.Parse((string)r["index"]));
                }

                for (int i = 49; i >= 0; i--)
                {
                    logFile.Position = positions[i];
                    r = logFile.Read();
                    Assert.AreEqual(i, int.Parse((string)r["index"]));
                }

                // Verify that seeking to an invalid position in the
                // log file throws an exception.

                try
                {
                    logFile.Position = (int.Parse(positions[1]) + 1).ToString();
                    Assert.Fail("Expected a LogException");
                }
                catch (Exception e)
                {
                    Assert.AreEqual(typeof(ArgumentException).Name, e.GetType().Name);
                }

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Peek_OneRecord()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord rRead, rWrite;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                rWrite = new AppLogRecord();
                rWrite.Add("index", "0");
                logFile.Write(rWrite);
                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                rRead = logFile.Peek();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                rRead = logFile.Peek();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                rRead = logFile.Read();
                Assert.AreEqual(rWrite, rRead);
                Assert.AreEqual("my schema", rRead.SchemaName);
                Assert.AreEqual(new Version("1.2.3.4"), rRead.SchemaVersion);

                Assert.IsNull(logFile.Peek());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Peek_TwoRecords()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                r = new AppLogRecord();
                r.Add("index", "0");
                logFile.Write(r);

                r = new AppLogRecord();
                r.Add("index", "1");
                logFile.Write(r);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.Peek();
                Assert.AreEqual("0", (string)r["index"]);

                r = logFile.Peek();
                Assert.AreEqual("0", (string)r["index"]);

                r = logFile.Read();
                Assert.AreEqual("0", (string)r["index"]);

                r = logFile.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = logFile.Read();
                Assert.AreEqual("1", (string)r["index"]);

                Assert.IsNull(logFile.Peek());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void AppLogFile_Peek_DeletedRecords()
        {
            string folder = TestFolder;
            AppLogFile logFile = null;
            AppLogRecord r;

            Helper.CreateFolderTree(folder);

            try
            {
                logFile = new AppLogFile();
                logFile.Create(folder + "\\test.new", AppLog.DefBufferSize, "my schema", new Version("1.2.3.4"));

                r = new AppLogRecord();
                r.Add("index", "0");
                logFile.Write(r);

                r = new AppLogRecord();
                r.Add("index", "1");
                logFile.Write(r);

                r = new AppLogRecord();
                r.Add("index", "2");
                logFile.Write(r);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.ReadDelete();
                Assert.AreEqual("0", (string)r["index"]);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = logFile.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = logFile.Read();
                Assert.AreEqual("1", (string)r["index"]);

                r = logFile.Peek();
                Assert.AreEqual("2", (string)r["index"]);

                r = logFile.ReadDelete();
                Assert.AreEqual("2", (string)r["index"]);

                logFile.Close();
                logFile = null;

                logFile = new AppLogFile();
                logFile.Open(folder + "\\test.log", AppLog.DefBufferSize);

                r = logFile.Peek();
                Assert.AreEqual("1", (string)r["index"]);

                r = logFile.Read();
                Assert.AreEqual("1", (string)r["index"]);

                Assert.IsNull(logFile.Peek());
                Assert.IsNull(logFile.Read());

                logFile.Close();
                logFile = null;
            }
            finally
            {
                if (logFile != null)
                    logFile.Close();
            }

            DeleteFolder(folder);
        }
    }
}

