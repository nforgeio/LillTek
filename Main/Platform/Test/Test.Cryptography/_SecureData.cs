//-----------------------------------------------------------------------------
// FILE:        _SecureData.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for SecureData

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
    public class _SecureData 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureData_Asymmetric()
        {
            string      privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string      publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            byte[]      plainText;
            byte[]      cipherText;

            plainText  = new byte[] {0,1,2,3};
            cipherText = SecureData.Encrypt(publicKey,plainText,CryptoAlgorithm.AES,256);
            CollectionAssert.AreNotEqual(plainText,cipherText);
            CollectionAssert.AreEqual(plainText, SecureData.Decrypt(privateKey, cipherText));

            plainText  = new byte[] { 0,1,2,3 };
            cipherText = SecureData.Encrypt(publicKey,plainText,CryptoAlgorithm.AES,256,1000);
            Assert.IsTrue(cipherText.Length >= 1000);

            plainText  = new byte[0];
            cipherText = SecureData.Encrypt(publicKey,plainText,CryptoAlgorithm.AES,256);
            CollectionAssert.AreNotEqual(plainText, cipherText);
            CollectionAssert.AreEqual(plainText, SecureData.Decrypt(privateKey, cipherText));

            plainText = new byte[2000000];
            for (int i=0;i<plainText.Length;i++)
                plainText[i] = (byte) i;

            cipherText = SecureData.Encrypt(publicKey,plainText,CryptoAlgorithm.AES,256);
            CollectionAssert.AreNotEqual(plainText, cipherText);
            CollectionAssert.AreEqual(plainText, SecureData.Decrypt(privateKey, cipherText));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureData_Symmetric()
        {
            string          privateKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string          publicKey  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey);
            SymmetricKey    argsEncrypt;
            SymmetricKey    argsDecrypt;
            byte[]          asymPlain;
            byte[]          asymCipher;
            byte[]          symPlain;
            byte[]          symCipher;

            asymPlain  = new byte[] {0,1,2,3};
            asymCipher = SecureData.Encrypt(publicKey,asymPlain,CryptoAlgorithm.AES,256,1000,out argsEncrypt);
            CollectionAssert.AreEqual(asymPlain, SecureData.Decrypt(privateKey, asymCipher, out argsDecrypt));

            symPlain  = new byte[] {10,20,30,40};
            symCipher = SecureData.Encrypt(argsEncrypt,symPlain,100);
            Assert.IsTrue(symCipher.Length >= 100);
            CollectionAssert.AreEqual(symPlain, SecureData.Decrypt(argsDecrypt, symCipher));
        }
    }
}

