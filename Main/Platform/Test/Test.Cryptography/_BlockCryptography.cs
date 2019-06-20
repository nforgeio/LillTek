//-----------------------------------------------------------------------------
// FILE:        _BlockCryptography.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for BlockEncryptor and BlockDecryptor

using System;
using System.Configuration;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test 
{
    [TestClass]
    public class _BlockCryptography 
    {
        private void EncryptDecrypt(string algorithm,int keySize,byte[] input) 
        {
            byte[]              key;
            byte[]              IV;

            EncryptionConfig.GenKeyIV(algorithm,keySize,out key,out IV);

            BlockEncryptor      encryptor = new BlockEncryptor(algorithm,key,IV);
            BlockDecryptor      decryptor = new BlockDecryptor(algorithm,key,IV);
            byte[]              encrypted;
            byte[]              decrypted;
            bool                equal;

            encrypted = encryptor.Encrypt(input);

            if (algorithm.ToUpper() == CryptoAlgorithm.PlainText)
            {
                // Make sure the input/output buffers are identical.

                CollectionAssert.AreEqual(input,encrypted);
            }
            else 
            {
                // Make sure the input/output buffers differ.

                if (input.Length > 0 && input.Length == encrypted.Length) 
                {
                    equal = true;
                    for (int i=0;i<input.Length;i++)
                        if (input[i] != encrypted[i]) {

                            equal = false;
                            break;
                        }

                    Assert.IsFalse(equal);
                }
            }

            decrypted = decryptor.Decrypt(encrypted);
            
            // Make sure we're back to the original bytes.

            CollectionAssert.AreEqual(input,decrypted);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void BlockCryptography_PlainText() 
        {
            for (var cb=0;cb<1024;cb++) 
            {
                var data = new byte[cb];

                for (int i=0;i<cb;i++)
                    data[i] = (byte) i;

                EncryptDecrypt(CryptoAlgorithm.PlainText,8,data);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void BlockCryptography_RC2() 
        {
            for (var cb=0;cb<1024;cb++) 
            {
                var data = new byte[cb];

                for (int i=0;i<cb;i++)
                    data[i] = (byte) i;

                EncryptDecrypt(CryptoAlgorithm.RC2,80,data);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void BlockCryptography_DES() 
        {
            for (var cb=0;cb<1024;cb++) 
            {
                var data = new byte[cb];

                for (int i=0;i<cb;i++)
                    data[i] = (byte) i;

                EncryptDecrypt(CryptoAlgorithm.DES,64,data);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void BlockCryptography_TripleDES() 
        {
            for (var cb=0;cb<1024;cb++) 
            {
                var data = new byte[cb];

                for (int i=0;i<cb;i++)
                    data[i] = (byte) i;

                EncryptDecrypt(CryptoAlgorithm.TripleDES,192,data);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void BlockCryptography_AES() 
        {
            for (var cb=0;cb<1024;cb++) 
            {
                var data = new byte[cb];

                for (int i=0;i<cb;i++)
                    data[i] = (byte) i;

                EncryptDecrypt(CryptoAlgorithm.AES,256,data);
            }
        }
    }
}
