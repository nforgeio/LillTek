//-----------------------------------------------------------------------------
// FILE:        ClusterMemberMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the cluster member protocol messages.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Used by <see cref="ClusterMember" /> instances to coordinate their activities
    /// including electing the master instance.
    /// </summary>
    public sealed class ClusterMemberMsg : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Protocol message command strings

        internal const string MemberStatusCmd  = "member-status";
        internal const string ClusterStatusCmd = "cluster-status";
        internal const string ElectionCmd      = "election";
        internal const string PromoteCmd       = "promote";

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".Cluster";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClusterMemberMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="senderEP">The sender instance endpoint.</param>
        /// <param name="command">The command string.</param>
        public ClusterMemberMsg(MsgEP senderEP, string command)
        {
            base._Set("sender", (string)senderEP);
            base._Set("cmd", command);
            base._Set("caps", (int)ClusterMemberProtocolCaps.Current);
            base._Set("flags", (int)ClusterMemberMsgFlag.None);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="senderEP">The sender instance endpoint.</param>
        /// <param name="protocolCaps">Indicates the sender's protocol implementation capabilities.</param>
        /// <param name="command">The command string.</param>
        /// <param name="data">The message BLOB data.</param>
        public ClusterMemberMsg(MsgEP senderEP, ClusterMemberProtocolCaps protocolCaps, string command, byte[] data)
        {
            base._Set("sender", (string)senderEP);
            base._Set("cmd", command);
            base._Set("caps", (int)protocolCaps);
            base._Set("flags", (int)ClusterMemberMsgFlag.None);
            base._Data = data;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private ClusterMemberMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// The sending cluster member instance endpoint.
        /// </summary>
        public MsgEP SenderEP
        {
            get { return base._Get("sender"); }
            set { base._Set("sender", value); }
        }

        /// <summary>
        /// Indicates the sender's protocol implementation capabilities.  This defaults
        /// to <see cref="ClusterMemberProtocolCaps.Current" />.
        /// </summary>
        public ClusterMemberProtocolCaps ProtocolCaps
        {
            get { return (ClusterMemberProtocolCaps)base._Get("caps", (int)ClusterMemberProtocolCaps.Baseline); }
            set { base._Set("caps", (int)value); }
        }

        /// <summary>
        /// Returns the optional message flags.  This defaults to <see cref="ClusterMemberMsgFlag.None" />.
        /// </summary>
        public ClusterMemberMsgFlag Flags
        {
            get { return (ClusterMemberMsgFlag)base._Get("flags", (int)ClusterMemberMsgFlag.None); }
            set { base._Set("flags", (int)value); }
        }

        /// <summary>
        /// The message command.
        /// </summary>
        public string Command
        {
            get { return base._Get("cmd"); }
            set { base._Set("cmd", value); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            ClusterMemberMsg clone;

            clone = new ClusterMemberMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

#if TRACE
        /// <summary>
        /// Returns tracing details about the message.
        /// </summary>
        /// <returns>The trace string.</returns>
        public string GetTrace()
        {
            var sb = new StringBuilder(512);

            foreach (string key in base._Keys)
                sb.AppendFormat("{0}={1}\r\n", key, base[key]);

            switch (Command.ToLowerInvariant())
            {
                case ClusterStatusCmd:

                    ClusterStatus clusterStatus = new ClusterStatus(base._Data);

                    clusterStatus.AppendTrace(sb);
                    break;

                case MemberStatusCmd:

                    ClusterMemberStatus memberStatus = new ClusterMemberStatus(base._Data);

                    memberStatus.AppendTrace(sb);
                    break;
            }

            return sb.ToString();
        }
#endif
    }
}
