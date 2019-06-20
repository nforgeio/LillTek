//-----------------------------------------------------------------------------
// FILE:        DuplexSessionMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Message used by DuplexSession to communicate between the
//              the two ends of the session.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using LillTek.Common;

using LillTek.Messaging;
using LillTek.Messaging.Queuing;

namespace LillTek.Messaging
{
    /// <summary>
    /// Message used by <see cref="DuplexSession" /> to communicate between the 
    /// two ends of the session.
    /// </summary>
    public sealed class DuplexSessionMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Sent by the client to a server's logical endpoint requesting that
        /// a session be established.
        /// </summary>
        public const string OpenCmd = "open";

        /// <summary>
        /// Response sent by the server to the client's physical endpoint
        /// indicating that a session has been established or is rejected.
        /// </summary>
        public const string OpenAck = "open-ack";

        /// <summary>
        /// Sent by either side of the session to the other's physical endpoint
        /// indicating that the session is to be closed.
        /// </summary>
        public const string CloseCmd = "close";

        /// <summary>
        /// Sent by the requesting side of a query pinging the processing side
        /// about the status of an outstanding query.
        /// </summary>
        public const string QueryStatusCmd = "query-status";

        /// <summary>
        /// Sent by the side receiving a query to confirm to the other side
        /// that the query request has been received.
        /// </summary>
        public const string QueryRequestAck = "query-request-ack";

        /// <summary>
        /// Sent by the side receiving a query to confirm to the other side
        /// that the query response has been received.
        /// </summary>
        public const string QueryResponseAck = "query-response-ack";

        /// <summary>
        /// Sent by both sides indicating that it is still alive.
        /// </summary>
        public const string KeepAliveCmd = "keep-alive";

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".Duplex";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public DuplexSessionMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command string.</param>
        public DuplexSessionMsg(string command)
        {
            this.Version = 0;
            this.Command = command;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="queryID">The query ID.</param>
        public DuplexSessionMsg(string command, Guid queryID)
        {
            this.Version = 0;
            this.Command = command;
            this.QueryID = queryID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private DuplexSessionMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the <see cref="DuplexSession" /> associated with this message.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is valid only for message received by a message handler.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the message was not received by a message handler.</exception>
        public DuplexSession Session
        {
            get
            {
                if (base._Session == null)
                    throw new InvalidOperationException("Message was not received by a message handler.");

                return (DuplexSession)base._Session;
            }
        }

        /// <summary>
        /// Indicates the version of the DuplexSession protocol implemented by the
        /// sender.
        /// </summary>
        public int Version
        {
            get { return base._Get("version", 0); }
            private set { base._Set("version", value); }
        }

        /// <summary>
        /// The message command string.
        /// </summary>
        public string Command
        {
            get { return base._Get("cmd"); }
            set { base._Set("cmd", value); }
        }

        /// <summary>
        /// non-<c>null</c> to specify an error message.
        /// </summary>
        public string Exception
        {

            get { return base._Get("exception"); }
            set { base._Set("exception", value); }
        }

        /// <summary>
        /// The interval at which the client side of the session should
        /// transmit keep-alive messages to the server.
        /// </summary>
        public TimeSpan KeepAliveTime
        {

            get { return base._Get("keep-alive-time", TimeSpan.Zero); }
            set { base._Set("keep-alive-time", value); }
        }

        /// <summary>
        /// The maximum time the session should wait for a keep-alive message
        /// before assuming that the session has been closed.
        /// </summary>
        public TimeSpan SessionTTL
        {
            get { return base._Get("session-ttl", TimeSpan.Zero); }
            set { base._Set("session-ttl", value); }
        }

        /// <summary>
        /// The ID of the query.
        /// </summary>
        public Guid QueryID
        {
            get { return base._Get("query-id", Guid.Empty); }
            set { base._Set("query-id", value); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            DuplexSessionMsg clone;

            clone = new DuplexSessionMsg(Stub.Param);
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

            base._TraceDetails(null, sb);
            return sb.ToString();
        }
#endif
    }
}
