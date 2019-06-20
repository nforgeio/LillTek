//-----------------------------------------------------------------------------
// FILE:        _DynDnsHostEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _DynDnsHostEntry
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsHostEntry_Construct()
        {
            DynDnsHostEntry entry;

            entry = new DynDnsHostEntry("www.lilltek.com", Helper.ParseIPAddress("10.0.0.1"));
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com.", Helper.ParseIPAddress("10.0.0.1"));
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com", Helper.ParseIPAddress("10.0.0.1"), TimeSpan.FromMinutes(2), DynDnsHostMode.AddressList, false);
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.AddressList, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);
            Assert.IsFalse(entry.IsNAT);

            entry = new DynDnsHostEntry("www.lilltek.com", "redirect.test.com");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);

            entry = new DynDnsHostEntry("www.lilltek.com.", "redirect.test.com.");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);

            entry = new DynDnsHostEntry("www.lilltek.com", "redirect.test.com", TimeSpan.FromMinutes(4), DynDnsHostMode.CName, true);
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromMinutes(4), entry.TTL);
            Assert.IsTrue(entry.IsNAT);

            entry = new DynDnsHostEntry("www.lilltek.com", "redirect.test.com", TimeSpan.FromMinutes(4), DynDnsHostMode.MX, false);
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.MX, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromMinutes(4), entry.TTL);
            Assert.IsFalse(entry.IsNAT);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsHostEntry_Parse()
        {
            DynDnsHostEntry entry;

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com.,10.0.0.1");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,ADDRESS");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);
            Assert.IsFalse(entry.IsNAT);

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,ADDRESS,NAT");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);
            Assert.IsTrue(entry.IsNAT);

            entry = new DynDnsHostEntry("   www.lilltek.com   ,   10.0.0.1   ,   120   ,   ADDRESS   ,   NAT   ");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);
            Assert.IsTrue(entry.IsNAT);

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,AddressList");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.AddressList, entry.HostMode);
            Assert.AreEqual(TimeSpan.FromMinutes(2), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("engine.paraworks.com,        65.249.42.182,       1800, ADDRESS");
            Assert.AreEqual("engine.paraworks.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.Address, entry.HostMode);
            Assert.IsTrue(IPAddress.Parse("65.249.42.182").Equals(entry.Address));
            Assert.AreEqual(TimeSpan.FromSeconds(1800), entry.TTL);
            Assert.IsNull(entry.CName);

            entry = new DynDnsHostEntry("www.lilltek.com,redirect.test.com");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);

            entry = new DynDnsHostEntry("www.lilltek.com,redirect.test.com,5");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromSeconds(5), entry.TTL);

            entry = new DynDnsHostEntry("www.lilltek.com.,redirect.test.com.");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromSeconds(-1), entry.TTL);

            entry = new DynDnsHostEntry("www.lilltek.com,redirect.test.com,240,CName");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.CName, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromMinutes(4), entry.TTL);
            Assert.IsFalse(entry.IsNAT);

            entry = new DynDnsHostEntry("www.lilltek.com,redirect.test.com,240,MX");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.MX, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromMinutes(4), entry.TTL);
            Assert.IsFalse(entry.IsNAT);

            entry = new DynDnsHostEntry("   www.lilltek.com      ,redirect.test.com   ,   240   ,   MX   ");
            Assert.AreEqual("www.lilltek.com.", entry.Host);
            Assert.AreEqual(DynDnsHostMode.MX, entry.HostMode);
            Assert.AreEqual("redirect.test.com.", entry.CName);
            Assert.AreEqual(TimeSpan.FromMinutes(4), entry.TTL);
            Assert.IsFalse(entry.IsNAT);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void DynDnsHostEntry_Serialize()
        {
            DynDnsHostEntry entry;

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,Address");
            Assert.AreEqual("www.lilltek.com.,10.0.0.1,120,Address", entry.ToString());

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,Address,nat");
            Assert.AreEqual("www.lilltek.com.,10.0.0.1,120,Address,NAT", entry.ToString());

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,Address,NAT");
            Assert.AreEqual("www.lilltek.com.,10.0.0.1,120,Address,NAT", entry.ToString());

            entry = new DynDnsHostEntry("www.lilltek.com,10.0.0.1,120,AddressList");
            Assert.AreEqual("www.lilltek.com.,10.0.0.1,120,AddressList", entry.ToString());

            entry = new DynDnsHostEntry("www.lilltek.com,test.com,120,cname");
            Assert.AreEqual("www.lilltek.com.,test.com.,120,CName", entry.ToString());

            entry = new DynDnsHostEntry("www.lilltek.com,test.com,120,mx");
            Assert.AreEqual("www.lilltek.com.,test.com.,120,MX", entry.ToString());
        }
    }
}

