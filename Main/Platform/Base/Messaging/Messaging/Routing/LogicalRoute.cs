//-----------------------------------------------------------------------------
// FILE:        LogicalRoute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Records the information necessary to map a logical route to
//              a physical route.

using System;
using System.Reflection;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Records the information necessary to map a logical route to a physical route.
    /// </summary>
    public sealed class LogicalRoute : IComparable
    {

        private MsgEP                           logicalEP;      // The logical endpoint
        private PhysicalRoute                   physicalRoute;  // The associated physical route (or null)
        private object                          targetGroup;    // The application message handler target group instance (or null)
        private Dictionary<string, MsgHandler>  handlers;       // The associated application message handlers (or null)
        private bool                            marked;         // Used for mark and sweep operations
        private RouteDistance                   distance;       // Physical distance from the current router
                                                                // (computed when a route is added to a LogicalRouteTable)

        /// <summary>
        /// Constructs a logical route that maps to an application
        /// message handler.
        /// </summary>
        /// <param name="logicalEP">The logical endpoint.</param>
        /// <param name="msgType">The fully qualified name of the message type handled.</param>
        /// <param name="handler">The message handler.</param>
        /// <remarks>
        /// This constructor creates and initializes the instance's Handlers
        /// hash table with the single entry specified by the msgType (the key)
        /// and handler (the value) parameters.
        /// </remarks>
        internal LogicalRoute(MsgEP logicalEP, string msgType, MsgHandler handler)
        {
            if (logicalEP.IsPhysical)
                throw new ArgumentException("Logical endpoint expected.", "logicalEP");

            this.logicalEP     = logicalEP;
            this.physicalRoute = null;
            this.targetGroup   = handler.Target;
            this.handlers      = new Dictionary<string, MsgHandler>();
            this.marked        = false;
            this.distance      = RouteDistance.Unknown;

            this.handlers.Add(msgType, handler);
        }

        /// <summary>
        /// Constructs a logical route that maps to a physical route.
        /// </summary>
        /// <param name="logicalEP">The logical endpoint.</param>
        /// <param name="physicalRoute">The associated physical route.</param>
        internal LogicalRoute(MsgEP logicalEP, PhysicalRoute physicalRoute)
        {
            Assertion.Test(physicalRoute != null);

            if (logicalEP.IsPhysical)
                throw new ArgumentException("Logical endpoint expected.", "logicalEP");

            this.logicalEP     = logicalEP;
            this.physicalRoute = physicalRoute;
            this.targetGroup   = null;
            this.handlers      = null;
            this.marked        = false;
            this.distance      = RouteDistance.Unknown;
        }

        /// <summary>
        /// Returns the logical endpoint.
        /// </summary>
        public MsgEP LogicalEP
        {
            get { return logicalEP; }
        }

        /// <summary>
        /// Returns the physical route associated with this logical route (or <c>null</c>).
        /// </summary>
        public PhysicalRoute PhysicalRoute
        {
            get { return physicalRoute; }
        }

        /// <summary>
        /// The application message handler target group instance (or <c>null</c>).
        /// </summary>
        public object TargetGroup
        {
            get { return targetGroup; }
            internal set { targetGroup = value; }
        }

        /// <summary>
        /// The <see cref="RouteDistance" /> between this route target and the
        /// the current router.  This is calculated and used by <see cref="LogicalRouteTable" />
        /// for determining favored local routes.
        /// </summary>
        public RouteDistance Distance
        {
            get { return distance; }
            set { distance = value; }
        }

        /// <summary>
        /// Returns the message handlers associated with this logical route (or <c>null</c>).
        /// </summary>
        internal Dictionary<string, MsgHandler> Handlers
        {
            get { return handlers; }
        }

        /// <summary>
        /// Used for mark and sweep operations.
        /// </summary>
        public bool IsMarked
        {
            get { return marked; }
            set { marked = value; }
        }

        /// <summary>
        /// Implements the IComparable.CompareTo() method.
        /// </summary>
        /// <param name="o">The object to be compared.</param>
        /// <returns>
        /// <list>
        ///     <item>-1 if this is less than the parameter.</item>
        ///     <item>0 if this is equal to the parameter.</item>
        ///     <item>+1 if this is greater than the parameter.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <note>
        /// Only the logical endpoints are actually compared.
        /// </note>
        /// </remarks>
        public int CompareTo(object o)
        {
            LogicalRoute param;

            param = o as LogicalRoute;
            if (param == null)
                throw new InvalidCastException();

            return String.Compare(this.logicalEP, param.logicalEP, true);
        }
    }
}
