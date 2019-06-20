//-----------------------------------------------------------------------------
// FILE:        PhysicalRouteTable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a table that maps a physical message endpoint to 
//              a physical route.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a table that maps a physical message endpoint to 
    /// a physical route.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic idea here is that this table is to be used to handle
    /// the routing of messages between the current router and routers
    /// immediately below it in the physical routing hierarchy.
    /// </para>
    /// <para>
    /// The router should call <see cref="Add" /> for each peer or child router
    /// it discovers and then continue calling it as the remote router refreshes
    /// its presence.
    /// </para>
    /// <para>
    /// Then when a message needs routing, the router should examine
    /// its endpoint.  If the endpoint is below the router in the hierarchy,
    /// the router should use the route table's indexer to search for the 
    /// route of the peer or child router to forward the message to.  
    /// Otherwise, the router will forward the message to its parent (if any).
    /// </para>
    /// <para>
    /// <see cref="Flush" /> should be called periodically to clear out
    /// any expired routes.  <see cref="Remove(MsgEP)" /> can be used to delete
    /// specific routes from the table.
    /// </para>
    /// </remarks>
    public sealed class PhysicalRouteTable : IEnumerable<PhysicalRoute>
    {
        private MsgRouter   router;     // The associated router
        private MsgEP       routerEP;   // The router endpoint
        private TimeSpan    routeTTL;   // The lifetime of an unrenewed route

        // The route hash table (hashed by the router endpoint string w/o the query)

        private Dictionary<string, PhysicalRoute> routes;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="router">The associated router.</param>
        /// <param name="routeTTL">The lifetime of an unrenewed route.</param>
        public PhysicalRouteTable(MsgRouter router, TimeSpan routeTTL)
        {

            this.router   = router;
            this.routerEP = router.RouterEP;
            this.routeTTL = routeTTL;
            this.routes   = new Dictionary<string, PhysicalRoute>();
        }

        /// <summary>
        /// Time-to-live for an unrefreshed route.
        /// </summary>
        public TimeSpan RouteTTL
        {
            get { return routeTTL; }
            set { routeTTL = value; }
        }

        /// <summary>
        /// Adds or refreshes the physical route to a peer or child router.
        /// </summary>
        /// <param name="routerEP">The router's physical endpoint.</param>
        /// <param name="appName">Name of the application hosting the router.</param>
        /// <param name="appDescription">Description of the hosting appplication.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="logicalEndpointSetID">The router's logical endpoint set ID.</param>
        /// <param name="udpEP">The router's UDP endpoint.</param>
        /// <param name="tcpEP">The router's TCP endpoint.</param>
        /// <remarks>
        /// <note>
        /// The router's endpoint must be a direct descendant of 
        /// the associated router's endpoint or a peer in the physical 
        /// hierarchy.
        /// </note>
        /// </remarks>
        public void Add(MsgEP routerEP, string appName, string appDescription, MsgRouterInfo routerInfo, Guid logicalEndpointSetID, IPEndPoint udpEP, IPEndPoint tcpEP)
        {
            PhysicalRoute   route;
            string          ep;
            DateTime        TTD;

            if (!routerEP.IsPhysical || routerEP.IsChannel)
                throw new ArgumentException("Only physical non-channel endpoints may be added to a PhysicalRouteTable.");

            if (!router.RouterEP.IsPhysicalPeer(routerEP) &&
                (!router.RouterEP.IsPhysicalDescendant(routerEP) || router.RouterEP.Segments.Length + 1 != routerEP.Segments.Length))
            {
                throw new ArgumentException("Only peer or direct child endpoints may be added to a PhysicalRouteTable.");
            }

            ep  = routerEP.ToString(-1, false);
            TTD = SysTime.Now + routeTTL;

            using (TimedLock.Lock(router.SyncRoot))
            {
                routes.TryGetValue(ep, out route);
                if (route == null)
                    routes.Add(ep, new PhysicalRoute(routerEP, appName, appDescription, routerInfo, logicalEndpointSetID, udpEP, tcpEP, TTD));
                else
                {
                    route.LogicalEndpointSetID = logicalEndpointSetID;
                    route.UdpEP                = udpEP == null ? null : new ChannelEP(Transport.Udp, udpEP);
                    route.TcpEP                = tcpEP == null ? null : new ChannelEP(Transport.Tcp, tcpEP);
                    route.TTD                  = TTD;

                    router.Trace(1, "KeepAlive", "ep=" + ep.ToString() + " TTD=" + TTD.ToString() + " RTTD=" + route.TTD.ToString(), null);
                }
            }
        }

        /// <summary>
        /// Removes any routes that map to the router endpoint passed.
        /// </summary>
        /// <param name="routerEP">The router endpoint.</param>
        public void Remove(MsgEP routerEP)
        {
            using (TimedLock.Lock(router.SyncRoot))
                routes.Remove(routerEP.ToString(-1, false));
        }

        /// <summary>
        /// Removes the route to the channel endpoint passed if it exists in
        /// the table.
        /// </summary>
        /// <param name="ep">The channel endpoint of the route to remove.</param>
        public void Remove(ChannelEP ep)
        {
            if (ep.Transport != Transport.Tcp)
                throw new MsgException("Only TCP endpoins supported.");

            using (TimedLock.Lock(router.SyncRoot))
            {
                var delList = new List<string>();

                foreach (PhysicalRoute route in routes.Values)
                {
                    if (route.TcpEP == ep)
                        delList.Add(route.RouterEP.ToString(-1, false));
                }

                for (int i = 0; i < delList.Count; i++)
                    routes.Remove(delList[i]);
            }
        }

        /// <summary>
        /// Searches the table for a physical route of a router that
        /// should be next router in the delivery path for the message whose
        /// physical destination endpoint is passed.
        /// </summary>
        /// <param name="msgEP">The message's destination endpoint.</param>
        /// <remarks>
        /// Returns null if no route can be found.
        /// </remarks>
        public PhysicalRoute this[MsgEP msgEP]
        {
            get
            {
                string          ep;
                PhysicalRoute   route;

                ep = msgEP.ToString(-1, false);
                using (TimedLock.Lock(router.SyncRoot))
                {
                    routes.TryGetValue(ep, out route);
                    return route;
                }
            }
        }

        /// <summary>
        /// Returns the number of routes in the table.
        /// </summary>
        public int Count
        {
            get
            {
                using (TimedLock.Lock(router.SyncRoot))
                    return routes.Count;
            }
        }

        /// <summary>
        /// Returns an array of the routes currently maintained by the table.
        /// </summary>
        /// <returns>The array of routes.</returns>
        public PhysicalRoute[] ToArray()
        {
            PhysicalRoute[]     arr;
            int                 i;

            using (TimedLock.Lock(router.SyncRoot))
            {
                arr = new PhysicalRoute[routes.Count];
                i   = 0;

                foreach (PhysicalRoute route in routes.Values)
                    arr[i++] = route;
            }

            return arr;
        }

        /// <summary>
        /// Flushes any expired entries from the table.
        /// </summary>
        public void Flush()
        {
            DateTime        now = SysTime.Now;
            List<string>    delList = null;

            using (TimedLock.Lock(router.SyncRoot))
            {
                foreach (PhysicalRoute route in routes.Values)
                    if (route.TTD <= now)
                    {
                        if (delList == null)
                            delList = new List<string>();

                        delList.Add(route.RouterEP.ToString(-1, false));
                        router.Trace(1, "Remove Route", route.RouterEP.ToString() + " netEP=" + router.TcpEP.ToString(), null);
                    }

                if (delList != null)
                {
                    foreach (var ep in delList)
                        routes.Remove(ep);
                }
            }
#if TRACE
            if (delList != null)
            {
                foreach (var ep in delList)
                    NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Physical Flush *******", this.GetType().Name + ": router=" + this.routerEP.ToString() + " ep=" + ep, string.Empty);
            }
#endif
        }

        /// <summary>
        /// Returns an enumerator for all of the physical routes in the table.
        /// </summary>
        /// <remarks>
        /// <note>
        /// For this to be of any use in a multi-threaded environment,
        /// the associated router's <see cref="MsgRouter.SyncRoot" /> must be 
        /// locked by the current thread.
        /// </note>
        /// </remarks>
        IEnumerator<PhysicalRoute> IEnumerable<PhysicalRoute>.GetEnumerator()
        {
            return routes.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator for the physical routes in the table.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return routes.Values.GetEnumerator();
        }
    }
}
