//-----------------------------------------------------------------------------
// FILE:        MsgQueueMemoryStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simple in-memory IMsgQueueStore implementation.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// A simple in-memory <see cref="IMsgQueueStore" /> implementation.
    /// </summary>
    /// <remarks>
    /// This implementation doesn't really persist the messages at all
    /// and suitable mostly for unit testing purposes.
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class MsgQueueMemoryStore : IMsgQueueStore, ILockable
    {
        private Dictionary<Guid, QueuedMsgInfo> messages;

        //---------------------------------------------------------------------
        // Implementation Note
        //
        // The QueuedMsgInfo.ProviderData property is set to the actual message
        // by this provider and the PersistID is the message's ID.

        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgQueueMemoryStore()
        {
            this.messages = null;
        }

        /// <summary>
        /// Opens the store, preparing it for reading and writing messages and
        /// metadata to the backing store.
        /// </summary>
        public void Open()
        {
            using (TimedLock.Lock(this))
            {
                if (this.messages != null)
                    throw new InvalidOperationException("Message store is already open.");

                this.messages = new Dictionary<Guid, QueuedMsgInfo>();
            }
        }

        /// <summary>
        /// Closes the store, releasing any resources.
        /// </summary>
        public void Close()
        {
            messages = null;
        }

        /// <summary>
        /// Returns the number of messages currently persisted.
        /// </summary>
        public int Count
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (messages == null)
                        throw new ObjectDisposedException(this.GetType().Name);

                    return messages.Count;
                }
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator" /> over the set of <see cref="QueuedMsgInfo" /> records describing
        /// each message currently persisted in the backing store.
        /// </summary>
        /// <returns>An <see cref="IEnumerator" /> instances.</returns>
        IEnumerator<QueuedMsgInfo> IEnumerable<QueuedMsgInfo>.GetEnumerator()
        {
            return messages.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator" /> over the set of <see cref="QueuedMsgInfo" /> records describing
        /// each message currently persisted in the backing store.
        /// </summary>
        /// <returns>An <see cref="IEnumerator" /> instances.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return messages.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns a non-<c>null</c> persist ID if the message exists in the backing
        /// store, <c>null</c> if it does not exist.
        /// </summary>
        /// <param name="ID">The message ID.</param>
        /// <returns>The persist ID of the object or <c>null</c>.</returns>
        public object GetPersistID(Guid ID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                QueuedMsgInfo msgInfo;

                if (messages.TryGetValue(ID, out msgInfo))
                    return msgInfo.ID;

                return null;
            }
        }

        /// <summary>
        /// Adds a message to the backing store and updates the <see cref="QueuedMsgInfo.PersistID" />
        /// and <see cref="QueuedMsgInfo.ProviderData" /> fields in the <see cref="QueuedMsgInfo" />
        /// instance passed.
        /// </summary>
        /// <param name="msgInfo">The message metadata.</param>
        /// <param name="msg">The message.</param>
        public void Add(QueuedMsgInfo msgInfo, QueuedMsg msg)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                msgInfo.PersistID    = msg.ID;
                msgInfo.ProviderData = msg;
                messages[msg.ID]     = msgInfo;
            }
        }

        /// <summary>
        /// Removes a message from the backing store if the message is present.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being removed.</param>
        public void Remove(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid msgID = (Guid)persistID;

                if (messages.ContainsKey(msgID))
                    messages.Remove(msgID);
            }
        }

        /// <summary>
        /// Loads a message from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        public QueuedMsg Get(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                    return (QueuedMsg)msgInfo.ProviderData;
                else
                    return null;
            }
        }

        /// <summary>
        /// Loads a message from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        public QueuedMsgInfo GetInfo(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid             msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                    return msgInfo;
                else
                    return null;
            }
        }

        /// <summary>
        /// Updates delivery attempt related metadata for a message.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="deliveryAttempts">The number of delivery attempts.</param>
        /// <param name="deliveryTime">The delivery attempt time.</param>
        public void SetDeliveryAttempt(object persistID, int deliveryAttempts, DateTime deliveryTime)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.DeliveryAttempts = deliveryAttempts;
                    msgInfo.DeliveryTime     = deliveryTime;
                }
            }
        }

        /// <summary>
        /// Updates a message's priority.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="priority">The new priority value.</param>
        public void SetPriority(object persistID, DeliveryPriority priority)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;
                QueuedMsg       msg;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.Priority = priority;
                    msg              = (QueuedMsg)msgInfo.ProviderData;
                    msg.Priority     = priority;
                }
            }
        }

        /// <summary>
        /// Updates a message's target endpoint and status.  This is typically used
        /// for moving an expired message to a dead letter queue.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="targetEP">The new message target endpoint.</param>
        /// <param name="deliveryTime">The new message delivery time.</param>
        /// <param name="expireTime">The new message expiration time.</param>
        /// <param name="status">The new <see cref="DeliveryStatus" />.</param>
        public void Modify(object persistID, MsgEP targetEP, DateTime deliveryTime, DateTime expireTime, DeliveryStatus status)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;
                QueuedMsg       msg;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.TargetEP     = targetEP;
                    msgInfo.DeliveryTime = deliveryTime;
                    msgInfo.ExpireTime   = expireTime;
                    msgInfo.Status       = status;

                    msg            = (QueuedMsg)msgInfo.ProviderData;
                    msg.TargetEP   = targetEP;
                    msg.ExpireTime = expireTime;
                }
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
