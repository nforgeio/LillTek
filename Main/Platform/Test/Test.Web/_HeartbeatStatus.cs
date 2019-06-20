//-----------------------------------------------------------------------------
// FILE:        _HeartbeatStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Service;
using LillTek.Testing;

namespace LillTek.Web.Test
{
    [TestClass]
    public class _HeartbeatStatus
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void HeartbeatStatus_Serialize()
        {
            HeartbeatStatus status;
            XElement element;

            status = new HeartbeatStatus(HealthStatus.Warning, "Test Message");
            element = status.ToElement();

            status = new HeartbeatStatus(element);
            Assert.AreEqual(HealthStatus.Warning, status.Status);
            Assert.AreEqual("Test Message", status.Message);
        }
    }
}

