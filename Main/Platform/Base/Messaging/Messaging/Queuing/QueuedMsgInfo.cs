//-----------------------------------------------------------------------------
// FILE:        QueuedMsgInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a message queued by an IMsgQueueStore implementation.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Transactions;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Describes a message queued by an <see cref="IMsgQueueStore" /> implementation.
    /// </summary>
    public sealed class QueuedMsgInfo
    {
        /// <summary>
        /// The <see cref="IMsgQueueStore" /> specific ID used to identify and
        /// locate the message.
        /// </summary>
        public object PersistID;

        /// <summary>
        /// The message's globally unique ID.
        /// </summary>
        public Guid ID;

        /// <summary>
        /// The message session ID or <see cref="Guid.Empty" />.
        /// </summary>
        public Guid SessionID;

        /// <summary>
        /// The message's target queue <see cref="MsgEP" />.
        /// </summary>
        public MsgEP TargetEP;

        /// <summary>
        /// The message's response queue <see cref="MsgEP" /> (or <c>null</c>).
        /// </summary>
        public MsgEP ResponseEP;

        /// <summary>
        /// The messages's <see cref="DeliveryPriority" />.
        /// </summary>
        public DeliveryPriority Priority;

        /// <summary>
        /// The message <see cref="MsgQueueFlag" />s.
        /// </summary>
        public MsgQueueFlag Flags;

        /// <summary>
        /// The time the message was originally queued (UTC).
        /// </summary>
        public DateTime SendTime;

        /// <summary>
        /// The message's delivery expiration time (UTC).
        /// </summary>
        public DateTime ExpireTime;

        /// <summary>
        /// The scheduled delivery time (UTC).
        /// </summary>
        public DateTime DeliveryTime;

        /// <summary>
        /// The message's approximate body size in bytes.
        /// </summary>
        public int BodySize;

        /// <summary>
        /// The number of attempts made so far to deliver this
        /// message to its final destination.
        /// </summary>
        public int DeliveryAttempts;

        /// <summary>
        /// Indicates the delivery status of the message.
        /// </summary>
        public DeliveryStatus Status;

        /// <summary>
        /// ID of the transaction holding a lock on this message 
        /// (or <see cref="Guid.Empty" />.
        /// </summary>
        public Guid LockID;

        /// <summary>
        /// Available for use by <see cref="IMsgQueueStore" /> implementations.
        /// </summary>
        public object ProviderData;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="persistID">
        /// The <see cref="IMsgQueueStore" /> specific ID used to identify and
        /// locate the message.
        /// </param>
        /// <param name="targetEP">The message's target queue <see cref="MsgEP" />.</param>
        public QueuedMsgInfo(object persistID, MsgEP targetEP)
        {
            this.PersistID        = persistID;
            this.ID               =
            this.SessionID        = Guid.Empty;
            this.TargetEP         = targetEP;
            this.ResponseEP       = null;
            this.Priority         = DeliveryPriority.Normal;
            this.Flags            = MsgQueueFlag.None;
            this.SendTime         = DateTime.UtcNow;
            this.ExpireTime       = DateTime.MaxValue;
            this.DeliveryTime     = DateTime.MinValue;
            this.BodySize         = 0;
            this.DeliveryAttempts = 0;
            this.LockID           = Guid.Empty;
            this.ProviderData     = null;
        }

        /// <summary>
        /// Initializes the record with information from a <see cref="QueuedMsg" />
        /// and sets default values for all remaining fields.
        /// </summary>
        /// <param name="persistID">
        /// The <see cref="IMsgQueueStore" /> specific ID used to identify and
        /// locate the message.
        /// </param>
        /// <param name="msg">The message</param>
        public QueuedMsgInfo(object persistID, QueuedMsg msg)
        {
            this.PersistID        = persistID;
            this.ID               = msg.ID;
            this.SessionID        = msg.SessionID;
            this.TargetEP         = msg.TargetEP;
            this.ResponseEP       = msg.ResponseEP;
            this.Priority         = msg.Priority;
            this.Flags            = msg.Flags;
            this.SendTime         = msg.SendTime;
            this.ExpireTime       = msg.ExpireTime;
            this.DeliveryTime     = DateTime.MinValue;
            this.BodySize         = msg.BodyRaw.Length;
            this.DeliveryAttempts = 0;
            this.LockID           = Guid.Empty;
            this.ProviderData     = null;
        }

        /// <summary>
        /// Parses the metadata from a <see cref="ArgCollection" /> formatted string
        /// generated by a previous <see cref="ToString" /> call.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <remarks>
        /// <note>
        /// This method does not initialize the <see cref="PersistID" /> and
        /// <see cref="ProviderData" /> properties.
        /// </note>
        /// </remarks>
        public QueuedMsgInfo(string input)
        {
            var args = ArgCollection.Parse(input, '=', '\t');

            this.PersistID        = null;
            this.ID               = args.Get("ID", Guid.Empty);
            this.SessionID        = args.Get("SessionID", Guid.Empty);
            this.TargetEP         = args.Get("TargetEP");
            this.ResponseEP       = args.Get("ResponseEP");
            this.Priority         = args.Get<DeliveryPriority>("Priority", DeliveryPriority.Normal);
            this.Flags            = (MsgQueueFlag)args.Get("Flags", 0);
            this.SendTime         = args.Get("SendTime", DateTime.MinValue);
            this.ExpireTime       = args.Get("ExpireTime", DateTime.MaxValue);
            this.DeliveryTime     = args.Get("DeliveryTime", DateTime.MinValue);
            this.BodySize         = args.Get("BodySize", 0);
            this.DeliveryAttempts = args.Get("DeliveryAttempts", 0);
            this.LockID           = args.Get("LockID", Guid.Empty);
            this.ProviderData     = null;
        }

        /// <summary>
        /// Used for implementing <see cref="Clone" />.
        /// </summary>
        private QueuedMsgInfo()
        {
        }

        /// <summary>
        /// Returns a shallowm clone of this instance.
        /// </summary>
        /// <returns>The cloned <see cref="QueuedMsgInfo" />.</returns>
        public QueuedMsgInfo Clone()
        {
            var clone = new QueuedMsgInfo();

            clone.PersistID        = this.PersistID;
            clone.ID               = this.ID;
            clone.SessionID        = this.SessionID;
            clone.TargetEP         = this.TargetEP;
            clone.ResponseEP       = this.ResponseEP;
            clone.Priority         = this.Priority;
            clone.Flags            = this.Flags;
            clone.SendTime         = this.SendTime;
            clone.ExpireTime       = this.ExpireTime;
            clone.DeliveryTime     = this.DeliveryTime;
            clone.BodySize         = this.BodySize;
            clone.DeliveryAttempts = this.DeliveryAttempts;
            clone.LockID           = this.LockID;
            clone.ProviderData     = this.ProviderData;

            return clone;
        }

        /// <summary>
        /// Renders the metadata as a <see cref="ArgCollection" /> formatted string.
        /// </summary>
        /// <returns>The formnatted string.</returns>
        /// <remarks>
        /// <note>
        /// This method does not serialize the <see cref="PersistID" /> and
        /// <see cref="ProviderData" /> properties.
        /// </note>
        /// </remarks>
        public override string ToString()
        {
            var args = ArgCollection.Parse(null, '=', '\t');

            args.Set("ID", this.ID);
            args.Set("SessionID", this.SessionID);
            args.Set("TargetEP", this.TargetEP);
            args.Set("ResponseEP", this.ResponseEP);
            args.Set("Priority", this.Priority);
            args.Set("Flags", (int)this.Flags);
            args.Set("SendTime", this.SendTime);
            args.Set("ExpireTime", this.ExpireTime);
            args.Set("DeliveryTime", this.DeliveryTime);
            args.Set("BodySize", this.BodySize);
            args.Set("DeliveryAttempts", this.DeliveryAttempts);
            args.Set("LockID", this.LockID);

            return args.ToString();
        }
    }
}
