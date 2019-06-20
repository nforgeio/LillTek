//-----------------------------------------------------------------------------
// FILE:        _WebTransferFile.cs
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
    public class _WebTransferFile
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
        public void WebTransferFile_Download()
        {
            // Test the file download scenario.

            WebTransferFile transferFile;
            Guid id;

            ClearFolder();

            // Create a file

            id = Guid.NewGuid();
            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsFalse(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Write some data

            using (var stream = transferFile.GetStream())
            {
                stream.WriteInt32(1001);
            }

            // Open an existing file

            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", false);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsFalse(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            using (var stream = transferFile.GetStream())
            {
                Assert.AreEqual(1001, stream.ReadInt32());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferFile_UploadBasic()
        {
            // Test the basic file upload scenario.

            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] block;

            ClearFolder();

            // Create a file

            id = Guid.NewGuid();
            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", true, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the first block

            transferFile.Append(block1);

            // Open an existing file

            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", false, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the second block

            transferFile.Append(block2);

            // Complete the upload and verify the file contents.

            transferFile.Commit();
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsFalse(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            using (var stream = transferFile.GetStream())
            {
                block = stream.ReadBytes((int)stream.Length);
                CollectionAssert.AreEqual(Helper.Concat(block1, block2), block);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferFile_UploadEncrypt()
        {
            // Test the file upload with encryption.

            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] block;

            ClearFolder();

            // Create a file

            id = Guid.NewGuid();
            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", true, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the first block

            transferFile.Append(block1);

            // Open an existing file

            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", false, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the second block

            transferFile.Append(block2);

            // Complete the upload and verify the file contents.

            transferFile.Commit();
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsFalse(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            using (var stream = transferFile.GetStream())
            {
                block = stream.ReadBytes((int)stream.Length);
                CollectionAssert.AreEqual(Helper.Concat(block1, block2), block);
            }

            // Now encrypt the file and verify.

            var key = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES, 256);

            transferFile.Encrypt(key);

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

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebTransferFile_UploadMD5()
        {
            // Test the file upload with MD5 validation scenario and
            // also test appending with position.

            WebTransferFile transferFile;
            Guid id;
            byte[] block1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] block2 = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            byte[] block;
            long pos;

            ClearFolder();

            // Create a file

            id = Guid.NewGuid();
            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", true, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the first block

            pos = 0;
            transferFile.Append(pos, block1);

            // Append it again to simulate a duplicate

            transferFile.Append(pos, block1);
            pos += block1.Length;

            // Open an existing file

            transferFile = new WebTransferFile(id, folder, new Uri("http://test.com"), ".pdf", false, true);
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsTrue(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path + WebTransferCache.UploadExtension));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            // Append the second block

            transferFile.Append(pos, block2);

            // Complete the upload and verify the file contents.

            transferFile.Commit(MD5Hasher.Compute(Helper.Concat(block1, block2)));
            Assert.AreEqual(id, transferFile.ID);
            Assert.IsFalse(transferFile.IsUploading);
            Assert.IsTrue(File.Exists(transferFile.Path));
            Assert.AreEqual(new Uri(new Uri("http://test.com"), transferFile.ID.ToString("D") + ".pdf"), transferFile.Uri);

            using (var stream = transferFile.GetStream())
            {
                block = stream.ReadBytes((int)stream.Length);
                CollectionAssert.AreEqual(Helper.Concat(block1, block2), block);
            }
        }
    }
}

