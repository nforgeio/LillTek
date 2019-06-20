//-----------------------------------------------------------------------------
// FILE:        LogicalAdvertiseMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Advertises one or more logical endpoints to other routers.

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
    /// Advertises one or more logical endpoints to other routers.
    /// </summary>
    public sealed class LogicalAdvertiseMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".LogicalAdvertise";
        }

        //---------------------------------------------------------------------
        // Instance members

        private int cEPs = -1;      // # of logical endpoints (or -1 indicating that this is unknown).

        /// <summary>
        /// Constructor.
        /// </summary>
        public LogicalAdvertiseMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">The advertising router's physical endpoint.</param>
        /// <param name="appName">The name of the application hosting the router.</param>
        /// <param name="appDescription">A description of the application.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="udpPort">The UDP port the router is monitoring.</param>
        /// <param name="tcpPort">The TCP port the router is monitoring.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints handled by the router.</param>
        public LogicalAdvertiseMsg(MsgEP routerEP, string appName, string appDescription, MsgRouterInfo routerInfo,
                                   int udpPort, int tcpPort, Guid logicalEndpointSetID)
        {
            this._Flags               = MsgFlag.Priority;
            this.RouterEP             = routerEP;
            this.AppName              = appName;
            this.AppDescription       = AppDescription;
            this.RouterInfo           = routerInfo;
            this.cEPs                 = 0;
            this.UdpPort              = udpPort;
            this.TcpPort              = tcpPort;
            this.LogicalEndpointSetID = logicalEndpointSetID;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logicalEPs">The set of logical endpoints being advertised.</param>
        /// <param name="routerEP">The advertising router's physical endpoint.</param>
        /// <param name="appName">Name of the application hosting the advertising router.</param>
        /// <param name="appDescription">Description of the hosting application.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="udpPort">The UDP port the router is monitoring.</param>
        /// <param name="tcpPort">The TCP port the router is monitoring.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints handled by the router.</param>
        public LogicalAdvertiseMsg(MsgEP[] logicalEPs, MsgEP routerEP, string appName, string appDescription, MsgRouterInfo routerInfo,
                                   int udpPort, int tcpPort, Guid logicalEndpointSetID)
        {
            for (int i = 0; i < logicalEPs.Length; i++)
            {
                Assertion.Test(logicalEPs[i].IsLogical);
                base[string.Format("ep[{0}]", i)] = logicalEPs[i].ToString();
            }

            this._Flags                = MsgFlag.Priority;
            this.cEPs                 = logicalEPs.Length;
            this.RouterEP             = routerEP;
            this.AppName              = appName;
            this.AppDescription       = appDescription;
            this.RouterInfo           = routerInfo;
            this.UdpPort              = udpPort;
            this.TcpPort              = tcpPort;
            this.LogicalEndpointSetID = logicalEndpointSetID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private LogicalAdvertiseMsg(Stub param)
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
            LogicalAdvertiseMsg clone;

            clone = new LogicalAdvertiseMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// Adds a logical endpoint to the message.
        /// </summary>
        /// <param name="logicalEP">The endpoint to be added.</param>
        public void AddLogicalEP(MsgEP logicalEP)
        {
            if (cEPs == -1)
                cEPs = base._GetArray("ep").Length;

            Assertion.Test(logicalEP.IsLogical);
            base[string.Format("ep[{0}]", cEPs++)] = logicalEP.ToString();
        }

        /// <summary>
        /// Returns the number of endpoints in the message.
        /// </summary>
        public int EndpointCount
        {
            get
            {
                if (cEPs == -1)
                    cEPs = base._GetArray("EP").Length;

                return cEPs;
            }
        }

        /// <summary>
        /// Returns the set of logical endpoints being advertised.
        /// </summary>
        public MsgEP[] LogicalEPs
        {
            get
            {
                string[]    EPs;
                MsgEP[]     logicalEPs;

                EPs       = base._GetArray("ep");
                logicalEPs = new MsgEP[EPs.Length];

                for (int i = 0; i < EPs.Length; i++)
                {
                    logicalEPs[i] = MsgEP.Parse(EPs[i]);
                    Assertion.Test(logicalEPs[i].IsLogical);
                }

                return logicalEPs;
            }
        }

        /// <summary>
        /// The advertising router's physical endpoint.
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
    }
}
