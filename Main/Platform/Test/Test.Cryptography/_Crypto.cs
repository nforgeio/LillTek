//-----------------------------------------------------------------------------
// FILE:        _Crypto.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test 
{
    [TestClass]
    public class _Crypto 
    {
        private const string    KeyContainer = "LillTek.Unit.CryptoTest";

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_GenerateChallenge() 
        {
            byte[]      challenge;

            challenge = Crypto.GenerateChallenge(16);
            Assert.AreEqual(16,challenge.Length);
            Assert.IsFalse(Helper.IsZeros(challenge));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_GeneratePassword() 
        {
            string      pwd1;
            string      pwd2;

            pwd1 = Crypto.GeneratePassword(10,true);
            Assert.AreEqual(10,pwd1.Length);

            pwd2 = Crypto.GeneratePassword(10,true);
            Assert.AreEqual(10,pwd2.Length);

            Assert.AreNotEqual(pwd1,pwd2);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecrypt() 
        {
            byte[]      key;
            byte[]      IV;
            byte[]      input;
            byte[]      encrypted;
            byte[]      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            input = new byte[256];
            for (int i=0;i<input.Length;i++)
                input[i] = (byte) i;

            encrypted = Crypto.Encrypt(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,encrypted);

            decrypted = Crypto.Decrypt(encrypted,CryptoAlgorithm.AES,key,IV);
            CollectionAssert.AreEqual(input,decrypted);

            CollectionAssert.AreEqual(new byte[0], Crypto.Encrypt(new byte[0], CryptoAlgorithm.AES, key, IV));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecryptSalt4() 
        {
            byte[]      key;
            byte[]      IV;
            byte[]      input;
            byte[]      encrypted;
            byte[]      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            input = new byte[256];
            for (int i=0;i<input.Length;i++)
                input[i] = (byte) i;

            encrypted = Crypto.EncryptWithSalt4(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,encrypted);

            decrypted = Crypto.DecryptWithSalt4(encrypted,CryptoAlgorithm.AES,key,IV);
            CollectionAssert.AreEqual(input, decrypted);

            CollectionAssert.AreEqual(new byte[0], Crypto.EncryptWithSalt4(new byte[0], CryptoAlgorithm.AES, key, IV));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecryptSalt8()
        {
            byte[]      key;
            byte[]      IV;
            byte[]      input;
            byte[]      encrypted;
            byte[]      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            input = new byte[256];
            for (int i=0;i<input.Length;i++)
                input[i] = (byte) i;

            encrypted = Crypto.EncryptWithSalt8(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,encrypted);

            decrypted = Crypto.DecryptWithSalt8(encrypted,CryptoAlgorithm.AES,key,IV);
            CollectionAssert.AreEqual(input, decrypted);

            CollectionAssert.AreEqual(new byte[0], Crypto.EncryptWithSalt8(new byte[0], CryptoAlgorithm.AES, key, IV));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecrypt_String() 
        {
            byte[]      key;
            byte[]      IV;
            string      input;
            byte[]      encrypted;
            string      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            //-------------------------

            input = "Hello World!";

            encrypted = Crypto.EncryptString(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptString(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);

            //-------------------------

            input = "";

            encrypted = Crypto.EncryptString(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptString(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecrypt_StringWithSalt4() 
        {
            byte[]      key;
            byte[]      IV;
            string      input;
            byte[]      encrypted;
            string      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            //-------------------------

            input = "Hello World!";

            encrypted = Crypto.EncryptStringWithSalt4(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptStringWithSalt4(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);

            //-------------------------

            input = "";

            encrypted = Crypto.EncryptStringWithSalt4(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptStringWithSalt4(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptDecrypt_StringWithSalt8()
        {
            byte[]      key;
            byte[]      IV;
            string      input;
            byte[]      encrypted;
            string      decrypted;

            Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256,out key,out IV);

            //-------------------------

            input = "Hello World!";

            encrypted = Crypto.EncryptStringWithSalt8(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptStringWithSalt8(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);

            //-------------------------

            input = "";

            encrypted = Crypto.EncryptStringWithSalt8(input,CryptoAlgorithm.AES,key,IV);
            Assert.AreNotEqual(input,Helper.FromUTF8(encrypted));

            decrypted = Crypto.DecryptStringWithSalt8(encrypted,CryptoAlgorithm.AES,key,IV);
            Assert.AreEqual(input,decrypted);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_SymmetricKey()
        {
            SymmetricKey    key = Crypto.GenerateSymmetricKey(CryptoAlgorithm.AES,256);
            SymmetricKey    copy;
            string          inputString;
            byte[]          inputBytes;

            copy = new SymmetricKey(key.ToString());
            Assert.AreEqual(key.Algorithm,copy.Algorithm);
            CollectionAssert.AreEqual(key.Key, copy.Key);
            CollectionAssert.AreEqual(key.IV, copy.IV);

            inputString  = "Hello World!";
            Assert.AreEqual(inputString,Crypto.DecryptString(Crypto.EncryptString(inputString,key),key));
            Assert.AreEqual(inputString,Crypto.DecryptStringWithSalt4(Crypto.EncryptStringWithSalt4(inputString,key),key));
            Assert.AreEqual(inputString,Crypto.DecryptStringWithSalt8(Crypto.EncryptStringWithSalt8(inputString,key),key));

            inputBytes  = new byte[] {0,1,2,3,4,5,6,7,8,9};
            CollectionAssert.AreEqual(inputBytes, Crypto.Decrypt(Crypto.Encrypt(inputBytes, key), key));
            CollectionAssert.AreEqual(inputBytes, Crypto.DecryptWithSalt4(Crypto.EncryptWithSalt4(inputBytes, key), key));
            CollectionAssert.AreEqual(inputBytes, Crypto.DecryptWithSalt8(Crypto.EncryptWithSalt8(inputBytes, key), key));

            key = new SymmetricKey("plaintext");
            Assert.AreEqual(CryptoAlgorithm.PlainText,key.Algorithm);
            CollectionAssert.AreEqual(new byte[0], key.Key);
            CollectionAssert.AreEqual(new byte[0], key.IV);
            Assert.AreEqual("PLAINTEXT",key.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptCredentials()
        {
            Credentials     credentials;
            byte[]          encrypted;

            credentials = new Credentials("lilltek.com","jeff.lill","password1234");
            encrypted   = Crypto.EncryptCredentials(credentials);
            credentials = Crypto.DecryptCredentials(encrypted);

            Assert.AreEqual("lilltek.com",credentials.Realm);
            Assert.AreEqual("jeff.lill",credentials.Account);
            Assert.AreEqual("password1234",credentials.Password);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_EncryptPasswordChange() 
        {
            string      orgPassword;
            string      newPassword;
            byte[]      encrypted;

            encrypted = Crypto.EncryptPasswordChange("old password","new password");
            Crypto.DecryptPasswordChange(encrypted,out orgPassword,out newPassword);

            Assert.AreEqual("old password",orgPassword);
            Assert.AreEqual("new password",newPassword);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Crypto_GenerateSymmetricKeyFromSecret() 
        {
            SymmetricKey    key;
            byte[]          encrypted;
           
            // Make sure we can generate a valid key with no salt.

            key       = Crypto.GenerateSymmetricKeyFromSecret(CryptoAlgorithm.AES,256,Encoding.UTF8.GetBytes("Hello World!"),null);
            encrypted = Crypto.EncryptString("This is a test.",key);
            Assert.AreEqual("This is a test.",Crypto.DecryptString(encrypted,key));

            // Make sure that we'll get the same key if we use the same parameters again.

            key = Crypto.GenerateSymmetricKeyFromSecret(CryptoAlgorithm.AES,256,Encoding.UTF8.GetBytes("Hello World!"),null);
            Assert.AreEqual("This is a test.",Crypto.DecryptString(encrypted,key));

            // Make sure that adding salt to the key generation process actually results in
            // a different key.

            try {

                key = Crypto.GenerateSymmetricKeyFromSecret(CryptoAlgorithm.AES,256,Encoding.UTF8.GetBytes("Hello World!"),Crypto.GetSalt8());
                Assert.AreNotEqual("This is a test.",Crypto.DecryptString(encrypted,key));
            }
            catch (CryptographicException) {

                // Getting this exception is OK too, since it indicates that the key was not valid.
            }
        }
    }
}

