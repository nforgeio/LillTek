//-----------------------------------------------------------------------------
// FILE:        MsgRouterInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a message router's capabilities and other information.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes a message router's capabilities and other information.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This information is encoded into a name/value collection
    /// implemented by <see cref="ArgCollection" /> so that it can be
    /// easily transmitted and processed.
    /// </para>
    /// <para>
    /// Router capabilities are communicated from one router to another
    /// via <see cref="TcpInitMsg" />, <see cref="RouterAdvertiseMsg" />,
    /// <see cref="HubAdvertiseMsg" />, <see cref="HubKeepAliveMsg" /> and 
    /// <see cref="LogicalAdvertiseMsg" /> messages.
    /// </para>
    /// </remarks>
    public class MsgRouterInfo
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The protocol version implemented by the current build.
        /// </summary>
        public static readonly Version CurrentProtocolVersion = new Version(1, 0);

        /// <summary>
        /// Message router capabilities for the current build.
        /// </summary>
        public static readonly MsgRouterInfo Default = new MsgRouterInfo();

        // Encoded parameter names

        private const string _ProtocolVersion  = "protocol-ver";
        private const string _BuildVersion     = "build-ver";
        private const string _IsP2P            = "p2p-enable";
        private const string _ReceiptSend      = "receipt-send";
        private const string _DeadRouterDetect = "dead-router-detect";
        private const string _MachineName      = "machine-name";

        //---------------------------------------------------------------------
        // Instance members

        private Version     protocolVer;        // Protocol version
        private Version     buildVer;           // Build version
        private bool        isP2P;              // True if P2P enabled
        private bool        receiptSend;        // True if ReceiptMsgs are generated
        private bool        deadRouterDetect;   // True if dead router detection is supported
        private string      machineName;        // Name of the machine hosting the router
        private string      capsArgs;           // Cached ArgCollection representation of the caps

        /// <summary>
        /// Initializes default capabilities for the current build.
        /// </summary>
        private MsgRouterInfo()
        {
            this.protocolVer      = CurrentProtocolVersion;
            this.buildVer         = new Version(Build.Version);
            this.isP2P            = true;
            this.receiptSend      = true;
            this.deadRouterDetect = true;
            this.machineName      = Helper.MachineName;
            this.capsArgs         = null;
        }

        /// <summary>
        /// Constructs a router capabilties instance for the specified router.
        /// </summary>
        /// <param name="router">The router.</param>
        public MsgRouterInfo(MsgRouter router)
            : this()
        {
            this.isP2P = router.EnableP2P;
        }

        /// <summary>
        /// Creates a capability instance with default values except for
        /// using the protocol version passed.  Used by unit tests.
        /// </summary>
        /// <param name="protocolVer">The protocol version to use.</param>
        internal MsgRouterInfo(Version protocolVer) : this() 
        {
            this.protocolVer = protocolVer;
        }

        /// <summary>
        /// Initializes the capabilities by parsing the encoded string passed.
        /// </summary>
        /// <param name="capsArgs">The capabilities arguments.</param>
        /// <remarks>
        /// This string must be formatted as implemented by <see cref="ArgCollection" />
        /// and returned by <see cref="ToString" />.
        /// </remarks>
        public MsgRouterInfo(string capsArgs)
        {
            ArgCollection caps;

            try
            {
                caps = ArgCollection.Parse(capsArgs);

                this.protocolVer      = new Version(caps[_ProtocolVersion]);
                this.buildVer         = new Version(caps[_BuildVersion]);
                this.isP2P            = Config.Parse(caps[_IsP2P], false);
                this.receiptSend      = Config.Parse(caps[_ReceiptSend], false);
                this.deadRouterDetect = Config.Parse(caps[_DeadRouterDetect], false);
                this.machineName      = Config.Parse(caps[_MachineName], "(unknown)");
                this.capsArgs         = null;
            }
            catch (Exception e)
            {
                throw new MsgException("Improperly formatted message router capabilities.", e);
            }
        }

        /// <summary>
        /// Returns the protocol version implemented by the message router.
        /// </summary>
        public Version ProtocolVersion
        {
            get { return protocolVer; }
        }

        /// <summary>
        /// Returns the build version for the message router.
        /// </summary>
        public Version BuildVersion
        {
            get { return buildVer; }
        }

        /// <summary>
        /// Returns <c>true</c> if the message router is peer-to-peer enabled.
        /// </summary>
        public bool IsP2P
        {
            get { return isP2P; }
        }

        /// <summary>
        /// Returns <c>true</c> if the router supports sending <see cref="ReceiptMsg" />
        /// messages.
        /// </summary>
        public bool ReceiptSend
        {
            get { return receiptSend; }
        }

        /// <summary>
        /// Returns <c>true</c> if the router supports dead router detection.
        /// </summary>
        public bool DeadRouterDetect
        {
            get { return deadRouterDetect; }
        }

        /// <summary>
        /// Returns the name of the machine hosting the router.
        /// </summary>
        public string MachineName
        {
            get { return machineName; }
        }

        /// <summary>
        /// Returns the router capabilities encoded in a <see cref="ArgCollection" /> format.
        /// </summary>
        public override string ToString()
        {
            if (capsArgs != null)
                return capsArgs;

            var caps = new ArgCollection();

            caps.Set(_ProtocolVersion, protocolVer.ToString());
            caps.Set(_BuildVersion, buildVer.ToString());
            caps.Set(_IsP2P, isP2P ? "1" : "0");
            caps.Set(_ReceiptSend, receiptSend ? "1" : "0");
            caps.Set(_DeadRouterDetect, deadRouterDetect ? "1" : "0");
            caps.Set(_MachineName, machineName);

            capsArgs = caps.ToString();
            return capsArgs;
        }
    }
}
