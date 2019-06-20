//-----------------------------------------------------------------------------
// FILE:        _EnhancedDns.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the EnhancedDns class

// $todo(jeff.lill): Implement a comprehensive set of tests.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _EnhancedDns
    {

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedDns_HostLookup_Cached()
        {
            DateTime start;
            string duration;

            try
            {
                EnhancedDns.EnableCaching = true;
                start = SysTime.Now;

                for (int i = 0; i < 10; i++)
                {

                    EnhancedDns.GetHostByName("localhost");
                    EnhancedDns.GetHostByName("www.google.com");
                    EnhancedDns.GetHostByName("www.microsoft.com");
                    EnhancedDns.GetHostByName("www.ibm.com");
                }

                duration = (SysTime.Now - start).TotalSeconds.ToString();
            }
            finally
            {
                EnhancedDns.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedDns_HostLookup_Uncached()
        {
            DateTime start;
            string duration;

            try
            {
                EnhancedDns.EnableCaching = false;
                start = SysTime.Now;

                for (int i = 0; i < 10; i++)
                {
                    EnhancedDns.GetHostByName("localhost");
                    EnhancedDns.GetHostByName("www.google.com");
                    EnhancedDns.GetHostByName("www.microsoft.com");
                    EnhancedDns.GetHostByName("www.ibm.com");
                }

                duration = (SysTime.Now - start).TotalSeconds.ToString();
            }
            finally
            {
                EnhancedDns.Reset();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedDns_AddRemoveHosts()
        {
            // Verify that we can modify the HOSTS file.  Note that it may
            // be necessary to disable local Virus and Spyware detection
            // software for this test to pass.

            try
            {
                IPHostEntry entry;

                try
                {
                    Dns.GetHostEntry("test01.test.lilltek.com");
                    Assert.Fail();
                }
                catch (SocketException)
                {
                    // Expecting this to fail
                }

                try
                {
                    Dns.GetHostEntry("test02.test.lilltek.com");
                    Assert.Fail();
                }
                catch (SocketException)
                {
                    // Expecting this to fail
                }

                EnhancedDns.AddHost("test01.test.lilltek.com", IPAddress.Parse("72.0.0.1"));
                EnhancedDns.AddHost("test02.test.lilltek.com", IPAddress.Parse("72.0.0.2"));

                entry = Dns.GetHostEntry("test01.test.lilltek.com");
                Assert.AreEqual(IPAddress.Parse("72.0.0.1"), entry.AddressList[0]);

                entry = Dns.GetHostEntry("test02.test.lilltek.com");
                Assert.AreEqual(IPAddress.Parse("72.0.0.2"), entry.AddressList[0]);

                EnhancedDns.RemoveHosts();

                try
                {
                    Dns.GetHostEntry("test01.test.lilltek.com");
                    Assert.Fail();
                }
                catch (SocketException)
                {
                    // Expecting this to fail
                }

                try
                {
                    Dns.GetHostEntry("test02.test.lilltek.com");
                    Assert.Fail();
                }
                catch (SocketException)
                {
                    // Expecting this to fail
                }

            }
            finally
            {
                EnhancedDns.RemoveHosts();
                EnhancedDns.Reset();
            }
        }
    }
}

