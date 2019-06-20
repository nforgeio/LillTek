//-----------------------------------------------------------------------------
// FILE:        HubKeepAliveMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sent by hub routers to their parent to keep a TCP channel open.

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
    /// Sent by hub routers to their parent to keep a TCP channel open.
    /// </summary>
    public sealed class HubKeepAliveMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".HubKeepAlive";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public HubKeepAliveMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="childEP">Endpoint of the child router sending the message.</param>
        /// <param name="appName">Name of the application hosting the child router.</param>
        /// <param name="appDescription">Description of the hosting application.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints handled by the router.</param>
        public HubKeepAliveMsg(MsgEP childEP, string appName, string appDescription, MsgRouterInfo routerInfo, Guid logicalEndpointSetID)
        {
            this._Flags               = MsgFlag.Priority;
            this.ChildEP              = childEP;
            this.AppName              = appName;
            this.AppDescription       = appDescription;
            this.RouterInfo           = routerInfo;
            this.LogicalEndpointSetID = logicalEndpointSetID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private HubKeepAliveMsg(Stub param)
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
            HubKeepAliveMsg clone;

            clone = new HubKeepAliveMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// Endpoint of the child router sending the message.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The query string of the endpoint will be clipped 
        /// before saving the value into the message.
        /// </note>
        /// </remarks>
        public MsgEP ChildEP
        {
            get { return MsgEP.Parse(base._Get("child-ep", string.Empty)); }
            set { base._Set("child-ep", value.ToString(-1, false)); }
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
