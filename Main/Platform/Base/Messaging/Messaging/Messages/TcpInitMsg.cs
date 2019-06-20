//-----------------------------------------------------------------------------
// FILE:        TcpInitMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a TCP channel initialization message.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Internal message sent by each channel upon connect to
    /// provide initialization information to the other endpoint.
    /// </summary>
    public sealed class TcpInitMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".TcpInit";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor
        /// </summary>
        public TcpInitMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">Physical endpoint of the sending router.</param>
        /// <param name="routerInfo">The sending router's capability information.</param>
        /// <param name="isUplink"><c>true</c> if the sender side is an uplink.</param>
        /// <param name="listenPort">The listening port.</param>
        public TcpInitMsg(MsgEP routerEP, MsgRouterInfo routerInfo, bool isUplink, int listenPort)
        {

            this._Flags     = MsgFlag.Priority;
            this.RouterEP   = routerEP;
            this.RouterInfo = routerInfo;
            this.IsUplink   = isUplink;
            this.ListenPort = listenPort;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private TcpInitMsg(Stub param)
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
            TcpInitMsg clone;

            clone = new TcpInitMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The sending router's physical endpoint.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return base._Get("router-ep", string.Empty); }

            set
            {
                Assertion.Test(value.IsPhysical);
                base._Set("router-ep", value.ToString(-1, false));
            }
        }

        /// <summary>
        /// The sending router's capability information.
        /// </summary>
        public MsgRouterInfo RouterInfo
        {
            get { return new MsgRouterInfo(base._Get("router-info", MsgRouterInfo.Default.ToString())); }
            set { base._Set("router-info", value.ToString()); }
        }

        /// <summary>
        /// <c>true</c> if the sending side of the channel is an uplink.
        /// </summary>
        public bool IsUplink
        {
            get { return base._Get("is-uplink", false); }
            set { base._Set("is-uplink", value); }
        }

        /// <summary>
        /// The port the sending endpoint channel is listening on or 0
        /// if the sending side of the channel is an uplink channel.
        /// </summary>
        public int ListenPort
        {
            get { return base._Get("listen-port", 0); }
            set { base._Set("listen-port", value); }
        }
    }
}
