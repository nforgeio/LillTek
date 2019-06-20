//-----------------------------------------------------------------------------
// FILE:        App_DynDnsClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.DynDnsClient_Test
{
    [TestClass]
    public class App_DynDnsClient
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void DynDnsClient_Broadcast()
        {
            // $todo(jeff.lill): Implement a test
            Assert.Inconclusive("Implement a test");
        }
    }
}

