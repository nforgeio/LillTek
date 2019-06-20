//-----------------------------------------------------------------------------
// FILE:        _SecureFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for SecureFile

using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test 
{
    [TestClass]
    public class _SecureFile 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream() 
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            EnhancedMemoryStream    decrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;

            for (int i=0;i<100;i++)
                original.WriteByte((byte) i);

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,SecureFileMode.Decrypt,privateKey);
            Assert.AreEqual(string.Empty,secure.FileName);
            Assert.AreEqual(string.Empty,secure.FullPath);
            secure.DecryptTo(decrypted);
            secure.Close();
            secure = null;

            original.Position  = 0;
            encrypted.Position = 0;
            CollectionAssert.AreNotEqual(original.ReadBytesToEnd(),encrypted.ReadBytesToEnd());

            original.Position  = 0;
            decrypted.Position = 0;
            CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_BadHash() 
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            EnhancedMemoryStream    decrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;
            byte                    b;

            for (int i=0;i<100;i++)
                original.WriteByte((byte) i);

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            // Munge the last byte of the hash digest and then
            // confirm the this is detected

            encrypted.Position = encrypted.Length-1;
            b                  = (byte) encrypted.ReadByte();
            encrypted.Position = encrypted.Length-1;
            encrypted.WriteByte((byte) (~b));

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,SecureFileMode.Decrypt,privateKey);

            try 
            {
                secure.DecryptTo(decrypted);
                Assert.Fail("Corrupt hash digest not detected.");
            }
            catch
            {
                // Expecting an exception
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_LargeContent() 
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            EnhancedMemoryStream    decrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;

            for (int i=0;i<128000;i++)
                original.WriteByte((byte) i);

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,SecureFileMode.Decrypt,privateKey);
            secure.DecryptTo(decrypted);
            secure.Close();
            secure = null;

            original.Position  = 0;
            encrypted.Position = 0;
            CollectionAssert.AreNotEqual(original.ReadBytesToEnd(), encrypted.ReadBytesToEnd());

            original.Position  = 0;
            decrypted.Position = 0;
            CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_NoContent() 
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            EnhancedMemoryStream    decrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            original.Position  = 0;
            encrypted.Position = 0;
            Assert.AreNotEqual(original.ReadBytesToEnd(),encrypted.ReadBytesToEnd());

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,SecureFileMode.Decrypt,privateKey);
            secure.DecryptTo(decrypted);
            secure.Close();
            secure = null;

            Assert.AreEqual(0,decrypted.Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_Metadata()
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            EnhancedMemoryStream    decrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;
            DateTime                createTime = Helper.UtcNowRounded - TimeSpan.FromMinutes(1);
            DateTime                writeTime  = Helper.UtcNowRounded;

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            secure.Properties["Foo"]   = "Bar";
            secure.Properties["Hello"] = "World";
            secure.FileName            = "Test.dat";
            secure.FullPath            = "c:\\test\\test.dat";
            secure.CreateTimeUtc       = createTime;
            secure.WriteTimeUtc        = writeTime;
            original.Position          = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            original.Position  = 0;
            encrypted.Position = 0;
            Assert.AreNotEqual(original.ReadBytesToEnd(),encrypted.ReadBytesToEnd());

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,SecureFileMode.Decrypt,privateKey);
            secure.DecryptTo(decrypted);
            Assert.AreEqual("Bar",secure.Properties["Foo"]);
            Assert.AreEqual("World",secure.Properties["Hello"]);
            Assert.AreEqual("Test.dat",secure.FileName);
            Assert.AreEqual("c:\\test\\test.dat",secure.FullPath);
            Assert.AreEqual(createTime,secure.CreateTimeUtc);
            Assert.AreEqual(writeTime,secure.WriteTimeUtc);
            secure.Close();
            secure = null;

            Assert.AreEqual(0,decrypted.Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_Validate()
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;
            byte                    b;

            for (int i=0;i<100;i++)
                original.WriteByte((byte) i);

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            encrypted.Position = 0;
            Assert.IsTrue(SecureFile.Validate(encrypted,privateKey));

            encrypted.Position = encrypted.Length - 1;
            b                  = (byte) encrypted.ReadByte();
            encrypted.Position = encrypted.Length - 1;
            encrypted.WriteByte((byte) (~b));

            encrypted.Position = 0;
            Assert.IsFalse(SecureFile.Validate(encrypted,privateKey));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_GetPublicKey() 
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;

            for (int i=0;i<100;i++)
                original.WriteByte((byte) i);

            // Verify that the public key is saved when requested (the default)

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            Assert.IsTrue(secure.SavePublicKey);
            Assert.AreEqual(publicKey,secure.PublicKey);

            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            encrypted.Position = 0;
            Assert.AreEqual(publicKey,SecureFile.GetPublicKey(encrypted));

            // Verify that the public key is not saved if SavePublicKey=false

            encrypted.SetLength(0);
            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            secure.SavePublicKey = false;
            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            encrypted.Position = 0;
            Assert.IsNull(SecureFile.GetPublicKey(encrypted));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_Stream_KeyChain()
        {
            string                  privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original   = new EnhancedMemoryStream();
            EnhancedMemoryStream    encrypted  = new EnhancedMemoryStream();
            SecureFile              secure     = null;

            for (int i=0;i<100;i++)
                original.WriteByte((byte) i);

            // Verify that SecureFile can find the correct private key in the key chain.

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            Assert.IsTrue(secure.SavePublicKey);
            Assert.AreEqual(publicKey,secure.PublicKey);

            original.Position = 0;
            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            KeyChain                keyChain  = new KeyChain();
            EnhancedMemoryStream    decrypted = new EnhancedMemoryStream();

            keyChain.Add(privateKey);

            encrypted.Position = 0;
            secure = new SecureFile(encrypted,keyChain);
            secure.DecryptTo(decrypted);
            secure.Close();
            secure = null;

            CollectionAssert.AreEqual(original.ToArray(), decrypted.ToArray());

            // Verify that SecureFile throws a CryptographicException if the
            // key is not present in the chain.

            keyChain.Clear();
            encrypted.Position = 0;

            try
            {
                secure = new SecureFile(encrypted,keyChain);
                secure.DecryptTo(decrypted);
                Assert.Fail("Expecting a CryptographicException");
            }
            catch (CryptographicException)
            {
                // Expecting this
            }
            finally
            {
                if (secure != null) {

                    secure.Close();
                    secure = null;
                }
            }

            // Verify that SecureFile throws a CryptographicException if the
            // public key was not saved to the file.

            keyChain.Add(privateKey);

            secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
            secure.SavePublicKey = false;

            original.Position = 0;
            encrypted = new EnhancedMemoryStream();

            secure.EncryptTo(encrypted,CryptoAlgorithm.AES,256);
            secure.Close();
            secure = null;

            try 
            {
                secure = new SecureFile(encrypted,keyChain);
                secure.DecryptTo(decrypted);
                Assert.Fail("Expecting a CryptographicException");
            }
            catch (CryptographicException) {

                // Expecting this
            }
            finally 
            {
                if (secure != null) {

                    secure.Close();
                    secure = null;
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File() 
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              decryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            EnhancedStream      decrypted    = null;
            SecureFile          secure       = null;
            DateTime            createTime   = Helper.UtcNowRounded - TimeSpan.FromMinutes(1);
            DateTime            writeTime    = Helper.UtcNowRounded;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                Directory.SetCreationTimeUtc(originalName,createTime);
                Directory.SetLastWriteTimeUtc(originalName,writeTime);

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(Path.GetFullPath(originalName),secure.FullPath);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.Close();
                secure = null;

                secure = new SecureFile(encryptName,SecureFileMode.Decrypt,privateKey);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.DecryptTo(decryptName);
                secure.Close();
                secure = null;

                Assert.AreEqual(createTime,Directory.GetCreationTimeUtc(decryptName));
                Assert.AreEqual(writeTime,Directory.GetLastWriteTimeUtc(decryptName));

                original  = new EnhancedFileStream(originalName,FileMode.Open,FileAccess.Read);
                encrypted = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.Read);
                decrypted = new EnhancedFileStream(decryptName,FileMode.Open,FileAccess.Read);

                original.Position  = 0;
                encrypted.Position = 0;
                CollectionAssert.AreNotEqual(original.ReadBytesToEnd(), encrypted.ReadBytesToEnd());

                original.Position  = 0;
                decrypted.Position = 0;
                CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
            }
            finally
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                if (decrypted != null)
                    decrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
                System.IO.File.Delete(decryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_BadHash() 
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              decryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            SecureFile          secure       = null;
            byte                b;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                // Munge the last byte of the hash digest and then confirm
                // that the bad hash is detected.

                encrypted          = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.ReadWrite);
                encrypted.Position = encrypted.Length-1;
                b                  = (byte) encrypted.ReadByte();
                encrypted.Position = encrypted.Length-1;
                encrypted.WriteByte((byte) (~b));
                encrypted.Close();
                encrypted = null;

                ExtendedAssert.Throws<CryptographicException>(
                    () =>
                    {
                        secure = new SecureFile(encryptName,SecureFileMode.Decrypt,privateKey);
                        secure.DecryptTo(decryptName);
                    });
            }
            finally 
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                try { System.IO.File.Delete(originalName); } catch { }
                try { System.IO.File.Delete(encryptName); } catch { }
                try { System.IO.File.Delete(decryptName); } catch { }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_LargeContent() 
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              decryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            EnhancedStream      decrypted    = null;
            SecureFile          secure       = null;
            DateTime            createTime   = Helper.UtcNowRounded - TimeSpan.FromMinutes(1);
            DateTime            writeTime    = Helper.UtcNowRounded;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<128000;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                Directory.SetCreationTimeUtc(originalName,createTime);
                Directory.SetLastWriteTimeUtc(originalName,writeTime);

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.Close();
                secure = null;

                secure = new SecureFile(encryptName,SecureFileMode.Decrypt,privateKey);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.DecryptTo(decryptName);
                secure.Close();
                secure = null;

                Assert.AreEqual(createTime,Directory.GetCreationTimeUtc(decryptName));
                Assert.AreEqual(writeTime,Directory.GetLastWriteTimeUtc(decryptName));

                original  = new EnhancedFileStream(originalName,FileMode.Open,FileAccess.Read);
                encrypted = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.Read);
                decrypted = new EnhancedFileStream(decryptName,FileMode.Open,FileAccess.Read);

                original.Position  = 0;
                encrypted.Position = 0;
                CollectionAssert.AreNotEqual(original.ReadBytesToEnd(), encrypted.ReadBytesToEnd());

                original.Position  = 0;
                decrypted.Position = 0;
                CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
            }
            finally 
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                if (decrypted != null)
                    decrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
                System.IO.File.Delete(decryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_NoContent()
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              decryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            EnhancedStream      decrypted    = null;
            SecureFile          secure       = null;
            DateTime            createTime   = Helper.UtcNowRounded - TimeSpan.FromMinutes(1);
            DateTime            writeTime    = Helper.UtcNowRounded;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);
                original.Close();
                original = null;

                Directory.SetCreationTimeUtc(originalName,createTime);
                Directory.SetLastWriteTimeUtc(originalName,writeTime);

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.Close();
                secure = null;

                secure = new SecureFile(encryptName,SecureFileMode.Decrypt,privateKey);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.DecryptTo(decryptName);
                secure.Close();
                secure = null;

                Assert.AreEqual(createTime,Directory.GetCreationTimeUtc(decryptName));
                Assert.AreEqual(writeTime,Directory.GetLastWriteTimeUtc(decryptName));

                original  = new EnhancedFileStream(originalName,FileMode.Open,FileAccess.Read);
                encrypted = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.Read);
                decrypted = new EnhancedFileStream(decryptName,FileMode.Open,FileAccess.Read);

                original.Position  = 0;
                encrypted.Position = 0;
                Assert.AreNotEqual(original.ReadBytesToEnd(),encrypted.ReadBytesToEnd());

                original.Position  = 0;
                decrypted.Position = 0;
                CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
            }
            finally 
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                if (decrypted != null)
                    decrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
                System.IO.File.Delete(decryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_Metadata() 
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              decryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            EnhancedStream      decrypted    = null;
            SecureFile          secure       = null;
            DateTime            createTime   = Helper.UtcNowRounded - TimeSpan.FromMinutes(1);
            DateTime            writeTime    = Helper.UtcNowRounded;

            try
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                Directory.SetCreationTimeUtc(originalName,createTime);
                Directory.SetLastWriteTimeUtc(originalName,writeTime);

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.Properties["Foo"]   = "Bar";
                secure.Properties["Hello"] = "World";
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.Close();
                secure = null;

                secure = new SecureFile(encryptName,SecureFileMode.Decrypt,privateKey);
                Assert.AreEqual("Bar",secure.Properties["Foo"]);
                Assert.AreEqual("World",secure.Properties["Hello"]);
                Assert.AreEqual(Path.GetFileName(originalName),secure.FileName);
                Assert.AreEqual(createTime,secure.CreateTimeUtc);
                Assert.AreEqual(writeTime,secure.WriteTimeUtc);
                secure.DecryptTo(decryptName);
                secure.Close();
                secure = null;

                Assert.AreEqual(createTime,Directory.GetCreationTimeUtc(decryptName));
                Assert.AreEqual(writeTime,Directory.GetLastWriteTimeUtc(decryptName));

                original  = new EnhancedFileStream(originalName,FileMode.Open,FileAccess.Read);
                encrypted = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.Read);
                decrypted = new EnhancedFileStream(decryptName,FileMode.Open,FileAccess.Read);

                original.Position  = 0;
                encrypted.Position = 0;
                Assert.AreNotEqual(original.ReadBytesToEnd(),encrypted.ReadBytesToEnd());

                original.Position  = 0;
                decrypted.Position = 0;
                CollectionAssert.AreEqual(original.ReadBytesToEnd(), decrypted.ReadBytesToEnd());
            }
            finally
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                if (decrypted != null)
                    decrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
                System.IO.File.Delete(decryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_Validate() 
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            SecureFile          secure       = null;
            byte                b;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                Assert.IsTrue(SecureFile.Validate(encryptName,privateKey));

                encrypted          = new EnhancedFileStream(encryptName,FileMode.Open,FileAccess.ReadWrite);
                encrypted.Position = encrypted.Length - 1;
                b                  = (byte) encrypted.ReadByte();
                encrypted.Position = encrypted.Length - 1;
                encrypted.WriteByte((byte) (~b));
                encrypted.Close();

                Assert.IsFalse(SecureFile.Validate(encryptName,privateKey));
            }
            finally 
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_GetPublicKey()
        {
            string              originalName = Path.GetTempFileName();
            string              encryptName  = Path.GetTempFileName();
            string              privateKey   = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string              publicKey    = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedStream      original     = null;
            EnhancedStream      encrypted    = null;
            SecureFile          secure       = null;

            try 
            {
                original = new EnhancedFileStream(originalName,FileMode.Create,FileAccess.ReadWrite);

                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                original.Close();
                original = null;

                // Verify that the public key is saved if requested

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                Assert.IsTrue(secure.SavePublicKey);
                Assert.AreEqual(publicKey,secure.PublicKey);
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                Assert.AreEqual(publicKey,SecureFile.GetPublicKey(encryptName));

                // Verify that the public key is not saved, if SavePublicKey=false

                System.IO.File.Delete(encryptName);

                secure = new SecureFile(originalName,SecureFileMode.Encrypt,publicKey);
                secure.SavePublicKey = false;
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                Assert.IsNull(SecureFile.GetPublicKey(encryptName));
            }
            finally 
            {
                if (original != null)
                    original.Close();

                if (encrypted != null)
                    encrypted.Close();

                System.IO.File.Delete(originalName);
                System.IO.File.Delete(encryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_File_KeyChain() 
        {
            string                  encryptName = Path.GetTempFileName();
            string                  privateKey  = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string                  publicKey   = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            EnhancedMemoryStream    original    = new EnhancedMemoryStream();
            SecureFile              secure      = null;

            try
            {
                for (int i=0;i<100;i++)
                    original.WriteByte((byte) i);

                // Verify that SecureFile can find the correct private key in the key chain.

                secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
                Assert.IsTrue(secure.SavePublicKey);
                Assert.AreEqual(publicKey,secure.PublicKey);

                original.Position = 0;
                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                var keyChain  = new KeyChain();
                var decrypted = new EnhancedMemoryStream();

                keyChain.Add(privateKey);

                secure = new SecureFile(encryptName,keyChain);
                secure.DecryptTo(decrypted);
                secure.Close();
                secure = null;

                CollectionAssert.AreEqual(original.ToArray(), decrypted.ToArray());

                // Verify that SecureFile throws a CryptographicException if the
                // key is not present in the chain.

                keyChain.Clear();

                try
                {
                    secure = new SecureFile(encryptName,keyChain);
                    secure.DecryptTo(decrypted);
                    Assert.Fail("Expecting a CryptographicException");
                }
                catch (CryptographicException)
                {
                    // Expecting this
                }
                finally
                {
                    if (secure != null) {

                        secure.Close();
                        secure = null;
                    }
                }

                // Verify that SecureFile throws a CryptographicException if the
                // public key was not saved to the file.

                keyChain.Add(privateKey);

                secure = new SecureFile(original,SecureFileMode.Encrypt,publicKey);
                secure.SavePublicKey = false;

                original.Position = 0;

                secure.EncryptTo(encryptName,CryptoAlgorithm.AES,256);
                secure.Close();
                secure = null;

                try 
                {
                    secure = new SecureFile(encryptName,keyChain);
                    secure.DecryptTo(decrypted);
                    Assert.Fail("Expecting a CryptographicException");
                }
                catch (CryptographicException) 
                {
                    // Expecting this
                }
                finally
                {
                    if (secure != null) {

                        secure.Close();
                        secure = null;
                    }
                }
            }
            finally
            {
                System.IO.File.Delete(encryptName);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureFile_WipeAndDelete()
        {
            // Verify that a file can be wiped and deleted.  Note that there's
            // no reasonable way to automate a check to see that the file
            // was actually wiped.  It's best to step through the code manually
            // to verify this.

            var path = Path.GetTempFileName();

            using (var fs = new FileStream(path,FileMode.Create,FileAccess.ReadWrite)) 
            {
                for (int i=0;i<1000000;i++)
                    fs.WriteByte((byte) i);
            }

            SecureFile.WipeAndDelete(path,3);
            Assert.IsFalse(System.IO.File.Exists(path));

            // Verify that calling WipeAndDelete() on a non-existant file
            // does not throw an exception.

            SecureFile.WipeAndDelete(path,3);
        }
    }
}

