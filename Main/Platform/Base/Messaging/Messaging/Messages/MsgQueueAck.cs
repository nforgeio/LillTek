//-----------------------------------------------------------------------------
// FILE:        MsgQueueAck.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Response message sent by a MsgQueueEngine client to a MsgQueue.

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
    /// Response message sent by a <see cref="MsgQueueEngine" /> client to a <see cref="MsgQueue" />.
    /// </summary>
    public sealed class MsgQueueAck : BlobPropertyMsg, IAck
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".LMQ.Ack";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgQueueAck()
        {
        }

        /// <summary>
        /// Constructs an instance from an exception to be transmitted back
        /// to the client.
        /// </summary>
        /// <param name="e">The exception.</param>
        public MsgQueueAck(Exception e)
        {
            this.Exception         = e.Message;
            this.ExceptionTypeName = e.GetType().FullName;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private MsgQueueAck(Stub param)
            : base(param)
        {
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
            MsgQueueAck clone;

            clone = new MsgQueueAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        //---------------------------------------------------------------------
        // IAck implementation

        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        public string Exception
        {
            get { return (string)base["_exception"]; }
            set { base["_exception"] = value; }
        }

        /// <summary>
        /// The fully qualified name of the exception type.
        /// </summary>
        public string ExceptionTypeName
        {
            get { return (string)base["_exception-type"]; }
            set { base["_exception-type"] = value; }
        }
    }
}
