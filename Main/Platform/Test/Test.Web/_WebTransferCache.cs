//-----------------------------------------------------------------------------
// FILE:        _WebTransferCache.cs
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
    public class _WebTransferCache
    {
        private string folder;

        [TestInitialize]
        public void Initialize()
        {
            folder = Helper.AddTrailingSlash(Path.GetTempPath()) + Helper.NewGuid().ToString();
            // folder = "C:\\Temp\\Test";
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClearFolder();
        }

        private void ClearFolder()
        {
            if (Directory.Exists(folder))
            {
                Helper.DeleteFile(folder + "\\*.*");
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_Download()
        {
            // Test the file download scenario.

            WebTransferCache cache;
            WebTransferFile transferFile;
            Guid id;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                // Create a file

                transferFile = cache.GetCommittedFile(Guid.Empty, ".pdf");
                id = transferFile.ID;
                Assert.AreNotEqual(Guid.Empty, transferFile.ID);
                Assert.IsFalse(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Write some data

                using (var stream = transferFile.GetStream())
                {
                    stream.WriteInt32(1001);
                }

                // Open an existing file

                transferFile = cache.GetCommittedFile(id, ".pdf");
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsFalse(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                using (var stream = transferFile.GetStream())
                {
                    Assert.AreEqual(1001, stream.ReadInt32());
                }
            }
            finally
            {

                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_Upload()
        {
            // Test the file upload scenario.

            WebTransferCache cache;
            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] block;
            long pos;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                // Create a file

                transferFile = cache.GetUploadFile(Guid.Empty, ".pdf");
                id = transferFile.ID;
                Assert.AreNotEqual(Guid.Empty, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the first block

                pos = 0;
                transferFile.Append(pos, block1);

                // Append it again to simulate a duplicate

                transferFile.Append(pos, block1);
                pos += block1.Length;

                // Open an existing file

                transferFile = cache.GetUploadFile(id, ".pdf");
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the second block

                transferFile.Append(pos, block2);

                // Complete the upload and verify the file contents.

                transferFile.Commit(MD5Hasher.Compute(Helper.Concat(block1, block2)));
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsFalse(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                using (var stream = transferFile.GetStream())
                {
                    block = stream.ReadBytes((int)stream.Length);
                    CollectionAssert.AreEqual(Helper.Concat(block1, block2), block);
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_UploadEncrypt()
        {
            // Test file upload with encryption.

            WebTransferCache cache;
            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] block;
            long pos;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                // Create a file

                transferFile = cache.GetUploadFile(Guid.Empty, ".pdf");
                id = transferFile.ID;
                Assert.AreNotEqual(Guid.Empty, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the first block

                pos = 0;
                transferFile.Append(pos, block1);

                // Append it again to simulate a duplicate

                transferFile.Append(pos, block1);
                pos += block1.Length;

                // Open an existing file

                transferFile = cache.GetUploadFile(id, ".pdf");
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the second block

                transferFile.Append(pos, block2);

                // Complete the upload and verify the file contents.

                transferFile.Commit(MD5Hasher.Compute(Helper.Concat(block1, block2)));
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsFalse(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                using (var stream = transferFile.GetStream())
                {
                    block = stream.ReadBytes((int)stream.Length);
                    CollectionAssert.AreEqual(Helper.Concat(block1, block2), block);
                }

                // Now encrypt the file and verify.

                var key = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES, 256);

                cache.EncryptFile(key, id, ".pdf");

                using (var stream = transferFile.GetStream())
                {
                    block = stream.ReadBytes((int)stream.Length);
                    CollectionAssert.AreNotEqual(Helper.Concat(block1, block2), block); // Shouldn't be equal any more due to the encryption.
                }

                // Decrypt and verify.

                using (var stream = transferFile.GetStream())
                {
                    using (var decryptor = new StreamDecryptor(key))
                    {
                        var ms = new MemoryStream();

                        decryptor.Decrypt(stream, ms);
                        CollectionAssert.AreEqual(Helper.Concat(block1, block2), ms.ToArray());
                    }
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_UploadBadMD5()
        {
            // Verify that we can detect a corrupted upload.

            WebTransferCache cache;
            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            long pos;
            byte[] md5;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                // Create a file

                transferFile = cache.GetUploadFile(Guid.Empty, ".pdf");
                id = transferFile.ID;
                Assert.AreNotEqual(Guid.Empty, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the first block

                pos = 0;
                transferFile.Append(pos, block1);

                // Append it again to simulate a duplicate

                transferFile.Append(pos, block1);
                pos += block1.Length;

                // Open an existing file

                transferFile = cache.GetUploadFile(id, ".pdf");
                Assert.AreEqual(id, transferFile.ID);
                Assert.IsTrue(transferFile.IsUploading);
                Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
                Assert.AreEqual(new Uri(new Uri("http://test.com/" + cache.HashIDToSubFolder(transferFile.ID)), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

                // Append the second block

                transferFile.Append(pos, block2);

                // Complete the upload and verify the file contents.

                md5 = MD5Hasher.Compute(Helper.Concat(block1, block2));
                md5[0] = (byte)~md5[0];    // Mess with the hash

                try
                {
                    transferFile.Commit(md5);
                    Assert.Fail("Expected an IOException detecting the MD5 signature mismatch.");
                }
                catch (IOException)
                {
                    // Expected
                }
            }
            finally
            {

                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DownloadMissing()
        {
            // Verify that we can detect a missing download file.

            WebTransferCache cache;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                cache.GetCommittedFile(Guid.NewGuid(), ".pdf");
            }
            catch (Exception e)
            {
                // We're expecting a FileNotFoundException.

                Assert.IsInstanceOfType(e, typeof(FileNotFoundException));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_UploadMissing()
        {
            // Verify that we can detect a missing upload file.

            WebTransferCache cache;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
            cache.Start();

            try
            {
                cache.GetUploadFile(Guid.NewGuid(), ".pdf");
            }
            catch (Exception e)
            {
                // We're expecting a FileNotFoundException.

                Assert.IsInstanceOfType(e, typeof(FileNotFoundException));
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_PurgeDownload()
        {
            // Verify that download files are purged properly as they age.

            WebTransferCache cache;
            WebTransferFile downloadFile;
            WebTransferFile uploadFile;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
            cache.SetSleepTime(TimeSpan.FromSeconds(0.5));
            cache.Start();

            try
            {
                uploadFile = cache.GetUploadFile(Guid.Empty, ".pdf");

                downloadFile = cache.GetCommittedFile(Guid.Empty, ".pdf");
                Thread.Sleep(5000);
                cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there
                Thread.Sleep(1000);
                cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there

                Thread.Sleep(7000);

                // File should have been purged

                try
                {
                    cache.GetCommittedFile(downloadFile.ID, ".pdf");
                    Assert.Fail("Expected the file to be purged.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }

                cache.GetUploadFile(uploadFile.ID, ".pdf");      // File should still be there
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_PurgeUpload()
        {
            // Verify that upload files are purged properly as they age.

            WebTransferCache cache;
            WebTransferFile downloadFile;
            WebTransferFile uploadFile;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.SetSleepTime(TimeSpan.FromSeconds(0.5));
            cache.Start();

            try
            {
                downloadFile = cache.GetCommittedFile(Guid.Empty, ".pdf");

                uploadFile = cache.GetUploadFile(Guid.Empty, ".pdf");
                Thread.Sleep(5000);
                cache.GetUploadFile(uploadFile.ID, ".pdf");      // File should still be there
                Thread.Sleep(1000);
                cache.GetUploadFile(uploadFile.ID, ".pdf");      // File should still be there

                Thread.Sleep(7000);

                // File should have been purged

                try
                {
                    cache.GetUploadFile(uploadFile.ID, ".pdf");
                    Assert.Fail("Expected the file to be purged.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }

                cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_PurgeDownloadWhileOpen()
        {
            // Verify that open download files are not purged even though
            // they exceed the age limit.

            WebTransferCache cache;
            WebTransferFile downloadFile;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
            cache.SetSleepTime(TimeSpan.FromSeconds(0.5));
            cache.Start();

            try
            {
                downloadFile = cache.GetCommittedFile(Guid.Empty, ".pdf");

                using (downloadFile.GetStream())
                {
                    Thread.Sleep(5000);
                    cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there
                    Thread.Sleep(1000);
                    cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there
                    Thread.Sleep(7000);
                    cache.GetCommittedFile(downloadFile.ID, ".pdf");  // File should still be there
                }

                // The file is closed now.  Wait a bit and verify that it gets purged.

                Thread.Sleep(2000);

                try
                {
                    cache.GetCommittedFile(downloadFile.ID, ".pdf");
                    Assert.Fail("Expected the file to be purged.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DeleteUploadingFile()
        {
            // Verify that we can delete a file in the process of being uploaded.

            WebTransferCache cache;
            WebTransferFile file;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.Start();

            try
            {
                file = cache.GetUploadFile(Guid.Empty, ".pdf");
                cache.GetUploadFile(file.ID, ".pdf");    // File should still be there

                cache.DeleteFile(file.ID, ".pdf");

                // File should have been deleted

                try
                {
                    cache.GetUploadFile(file.ID, ".pdf");
                    Assert.Fail("Expected the file to be deleted.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DeleteFileByID()
        {
            // Verify that we can delete a cached file via its ID.

            WebTransferCache cache;
            WebTransferFile file;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.Start();

            try
            {
                file = cache.GetCommittedFile(Guid.Empty, ".pdf");
                cache.GetCommittedFile(file.ID, ".pdf");    // File should still be there

                cache.DeleteFile(file.ID, ".pdf");

                // File should have been deleted

                try
                {
                    cache.GetCommittedFile(file.ID, ".pdf");
                    Assert.Fail("Expected the file to be deleted.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DeleteFileByUri()
        {
            // Verify that we can delete a cached file via its URI.

            WebTransferCache cache;
            WebTransferFile file;
            Uri uri;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.Start();

            try
            {
                file = cache.GetCommittedFile(Guid.Empty, ".pdf");
                uri = cache.GetCommittedFile(file.ID, ".pdf").Uri;  // File should still be there

                cache.DeleteFile(uri);

                // File should have been deleted

                try
                {
                    cache.GetCommittedFile(file.ID, ".pdf");
                    Assert.Fail("Expected the file to be deleted.");
                }
                catch (FileNotFoundException)
                {
                    // Expected
                }

                // Verify that some error cases don't throw exceptions.

                cache.DeleteFile(uri);                                                              // File already deleted
                cache.DeleteFile(new Uri("http://test.com"));                                       // No segments in URI
                cache.DeleteFile(new Uri("http://test.com/hello.htm"));                             // Invalid GUID
                cache.DeleteFile(new Uri("http://test.com/6ED48410-A140-434c-B294-0CE81042EEA9"));  // No file extension
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DeleteFileWhenOpen()
        {
            // Verify that an open file will not be deleted and no exception
            // is thrown.  This simulates the case where the website is in the
            // process of downloading the file.

            WebTransferCache cache;
            WebTransferFile file;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.Start();

            try
            {
                file = cache.GetCommittedFile(Guid.Empty, ".pdf");
                cache.GetCommittedFile(file.ID, ".pdf");    // File should still be there

                using (file.GetStream())
                {
                    cache.DeleteFile(file.ID, ".pdf");
                    cache.GetCommittedFile(file.ID, ".pdf"); // Shouldn't throw an exception
                }
            }
            finally
            {
                cache.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferCache_DeleteFileNoExist()
        {
            // Verify that we can delete a file that doesn't exist without an exception.

            WebTransferCache cache;

            ClearFolder();

            cache = new WebTransferCache(new Uri("http://test.com"), folder, "", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10));
            cache.Start();

            try
            {
                cache.DeleteFile(Guid.NewGuid(), ".pdf");
            }
            finally
            {
                cache.Stop();
            }
        }
    }
}

