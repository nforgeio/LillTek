//-----------------------------------------------------------------------------
// FILE:        LeafSettingsMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hub routers send this back to leaf routers to advertise
//              the hub's presence and specify intialization settings

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
    /// Hub routers send this back to leaf routers to advertise
    /// the hub's presence and specify intialization settings.
    /// </summary>
    public sealed class LeafSettingsMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".LeafSettings";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public LeafSettingsMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hubEP">The hub router's endpoint.</param>
        /// <param name="hubUdpPort">The UDP port the hub is monitoring.</param>
        /// <param name="hubTcpPort">The TCP port the hub is monitoring.</param>
        /// <param name="advertiseTime">The interval at which the leaf should continue broadcasting RouterAdvertiseMsgs.</param>
        /// <param name="discoverLogical"><c>true</c> if the receiving router should reply with LogicalAdvertiseMsgs.</param>
        public LeafSettingsMsg(MsgEP hubEP, int hubUdpPort, int hubTcpPort, TimeSpan advertiseTime, bool discoverLogical)
        {
            this._Flags          = MsgFlag.Priority;
            this.HubEP           = hubEP;
            this.HubUdpPort      = hubUdpPort;
            this.HubTcpPort      = hubTcpPort;
            this.AdvertiseTime   = advertiseTime;
            this.DiscoverLogical = discoverLogical;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private LeafSettingsMsg(Stub param)
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
            LeafSettingsMsg clone;

            clone = new LeafSettingsMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The hub router's message endpoint.
        /// </summary>
        public MsgEP HubEP
        {
            get { return MsgEP.Parse(base["hub-ep"]); }

            set
            {
                Assertion.Test(value.ChannelEP == null, "ChannelEP must be null.");
                base["hub-ep"] = value.ToString();
            }
        }

        /// <summary>
        /// Returns the hub router's IP address.
        /// </summary>
        public IPAddress HubIPAddress
        {
            get { return base._FromEP.ChannelEP.NetEP.Address; }
        }

        /// <summary>
        /// The hub router's listening TCP port.
        /// </summary>
        public int HubTcpPort
        {
            get { return base._Get("hub-tcp-port", 0); }
            set { base._Set("hub-tcp-port", value); }
        }

        /// <summary>
        /// The hub router's listening UDP port.
        /// </summary>
        public int HubUdpPort
        {
            get { return _Get("hub-udp-port", 0); }
            set { base._Set("hub-udp-port", value); }
        }

        /// <summary>
        /// The interval at which the leaf should continue broadcasting RouterInfoMsg.
        /// </summary>
        public TimeSpan AdvertiseTime
        {
            get { return base._Get("advertise-time", TimeSpan.FromSeconds(60.0)); }
            set { base._Set("advertise-time", value); }
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
