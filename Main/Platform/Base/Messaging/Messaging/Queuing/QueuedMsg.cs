//-----------------------------------------------------------------------------
// FILE:        QueuedMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a queued message's header and body information while
//              the message is in transit.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Describes a queued message's header and body information while the 
    /// message is in transit.
    /// </summary>
    public sealed class QueuedMsg
    {
        private Guid                id;             // The message's ID
        private Guid                sessionID;      // The session ID (or Guid.Empty)
        private MsgEP               targetEP;       // The target queue endpoint
        private MsgEP               responseEP;     // The response queue endpoint (or null)
        private DateTime            sendTime;       // Time the message was sent (UTC)
        private DateTime            expireTime;     // Message expiration time (UTC)
        private MsgQueueFlag        flags;          // The message flag bits
        private DeliveryPriority    priority;       // The message's delivery priority
        private object              body;           // The unserialized message body (or null)
        private byte[]              bodyRaw;        // The serialized message body (or null)

        /// <summary>
        /// Constructs an empty message with a unique ID.
        /// </summary>
        public QueuedMsg()
        {
            this.id         = Helper.NewGuid();
            this.expireTime = DateTime.MinValue;
            this.flags      = MsgQueueFlag.None;
            this.priority   = DeliveryPriority.Normal;
            this.body       = null;
            this.bodyRaw    = null;
        }

        /// <summary>
        /// Constructs a message from a body object instance.
        /// </summary>
        /// <param name="body">The message body object.</param>
        public QueuedMsg(object body)
            : this()
        {
            this.body = body;
        }

        /// <summary>
        /// Constructs a prioritized message from a body object instance.
        /// </summary>
        /// <param name="priority">The message <see cref="DeliveryPriority" />.</param>
        /// <param name="body">The message body object.</param>
        public QueuedMsg(DeliveryPriority priority, object body)
            : this()
        {
            this.priority = priority;
            this.body     = body;
        }

        /// <summary>
        /// Initializes the message from from a <see cref="MsgQueueCmd" />,
        /// optionally deserializing the message body.
        /// </summary>
        /// <param name="msg">The <see cref="MsgQueueCmd" /> message.</param>
        /// <param name="deserialize">
        /// Pass <c>true</c> to deserialize the message body, <c>false</c> to limit
        /// deserialization to the message headers.
        /// </param>
        /// <remarks>
        /// <note>
        /// This constructor is provided for applications that need to implement
        /// a custom <see cref="IMsgQueueStore" />.
        /// </note>
        /// </remarks>
        public QueuedMsg(MsgQueueCmd msg, bool deserialize)
        {
            Assertion.Test(msg.Command == MsgQueueCmd.EnqueueCmd);

            ParseHeaders(msg.MessageHeader);
            this.bodyRaw = msg.MessageBody;

            if (deserialize)
                body = Serialize.FromBinary(bodyRaw);
            else
                body = null;
        }

        /// <summary>
        /// Initializes the message from from a <see cref="MsgQueueAck" />,
        /// optionally deserializing the message body.
        /// </summary>
        /// <param name="msg">The <see cref="MsgQueueAck" /> message.</param>
        /// <param name="deserialize">
        /// Pass <c>true</c> to deserialize the message body, <c>false</c> to limit
        /// deserialization to the message headers.
        /// </param>
        /// <remarks>
        /// <note>
        /// This constructor is provided for applications that need to implement
        /// a custom <see cref="IMsgQueueStore" />.
        /// </note>
        /// </remarks>
        public QueuedMsg(MsgQueueAck msg, bool deserialize)
        {
            ParseHeaders(msg.MessageHeader);
            this.bodyRaw = msg.MessageBody;

            if (deserialize)
                body = Serialize.FromBinary(bodyRaw);
            else
                body = null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="msgInfo">A <see cref="QueuedMsgInfo" /> instance with the header information.</param>
        /// <param name="bodyRaw">The raw message body.</param>
        /// <param name="deserialize">
        /// Pass <c>true</c> to deserialize the message body, <c>false</c> to limit
        /// deserialization to the message headers.
        /// </param>
        /// <remarks>
        /// <note>
        /// This constructor is provided for applications that need to implement
        /// a custom <see cref="IMsgQueueStore" />.
        /// </note>
        /// </remarks>
        public QueuedMsg(QueuedMsgInfo msgInfo, byte[] bodyRaw, bool deserialize)
        {
            this.id         = msgInfo.ID;
            this.targetEP   = msgInfo.TargetEP;
            this.responseEP = msgInfo.ResponseEP;
            this.sessionID  = msgInfo.SessionID;
            this.sendTime   = msgInfo.SendTime;
            this.expireTime = msgInfo.ExpireTime;
            this.flags      = msgInfo.Flags;
            this.priority   = msgInfo.Priority;
            this.bodyRaw    = bodyRaw;

            if (deserialize)
                body = Serialize.FromBinary(bodyRaw);
            else
                body = null;
        }

        /// <summary>
        /// Extracts the message headers from a <see cref="ArgCollection" />
        /// encoded string.
        /// </summary>
        /// <param name="headers">The encoded headers.</param>
        private void ParseHeaders(string headers)
        {
            var     args = ArgCollection.Parse(headers, '=', '\t');
            string   v;

            this.id         = args.Get("id", Guid.Empty);
            this.sessionID  = args.Get("session-id", Guid.Empty);
            this.sendTime   = args.Get("send-time", DateTime.MinValue);
            this.expireTime = args.Get("expire-time", DateTime.MinValue);
            this.flags      = (MsgQueueFlag)args.Get("flags", 0);
            this.priority   = (DeliveryPriority)args.Get("priority", (int)DeliveryPriority.Normal);

            v = args.Get("target-ep");
            if (v == null)
                this.targetEP = null;
            else
            {
                this.targetEP = MsgEP.Parse(v);
                if (!targetEP.IsLogical)
                    throw new ArgumentException("TargetEP must be logical.");
            }

            v = args.Get("response-ep");
            if (v == null)
                this.responseEP = null;
            else
            {
                this.responseEP = MsgEP.Parse(v);
                if (!responseEP.IsLogical)
                    throw new ArgumentException("ResponseEP must be logical.");
            }
        }

        /// <summary>
        /// The message's globally unique ID.
        /// </summary>
        public Guid ID
        {
            get { return id; }
            set { id = value; }
        }

        /// <summary>
        /// The globally unique ID of the session this message belongs to
        /// or <see cref="Guid.Empty" /> if the message is not part of
        /// a session.
        /// </summary>
        public Guid SessionID
        {
            get { return sessionID; }
            set { sessionID = value; }
        }

        /// <summary>
        /// The target queue's logical endpoint.
        /// </summary>
        public MsgEP TargetEP
        {
            get { return targetEP; }

            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                if (!value.IsLogical)
                    throw new ArgumentException("TargetEP must be logical.");

                targetEP = value;
            }
        }

        /// <summary>
        /// The response queue's target endpoint or <c>null</c> if no
        /// response is expected.
        /// </summary>
        public MsgEP ResponseEP
        {
            get { return responseEP; }

            set
            {
                if (value == null && !value.IsLogical)
                    throw new ArgumentException("ResponseEP must be logical.");

                responseEP = value;
            }
        }

        /// <summary>
        /// The time the message was queued (UTC).
        /// </summary>
        public DateTime SendTime
        {
            get { return sendTime; }
            set { sendTime = value; }
        }

        /// <summary>
        /// The time the message expires and all delivery attempts should be ceased (UTC).
        /// </summary>
        public DateTime ExpireTime
        {
            get { return expireTime; }
            set { expireTime = value; }
        }

        /// <summary>
        /// The <see cref="MsgQueueFlag" /> message flag bits.
        /// </summary>
        public MsgQueueFlag Flags
        {
            get { return flags; }
            set { flags = value; }
        }

        /// <summary>
        /// Specifies the message's <see cref="DeliveryPriority" />.
        /// </summary>
        public DeliveryPriority Priority
        {
            get { return priority; }
            set { priority = value; }
        }

        /// <summary>
        /// The deserialized message body object graph (or <c>null</c>).
        /// </summary>
        public object Body
        {
            get { return body; }
            set { body = value; }
        }

        /// <summary>
        /// The serialized message body (or <c>null</c>).
        /// </summary>
        public byte[] BodyRaw
        {
            get
            {
                if (bodyRaw == null)
                    bodyRaw = Serialize.ToBinary(body, Compress.Best);

                return bodyRaw;
            }

            set { bodyRaw = value; }
        }

        /// <summary>
        /// Returns the message's headers as a string suitable for
        /// transmitting in a <see cref="MsgQueueCmd" /> or 
        /// <see cref="MsgQueueAck" /> message.
        /// </summary>
        /// <param name="settings">The <see cref="MsgQueueSettings" /> settings.</param>
        /// <returns>A <see cref="ArgCollection" /> formatted string.</returns>
        internal string GetMessageHeader(MsgQueueSettings settings)
        {
            var args = new ArgCollection('=', '\t');

            if (expireTime == DateTime.MinValue)
            {
                // If the expiration time was not explicitly set by the application
                // then we're going to set it ourselves.

                if (settings.MessageTTL <= TimeSpan.Zero)
                    expireTime = DateTime.MaxValue;
                else
                    expireTime = DateTime.UtcNow + settings.MessageTTL;
            }

            args.Set("id", id);

            if (sessionID != Guid.Empty)
                args.Set("session-id", sessionID);

            args.Set("target-ep", targetEP.ToString());

            if (responseEP != null)
                args.Set("response-ep", responseEP.ToString());

            args.Set("send-time", sendTime);
            args.Set("expire-time", expireTime);
            args.Set("flags", (int)flags);
            args.Set("priority", (int)priority);

            return args.ToString();
        }

        /// <summary>
        /// Returns the serialized message body.
        /// </summary>
        /// <param name="compress">
        /// The <see cref="Compress" /> code indicating whether the serialized
        /// body should be compressed.
        /// </param>
        /// <returns>A byte array.</returns>
        internal byte[] GetMessageBody(Compress compress)
        {
            if (bodyRaw != null)
                return bodyRaw;

            bodyRaw = Serialize.ToBinary(body, compress);
            return bodyRaw;
        }

        /// <summary>
        /// Deserializes the message body if it is not already serialized.
        /// </summary>
        internal void DeserializedBody()
        {
            if (body == null)
                body = Serialize.FromBinary(bodyRaw);
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        /// <summary>
        /// Compares the current instance against the instance passed.
        /// </summary>
        /// <param name="obj">The instance to be compared.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        public override bool Equals(object obj)
        {
            var msg = obj as QueuedMsg;

            if (msg == null)
                return false;

            return this.id == msg.id &&
                   this.sessionID == msg.sessionID &&
                   this.targetEP.Equals(msg.targetEP) &&
                   this.responseEP.Equals(msg.responseEP) &&
                   this.sendTime == msg.sendTime &&
                   this.expireTime == msg.expireTime &&
                   this.flags == msg.flags &&
                   this.priority == msg.priority &&
                   this.body.Equals(msg.body);
        }
    }
}
