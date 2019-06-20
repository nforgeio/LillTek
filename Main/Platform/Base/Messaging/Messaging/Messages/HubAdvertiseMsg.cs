//-----------------------------------------------------------------------------
// FILE:        HubAdvertiseMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Message sent on the uplink TCP channel to identify the router
//              to its parent

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
    /// Message sent on the uplink TCP channel to identify the router
    /// to its parent.
    /// </summary>
    public sealed class HubAdvertiseMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".HubAdvertise";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public HubAdvertiseMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hubEP">The hub router's endpoint.</param>
        /// <param name="appName">The name of the application hosting the router.</param>
        /// <param name="appDescription">A description of the application.</param>
        /// <param name="routerInfo">The hub router's capability information.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints implemented by the router.</param>
        public HubAdvertiseMsg(MsgEP hubEP, string appName, string appDescription, MsgRouterInfo routerInfo, Guid logicalEndpointSetID)
        {
            Assertion.Test(hubEP.IsPhysical);

            this._Flags               = MsgFlag.Priority;
            this.HubEP                = hubEP;
            this.AppName              = appName;
            this.AppDescription       = appDescription;
            this.RouterInfo           = routerInfo;
            this.LogicalEndpointSetID = logicalEndpointSetID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private HubAdvertiseMsg(Stub param)
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
            HubAdvertiseMsg clone;

            clone = new HubAdvertiseMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The hub router's physical endpoint.
        /// </summary>
        public MsgEP HubEP
        {
            get { return MsgEP.Parse(base["hub-ep"]); }
            set { base["hub-ep"] = value.ToString(); }
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
        /// The hub router's capability information.
        /// </summary>
        public MsgRouterInfo RouterInfo
        {
            get { return new MsgRouterInfo(base["router-info"]); }
            set { base._Set("router-info", value.ToString()); }
        }

        /// <summary>
        /// Returns the child router's IP address.
        /// </summary>
        public IPAddress IPAddress
        {
            get { return base._FromEP.ChannelEP.NetEP.Address; }
        }

        /// <summary>
        /// Returns the child router's TCP port.
        /// </summary>
        public int TcpPort
        {
            get { return base._FromEP.ChannelEP.NetEP.Port; }
        }

        /// <summary>
        /// The GUID identifying the current set of logical endpoints implemented by the router.
        /// </summary>
        public Guid LogicalEndpointSetID
        {
            get { return base._Get("logical-epset-id", Guid.Empty); }
            set { base._Set("logical-epset-id", value); }
        }
    }
}
