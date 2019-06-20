//-----------------------------------------------------------------------------
// FILE:        _RequestSignature.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Cryptography.Test {

    [TestClass]
    public class _RequestSignature 
    {
        private TimeSpan    graceInterval = TimeSpan.FromMinutes(1);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void RequestSignature_Args() 
        {
            ArgCollection   args = new ArgCollection();
            SymmetricKey    key  = Crypto.GenerateSymmetricKey();
            string          signature;

            // Verify a request with no arguments.

            args.Clear();
            signature = RequestSignature.Generate(key,args);
            RequestSignature.Verify(key,signature,args,null,graceInterval);

            // Verify a request with a few arguments

            args.Clear();
            args["arg1"] = "hello";
            args["arg2"] = "world";
            args["arg3"] = "test";

            signature = RequestSignature.Generate(key,args);
            Assert.IsTrue(RequestSignature.TryVerify(key,signature,args,null,graceInterval));

            // Verify that argument normaization works by reversing the
            // orher of the arguments added.

            args.Clear();
            args["arg3"] = "test";
            args["arg2"] = "world";
            args["arg1"] = "hello";
            Assert.IsTrue(RequestSignature.TryVerify(key,signature,args,null,graceInterval));

            // Test with the signature as part of the arguments.

            args["signature"] = signature;
            Assert.IsTrue(RequestSignature.TryVerify(key,signature,args,"signature",graceInterval));

            // Tamper with one of the argument values and verify failure

            args["arg2"] = "xxx";
            Assert.IsFalse(RequestSignature.TryVerify(key,signature,args,null,graceInterval));

            // Do it again

            args["arg2"] = "WORLD";

            ExtendedAssert.Throws<SecurityException>(
                () =>
                {
                    RequestSignature.Verify(key,signature,args,null,graceInterval);
                    Assert.Fail("SecurityException expected");
                });

            // Change the shared key and verify failure

            args.Clear();
            args["arg1"] = "hello";
            args["arg2"] = "world";
            args["arg3"] = "test";

            signature = RequestSignature.Generate(key,args);
            Assert.IsFalse(RequestSignature.TryVerify(Crypto.GenerateSymmetricKey(),signature,args,null,graceInterval));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void RequestSignature_Time()
        {
            // Verify that time checks work

            ArgCollection   args = new ArgCollection();
            SymmetricKey    key  = Crypto.GenerateSymmetricKey();
            string          signature;

            args.Clear();
            args["arg1"] = "hello";
            args["arg2"] = "world";
            args["arg3"] = "test";

            // Verify a request with no arguments.

            signature = RequestSignature.Generate(key,args);
            Thread.Sleep(2000);
            Assert.IsTrue(RequestSignature.TryVerify(key,signature,args,null,TimeSpan.FromSeconds(4)));

            args.Clear();
            signature = RequestSignature.Generate(key,args);
            Thread.Sleep(6000);
            Assert.IsFalse(RequestSignature.TryVerify(key,signature,args,null,TimeSpan.FromSeconds(4)));
        }
    }
}

