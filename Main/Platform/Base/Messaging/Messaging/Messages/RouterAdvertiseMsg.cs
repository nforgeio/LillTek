//-----------------------------------------------------------------------------
// FILE:        RouterAdvertiseMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Message broadcast by routers to advertise their presence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Routers use this to periodically broadcast information about themselves
    /// and their message endpoints.
    /// </summary>
    public sealed class RouterAdvertiseMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".RouterAdvertise";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public RouterAdvertiseMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">The router's physical endpoint.</param>
        /// <param name="appName">The name of the application hosting the router.</param>
        /// <param name="appDescription">A description of the application.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="udpPort">The UDP port the router is monitoring.</param>
        /// <param name="tcpPort">The TCP port the router is monitoring.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints handled by the router.</param>
        /// <param name="replyAdvertise"><c>true</c> if the receiving router should reply with a RouterAdvertiseMsg of its own.</param>
        /// <param name="discoverLogical"><c>true</c> if the receiving router should reply with LogicalAdvertiseMsgs.</param>
        public RouterAdvertiseMsg(MsgEP routerEP, string appName, string appDescription,
                                  MsgRouterInfo routerInfo, int udpPort, int tcpPort,
                                  Guid logicalEndpointSetID, bool replyAdvertise, bool discoverLogical)
        {
            Assertion.Test(routerEP.IsPhysical);

            this._Flags               = MsgFlag.Priority;
            this.RouterEP             = routerEP;
            this.AppName              = appName;
            this.AppDescription       = appDescription;
            this.RouterInfo           = routerInfo;
            this.UdpPort              = udpPort;
            this.TcpPort              = tcpPort;
            this.LogicalEndpointSetID = logicalEndpointSetID;
            this.ReplyAdvertise       = replyAdvertise;
            this.DiscoverLogical      = discoverLogical;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private RouterAdvertiseMsg(Stub param)
            : base(param)
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            RouterAdvertiseMsg clone;

            clone = new RouterAdvertiseMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The router's physical endpoint.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return MsgEP.Parse(base["router-ep"]); }

            set
            {
                Assertion.Test(value.ChannelEP == null, "ChannelEP must be null.");
                base["router-ep"] = value.ToString();
            }
        }

        /// <summary>
        /// Name of the application hosting the router.
        /// </summary>
        public string AppName
        {
            get { return base._Get("app-name", string.Empty); }
            set { base._Set("app-name", value); }
        }

        /// <summary>
        /// Description of the application hosting the router.
        /// </summary>
        public string AppDescription
        {
            get { return base._Get("app-description", string.Empty); }
            set { base._Set("app-description", value); }
        }

        /// <summary>
        /// The router's capability information.
        /// </summary>
        public MsgRouterInfo RouterInfo
        {
            get { return new MsgRouterInfo(base["router-info"]); }
            set { base._Set("router-info", value.ToString()); }
        }

        /// <summary>
        /// Returns the router's IP address as determined by the message 
        /// channel that received the message.
        /// </summary>
        public IPAddress IPAddress
        {
            get { return base._FromEP.ChannelEP.NetEP.Address; }
        }

        /// <summary>
        /// The router's listening TCP port.
        /// </summary>
        public int TcpPort
        {
            get { return base._Get("tcp-port", 0); }
            set { base._Set("tcp-port", value); }
        }

        /// <summary>
        /// The router's listening UDP port.
        /// </summary>
        public int UdpPort
        {
            get { return base._Get("udp-port", 0); }
            set { base._Set("udp-port", value); }
        }

        /// <summary>
        /// The GUID identifying the current set of logical endpoints implemented by the router.
        /// </summary>
        public Guid LogicalEndpointSetID
        {
            get { return base._Get("logical-epset-id", Guid.Empty); }
            set { base._Set("logical-epset-id", value); }
        }

        /// <summary>
        /// <c>true</c> if the receiving router should reply with a RouterAdvertiseMsg of its own.
        /// </summary>
        public bool ReplyAdvertise
        {
            get { return base._Get("reply-advertise", false); }
            set { base._Set("reply-advertise", value); }
        }

        /// <summary>
        /// <c>true</c> if the receiving router should reply with LogicalAdvertiseMsgs.
        /// </summary>
        public bool DiscoverLogical
        {
            get { return base._Get("discover-logical", false); }
            set { base._Set("discover-logical", value); }
        }
    }
}
