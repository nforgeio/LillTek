//-----------------------------------------------------------------------------
// FILE:        _AsymmetricCrypto.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Configuration;
using System.IO;
using System.Security;
using System.Threading;

// $todo(jeff.lill): Add tests for key container providers.

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test
{
    [TestClass]
    public class _AsymmetricCrypto
    {
        private const string KeyContainer = "LT.Unit.Test";

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_RsaKeys()
        {
            string privateKey;
            string publicKey;

            try
            {
                privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
                Assert.IsNotNull(privateKey);
                publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);
                Assert.IsNotNull(publicKey);
                Assert.IsTrue(privateKey.Length > publicKey.Length);

                AsymmetricCrypto.SaveKey(CryptoAlgorithm.RSA, KeyContainer, null, privateKey);
                Assert.AreEqual(privateKey, AsymmetricCrypto.LoadPrivateKey(CryptoAlgorithm.RSA, KeyContainer, null));
                Assert.AreEqual(publicKey, AsymmetricCrypto.LoadPublicKey(CryptoAlgorithm.RSA, KeyContainer, null));
            }
            finally
            {
                AsymmetricCrypto.DeleteKey(CryptoAlgorithm.RSA, KeyContainer, null);
            }

            AsymmetricCrypto.DeleteKey(CryptoAlgorithm.RSA, KeyContainer, null);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_RsaEncryption()
        {
            string privateKey;
            string publicKey;
            byte[] plainText = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
            byte[] encrypted;
            byte[] decrypted;

            try
            {
                privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
                publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);

                encrypted = AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, publicKey, plainText);
                CollectionAssert.AreNotEqual(encrypted, plainText);
                decrypted = AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, privateKey, encrypted);
                CollectionAssert.AreEqual(plainText, decrypted);

                encrypted = AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, privateKey, plainText);
                Assert.AreNotEqual(encrypted, plainText);
                decrypted = AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, privateKey, encrypted);
                CollectionAssert.AreEqual(plainText, decrypted);

                AsymmetricCrypto.SaveKey(CryptoAlgorithm.RSA, KeyContainer, null, privateKey);

                encrypted = AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, KeyContainer, plainText);
                CollectionAssert.AreNotEqual(encrypted, plainText);
                decrypted = AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, KeyContainer, encrypted);
                CollectionAssert.AreEqual(plainText, decrypted);

                encrypted = AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, KeyContainer, plainText);
                CollectionAssert.AreNotEqual(encrypted, plainText);
                decrypted = AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, KeyContainer, encrypted);
                CollectionAssert.AreEqual(plainText, decrypted);
            }
            finally
            {
                AsymmetricCrypto.DeleteKey(CryptoAlgorithm.RSA, KeyContainer, null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_ValidateKey()
        {
            string privateKey;
            string publicKey;

            privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);

            Assert.IsTrue(AsymmetricCrypto.IsValidKey(CryptoAlgorithm.RSA, privateKey));
            Assert.IsTrue(AsymmetricCrypto.IsValidKey(CryptoAlgorithm.RSA, publicKey));
            Assert.IsTrue(AsymmetricCrypto.IsXmlKey(CryptoAlgorithm.RSA, privateKey));
            Assert.IsTrue(AsymmetricCrypto.IsXmlKey(CryptoAlgorithm.RSA, publicKey));

            Assert.IsFalse(AsymmetricCrypto.IsXmlKey(CryptoAlgorithm.RSA, KeyContainer));
            Assert.IsFalse(AsymmetricCrypto.IsValidKey(CryptoAlgorithm.RSA, string.Empty));
            Assert.IsFalse(AsymmetricCrypto.IsValidKey(CryptoAlgorithm.RSA, "xxxx > << "));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_KeyEquality()
        {
            string key;

            key = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            Assert.IsTrue(AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, key, key));
            Assert.IsTrue(AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, key, " \t\r\n" + key + " \t\r\n"));
            Assert.IsTrue(AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, key, key.Replace("<Modulus>", " \t<Modulus>\r\n")));
            Assert.IsFalse(AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, key, AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024)));
            Assert.IsFalse(AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, key, AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, key)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_IsXmlPrivateKey()
        {
            string privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            string publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);

            Assert.IsTrue(AsymmetricCrypto.IsXmlPrivateKey(CryptoAlgorithm.RSA, privateKey));
            Assert.IsFalse(AsymmetricCrypto.IsXmlPrivateKey(CryptoAlgorithm.RSA, publicKey));
            Assert.IsFalse(AsymmetricCrypto.IsXmlPrivateKey(CryptoAlgorithm.RSA, "hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_IsXmlPublicKey()
        {
            string privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            string publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);

            Assert.IsFalse(AsymmetricCrypto.IsXmlPublicKey(CryptoAlgorithm.RSA, privateKey));
            Assert.IsTrue(AsymmetricCrypto.IsXmlPublicKey(CryptoAlgorithm.RSA, publicKey));
            Assert.IsFalse(AsymmetricCrypto.IsXmlPublicKey(CryptoAlgorithm.RSA, "hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void AsymmetricCrypto_EncryptedCredentials()
        {
            string privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            string publicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey);
            Credentials credentials;
            byte[] encrypted;

            credentials = new Credentials("realm", "user", "password");
            encrypted = AsymmetricCrypto.EncryptCredentials(credentials, "RSA", publicKey);
            credentials = AsymmetricCrypto.DecryptCredentials(encrypted, "RSA", privateKey);

            Assert.AreEqual("realm", credentials.Realm);
            Assert.AreEqual("user", credentials.Account);
            Assert.AreEqual("password", credentials.Password);

            // Force a security failure by decrypting with the wrong key

            ExtendedAssert.Throws<SecurityException>(
                () =>
                {
                    privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
                    AsymmetricCrypto.DecryptCredentials(encrypted, "RSA", privateKey);
                });

            // Force a security failure by tampering with the encrypted credentials

            ExtendedAssert.Throws<SecurityException>(
                () =>
                {
                    encrypted[4] = (byte)~encrypted[4];
                    AsymmetricCrypto.DecryptCredentials(encrypted, "RSA", privateKey);
                });
        }
    }
}

