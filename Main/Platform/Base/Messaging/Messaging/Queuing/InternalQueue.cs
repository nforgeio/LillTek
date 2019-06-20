//-----------------------------------------------------------------------------
// FILE:        InternalQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an internal queue class used for queuing and
//              prioritizing messages.

using System;
using System.Collections.Generic;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Transactions;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Defines the information about messages flushed by <see cref="InternalQueue.Flush" />.
    /// </summary>
    internal struct FlushInfo
    {
        public readonly string          QueueEP;
        public readonly QueuedMsgInfo   MsgInfo;

        public FlushInfo(string queuedEP, QueuedMsgInfo msgInfo)
        {
            this.QueueEP = queuedEP;
            this.MsgInfo = msgInfo;
        }
    }

    /// <summary>
    /// Implements an internal queue class used for queuing and prioritizing messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class maintains an internal array of 5 queues, one per possible DeliveryPriority
    /// value.  Message information will be appended these queues based on priority
    /// and messages will be dequeued from the higher priority queues first.  Use
    /// <see cref="Enqueue" /> to add message information and <see cref="Dequeue" />
    /// to remove it.  <see cref="Peek" /> returns the same message information that
    /// <see cref="Dequeue" /> would but it leaves the item in the queue.
    /// </para>
    /// <para>
    /// The class implements a simple form of transactional locking.  The situation
    /// where this comes up is when <see cref="Peek" /> is called in the context
    /// of a transaction.  The message information return needs to be locked in
    /// such a way that during the course of the transaction, no other transaction 
    /// will see the message returned by <see cref="Dequeue" /> or <see cref="Peek" />.
    /// Similarily, messages queued within a transaction cannot be returned to
    /// other transactions until the queuing transaction is committed.  But the 
    /// locking transaction will expect these methods to return this message.
    /// This is necessary to maintain transactional consistency.
    /// </para>
    /// <para>
    /// The class handles this by having <see cref="Transaction" /> parameters 
    /// accepted by <see cref="Enqueue" />, <see cref="Dequeue" />, and <see cref="Peek" />.
    /// If the call is not being made in the context of a transaction then the 
    /// transaction parameter will be null.  If it is in the context of a transaction,
    /// then the parameter will be non-<c>null</c> and the method will set the message
    /// information's <see cref="QueuedMsgInfo.LockID" /> field to the 
    /// transaction's ID.
    /// </para>
    /// <para>
    /// <see cref="Dequeue" /> and <see cref="Peek" /> will then return message
    /// information only for those messages whose <see cref="QueuedMsgInfo.LockID" /> 
    /// field is <c>null</c> or is the same as the transaction parameter passed.
    /// </para>
    /// <para>
    /// <see cref="InternalQueue" /> also maintains a hash table of <see cref="QueuedMsgInfo" />
    /// records hashed by message <see cref="Guid" />.  The class indexer can be used
    /// to quickly reference a specific message record or determine if a message is present.
    /// </para>
    /// <para>
    /// When the transaction is commited or rolled back, <see cref="Unlock" />
    /// needs to be called, passing the completed transaction.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    internal sealed class InternalQueue
    {
        private string                          queueEP;
        private QueueArray<QueuedMsgInfo>[]     queuesByPriority;
        private Dictionary<Guid, QueuedMsgInfo> messages;
        private int                             count;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="queueEP">The queue's endpoint.</param>
        public InternalQueue(string queueEP)
        {
            this.queueEP = queueEP;

            // This class makes some assumptions about the DeliveryPriority values.

            Assertion.Test(5 == Enum.GetValues(typeof(DeliveryPriority)).Length);
            Assertion.Test((int)DeliveryPriority.VeryLow == 0);
            Assertion.Test((int)DeliveryPriority.Low == 1);
            Assertion.Test((int)DeliveryPriority.Normal == 2);
            Assertion.Test((int)DeliveryPriority.High == 3);
            Assertion.Test((int)DeliveryPriority.VeryHigh == 4);

            queuesByPriority = new QueueArray<QueuedMsgInfo>[5];
            for (int i = 0; i < queuesByPriority.Length; i++)
                queuesByPriority[i] = new QueueArray<QueuedMsgInfo>();

            messages = new Dictionary<Guid, QueuedMsgInfo>();
            count    = 0;
        }

        /// <summary>
        /// Adds the message information to the end other queued messages with the same priority.
        /// </summary>
        /// <param name="transaction">The current <see cref="BaseTransaction" /> (or <c>null</c>).</param>
        /// <param name="msgInfo">The message information.</param>
        public void Enqueue(BaseTransaction transaction, QueuedMsgInfo msgInfo)
        {
            var queue   = queuesByPriority[(int)msgInfo.Priority];
            var lockID = transaction != null ? transaction.ID : Guid.Empty;

            msgInfo.LockID = lockID;
            queue.Enqueue(msgInfo);
            messages.Add(msgInfo.ID, msgInfo);
            count++;
        }

        /// <summary>
        /// Dequeues the next unlocked message information with the highest priority from the queue.
        /// </summary>
        /// <param name="transaction">The current <see cref="BaseTransaction" /> (or <c>null</c>).</param>
        /// <returns>The message information or <c>null</c> if no suitable messages can be returned.</returns>
        public QueuedMsgInfo Dequeue(BaseTransaction transaction)
        {
            Guid lockID = transaction != null ? transaction.ID : Guid.Empty;

            for (int i = queuesByPriority.Length - 1; i >= 0; i--)
            {
                var             queue = queuesByPriority[i];
                QueuedMsgInfo   msgInfo;

                for (int j = 0; j < queue.Count; j++)
                {
                    msgInfo = queue[j];
                    if (msgInfo.LockID == Guid.Empty || msgInfo.LockID == lockID)
                    {
                        msgInfo.LockID = lockID;
                        queue.RemoveAt(j);
                        count--;

                        messages.Remove(msgInfo.ID);
                        return msgInfo;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next unlocked message information with the highest priority from the queue,
        /// leaving the information in the queue.
        /// </summary>
        /// <param name="transaction">The current <see cref="BaseTransaction" /> (or <c>null</c>).</param>
        /// <returns>The message information or <c>null</c> if no suitable messages can be returned.</returns>
        public QueuedMsgInfo Peek(BaseTransaction transaction)
        {
            Guid lockID = transaction != null ? transaction.ID : Guid.Empty;

            for (int i = queuesByPriority.Length - 1; i >= 0; i--)
            {
                var             queue = queuesByPriority[i];
                QueuedMsgInfo   msgInfo;

                for (int j = 0; j < queue.Count; j++)
                {
                    msgInfo = queue[j];
                    if (msgInfo.LockID == Guid.Empty || lockID == msgInfo.LockID)
                    {
                        msgInfo.LockID = lockID;
                        return msgInfo;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the <see cref="QueuedMsgInfo" /> for the message whose <see cref="Guid" />
        /// is passed if ther message is present in the table, <c>null</c> otherwise.
        /// </summary>
        /// <param name="messageID">The message <see cref="Guid" />.</param>
        /// <returns>The <see cref="QueuedMsgInfo" /> or <c>null</c>.</returns>
        public QueuedMsgInfo this[Guid messageID]
        {
            get
            {
                QueuedMsgInfo msgInfo;

                if (messages.TryGetValue(messageID, out msgInfo))
                    return msgInfo;
                else
                    return null;
            }
        }

        /// <summary>
        /// Clears all locks held by a transaction.
        /// </summary>
        /// <param name="transaction">The completed <see cref="BaseTransaction" />.</param>
        public void Unlock(BaseTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            Guid lockID = transaction.ID;

            // $todo(jeff.lill): 
            //
            // This will be slow if there are a lot of queued messages
            // since I'm going to walk the entire queue.  A faster but more
            // complex approach would be to maintain a separate table of
            // locked messages, indexed by transaction ID.
            //
            // I don't have the time to mess with this right now but I
            // will want to come back to this at some point.

            for (int i = 0; i < queuesByPriority.Length; i++)
            {
                var queue = queuesByPriority[i];

                foreach (QueuedMsgInfo msgInfo in queue)
                    if (lockID == msgInfo.LockID)
                        msgInfo.LockID = Guid.Empty;
            }
        }

        /// <summary>
        /// Scans the queue for any unlocked messages that have expired, removes
        /// them and adding information about the messages removed to a list.
        /// </summary>
        public void Flush(List<FlushInfo> flushList)
        {
            var now = DateTime.UtcNow;

            for (int i = 0; i < queuesByPriority.Length; i++)
            {
                var     queue = queuesByPriority[i];
                bool    removed = false;

                for (int j = queue.Count - 1; j >= 0; j--)
                {
                    if (now >= queue[j].ExpireTime && queue[j].LockID == Guid.Empty)
                    {
                        flushList.Add(new FlushInfo(queueEP, queue[j]));
                        queue.RemoveAt(j);
                        removed = true;
                    }
                }

                if (removed)
                    queue.TrimExcess();
            }
        }

        /// <summary>
        /// Removes a specific message from the queue.
        /// </summary>
        /// <param name="msgInfo">Information about the message being removed.</param>
        /// <returns><c>true</c> if the message was found and returned.</returns>
        /// <remarks>
        /// This method is used during transactional processing when undoing or redoing
        /// transactions.
        /// </remarks>
        public bool Remove(QueuedMsgInfo msgInfo)
        {
            // This method will probably be called most often to removed a message
            // enqueued during a transaction that is being rolled back.  In this
            // case, the transaction will be at the end of the queue so to optimize
            // performance I'm going to search from the end of the queues to the
            // beginning.

            var queue = queuesByPriority[(int)msgInfo.Priority];

            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].ID == msgInfo.ID)
                {
                    queue.RemoveAt(i);
                    return true;
                }

            return false;
        }

        /// <summary>
        /// Returns the queue's endpoint.
        /// </summary>
        public string Endpoint
        {
            get { return queueEP; }
        }

        /// <summary>
        /// Returns the number of message information records in the queue.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property can return non-zero even if <see cref="Dequeue" /> and <see cref="Peek" />
        /// return <c>null</c>.  This can happen when other transactions hold locks on messages.
        /// </note>
        /// </remarks>
        public int Count
        {
            get { return count; }
        }
    }
}
