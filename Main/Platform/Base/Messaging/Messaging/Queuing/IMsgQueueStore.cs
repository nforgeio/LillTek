//-----------------------------------------------------------------------------
// FILE:        IMsgQueueStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of the plug-in that the MsgQueueEngine 
//              uses to persist and query for messages queued for transmission.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Defines the behavior of the plug-in that the <see cref="MsgQueueEngine" />
    /// uses to persist and query for messages queued for transmission.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An implementation of this interface is used by the <see cref="MsgQueueEngine" />
    /// to persist messages queued for delivery against some kind of backing store.
    /// This store could be the file system, a database, or even an in-memory structure
    /// (as is the case in the <see cref="MsgQueueMemoryStore" /> implementation).
    /// </para>
    /// <para>
    /// The <see cref="MsgQueueEngine" /> implementation's persistence requirements
    /// are pretty simple since the current design loads the entire collection of message
    /// metadata into memory and then manages the scheduling of message delivery
    /// internally.  The engine does not rely on the persistence provider to implement
    /// any sophisticated and potentially complex querying behavior.  Because of this,
    /// persistence providers should be relatively easy to write.
    /// </para>
    /// <para>
    /// The main implication of this design is that a backing store cannot be shared
    /// by more than one <see cref="MsgQueueEngine" /> since each instance has its
    /// own copy of the message metadata and changes made by one instance will not
    /// be recognized by the other.
    /// </para>
    /// <para>
    /// Use <see cref="Open" /> to open the message store and <see cref="Close" />
    /// to close it.  <see cref="Count" /> returns the current number of items
    /// in the store and the class enumerator provides for enumerating
    /// all of the messages.
    /// </para>
    /// <para>
    /// Messages are identified by store specific <b>persist IDs</b> and metadata
    /// about the messages is encoded into <see cref="QueuedMsgInfo" /> instances
    /// to be passed between the <see cref="MsgQueueEngine" /> and the message
    /// store implementations.  Use <see cref="GetPersistID" /> to determine if
    /// a specific message in present in the store (based on its <see cref="Guid" />),
    /// <see cref="GetInfo" /> to obtain the metadata for a message, and
    /// <see cref="Get" /> to get a copy of the message itself.
    /// </para>
    /// <para>
    /// Message metadata can be modified by calling <see cref="Modify" />, 
    /// <see cref="SetDeliveryAttempt" />, and <see cref="SetPriority" />. 
    /// </para>
    /// <note>
    /// <see cref="IMsgQueueStore" /> implementations must be threadsafe.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public interface IMsgQueueStore : IEnumerable<QueuedMsgInfo>
    {
        /// <summary>
        /// Opens the store, preparing it for reading and writing messages and
        /// metadata to the backing store.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the store, releasing any resources.
        /// </summary>
        void Close();

        /// <summary>
        /// Returns the number of messages currently persisted.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns a non-<c>null</c> persist ID if the message exists in the backing
        /// store, <c>null</c> if it does not exist.
        /// </summary>
        /// <param name="ID">The message ID.</param>
        /// <returns>The persist ID of the object or <c>null</c>.</returns>
        object GetPersistID(Guid ID);

        /// <summary>
        /// Adds a message to the backing store and updates the <see cref="QueuedMsgInfo.PersistID" />
        /// and <see cref="QueuedMsgInfo.ProviderData" /> fields in the <see cref="QueuedMsgInfo" />
        /// instance passed.
        /// </summary>
        /// <param name="msgInfo">The message metadata.</param>
        /// <param name="msg">The message.</param>
        void Add(QueuedMsgInfo msgInfo, QueuedMsg msg);

        /// <summary>
        /// Removes a message from the backing store if the message is present.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being removed.</param>
        void Remove(object persistID);

        /// <summary>
        /// Loads a message from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        QueuedMsg Get(object persistID);

        /// <summary>
        /// Loads a message metadata from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        QueuedMsgInfo GetInfo(object persistID);

        /// <summary>
        /// Updates delivery attempt related metadata for a message.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="deliveryAttempts">The number of delivery attempts.</param>
        /// <param name="deliveryTime">The delivery attempt time.</param>
        void SetDeliveryAttempt(object persistID, int deliveryAttempts, DateTime deliveryTime);

        /// <summary>
        /// Updates a message's priority.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="priority">The new priority value.</param>
        void SetPriority(object persistID, DeliveryPriority priority);

        /// <summary>
        /// Updates a message's target endpoint and status.  This is typically used
        /// for moving an expired message to a dead letter queue.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="targetEP">The new message target endpoint.</param>
        /// <param name="deliveryTime">The new message delivery time.</param>
        /// <param name="expireTime">The new message expiration time.</param>
        /// <param name="status">The new <see cref="DeliveryStatus" />.</param>
        void Modify(object persistID, MsgEP targetEP, DateTime deliveryTime, DateTime expireTime, DeliveryStatus status);
    }
}
