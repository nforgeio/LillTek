//-----------------------------------------------------------------------------
// FILE:        _IPFilter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _IPFilter
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void IPFilter_Default()
        {
            IPFilter filter;

            filter = new IPFilter(true);
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            filter.Add(new IPFilterItem(false, IPAddress.Parse("4.3.2.1")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("4.3.2.1")));

            filter = new IPFilter(false);
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            filter.Add(new IPFilterItem(true, IPAddress.Parse("4.3.2.1")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("4.3.2.1")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void IPFilter_Basic()
        {
            IPFilter filter;

            filter = new IPFilter(false);
            filter.Add(new IPFilterItem(true, IPAddress.Parse("10.0.0.1")));
            filter.Add(new IPFilterItem(true, IPAddress.Parse("10.0.0.2")));
            filter.Add(new IPFilterItem(true, IPAddress.Parse("10.0.0.3")));
            filter.Add(new IPFilterItem(true, IPAddress.Parse("168.172.1.1")));

            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.1")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.2")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.3")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("168.172.1.1")));

            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("11.0.0.1")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("12.0.0.2")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void IPFilter_Subnet()
        {
            IPFilter filter;

            filter = new IPFilter(false);
            filter.Add(new IPFilterItem(true, IPAddress.Parse("1.2.3.4"), 24));
            filter.Add(new IPFilterItem(true, IPAddress.Parse("10.11.12.13"), 16));

            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.0")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.255")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.4.0")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.1.255")));

            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.11.12.13")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.11.255.13")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.11.0.13")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.12.0.0")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.10.255.255")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void IPFilter_Parse()
        {
            IPFilter filter;

            filter = IPFilter.Parse(@"");
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));

            filter = IPFilter.Parse(@"default:grant");
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));

            filter = IPFilter.Parse(@"default:deny");
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));

            filter = IPFilter.Parse(" \t  default\r\n:  grant\r\n,");
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));

            filter = IPFilter.Parse(@"

    default:deny,
    1.2.3.4:grant,
    10.0.0.0/24:grant,
");
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.0")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.128")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.0.255")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("4.3.2.1")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.0.1.0")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("9.255.255.255")));

            filter = IPFilter.Parse(@"

    default:grant,
    1.2.3.4:deny,
    10.0.0.0/24:deny,
");
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("1.2.3.4")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.0.0.0")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.0.0.128")));
            Assert.IsFalse(filter.GrantAccess(IPAddress.Parse("10.0.0.255")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("4.3.2.1")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("10.0.1.0")));
            Assert.IsTrue(filter.GrantAccess(IPAddress.Parse("9.255.255.255")));
        }
    }
}

