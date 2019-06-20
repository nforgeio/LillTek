//-----------------------------------------------------------------------------
// FILE:        _DynDnsMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Reflection;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _DynDnsMessage
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsMessage_Construct()
        {
            DynDnsMessage msg;

            msg = new DynDnsMessage(DynDnsMessageFlag.OpRegister, new DynDnsHostEntry("www.test.com.", Helper.ParseIPAddress("10.0.0.1")));
            Assert.AreEqual(DynDnsMessageFlag.OpRegister, msg.Flags);
            Assert.AreNotEqual(default(DateTime), msg.TimeStampUtc);
            Assert.AreEqual("www.test.com.", msg.HostEntry.Host);
            Assert.AreEqual(Helper.ParseIPAddress("10.0.0.1"), msg.HostEntry.Address);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsMessage_Serialize()
        {
            SymmetricKey sharedKey = new SymmetricKey("aes:6pFejMePFq2f9746ddUBHOCnmobFKY2/byPC47nBBaA=:kyvW/zm4JbXtCycxGg9s7Q==");
            DateTime now = DateTime.UtcNow;
            DynDnsMessage msg;
            byte[] packet;

            msg = new DynDnsMessage(DynDnsMessageFlag.OpUnregister, new DynDnsHostEntry("www.lilltek.com.,redirect.test.com.,240,CName"));
            msg.TimeStampUtc = now;
            packet = msg.ToArray(sharedKey);

            msg = new DynDnsMessage(packet, sharedKey);
            Assert.AreEqual(DynDnsMessageFlag.OpUnregister, msg.Flags);
            Assert.AreEqual(now, msg.TimeStampUtc);
            Assert.AreEqual("www.lilltek.com.,redirect.test.com.,240,CName", msg.HostEntry.ToString());
        }
    }
}

