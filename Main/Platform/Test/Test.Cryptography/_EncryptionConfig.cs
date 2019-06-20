//-----------------------------------------------------------------------------
// FILE:        _BlockCryptography.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for EncryptionConfig

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
    public class _EncryptionConfig 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void EncryptionConfig_Parse()
        {
            EncryptionConfig    config;

            config = EncryptionConfig.Parse("foo:10");
            Assert.AreEqual("foo",config.Algorithm);
            Assert.AreEqual(1,config.KeySizes.Length);
            Assert.AreEqual(10,config.KeySizes[0]);

            config = EncryptionConfig.Parse("foo:10,20");
            Assert.AreEqual("foo",config.Algorithm);
            Assert.AreEqual(2,config.KeySizes.Length);
            Assert.AreEqual(10,config.KeySizes[0]);
            Assert.AreEqual(20,config.KeySizes[1]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void EncryptionConfig_Render()
        {
            EncryptionConfig    config;

            config = EncryptionConfig.Parse("foo:10");
            Assert.AreEqual("foo:10",config.ToString());

            config = EncryptionConfig.Parse("foo:10,20");
            Assert.AreEqual("foo:10,20",config.ToString());
        }
    }
}

