//-----------------------------------------------------------------------------
// FILE:        _KeyChain.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

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
    public class _KeyChain 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void KeyChain_Basic()
        {
            string      privateKey1 = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string      publicKey1  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey1);
            string      privateKey2 = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string      publicKey2  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey2);
            KeyChain    keyChain    = new KeyChain();

            ExtendedAssert.Throws<CryptographicException>(
                () =>
                {
                    keyChain.GetPrivateKey(publicKey1);
                });

            keyChain.Add(privateKey1);
            keyChain.Add(privateKey2);

            Assert.AreEqual(privateKey1,keyChain.GetPrivateKey(publicKey1));
            Assert.AreEqual(privateKey2,keyChain.GetPrivateKey(publicKey2));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void KeyChain_Encrypt() 
        {
            string          privateKey1 = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string          publicKey1  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey1);
            string          privateKey2 = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA,1024);
            string          publicKey2  = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA,privateKey2);
            KeyChain        keyChain    = new KeyChain();
            SymmetricKey    key         = Crypto.GenerateSymmetricKey();
            byte[]          encrypted;

            keyChain.Add(privateKey1);
            keyChain.Add(privateKey2);

            encrypted = keyChain.Encrypt(key);
            keyChain  = new KeyChain(key,encrypted);

            Assert.AreEqual(2,keyChain.Count);
            Assert.AreEqual(privateKey1,keyChain.GetPrivateKey(publicKey1));
            Assert.AreEqual(privateKey2,keyChain.GetPrivateKey(publicKey2));
        }
    }
}

