//-----------------------------------------------------------------------------
// FILE:        HubSettingsMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Parent routers send this message to child routers that establish
//              an uplink connection in response to a received HubAdvertiseMsg.

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
    /// Parent routers send this message to child routers that establish
    /// an uplink connection in response to a received HubAdvertiseMsg.
    /// </summary>
    public sealed class HubSettingsMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".HubSettings";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public HubSettingsMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints implemented by the router.</param>
        /// <param name="keepAliveTime">Interval at which KeepAliveMsgs should be sent on the uplink channel.</param>
        public HubSettingsMsg(Guid logicalEndpointSetID, TimeSpan keepAliveTime)
        {
            this.LogicalEndpointSetID = logicalEndpointSetID;
            this.KeepAliveTime        = keepAliveTime;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private HubSettingsMsg(Stub param)
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
            HubSettingsMsg clone;

            clone = new HubSettingsMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
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
        /// The hub router's message endpoint.
        /// </summary>
        public TimeSpan KeepAliveTime
        {
            get { return base._Get("keepalive-time", TimeSpan.FromSeconds(1.0)); }
            set { base._Set("keepalive-time", value); }
        }
    }
}
