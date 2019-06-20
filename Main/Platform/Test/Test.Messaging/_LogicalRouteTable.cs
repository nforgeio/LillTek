//-----------------------------------------------------------------------------
// FILE:        _LogicalRouteTable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests the LogicalRouteTable class.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _LogicalRouteTable
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRouteTable_Basic()
        {

            Assert.AreEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                            new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                            new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Helper.NewGuid(), new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                            new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Helper.NewGuid(), new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now + TimeSpan.FromDays(1)));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 10), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 55), new IPEndPoint(IPAddress.Loopback, 10), SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Parse("10.0.0.1"), 55), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Parse("10.0.0.1"), 55), SysTime.Now));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/x", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 55), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, null, new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));

            Assert.AreNotEqual(new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), null, SysTime.Now),
                               new PhysicalRoute("physical://root/hub/leaf", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 55), SysTime.Now));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRouteTable_Lookup()
        {
            MsgRouter router = new MsgRouter();
            LogicalRouteTable routes = new LogicalRouteTable(router);
            DateTime sysNow = SysTime.Now;
            TimeSpan ttl = TimeSpan.FromSeconds(2);
            List<LogicalRoute> matches;
            string[] s;

            Assert.AreEqual(0, routes.Count);
            Assert.AreEqual(0, routes.GetRoutes("logical://foo").Count);

            routes.Add(new LogicalRoute("logical://foo", new PhysicalRoute("physical://root0", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            Assert.AreEqual(1, routes.GetRoutes("logical://foo").Count);
            Assert.AreEqual(new MsgEP("physical://root0"), routes.GetRoutes("logical://foo")[0].PhysicalRoute.RouterEP);

            Assert.AreEqual(0, routes.GetRoutes("logical://bar").Count);

            routes.Add(new LogicalRoute("logical://foo", new PhysicalRoute("physical://root1", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            matches = routes.GetRoutes("logical://foo");
            Assert.AreEqual(2, matches.Count);
            s = new string[2];
            s[0] = matches[0].PhysicalRoute.RouterEP.ToString();
            s[1] = matches[1].PhysicalRoute.RouterEP.ToString();
            Array.Sort(s);
            Assert.AreEqual("physical://root0", s[0]);
            Assert.AreEqual("physical://root1", s[1]);

            Assert.AreEqual(0, routes.GetRoutes("logical://bar").Count);

            matches = routes.GetRoutes("logical://*");
            Assert.AreEqual(2, matches.Count);
            s = new string[2];
            s[0] = matches[0].PhysicalRoute.RouterEP.ToString();
            s[1] = matches[1].PhysicalRoute.RouterEP.ToString();
            Array.Sort(s);
            Assert.AreEqual("physical://root0", s[0]);
            Assert.AreEqual("physical://root1", s[1]);

            routes.Add(new LogicalRoute("logical://bar", new PhysicalRoute("physical://root2", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            routes.Add(new LogicalRoute("logical://foo/bar", new PhysicalRoute("physical://root3", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            routes.Add(new LogicalRoute("logical://foo/bar0", new PhysicalRoute("physical://root4", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            routes.Add(new LogicalRoute("logical://foo/bar1", new PhysicalRoute("physical://root5", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            Assert.AreEqual(6, routes.Count);

            matches = routes.GetRoutes("logical://*");
            Assert.AreEqual(6, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root0", s[0]);
            Assert.AreEqual("physical://root1", s[1]);
            Assert.AreEqual("physical://root2", s[2]);
            Assert.AreEqual("physical://root3", s[3]);
            Assert.AreEqual("physical://root4", s[4]);
            Assert.AreEqual("physical://root5", s[5]);

            matches = routes.GetRoutes("logical://foo/*");
            Assert.AreEqual(5, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root0", s[0]);
            Assert.AreEqual("physical://root1", s[1]);
            Assert.AreEqual("physical://root3", s[2]);
            Assert.AreEqual("physical://root4", s[3]);
            Assert.AreEqual("physical://root5", s[4]);

            matches = routes.GetRoutes("logical://foo/bar0");
            Assert.AreEqual(1, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root4", s[0]);

            routes.Add(new LogicalRoute("logical://foo/bar/*", new PhysicalRoute("physical://root6", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            routes.Add(new LogicalRoute("logical://foobar/*", new PhysicalRoute("physical://root7", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            Assert.AreEqual(8, routes.Count);

            matches = routes.GetRoutes("logical://foo/bar/test/1");
            Assert.AreEqual(1, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root6", s[0]);

            matches = routes.GetRoutes("logical://foobar/test/1");
            Assert.AreEqual(1, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root7", s[0]);

            matches = routes.GetRoutes("logical://*");
            Assert.AreEqual(8, matches.Count);
            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root0", s[0]);
            Assert.AreEqual("physical://root1", s[1]);
            Assert.AreEqual("physical://root2", s[2]);
            Assert.AreEqual("physical://root3", s[3]);
            Assert.AreEqual("physical://root4", s[4]);
            Assert.AreEqual("physical://root5", s[5]);
            Assert.AreEqual("physical://root6", s[6]);
            Assert.AreEqual("physical://root7", s[7]);

            routes.Clear();
            Assert.AreEqual(0, routes.Count);
            Assert.AreEqual(0, routes.GetRoutes("logical://*").Count);

            routes.Clear();
            routes.Add(new LogicalRoute("logical://foo/bar/*", new PhysicalRoute("physical://root8", "", "", MsgRouterInfo.Default, Guid.Empty, null, null, sysNow + ttl)));
            matches = routes.GetRoutes("logical://foo/bar/*");
            Assert.AreEqual(1, matches.Count);

            s = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                s[i] = matches[i].PhysicalRoute.RouterEP.ToString();

            Array.Sort(s);
            Assert.AreEqual("physical://root8", s[0]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRouteTable_Flush()
        {
            MsgRouter router = new MsgRouter();
            LogicalRouteTable routes = new LogicalRouteTable(router);
            DateTime sysNow = SysTime.Now;
            TimeSpan ttl = TimeSpan.FromSeconds(2);
            List<LogicalRoute> matches;

            router.PhysicalRoutes = new PhysicalRouteTable(router, TimeSpan.FromMinutes(1));
            router.RouterEP = "physical://root/hub/leaf";

            router.Start(IPAddress.Any, null, new IPEndPoint(IPAddress.Any, 0), new IPEndPoint(IPAddress.Any, 0), 100, TimeSpan.FromMinutes(1));

            try
            {
                routes.Add(new LogicalRoute("logical://foo", new PhysicalRoute("physical://root/hub/leaf0", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 15), new IPEndPoint(IPAddress.Loopback, 10), sysNow + ttl)));
                routes.Add(new LogicalRoute("logical://foo", new PhysicalRoute("physical://root/hub/leaf0", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 25), new IPEndPoint(IPAddress.Loopback, 20), sysNow + ttl)));
                routes.Add(new LogicalRoute("logical://bar", new PhysicalRoute("physical://root/hub/leaf1", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 35), new IPEndPoint(IPAddress.Loopback, 30), sysNow + ttl)));
                routes.Add(new LogicalRoute("logical://bar", new PhysicalRoute("physical://root/hub/leaf1", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 40), sysNow + ttl)));

                matches = routes.GetRoutes("logical://foo");
                Assert.AreEqual(2, matches.Count);
                matches = routes.GetRoutes("logical://bar");
                Assert.AreEqual(2, matches.Count);

                router.AddPhysicalRoute("physical://root/hub/leaf0", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 15), new IPEndPoint(IPAddress.Loopback, 10));
                router.AddPhysicalRoute("physical://root/hub/leaf1", "", "", MsgRouterInfo.Default, Guid.Empty, new IPEndPoint(IPAddress.Loopback, 45), new IPEndPoint(IPAddress.Loopback, 40));

                routes.Flush();

                matches = routes.GetRoutes("logical://foo");
                Assert.AreEqual(1, matches.Count);
                Assert.AreEqual(10, matches[0].PhysicalRoute.TcpEP.NetEP.Port);

                matches = routes.GetRoutes("logical://bar");
                Assert.AreEqual(1, matches.Count);
                Assert.AreEqual(40, matches[0].PhysicalRoute.TcpEP.NetEP.Port);
            }
            finally
            {
                router.Stop();
            }
        }

        private PhysicalRoute GetPhysRoute(string routerEP, IPAddress address)
        {
            return new PhysicalRoute(routerEP, "", "", MsgRouterInfo.Default, Helper.NewGuid(),
                                     new IPEndPoint(address, 10), new IPEndPoint(address, 10), SysTime.Now);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRouteTable_ComputeDistance()
        {
            const string logicalEP = "logical://test";

            MsgRouter router;
            LogicalRouteTable routes;

            // Compute distances from a leaf router

            router = new MsgRouter();
            router.RouterEP = "physical://root/hub0/leaf0";
            router.TcpEP = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            routes = new LogicalRouteTable(router);

            Assert.AreEqual(RouteDistance.Process, routes.ComputeDistance(new LogicalRoute(logicalEP, "test.msg", MsgHandler.Stub)));
            Assert.AreEqual(RouteDistance.Process, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf0", IPAddress.Parse("10.0.0.1")))));
            Assert.AreEqual(RouteDistance.Machine, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf0a", IPAddress.Parse("10.0.0.1")))));
            Assert.AreEqual(RouteDistance.Subnet, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.2")))));
            Assert.AreEqual(RouteDistance.Subnet, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0", IPAddress.Parse("10.0.0.3")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.4")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1/leaf0", IPAddress.Parse("10.0.0.1")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1/leaf1", IPAddress.Parse("10.0.0.2")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1", IPAddress.Parse("10.0.0.3")))));

            // Compute distances from a hub router.

            router = new MsgRouter();
            router.RouterEP = "physical://root/hub0";
            router.TcpEP = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            routes = new LogicalRouteTable(router);

            Assert.AreEqual(RouteDistance.Process, routes.ComputeDistance(new LogicalRoute(logicalEP, "test.msg", MsgHandler.Stub)));
            Assert.AreEqual(RouteDistance.Subnet, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf0", IPAddress.Parse("10.0.0.2")))));
            Assert.AreEqual(RouteDistance.Subnet, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.3")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.4")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1/leaf0", IPAddress.Parse("10.0.0.1")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1/leaf1", IPAddress.Parse("10.0.0.2")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1", IPAddress.Parse("10.0.0.3")))));

            // Compute distances from a root router.

            router = new MsgRouter();
            router.RouterEP = "physical://root";
            router.TcpEP = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            routes = new LogicalRouteTable(router);

            Assert.AreEqual(RouteDistance.Process, routes.ComputeDistance(new LogicalRoute(logicalEP, "test.msg", MsgHandler.Stub)));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0", IPAddress.Parse("10.0.0.2")))));
            Assert.AreEqual(RouteDistance.External, routes.ComputeDistance(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf0", IPAddress.Parse("10.0.0.3")))));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void LogicalRouteTable_GetClosestRoutes()
        {
            const string logicalEP = "logical://test";

            MsgRouter router;
            LogicalRouteTable routes;
            LogicalRoute route;

            router = new MsgRouter();
            router.RouterEP = "physical://root/hub0/leaf0";
            router.TcpEP = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10);
            routes = new LogicalRouteTable(router);

            // Empty routing table

            Assert.AreEqual(0, routes.GetClosestRoutes("logical://test").Count);

            // Routing table with a variety of route distances

            routes.Clear();
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.3"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf0", IPAddress.Parse("10.0.0.1"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.2"))));
            Assert.AreEqual(1, routes.GetClosestRoutes(logicalEP).Count);
            route = routes.GetClosestRoutes(logicalEP)[0];
            Assert.AreEqual(RouteDistance.Process, route.Distance);

            routes.Clear();
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.3"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.2"))));
            Assert.AreEqual(1, routes.GetClosestRoutes(logicalEP).Count);
            route = routes.GetClosestRoutes(logicalEP)[0];
            Assert.AreEqual(RouteDistance.Subnet, route.Distance);

            routes.Clear();
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.3"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0", IPAddress.Parse("10.0.0.2"))));
            Assert.AreEqual(1, routes.GetClosestRoutes(logicalEP).Count);
            route = routes.GetClosestRoutes(logicalEP)[0];
            Assert.AreEqual(RouteDistance.Subnet, route.Distance);

            routes.Clear();
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.3"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub1/leaf1", IPAddress.Parse("10.0.0.2"))));
            Assert.AreEqual(2, routes.GetClosestRoutes(logicalEP).Count);
            route = routes.GetClosestRoutes(logicalEP)[0];
            Assert.AreEqual(RouteDistance.External, route.Distance);
            route = routes.GetClosestRoutes(logicalEP)[1];
            Assert.AreEqual(RouteDistance.External, route.Distance);

            // Routing table with multiple routes at the same distance

            routes.Clear();
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root", IPAddress.Parse("10.0.0.3"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.2"))));
            routes.Add(new LogicalRoute(logicalEP, GetPhysRoute("physical://root/hub0/leaf1", IPAddress.Parse("10.0.0.4"))));
            Assert.AreEqual(2, routes.GetClosestRoutes(logicalEP).Count);
            route = routes.GetClosestRoutes(logicalEP)[0];
            Assert.AreEqual(RouteDistance.Subnet, route.Distance);
            route = routes.GetClosestRoutes(logicalEP)[1];
            Assert.AreEqual(RouteDistance.Subnet, route.Distance);
        }
    }
}

