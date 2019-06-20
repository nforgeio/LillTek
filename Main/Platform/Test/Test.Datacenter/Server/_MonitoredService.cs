//-----------------------------------------------------------------------------
// FILE:        _MonitoredService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Sockets;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _MonitoredService
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void MonitoredService_Parse()
        {
            MonitoredService service;

            service = new MonitoredService("http://myservice.com");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsTrue(service.IsCritical);

            service = new MonitoredService("http://myservice.com,CRITICAL");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsTrue(service.IsCritical);

            service = new MonitoredService("http://myservice.com,critical");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsTrue(service.IsCritical);

            service = new MonitoredService("  http://myservice.com  ,  CRITICAL  ");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsTrue(service.IsCritical);

            service = new MonitoredService("http://myservice.com,NONCRITICAL");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsFalse(service.IsCritical);

            service = new MonitoredService("http://myservice.com,noncritical");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsFalse(service.IsCritical);

            service = new MonitoredService("  http://myservice.com  ,  NONCRITICAL  ");
            Assert.AreEqual(new Uri("http://myservice.com"), service.Uri);
            Assert.IsFalse(service.IsCritical);
        }
    }
}

