//-----------------------------------------------------------------------------
// FILE:        _SecureTicket.cs
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
    public class _SecureTicket
    {
        private TimeSpan    delta = TimeSpan.FromSeconds(1.25);     // Need a bit of slop in time comparisons below
                                                                    // because ticket times only resolve down to 
                                                                    // seconds and there may be a bit of extra time
                                                                    // introduced during the processing of the test.
        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureTicket_Basic() 
        {
            DateTime        now = DateTime.UtcNow;
            SecureTicket    ticket;

            ticket = new SecureTicket("my-resource",TimeSpan.FromSeconds(10));
            ticket.Set("arg0","myarg-0");
            ticket.Set("arg1","myarg-1");
            ticket["arg2"] = "myarg-2";

            Assert.AreEqual("my-resource",ticket.Resource);
            Assert.AreEqual(TimeSpan.FromSeconds(10),ticket.Lifespan);
            Assert.IsTrue(now <= ticket.IssuerExpirationUtc);
            Assert.IsTrue(now <= ticket.ClientExpirationUtc);
            Assert.AreEqual("myarg-0",ticket["arg0"]);
            Assert.AreEqual("myarg-1",ticket["arg1"]);
            Assert.AreEqual("myarg-2",ticket["arg2"]);
            Assert.AreEqual("myarg-0",ticket.Get("arg0","hello"));
            Assert.AreEqual("hello",ticket.Get("XXXX","hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureTicket_Encrypt()
        {
            DateTime        now = DateTime.UtcNow;
            SymmetricKey    key = new SymmetricKey("aes:nKeO0+uMoi1YokIzmKX6PhFFGp7RO/gPWLhQ2XBftfU=:nzLvnGh2HUPdQCXUgZybeQ==");
            SecureTicket    ticket;
            byte[]          encrypted;
            byte[]          encryptedBytes;

            // Server side ticket.

            ticket = new SecureTicket("my-resource",TimeSpan.FromSeconds(10));
            ticket.Set("arg0","myarg-0-this is a really long string-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
            ticket.Set("arg1","myarg-1-this is a really long string");
            ticket["arg2"] = "myarg-2-this is a really long string";

            encrypted = ticket.ToArray(key);
            ticket    = SecureTicket.Parse(key,encrypted);

            Assert.AreEqual("my-resource",ticket.Resource);
            Assert.AreEqual(TimeSpan.FromSeconds(10),ticket.Lifespan);
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.IssuerExpirationUtc,delta));
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.ClientExpirationUtc,delta));
            Assert.AreEqual("myarg-0-this is a really long string-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",ticket["arg0"]);
            Assert.AreEqual("myarg-1-this is a really long string",ticket["arg1"]);
            Assert.AreEqual("myarg-2-this is a really long string",ticket["arg2"]);
            Assert.AreEqual("myarg-0-this is a really long string-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",ticket.Get("arg0","hello"));
            Assert.AreEqual("hello",ticket.Get("XXXX","hello"));

            // Client side ticket.

            ticket = SecureTicket.Parse(ticket.ToArray(key));

            Assert.AreEqual(TimeSpan.FromSeconds(10),ticket.Lifespan);
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.ClientExpirationUtc,delta));

            // Parse server side ticket from client generated bytes.

            encryptedBytes = ticket.ToArray();
            ticket         = new SecureTicket(key,encryptedBytes);

            Assert.AreEqual("my-resource",ticket.Resource);
            Assert.AreEqual(TimeSpan.FromSeconds(10),ticket.Lifespan);
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.IssuerExpirationUtc,delta));
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.ClientExpirationUtc,delta));
            Assert.AreEqual("myarg-0-this is a really long string-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",ticket["arg0"]);
            Assert.AreEqual("myarg-1-this is a really long string",ticket["arg1"]);
            Assert.AreEqual("myarg-2-this is a really long string",ticket["arg2"]);
            Assert.AreEqual("myarg-0-this is a really long string-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",ticket.Get("arg0","hello"));
            Assert.AreEqual("hello",ticket.Get("XXXX","hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureTicket_Encrypt_Base64()
        {
            DateTime        now = DateTime.UtcNow;
            SymmetricKey    key = new SymmetricKey("aes:Cz1uS3EYB5aLDgXdKKmzGPnMU3/QwK1i+8nY3KuUaCw=:Toel2ZQR6TBtOvq+zatyoA==");
            SecureTicket    ticket;
            string          encrypted;

            ticket = new SecureTicket("my-resource",TimeSpan.FromSeconds(10));
            ticket.Set("arg0","myarg-0");
            ticket.Set("arg1","myarg-1");
            ticket["arg2"] = "myarg-2";

            encrypted = ticket.ToBase64String(key);
            ticket    = SecureTicket.Parse(key,encrypted);

            Assert.AreEqual("my-resource",ticket.Resource);
            Assert.AreEqual(TimeSpan.FromSeconds(10),ticket.Lifespan);
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.IssuerExpirationUtc,delta));
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(10),ticket.ClientExpirationUtc,delta));
            Assert.AreEqual("myarg-0",ticket["arg0"]);
            Assert.AreEqual("myarg-1",ticket["arg1"]);
            Assert.AreEqual("myarg-2",ticket["arg2"]);
            Assert.AreEqual("myarg-0",ticket.Get("arg0","hello"));
            Assert.AreEqual("hello",ticket.Get("XXXX","hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureTicket_ClientClock() 
        {
            DateTime        now = DateTime.UtcNow;
            SymmetricKey    key = new SymmetricKey("aes:Cz1uS3EYB5aLDgXdKKmzGPnMU3/QwK1i+8nY3KuUaCw=:Toel2ZQR6TBtOvq+zatyoA==");
            SecureTicket    ticket;
            string          encrypted;

            ticket = new SecureTicket("my-resource",TimeSpan.FromSeconds(100));
            ticket.Set("arg0","myarg-0");
            ticket.Set("arg1","myarg-1");
            ticket["arg2"] = "myarg-2";

            Thread.Sleep(5000);     // Simulate clock skew between the issuer and client

            encrypted = ticket.ToBase64String(key);
            ticket    = SecureTicket.Parse(key,encrypted);

            Assert.AreEqual("my-resource",ticket.Resource);
            Assert.AreEqual(TimeSpan.FromSeconds(100),ticket.Lifespan);
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(100),ticket.IssuerExpirationUtc,delta));
            Assert.IsTrue(Helper.Within(now + TimeSpan.FromSeconds(105),ticket.ClientExpirationUtc,delta));
            Assert.AreEqual("myarg-0",ticket["arg0"]);
            Assert.AreEqual("myarg-1",ticket["arg1"]);
            Assert.AreEqual("myarg-2",ticket["arg2"]);
            Assert.AreEqual("myarg-0",ticket.Get("arg0","hello"));
            Assert.AreEqual("hello",ticket.Get("XXXX","hello"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Cryptography")]
        public void SecureTicket_Tamper() 
        {
            DateTime        now = DateTime.UtcNow;
            SymmetricKey    key = new SymmetricKey("aes:Cz1uS3EYB5aLDgXdKKmzGPnMU3/QwK1i+8nY3KuUaCw=:Toel2ZQR6TBtOvq+zatyoA==");
            SecureTicket    ticket;
            byte[]          encrypted;

            ticket = new SecureTicket("my-resource",TimeSpan.FromSeconds(10));
            ticket.Set("arg0","myarg-0");
            ticket.Set("arg1","myarg-1");
            ticket["arg2"] = "myarg-2";

            encrypted    = ticket.ToArray(key);
            encrypted[0] = (byte) ~encrypted[0];

            ExtendedAssert.Throws<CryptographicException>(
                () =>
                {
                    ticket = SecureTicket.Parse(key,encrypted);
                    Assert.Fail("Expected a CryptographicException");
                });
        }
    }
}

