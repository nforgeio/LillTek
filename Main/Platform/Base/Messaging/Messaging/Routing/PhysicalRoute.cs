//-----------------------------------------------------------------------------
// FILE:        PhysicalRoute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Records the information necessary to map a physical route to
//              a specific router's channel endpoints.

using System;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Records the necessary information about the mapping between a
    /// physical endpoint and a network address.
    /// </summary>
    public sealed class PhysicalRoute : IComparable
    {
        private MsgEP           routerEP;               // The router endpoint
        private string          appName;                // The hosting application name
        private string          appDescription;         // The hosting application description
        private MsgRouterInfo   routerInfo;             // The router's capability information
        private ChannelEP       udpEP;                  // The UDP channel endpoint of the target router (or null)
        private ChannelEP       tcpEP;                  // The TCP channel endpoint of the target router (or null)
        private DateTime        ttd;                    // Route time-to-die (SYS)
        private bool            isP2P;                  // True if the endpoint router is peer-to-peer enabled
        private Guid            logicalEndpointSetID;   // The current logical endpoint set ID

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">The router endpoint.</param>
        /// <param name="appName">The name of the application hosting the router.</param>
        /// <param name="appDescription">A description of the application.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="logicalEndpointSetID">The router's logical endpoint set ID.</param>
        /// <param name="udpEP">The UDP network endpoint (or <c>null</c>).</param>
        /// <param name="tcpEP">The TCP network endpoint (or <c>null</c>).</param>
        /// <param name="ttd">Route time-to-die (SYS).</param>
        /// <remarks>
        /// The router endpoint must be a physical non-channel endpoint.
        /// </remarks>
        internal PhysicalRoute(MsgEP routerEP, string appName, string appDescription, MsgRouterInfo routerInfo,
                               Guid logicalEndpointSetID, IPEndPoint udpEP, IPEndPoint tcpEP, DateTime ttd)
        {
            Assertion.Test(routerEP.IsPhysical);
            Assertion.Test(!routerEP.IsChannel);

            this.routerEP             = routerEP;
            this.appName              = appName;
            this.appDescription       = appDescription;
            this.routerInfo           = routerInfo;
            this.logicalEndpointSetID = logicalEndpointSetID;
            this.udpEP                = udpEP == null ? null : new ChannelEP(Transport.Udp, udpEP);
            this.tcpEP                = tcpEP == null ? null : new ChannelEP(Transport.Tcp, tcpEP);
            this.ttd                  = ttd;
            this.isP2P                = routerInfo.IsP2P;
        }

        /// <summary>
        /// The target router's endpoint.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return routerEP; }

            set
            {
                Assertion.Test(routerEP.IsPhysical);
                Assertion.Test(!routerEP.IsChannel);

                routerEP = value;
            }
        }

        /// <summary>
        /// The name of the application hosting the target router.
        /// </summary>
        public string AppName
        {
            get { return appName; }
            set { appName = value; }
        }

        /// <summary>
        /// Description of the application hosting the target router.
        /// </summary>
        public string AppDescription
        {
            get { return appDescription; }
            set { appDescription = value; }
        }

        /// <summary>
        /// The target router's capability information.
        /// </summary>
        public MsgRouterInfo RouterInfo
        {
            get { return routerInfo; }
            set { routerInfo = value; }
        }

        /// <summary>
        /// The current known logical endpoint set ID for the router identified by this
        /// route.
        /// </summary>
        /// <remarks>
        /// This information is used to determine when the set of logical endpoints for
        /// a router have changed and thus know when to request the new set of logical
        /// endpoints via a LogicalDiscoverMsg.
        /// </remarks>
        public Guid LogicalEndpointSetID
        {
            get { return logicalEndpointSetID; }
            set { logicalEndpointSetID = value; }
        }

        /// <summary>
        /// The UDP channel endpoint of the router (or <c>null</c>).
        /// </summary>
        public ChannelEP UdpEP
        {
            get { return udpEP; }
            set { udpEP = value; }
        }

        /// <summary>
        /// The TCP channel endpoint of the router (or <c>null</c>).
        /// </summary>
        public ChannelEP TcpEP
        {
            get { return tcpEP; }
            set { tcpEP = value; }
        }

        /// <summary>
        /// Route time-to-die (SYS)
        /// </summary>
        public DateTime TTD
        {
            get { return ttd; }
            set { ttd = value; }
        }

        /// <summary>
        /// Returns a representation of the route as a string.
        /// </summary>
        /// <returns>The route string representation.</returns>
        /// <remarks>
        /// <note>
        /// The string returned does not encode the TTD, LogicalEndPointSetID
        /// and IsP2P values.  It encodes only the information necessary to identify
        /// the router endpoint and the network endpoints.
        /// </note>
        /// </remarks>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(routerEP.ToString());

            if (tcpEP != null)
            {
                sb.Append(";tcpEP=");
                sb.Append(tcpEP.ToString());
            }

            if (udpEP != null)
            {
                sb.Append(";udpEP=");
                sb.Append(udpEP.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals this object.
        /// </summary>
        /// <param name="o">The object to be compared.</param>
        /// <returns><c>true</c> if the objects have the same values.</returns>
        /// <remarks>
        /// <note>
        /// Only the physical endpoint and TCP/UDP endpoints are
        /// compared.  The LogicalRouterSetID and TTD fields are ignored.
        /// </note>
        /// </remarks>
        public override bool Equals(object o)
        {
            var r = o as PhysicalRoute;

            if (r == null)
                return false;

            if (!routerEP.Equals(r.routerEP))
                return false;

            if ((r.tcpEP == null) != (this.tcpEP == null) || (this.tcpEP == null) || !this.tcpEP.Equals(r.tcpEP))
                return false;

            if ((r.udpEP == null) != (this.udpEP == null) || (this.udpEP == null) || !this.udpEP.Equals(r.udpEP))
                return false;

            return true;
        }

        /// <summary>
        /// Returns a hash code for the route.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return routerEP.GetHashCode();
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
        /// Only the physical endpoints are actually compared.
        /// </note>
        /// </remarks>
        public int CompareTo(object o)
        {
            PhysicalRoute param;

            param = o as PhysicalRoute;
            if (param == null)
                throw new InvalidCastException();

            return String.Compare(this.RouterEP, param.RouterEP, true);
        }
    }
}
