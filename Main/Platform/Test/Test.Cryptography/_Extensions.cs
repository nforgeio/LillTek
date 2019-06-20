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

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test 
{
    [TestClass]
    public class _Extensions 
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void Extensions_ArgCollection_Encryption() 
        {
            // Verify that encrypted settings work.

            var key  = new SymmetricKey("aes:RTML47oOp7OoRVDyJLwJOWNaAQRbwGgAU5skz3d6L9A=:hWpp+NJrWNCPkS3H89dINA==");
            var args = new ArgCollection(ArgCollectionType.Unconstrained);

            args.SetEncrypted("test","Hello World!",key);
            Assert.AreEqual("Hello World!",args.GetEncrypted("test",key));
            Assert.AreNotEqual("Hello World!",args.Get("test"));
        }
    }
}


