//-----------------------------------------------------------------------------
// FILE:        MsgQueueCmd.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Command message sent by a MsgQueue client to a MsgQueueEngine.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Command message sent by a <see cref="MsgQueue" /> client to a <see cref="MsgQueueEngine" />.
    /// </summary>
    public sealed class MsgQueueCmd : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Sent by a source client to enqueue a message.
        /// </summary>
        public const string EnqueueCmd = "enqueue";

        /// <summary>
        /// Sent by a consuming client to dequeue a message.
        /// </summary>
        public const string DequeueCmd = "dequeue";

        /// <summary>
        /// Sent by a consuming client to peek a message.
        /// </summary>
        public const string PeekCmd = "peek";

        /// <summary>
        /// Initiates a transaction.
        /// </summary>
        public const string BeginTransCmd = "trans";

        /// <summary>
        /// Commit the current transaction.
        /// </summary>
        public const string CommitTransCmd = "commit";

        /// <summary>
        /// Roll back the current transaction.
        /// </summary>
        public const string RollbackTransCmd = "rollback";

        /// <summary>
        /// Rolls back any transactions.
        /// </summary>
        public const string RollbackAllTransCmd = "rollback-all";

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".LMQ.Cmd";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgQueueCmd()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command string.</param>
        public MsgQueueCmd(string command)
        {
            this.Command = command;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private MsgQueueCmd(Stub param)
            : base(param)
        {
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
        /// The source or destination queue endpoint.
        /// </summary>
        public string QueueEP
        {
            get { return base._Get("queue-ep"); }
            set { base._Set("queue-ep", value); }
        }

        /// <summary>
        /// Maximum time to wait for an operation to complete or 
        /// <see cref="TimeSpan.MaxValue" /> to wait indefinitely.
        /// </summary>
        public TimeSpan Timeout
        {
            get { return base._Get("timeout", TimeSpan.Zero); }
            set { base._Set("timeout", value); }
        }

        /// <summary>
        /// A string holding the queued message's header fields.
        /// </summary>
        public string MessageHeader
        {
            get { return base._Get("msg-headers", string.Empty); }
            set { base._Set("msg-headers", value.ToString()); }
        }

        /// <summary>
        /// Holds the serialized the queue message body.
        /// </summary>
        public byte[] MessageBody
        {
            get { return base._Data; }
            set { base._Data = value; }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            MsgQueueCmd clone;

            clone = new MsgQueueCmd(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
