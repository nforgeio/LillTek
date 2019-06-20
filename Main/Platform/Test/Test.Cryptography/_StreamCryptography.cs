//-----------------------------------------------------------------------------
// FILE:        _StreamCryptography.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for StreamEncryptor and StreamDecryptor

using System;
using System.Configuration;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test
{
    [TestClass]
    public class _StreamCryptography 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void StreamCryptography_EntireStream() 
        {
            // Verify that we can encrypt an entire stream and the decrypt it.

            var     key       = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256);
            var     data      = new byte[] { 0,1,2,3,4,5,6,7,8,9 };
            var     input     = new MemoryStream();
            var     encrypted = new MemoryStream();
            var     decrypted = new MemoryStream();
            byte[]  buffer;

            input.Write(data,0,data.Length);

            // Verify encryption.

            using (var encryptor = new StreamEncryptor(key)) 
            {
                input.Position = 0;
                encryptor.Encrypt(input,encrypted);
                Assert.IsTrue(encrypted.Length > 0);
                encrypted.Position = 0;
                buffer = new byte[(int) encrypted.Length];
                encrypted.Read(buffer,0,buffer.Length);
                Assert.IsFalse(Helper.ArrayEquals(data,buffer));
            }

            // Verify decryption.

            using (var decryptor = new StreamDecryptor(key)) 
            {
                encrypted.Position = 0;
                decryptor.Decrypt(encrypted,decrypted);
                Assert.IsTrue(decrypted.Length > 0);
                decrypted.Position = 0;
                buffer = new byte[(int) decrypted.Length];
                decrypted.Read(buffer,0,buffer.Length);
                Assert.IsTrue(Helper.ArrayEquals(data,buffer));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void StreamCryptography_PartialStream() 
        {
            // Verify that we can encrypt/decrypt a portion of a stream.

            var key       = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256);
            var data      = new byte[] { 0,1,2,3,4,5,6,7,8,9 };
            var input     = new MemoryStream();
            var encrypted = new MemoryStream();
            var decrypted = new MemoryStream();
            byte[] buffer;

            input.Write(data,0,data.Length);

            // Verify encryption.

            using (var encryptor = new StreamEncryptor(key))
            {
                encrypted.Write(new byte[] { 0 },0,1);

                input.Position = 1;
                encryptor.Encrypt(input,encrypted,8);

                encrypted.Write(new byte[] { 9 },0,1);
                
                Assert.IsTrue(encrypted.Length > 2);
                encrypted.Position = 1;
                buffer = new byte[8];
                encrypted.Read(buffer,0,8);
                Assert.IsFalse(Helper.ArrayEquals(new byte[] { 1,2,3,4,5,6,7,8 },buffer));
            }

            // Verify decryption.

            using (var decryptor = new StreamDecryptor(key)) 
            {
                buffer = new byte[] { 0 };

                encrypted.Position = 0;
                encrypted.Read(buffer,0,1);
                decrypted.Write(buffer,0,1);

                decrypted.Position = 1;
                decryptor.Decrypt(encrypted,decrypted,(int) (encrypted.Length-2));

                buffer = new byte[] { 9 };
                encrypted.Read(buffer,0,1);
                decrypted.Write(buffer,0,1);

                decrypted.Position = 0;
                buffer = new byte[(int) decrypted.Length];
                decrypted.Read(buffer,0,buffer.Length);
                Assert.IsTrue(Helper.ArrayEquals(data,buffer));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void StreamCryptography_FileEncrypt()
        {
            // Verify that we can encrypt one file to another and then decrypt it.

            var key           = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256);
            var inputPath     = Path.GetTempFileName();
            var encryptedPath = Path.GetTempFileName();
            var decryptedPath = Path.GetTempFileName();

            byte[]      data;
            byte[]      buffer;

            data = new byte[16*1024];
            for (int i=0;i<data.Length;i++)
                data[i] = (byte) i;

            try
            {
                using (var fs = new FileStream(inputPath,FileMode.Create,FileAccess.ReadWrite))
                {
                    fs.Write(data,0,data.Length);
                }

                using (var encryptor = new StreamEncryptor(key))
                {
                    encryptor.Encrypt(inputPath,encryptedPath);
                }

                buffer = File.ReadAllBytes(encryptedPath);
                Assert.IsFalse(Helper.ArrayEquals(data,buffer));

                using (var decryptor = new StreamDecryptor(key)) 
                {
                    decryptor.Decrypt(encryptedPath,decryptedPath);
                }

                buffer = File.ReadAllBytes(decryptedPath);
                Assert.IsTrue(Helper.ArrayEquals(data,buffer));
            }
            finally
            {
                if (File.Exists(inputPath))
                    File.Delete(inputPath);

                if (File.Exists(encryptedPath))
                    File.Delete(encryptedPath);

                if (File.Exists(decryptedPath))
                    File.Delete(decryptedPath);
            }
        }
    }
}

