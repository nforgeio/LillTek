//-----------------------------------------------------------------------------
// FILE:        _DialedEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Common.NUnit
{
    [TestClass]
    public class _DialedEndpoint
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void DialedEndpoint_Basic()
        {
            var endpoint = new DialedEndpoint("extensions/1000");

            Assert.AreEqual("extensions/1000", endpoint.ToString());

            endpoint.Variables["foo"] = "bar";
            Assert.AreEqual("[foo=bar]extensions/1000", endpoint.ToString());

            endpoint.Variables["Hello"] = "World";
            Assert.AreEqual("[foo=bar,Hello=World]extensions/1000", endpoint.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void DialedEndpoint_Escaping()
        {
            var endpoint = new DialedEndpoint("extensions/1000");

            endpoint.Variables["list"] = "one,two";
            Assert.AreEqual("[list=^^:one:two]extensions/1000", endpoint.ToString());
        }
    }
}

