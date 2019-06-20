//-----------------------------------------------------------------------------
// FILE:        _Hashers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the hasher classes

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
    public class _Hashers 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_MD5()
        {
            EnhancedMemoryStream    ms;
            byte[]                  data;
            byte[]                  digest1,digest2;

            digest1 = MD5Hasher.Compute(new byte[] {0,1,2,3},0,4);
            Assert.AreEqual(16,digest1.Length);
            Assert.AreEqual(16,MD5Hasher.DigestSize);

            digest2 = MD5Hasher.Compute(new byte[] {1,1,2,3},0,4);
            Assert.AreNotEqual(digest1,digest2);

            digest1 = MD5Hasher.Compute(new byte[0]);
            Assert.AreEqual(16,digest1.Length);
            Assert.AreEqual(16,MD5Hasher.DigestSize);

            digest1 = MD5Hasher.Compute(new byte[] {0,1,2,3});
            ms      = new EnhancedMemoryStream();

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(new byte[] {0,1,2,3},0,4);
            ms.Seek(0,SeekOrigin.Begin);
            digest2 = MD5Hasher.Compute(ms,4);
            CollectionAssert.AreEqual(digest1,digest2);
            Assert.AreEqual(16,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            data = new byte[2048];
            for (int i=0;i<data.Length;i++)
                data[i] = (byte) i;

            digest1 = MD5Hasher.Compute(data);

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(data,0,data.Length);
            ms.Seek(0,SeekOrigin.Begin);

            digest2 = MD5Hasher.Compute(ms,data.Length);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(16,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            digest1 = MD5Hasher.Compute("hello");
            digest2 = MD5Hasher.Compute("world");
            CollectionAssert.AreNotEqual(digest1, digest2);
            CollectionAssert.AreEqual(digest1, MD5Hasher.Compute("hello"));

            // These really aren't very good tests for folding but
            // at least they'll verify that it doesn't crash

            Assert.AreEqual(MD5Hasher.FoldOnce(new byte[] {0,1,2,3}),MD5Hasher.FoldOnce(new byte[] {0,1,2,3}));
            Assert.AreNotEqual((object) MD5Hasher.FoldOnce(new byte[] {1,1,2,3}),(object) MD5Hasher.FoldOnce(new byte[] {0,1,2,3}));
            Assert.AreEqual(MD5Hasher.FoldTwice(new byte[] {0,1,2,3}),MD5Hasher.FoldTwice(new byte[] {0,1,2,3}));
            Assert.AreNotEqual(MD5Hasher.FoldTwice(new byte[] {1,1,2,3}),MD5Hasher.FoldTwice(new byte[] {0,1,2,3}));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_HMAC_MD5() 
        {
            byte[]      data = new byte[] {0,1,2,3,4,5,6,7,8,9};
            byte[]      key;
            byte[]      digest;

            key    = new byte[] {0,1,2,3,4,5};
            digest = MD5Hasher.Compute(key,data);
            Assert.AreEqual(MD5Hasher.DigestSize,digest.Length);

            key = new byte[128];
            for (int i=0;i<key.Length;i++)
                key[i] = (byte) i;

            digest = MD5Hasher.Compute(key,data);
            Assert.AreEqual(MD5Hasher.DigestSize,digest.Length);

            // The data for this test came from RFC2104

            key  = new byte[16];
            for (int i=0;i<key.Length;i++)
                key[i] = 0xAA;

            data = new byte[50];
            for (int i=0;i<data.Length;i++)
                data[i] = 0xDD;

            digest = MD5Hasher.Compute(key,data);
            CollectionAssert.AreEqual(Helper.FromHex("56be34521d144c88dbb8c733f0e8b3f6"), digest);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_SHA1() 
        {
            EnhancedMemoryStream    ms;
            byte[]                  data;
            byte[]                  digest1,digest2;

            digest1 = SHA1Hasher.Compute(new byte[] {0,1,2,3},0,4);
            Assert.AreEqual(20,digest1.Length);
            Assert.AreEqual(20,SHA1Hasher.DigestSize);

            digest2 = SHA1Hasher.Compute(new byte[] {1,1,2,3},0,4);
            Assert.AreNotEqual(digest1,digest2);

            digest1 = SHA1Hasher.Compute(new byte[0]);
            Assert.AreEqual(20,digest1.Length);
            Assert.AreEqual(20,SHA1Hasher.DigestSize);

            digest1 = SHA1Hasher.Compute(new byte[] {0,1,2,3});
            ms      = new EnhancedMemoryStream();

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(new byte[] {0,1,2,3},0,4);
            ms.Seek(0,SeekOrigin.Begin);
            digest2 = SHA1Hasher.Compute(ms,4);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(20,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            data = new byte[2048];
            for (int i=0;i<data.Length;i++)
                data[i] = (byte) i;

            digest1 = SHA1Hasher.Compute(data);

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(data,0,data.Length);
            ms.Seek(0,SeekOrigin.Begin);

            digest2 = SHA1Hasher.Compute(ms,data.Length);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(20,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            digest1 = SHA1Hasher.Compute("hello");
            digest2 = SHA1Hasher.Compute("world");
            CollectionAssert.AreNotEqual(digest1, digest2);
            CollectionAssert.AreEqual(digest1, SHA1Hasher.Compute("hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_HMAC_SHA1() 
        {
            byte[]      data = new byte[] {0,1,2,3,4,5,6,7,8,9};
            byte[]      key;
            byte[]      digest;

            key    = new byte[] {0,1,2,3,4,5};
            digest = SHA1Hasher.Compute(key,data);
            Assert.AreEqual(SHA1Hasher.DigestSize,digest.Length);

            key = new byte[128];
            for (int i=0;i<key.Length;i++)
                key[i] = (byte) i;

            digest = SHA1Hasher.Compute(key,data);
            Assert.AreEqual(SHA1Hasher.DigestSize,digest.Length);

            // $todo(jeff.lill): 
            //
            // At some point I'd like to verify this
            // against a hash produced by another
            // codebase.
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_SHA256() 
        {
            EnhancedMemoryStream    ms;
            byte[]                  data;
            byte[]                  digest1,digest2;

            digest1 = SHA256Hasher.Compute(new byte[] {0,1,2,3},0,4);
            Assert.AreEqual(32,digest1.Length);
            Assert.AreEqual(32,SHA256Hasher.DigestSize);

            digest2 = SHA256Hasher.Compute(new byte[] { 1,1,2,3 },0,4);
            CollectionAssert.AreNotEqual(digest1, digest2);

            digest1 = SHA256Hasher.Compute(new byte[0]);
            Assert.AreEqual(32,digest1.Length);
            Assert.AreEqual(32,SHA256Hasher.DigestSize);

            digest1 = SHA256Hasher.Compute(new byte[] { 0,1,2,3 });
            ms      = new EnhancedMemoryStream();

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(new byte[] {0,1,2,3},0,4);
            ms.Seek(0,SeekOrigin.Begin);
            digest2 = SHA256Hasher.Compute(ms,4);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(32,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            data = new byte[2048];
            for (int i=0;i<data.Length;i++)
                data[i] = (byte) i;

            digest1 = SHA256Hasher.Compute(data);

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(data,0,data.Length);
            ms.Seek(0,SeekOrigin.Begin);

            digest2 = SHA256Hasher.Compute(ms,data.Length);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(32,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            digest1 = SHA256Hasher.Compute("hello");
            digest2 = SHA256Hasher.Compute("world");
            CollectionAssert.AreNotEqual(digest1, digest2);
            CollectionAssert.AreEqual(digest1, SHA256Hasher.Compute("hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_HMAC_SHA256()
        {
            byte[]      data = new byte[] {0,1,2,3,4,5,6,7,8,9};
            byte[]      key;
            byte[]      digest;

            key    = new byte[] {0,1,2,3,4,5};
            digest = SHA256Hasher.Compute(key,data);
            Assert.AreEqual(SHA256Hasher.DigestSize,digest.Length);

            key = new byte[128];
            for (int i=0;i<key.Length;i++)
                key[i] = (byte) i;

            digest = SHA256Hasher.Compute(key,data);
            Assert.AreEqual(SHA256Hasher.DigestSize,digest.Length);

            // $todo(jeff.lill):
            //
            // At some point I'd like to verify this
            // against a hash produced by another
            // codebase.
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_SHA512() 
        {
            EnhancedMemoryStream    ms;
            byte[]                  data;
            byte[]                  digest1,digest2;

            digest1 = SHA512Hasher.Compute(new byte[] {0,1,2,3},0,4);
            Assert.AreEqual(64,digest1.Length);
            Assert.AreEqual(64,SHA512Hasher.DigestSize);

            digest2 = SHA512Hasher.Compute(new byte[] { 1,1,2,3 },0,4);
            CollectionAssert.AreNotEqual(digest1, digest2);

            digest1 = SHA512Hasher.Compute(new byte[0]);
            Assert.AreEqual(64,digest1.Length);
            Assert.AreEqual(64,SHA512Hasher.DigestSize);

            digest1 = SHA512Hasher.Compute(new byte[] { 0,1,2,3 });
            ms      = new EnhancedMemoryStream();

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(new byte[] {0,1,2,3},0,4);
            ms.Seek(0,SeekOrigin.Begin);
            digest2 = SHA512Hasher.Compute(ms,4);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(64,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            data = new byte[2048];
            for (int i=0;i<data.Length;i++)
                data[i] = (byte) i;

            digest1 = SHA512Hasher.Compute(data);

            ms.Seek(0,SeekOrigin.Begin);
            ms.Write(data,0,data.Length);
            ms.Seek(0,SeekOrigin.Begin);

            digest2 = SHA512Hasher.Compute(ms,data.Length);
            CollectionAssert.AreEqual(digest1, digest2);
            Assert.AreEqual(64,digest2.Length);
            Assert.AreEqual(0,ms.Position);

            digest1 = SHA512Hasher.Compute("hello");
            digest2 = SHA512Hasher.Compute("world");
            CollectionAssert.AreNotEqual(digest1, digest2);
            CollectionAssert.AreEqual(digest1, SHA512Hasher.Compute("hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Hashers_HMAC_SHA512Hasher()
        {
            byte[]      data = new byte[] {0,1,2,3,4,5,6,7,8,9};
            byte[]      key;
            byte[]      digest;

            key    = new byte[] {0,1,2,3,4,5};
            digest = SHA512Hasher.Compute(key,data);
            Assert.AreEqual(SHA512Hasher.DigestSize,digest.Length);

            key = new byte[128];
            for (int i=0;i<key.Length;i++)
                key[i] = (byte) i;

            digest = SHA512Hasher.Compute(key,data);
            Assert.AreEqual(SHA512Hasher.DigestSize,digest.Length);

            // $todo(jeff.lill): 
            //
            // At some point I'd like to verify this
            // against a hash produced by another
            // codebase.
        }
    }
}

