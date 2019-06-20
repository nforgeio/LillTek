//-----------------------------------------------------------------------------
// FILE:        LogicalRouteTable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A table that handles the mapping logical routes to
//              a set of physical routes.

using System;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill): 
//
// For now I'm going to implement a really simple linear search
// algorithm.  This should work fairly well for small numbers 
// of logical routes (which, realistically will be pretty typical).
// But this will not scale to handle a large number of routes.
//               
// I need to come back and implement a tree of hash tables for
// each segment level to implement a scalable solution.

namespace LillTek.Messaging
{
    /// <summary>
    /// A table that handles the mapping logical routes to a set of physical routes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic idea here is that a logical route may map to zero or
    /// more physical routes.  The indexer performs this mapping and
    /// returns a collection of the physical routes for the logical
    /// route passed.
    /// </para>
    /// <para>
    /// The tricky thing about this is that the table needs to handle
    /// wildcarded logical routes.  These are routes whose last URI
    /// segment is the [*] character.
    /// </para>
    /// <para>
    /// Physical routes in the table may map to wildcarded logical
    /// routes and logical routes passed to <see cref="GetRoutes" />
    /// may also be wildcarded.
    /// </para>
    /// </remarks>
    public sealed class LogicalRouteTable : IEnumerable<LogicalRoute>
    {
        private MsgRouter           router;     // The associated router
        private List<LogicalRoute>  routes;     // The set of logical routes 
        private int                 changeID;   // Incremented whenever the table is modified

        /// <summary>
        /// Constructs a logical routing table.
        /// </summary>
        /// <param name="router">The associated router.</param>
        public LogicalRouteTable(MsgRouter router)
        {
            this.router   = router;
            this.routes   = new List<LogicalRoute>();
            this.changeID = 0;
        }

        /// <summary>
        /// This property is incremented every time the route table is modified
        /// in any way.  This can be used to determine of the table has changed
        /// over a period of time.
        /// </summary>
        public int ChangeID
        {
            get { return changeID; }
        }

        /// <summary>
        /// Adds a logical to physical route to the table if it's not already present.
        /// </summary>
        /// <param name="logicalRoute">The logical route.</param>
        /// <remarks>
        /// This method should be passed only those logical routes that map
        /// to physical routes.
        /// </remarks>
        public void Add(LogicalRoute logicalRoute)
        {
            LogicalRoute found;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (logicalRoute.Handlers != null)
                    throw new MsgException("Logical routes mapping to application message handlers are not allowed.");

                found = null;
                for (int i = 0; i < routes.Count; i++)
                {
                    if (routes[i].LogicalEP.Equals(logicalRoute.LogicalEP) &&
                        routes[i].PhysicalRoute.Equals(logicalRoute.PhysicalRoute) &&
                        routes[i].Handlers == null)
                    {
                        found = routes[i];
                        break;
                    }
                }

                if (found == null)
                {
                    logicalRoute.Distance = ComputeDistance(logicalRoute);
                    routes.Add(logicalRoute);
                    changeID++;
                }
            }
        }

        /// <summary>
        /// Computes the <see cref="RouteDistance" /> between the associated <see cref="MsgRouter" />
        /// and a <see cref="LogicalRoute" />.
        /// </summary>
        /// <param name="logicalRoute">The first <see cref="LogicalRoute" />.</param>
        /// <returns>The computed <see cref="RouteDistance" />.</returns>
        internal RouteDistance ComputeDistance(LogicalRoute logicalRoute)
        {
            if (logicalRoute.Handlers != null)
                return RouteDistance.Process;
            else if (router.RouterEP == null)
                return RouteDistance.Unknown;

            MsgEP           routerEP    = router.RouterEP;
            MsgEP           targetEP    = logicalRoute.PhysicalRoute.RouterEP;
            PhysicalRoute   physical    = logicalRoute.PhysicalRoute;
            int             routerLevel = routerEP.Segments.Length;
            int             targetLevel = targetEP.Segments.Length;
            bool            sameIP      = physical.TcpEP != null && router.TcpEP.Address.Equals(physical.TcpEP.NetEP.Address);

            if (routerEP.IsPhysicalMatch(targetEP))
            {
                // The routes match so we're in the same process.

                return RouteDistance.Process;
            }

            if (routerLevel == targetLevel)
            {
                // The endpoints are at the same level in the heirarchy.

                switch (routerLevel)
                {
                    case 0:

                        // Both routers are at the root level

                        if (sameIP)
                            return RouteDistance.Machine;
                        else
                            return RouteDistance.Subnet;

                    case 1:

                        // Must be hub routers on different subnets

                        return RouteDistance.External;

                    case 2:

                        // Both routes are to leaf routers.

                        if (routerEP.IsPhysicalPeer(targetEP))
                        {
                            if (sameIP)
                                return RouteDistance.Machine;
                            else
                                return RouteDistance.Subnet;
                        }

                        // If not peers, then they must be in different datacenters.

                        return RouteDistance.External;

                    default:

                        throw new NotImplementedException();
                }
            }

            // The endpoints are at different levels in the heirarchy.  Normalize
            // these so that higherEP and higherLevel specified the route target
            // that is higher in the heirarchy and lowerEP and lowerLevel the
            // target lower in the heirarchy.

            MsgEP   higherEP;
            int     higherLevel;
            MsgEP   lowerEP;
            int     lowerLevel;

            if (routerLevel < targetLevel)
            {
                higherEP    = routerEP;
                higherLevel = routerLevel;
                lowerEP     = targetEP;
                lowerLevel  = targetLevel;
            }
            else
            {
                higherEP    = targetEP;
                higherLevel = targetLevel;
                lowerEP     = routerEP;
                lowerLevel  = routerLevel;
            }

            if (higherLevel == 1 && lowerLevel == 2 && higherEP.IsPhysicalDescendant(lowerEP))
            {
                // The higher level endpoint is on a hub and the lower level endpoint is
                // on a router under the hub so they're on the same subnet.

                return RouteDistance.Subnet;
            }

            // All remaining cases will be considered to be external since
            // they can't be on the same subnet.

            return RouteDistance.External;
        }

        /// <summary>
        /// Adds a logical to application message handler route to the table.
        /// </summary>
        /// <param name="logicalRoute">The logical route.</param>
        /// <param name="msgType">The fully qualified name of the message type handled.</param>
        /// <returns>
        /// True of the operation succeeded, <c>false</c> if it failed because an
        /// application handler for the logical endpoint specified, target
        /// object, and message type already exists.
        /// </returns>
        /// <remarks>
        /// <note>
        /// This method should be passed only those logical routes that
        /// map to application message handlers.
        /// </note>
        /// <note>
        /// This overload will cause separate routes will be maintained for 
        /// each target instance resultingin messages be load balanced randomly 
        /// across the instances.
        /// </note>
        /// </remarks>
        public bool Add(LogicalRoute logicalRoute, string msgType)
        {
            return Add(logicalRoute, msgType, null);
        }

        /// <summary>
        /// Adds a logical to application message handler route to the table.
        /// </summary>
        /// <param name="logicalRoute">The logical route.</param>
        /// <param name="msgType">The fully qualified name of the message type handled.</param>
        /// <param name="targetGroup">Optional dispatch target grouping instance (or <c>null</c>).</param>
        /// <returns>
        /// True of the operation succeeded, <c>false</c> if it failed because an
        /// application handler for the logical endpoint specified, target
        /// object, and message type already exists.
        /// </returns>
        /// <remarks>
        /// <note>
        /// This method should be passed only those logical routes that
        /// map to application message handlers.
        /// </note>
        /// <para>
        /// The <paramref name="targetGroup" /> parameter can be used to group
        /// together message dispatch handlers implemented by a different
        /// target object instances.  This functionality is important when 
        /// the impleentation of message type specific handlers is spread
        /// across multiple target classes.
        /// </para>
        /// <para>
        /// An example of this use is how the <see cref="ClusterMember" />
        /// class' <see cref="ClusterMember.AddTarget" /> method to group the
        /// new target's message handler with the <see cref="ClusterMember" />
        /// handlers since they'll share the same logical endpoint.
        /// </para>
        /// <para>
        /// If <paramref name="targetGroup" /> is passed as <c>null</c> then
        /// separate routes will be maintained for each target instance resulting
        /// in messages be load balanced randomly across the instances.
        /// </para>
        /// </remarks>
        public bool Add(LogicalRoute logicalRoute, string msgType, object targetGroup)
        {
            // This method assumes that exactly one MsgHandler is present in the 
            // logical route passed.  It creates a new logical route in the table 
            // for each logical endpoint and then adds handlers for each message
            // type.

            LogicalRoute    found;
            MsgHandler      newHandler;

            newHandler = null;
            foreach (var h in logicalRoute.Handlers.Values)
            {
                newHandler = h;
                break;
            }

            Assertion.Test(newHandler != null);

            if (targetGroup == null)
                targetGroup = newHandler.Target;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (logicalRoute.Handlers == null)
                    throw new MsgException("Logical routes mapping to physical routes are not allowed.");

                found = null;

                for (int i = 0; i < routes.Count; i++)
                {
                    if (object.ReferenceEquals(targetGroup, routes[i].TargetGroup) &&
                        routes[i].LogicalEP.Equals(logicalRoute.LogicalEP))
                    {
                        found = routes[i];
                        break;
                    }
                }

                if (found != null)
                {
                    if (found.Handlers.ContainsKey(msgType))
                        return false;

                    found.Handlers.Add(msgType, newHandler);
                }
                else
                {
                    logicalRoute.TargetGroup = targetGroup;
                    routes.Add(logicalRoute);
                }

                logicalRoute.Distance = ComputeDistance(logicalRoute);
                changeID++;
            }

            return true;
        }

        /// <summary>
        /// Removes any routes that map to a message handler in the target object.
        /// </summary>
        /// <param name="target">The target object instance.</param>
        /// <returns><c>true</c> if any routes were actually removed.</returns>
        public bool RemoveTarget(object target)
        {
            var     delRoutes = new List<LogicalRoute>();
            var     delHandlerKeys = new List<string>();
            bool    fDeleted = false;

            using (TimedLock.Lock(router.SyncRoot))
            {
                foreach (LogicalRoute route in routes)
                {
                    if (route.Handlers != null)
                    {
                        delHandlerKeys.Clear();

                        foreach (var key in route.Handlers.Keys)
                        {
                            var handler = route.Handlers[key];

                            if (handler.Target == target)
                            {
                                fDeleted = true;
                                delHandlerKeys.Add(key);
                            }
                        }

                        foreach (var key in delHandlerKeys)
                            route.Handlers.Remove(key);

                        if (route.Handlers.Count == 0 && route.PhysicalRoute == null)
                            delRoutes.Add(route);
                    }
                }

                foreach (LogicalRoute route in delRoutes)
                    routes.Remove(route);
            }

            if (fDeleted)
                changeID++;

            return fDeleted;
        }

        /// <summary>
        /// Removes all routes from the table.
        /// </summary>
        public void Clear()
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                routes.Clear();
                changeID++;
            }
        }

        /// <summary>
        /// Returns the current number of logical routes in the table.
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
        /// Returns the set of logical routes that map to the logical endpoint passed.
        /// </summary>
        /// <param name="logicalEP">The </param>
        /// <returns>The set of logical routes that match the endpoint passed.</returns>
        public List<LogicalRoute> GetRoutes(MsgEP logicalEP)
        {
            var matches = new List<LogicalRoute>();

            using (TimedLock.Lock(router.SyncRoot))
            {
                for (int i = 0; i < routes.Count; i++)
                {
                    if (routes[i].LogicalEP.LogicalMatch(logicalEP))
                        matches.Add(routes[i]);
                }
            }

            return matches;
        }

        /// <summary>
        /// Used below for sorting logical routes by closeness.
        /// </summary>
        private sealed class RouteClosenessComparer : IComparer<LogicalRoute>
        {
            public int Compare(LogicalRoute route1, LogicalRoute route2)
            {
                if (route1.Distance == route2.Distance)
                    return 0;
                else if (route1.Distance < route2.Distance)
                    return -1;
                else
                    return +1;
            }
        }

        /// <summary>
        /// Returns the set of logical routes that map to the logical endpoint passed
        /// and are closest to the <see cref="MsgRouter" /> from a physical routing
        /// sense.
        /// </summary>
        /// <param name="logicalEP">The </param>
        /// <returns>The set of logical closest routes that match the endpoint passed.</returns>
        /// <remarks>
        /// <para>
        /// This method returns the logical route whose endpoint is closest to the current
        /// router.  If more than one route is equally close, then all of these routes
        /// will be returned.  The list below describes the closeness criteria in order
        /// of closest first:
        /// </para>
        /// <list type="bullet">
        ///     <item>Routes to the current process (ie. routes with message handlers).</item>
        ///     <item>
        ///     Routes to the current computer (ie. the route's physical endpoint is a 
        ///     peer to this router's endpoint and the IP addresses are the same).
        ///     </item>
        ///     <item>Routes to the current subnet.</item>
        ///     <item>All remaining routes.</item>
        /// </list>
        /// </remarks>
        public List<LogicalRoute> GetClosestRoutes(MsgEP logicalEP)
        {
            var                 matches = GetRoutes(logicalEP);
            List<LogicalRoute>  closest;
            int                 cClosest;
            RouteDistance       distance;

            if (matches.Count <= 1)
                return matches;

            // Sort the result by closeness

            matches.Sort(new RouteClosenessComparer());

            // Count the number of closest matches and then extract
            // all routes at this distance and return this set as
            // the result.

            distance = matches[0].Distance;
            for (cClosest = 1; cClosest < matches.Count && matches[cClosest].Distance == distance; cClosest++) ;

            if (cClosest == matches.Count)
                return matches;

            closest = new List<LogicalRoute>(cClosest);
            for (int i = 0; i < cClosest; i++)
                closest.Add(matches[i]);

            return closest;
        }

        /// <summary>
        /// Returns an array of the routes currently maintained by the table.
        /// </summary>
        /// <returns>The array of routes.</returns>
        public LogicalRoute[] ToArray()
        {
            LogicalRoute[]  arr;
            int             i;

            using (TimedLock.Lock(router.SyncRoot))
            {
                arr = new LogicalRoute[routes.Count];
                i = 0;

                foreach (LogicalRoute route in routes)
                    arr[i++] = route;
            }

            return arr;
        }

        /// <summary>
        /// Removes logical routes that map to physical routes that are no
        /// longer present in the associated router's physical route table.
        /// </summary>
        public void Flush()
        {
            var                 physicalRoutes = new Dictionary<string, PhysicalRoute>();
            int                 cDelete        = 0;
            List<LogicalRoute>  rebuild;

            using (TimedLock.Lock(router.SyncRoot))
            {
                foreach (var physRoute in router.PhysicalRoutes)
                    physicalRoutes.Add(physRoute.ToString(), physRoute);

                // Mark the routes to be deleted.

                for (int i = 0; i < routes.Count; i++)
                {
                    routes[i].IsMarked = !physicalRoutes.ContainsKey(routes[i].PhysicalRoute.ToString());
                    if (routes[i].IsMarked)
                        cDelete++;
                }

                // Rebuild the route table if necessary

                if (cDelete > 0)
                {
                    rebuild = new List<LogicalRoute>(routes.Count - cDelete);
                    for (int i = 0; i < routes.Count; i++)
                    {
                        if (!routes[i].IsMarked)
                            rebuild.Add(routes[i]);
#if TRACE
                        else
                        {
                            NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Logical Flush *******", this.GetType().Name + ": router=" + router.RouterEP.ToString(),
                                           "LogicalEP=" + routes[i].LogicalEP.ToString() + "\r\n" +
                                           "PhysicalEP=" + routes[i].PhysicalRoute.RouterEP.ToString());
                        }
#endif
                    }

                    routes = rebuild;
                    changeID++;
                }
            }
        }

        /// <summary>
        /// Removes the logical routes associated with a logical endpoint set
        /// from the table.
        /// </summary>
        /// <param name="logicalEndpointSetID">The logical endpoint set ID.</param>
        public void Flush(Guid logicalEndpointSetID)
        {
            var                 physicalRoutes = new Dictionary<string, PhysicalRoute>();
            int                 cDelete        = 0;
            List<LogicalRoute>  rebuild;

            using (TimedLock.Lock(router.SyncRoot))
            {

                foreach (var physRoute in router.PhysicalRoutes)
                    physicalRoutes.Add(physRoute.ToString(), physRoute);

                // Mark the routes to be deleted.

                for (int i = 0; i < routes.Count; i++)
                {
                    routes[i].IsMarked = routes[i].PhysicalRoute.LogicalEndpointSetID == logicalEndpointSetID;
                    if (routes[i].IsMarked)
                        cDelete++;
                }

                // Rebuild the route table if necessary

                if (cDelete > 0)
                {
                    rebuild = new List<LogicalRoute>(routes.Count - cDelete);
                    for (int i = 0; i < routes.Count; i++)
                    {
                        if (!routes[i].IsMarked)
                            rebuild.Add(routes[i]);
#if TRACE
                        else
                        {
                            NetTrace.Write(MsgRouter.TraceSubsystem, 0, "Logical Set Flush *******", this.GetType().Name + ": router=" + router.RouterEP.ToString(),
                                           "LogicalEP=" + routes[i].LogicalEP.ToString() + "\r\n" +
                                           "PhysicalEP=" + routes[i].PhysicalRoute.RouterEP.ToString());
                        }
#endif
                    }

                    routes = rebuild;
                    changeID++;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator for the routes in the table.
        /// </summary>
        IEnumerator<LogicalRoute> IEnumerable<LogicalRoute>.GetEnumerator()
        {
            using (TimedLock.Lock(router.SyncRoot))
                return routes.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator for the routes in the table.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            using (TimedLock.Lock(router.SyncRoot))
                return routes.GetEnumerator();
        }

        /// <summary>
        /// Searches the table for a specific logical route.
        /// </summary>
        /// <param name="logicalEP">The logical endpoint.</param>
        /// <param name="physicalEP">The physical endpoint.</param>
        /// <returns><c>true</c> if the table holds the specified logical route.</returns>
        /// <remarks>
        /// Used by Unit tests to verify the existence of a specific logical route.
        /// </remarks>
        internal bool HasRoute(MsgEP logicalEP,MsgEP physicalEP)
        {
            using (TimedLock.Lock(router.SyncRoot)) 
            {
                foreach (var route in routes) 
                {
                    if (route.LogicalEP.Equals(logicalEP) && route.PhysicalRoute.RouterEP.Equals(physicalEP))
                        return true;
                }
            }

            return false;
        }
    }
}
