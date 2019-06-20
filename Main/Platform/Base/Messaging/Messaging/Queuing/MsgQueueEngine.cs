//-----------------------------------------------------------------------------
// FILE:        MsgQueueEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core message queue server functionality.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Transactions;

// $todo(jeff.lill): Implement cross queue service routing.

// $todo(jeff.lill): 
//
// Implement some sort of mechanism where a clustered
// queue notices that a message is received by another
// queue instance that a client is waiting on this
// instance for.  Perhaps the queue can dequeue this
// message from the other queue on behalf of the client.

// $todo(jeff.lill): Implement poison message handling

// $todo(jeff.lill): 
//
// Implement delivery receipts.  I had this half implemented
// at one point and decided to rip this out due to complexities
// with transactions.  The problem is that I need to track 
// which messages were actually dequeued during a committed
// transaction and only do delivery receipt processing for
// these messages.  At this point in the code, there's no
// good place to do this.

// $todo(jeff.lill): 
//
// The dead letter queue endpoint should probably be
// configurable.

// $todo(jeff.lill): 
//
// Add methods to explicitly Clear, List, Get, and Remove
// messages from a queue.

// $todo(jeff.lill): I need to do a better job at transaction recovery testing.

// $todo(jeff.lill): 
//
// The engine needs to reject new base transactions and
// operations during shutdown.

// $todo(jeff.lill): 
//
// Sort the messages in ascending order by submission date
// after reading them from the store index and then submit
// the messages to the internal queue in this order.

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// The delegate used by <see cref="MsgQueueEngine" /> for signalling events
    /// to applications.
    /// </summary>
    /// <param name="engine"></param>
    public delegate void MsgQueueEngineDelegate(MsgQueueEngine engine);

    /// <summary>
    /// Implements the core message queue server functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current implementation is fairly simplistic.  When the engine is started,
    /// it will load all of the message metadata from the associated <see cref="IMsgQueueStore" />
    /// and create an internal message queue for each unique queue endpoint it finds.
    /// </para>
    /// <para>
    /// Call <see cref="Start" /> after constructing an instance passing the <see cref="MsgRouter" />
    /// to be used by the engine to comunicate with external applications and the
    /// <see cref="IMsgQueueStore" /> implementation to be used to persist the
    /// messages.  The <c>LillTek.Messaging.Queuing</c> namespace includes
    /// the <see cref="MsgQueueFileStore" /> and <see cref="MsgQueueMemoryStore" />
    /// classes that can be used or application can use a custom implementation.
    /// </para>
    /// <para>
    /// When client <see cref="MsgQueue" /> instances request a message, this class
    /// removes the message from the appropriate queue and returns it.  If there
    /// are no messages, then the class keeps track of the outstanding client
    /// request and completes it when a message for that queue is received.
    /// If the message requests transacted delivery and the receiving client
    /// does not confirm reception then the message's delivery attempt count
    /// will be incremented and the message will be added to the back of its
    /// queue.  If delivery fails more than a specified number of times then
    /// the message will be considered to be poisoned and will be added to
    /// the dead letter queue.
    /// </para>
    /// <para>
    /// When a message is received from a <see cref="MsgQueue" /> the class persists
    /// it and adds it to the appropriate queue, creating the queue if necessary.
    /// If a client is already waiting then the message will be dispatched
    /// immediately.
    /// </para>
    /// <para>
    /// The engine maintains one internal message queues, the <b>Dead Letter</b>
    /// queue where messages that could not be delivered are stored.
    /// </para>
    /// <para>
    /// <see cref="EnqueueEvent" /> will be raised when a message is queued to
    /// the server <see cref="DequeueEvent" /> will be raised when a message
    /// is dequeued, and <see cref="PeekEvent" /> will be raised when a message
    /// is peeked from a queue.  <see cref="ExpireEvent" /> is raised when a message expires
    /// and is discarded.  <see cref="CommitEvent" /> and <see cref="RollbackEvent" />
    /// are raised during the course of executing transactions.  <see cref="ConnectEvent" />
    /// is raised whenever a connection is established with the engine.  Applications will
    /// typically enlist in these events to implement performance counters.
    /// </para>
    /// <note>
    /// The current implementation of the code calling these events is pretty
    /// simplistic and performs the call within a lock.  This means that the 
    /// event handlers should not attempt to perform reentrant queuing operations
    /// to avoid the possibility of causing a deadlock.
    /// </note>
    /// <para><b><u>Implementation Details</u></b></para>
    /// <para>
    /// <see cref="MsgQueueEngine" /> server instances use <see cref="DuplexSession" />s
    /// to communicate with client <see cref="MsgQueue" /> instances.  A client 
    /// enqueues and dequeues messages using queries on this session.  Clients can
    /// also initiate a transaction against the server and then enqueue and dequeue
    /// messages in the context of this transaction.
    /// </para>
    /// <para>
    /// The engine maintains an in-memory table of the messages present in the
    /// system.  This table is loaded from the <see cref="IMsgQueueStore" />
    /// implementation when the engine is started.  Entries are added to this
    /// table when messages are enqueued and are deleted when messages are
    /// dequeued.  The engine uses this table when deciding which message to
    /// return for a dequeue request.
    /// </para>
    /// <para>
    /// The engine implements full transactional support for <see cref="MsgQueue" />
    /// clients.  The basic transacted operations are: <b>enqueue</b>, <b>dequeue</b>,
    /// and <b>peek</b>.  Transacting enqueue and dequeue operations should be obvious:
    /// commit performs the operation and rollback undoes it.  <b>peek</b> is a
    /// little more interesting.  The issue with peek is maintaining message
    /// queue consistency during the course of a transaction.  In the case of
    /// peek this means that any messages returned by a peek operation within a
    /// transaction cannot be returned by another peek or dequeue operation on
    /// a different transaction.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class MsgQueueEngine : ITransactedResource, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds application specific information about a connected
        /// duplex session which is set as the session's 
        /// <see cref="DuplexSession.UserData" /> property.
        /// </summary>
        private sealed class SessionInfo
        {
            /// <summary>
            /// The session ID used as the key in the sessions table.
            /// </summary>
            public long ID;

            /// <summary>
            /// The base transaction associated with the session (or <c>null</c>).
            /// </summary>
            public BaseTransaction BaseTransaction;

            public SessionInfo(long id)
            {
                this.ID              = id;
                this.BaseTransaction = null;
            }
        }

        /// <summary>
        /// Transacted queue operation commands.
        /// </summary>
        private enum OpCmd
        {
            /// <summary>
            /// Enqueues a message.
            /// </summary>
            Enqueue = 0,

            /// <summary>
            /// Dequeues a message.
            /// </summary>
            Dequeue = 1,
        }

        /// <summary>
        /// Used to encode a transacted queue operation.
        /// </summary>
        private sealed class QueueOperation : IOperation
        {
            private string description;        // The operation description

            /// <summary>
            /// The operation command.
            /// </summary>
            public OpCmd Command;

            /// <summary>
            /// The metadata for the message being enqueued or dequeued.
            /// </summary>
            public QueuedMsgInfo MessageInfo = null;

            /// <summary>
            /// The body of the message being enqueued or dequeued.
            /// </summary>
            public byte[] MessageBody = null;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="command">The operation command.</param>
            /// <param name="description">The operation description.</param>
            public QueueOperation(OpCmd command, string description)
            {
                this.Command     = command;
                this.description = description;
            }

            /// <summary>
            /// Constructs the operation by deserializing it from a stream.
            /// </summary>
            /// <param name="input">The input stream.</param>
            public QueueOperation(EnhancedStream input)
            {
                this.Command = (OpCmd)input.ReadByte();

                if (input.ReadByte() != 0)
                {

                    MessageInfo = new QueuedMsgInfo(input.ReadString32());
                    MessageBody = input.ReadBytes32();
                }
            }

            /// <summary>
            /// The operation description.
            /// </summary>
            public string Description
            {
                get { return description; }
                set { description = value; }
            }

            /// <summary>
            /// Serializes the operation to a stream.
            /// </summary>
            /// <param name="output">The output stream.</param>
            public void Write(EnhancedStream output)
            {
                output.WriteByte((byte)Command);

                if (MessageInfo == null || MessageBody == null)
                    output.WriteByte(0);
                else
                {
                    output.WriteByte(1);
                    output.WriteString32(MessageInfo.ToString());
                    output.WriteBytes32(MessageBody);
                }
            }
        }

        /// <summary>
        /// Used to track of a pending client dequeue or peek request.
        /// </summary>
        private sealed class PendingInfo
        {
            public readonly DuplexSession Session;
            public readonly BaseTransaction Transaction;
            public readonly MsgQueueCmd Query;
            public readonly DateTime TTD;        // (SYS)

            public PendingInfo(DuplexSession session, BaseTransaction transaction, MsgQueueCmd query, TimeSpan ttl)
            {
                this.Session     = session;
                this.Transaction = transaction;
                this.Query       = query;
                this.TTD         = ttl == TimeSpan.Zero ? DateTime.MaxValue : Helper.Add(SysTime.Now, ttl);
            }
        }

        /// <summary>
        /// Implements a table mapping pending dequeue and peek operations to
        /// queue endpoints.  This is used for quickly satisfying pending dequeue
        /// requests when a message is added to the queue.  This class is not
        /// threadsafe.
        /// </summary>
        private sealed class PendingOperations : IEnumerable<string>
        {
            private Dictionary<string, List<PendingInfo>> map;

            /// <summary>
            /// Constructor.
            /// </summary>
            public PendingOperations()
            {
                this.map = new Dictionary<string, List<PendingInfo>>(StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Adds a pending operation to the table.
            /// </summary>
            /// <param name="queueEP">The queue endpoint.</param>
            /// <param name="info">The operation information.</param>
            public void Add(string queueEP, PendingInfo info)
            {
                List<PendingInfo> list;

                if (!map.TryGetValue(queueEP, out list))
                {
                    list = new List<PendingInfo>();
                    map.Add(queueEP, list);
                }

                list.Add(info);
            }

            /// <summary>
            /// Extracts and returns the next pending operation for a queue.
            /// </summary>
            /// <param name="queueEP">The queue endpoint.</param>
            /// <returns>The next pending operation on the queue or <c>null</c> if none.</returns>
            public PendingInfo Extract(string queueEP)
            {
                List<PendingInfo>   list;
                PendingInfo         result;

                if (!map.TryGetValue(queueEP, out list))
                    return null;

                if (list.Count > 0)
                {
                    result = list[0];
                    list.RemoveAt(0);
                }
                else
                    result = null;

                if (list.Count == 0)
                    map.Remove(queueEP);

                return result;
            }

            /// <summary>
            /// Returns the list of pending operations for a queue.
            /// </summary>
            /// <param name="queueEP">The queue endpoint.</param>
            /// <returns>The operation list.</returns>
            public List<PendingInfo> GetList(string queueEP)
            {
                List<PendingInfo> list;

                if (map.TryGetValue(queueEP, out list))
                    return list;

                return new List<PendingInfo>();
            }

            /// <summary>
            /// Sets a list of pending operations for a queue.
            /// </summary>
            /// <param name="queueEP">The queue endpoint.</param>
            /// <param name="list">The pending operations.</param>
            public void SetList(string queueEP, List<PendingInfo> list)
            {
                if (list.Count == 0)
                {
                    if (map.ContainsKey(queueEP))
                        map.Remove(queueEP);
                }
                else
                    map[queueEP] = list;
            }

            /// <summary>
            /// Returns the number queues with pending operations.
            /// </summary>
            public int Count
            {
                get { return map.Count; }
            }

            /// <summary>
            /// Returns an enumerator over the endpoints of queues with 
            /// pending dequeue/peek operations.
            /// </summary>
            IEnumerator<string> IEnumerable<string>.GetEnumerator()
            {
                return map.Keys.GetEnumerator();
            }

            /// <summary>
            /// Returns an enumerator over the endpoints of queues with 
            /// pending dequeue/peek operations.
            /// </summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return map.Keys.GetEnumerator();
            }
        }

        /// <summary>
        /// Describes a transaction message lock.
        /// </summary>
        private struct LockInfo
        {
            /// <summary>
            /// The target queue endpoint.
            /// </summary>
            public string QueueEP;

            /// <summary>
            /// The locked message ID.
            /// </summary>
            public Guid MessageID;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="queueEP">The target queue endpoint.</param>
            /// <param name="messageID">The locked message ID.</param>
            public LockInfo(string queueEP, Guid messageID)
            {
                this.QueueEP   = queueEP;
                this.MessageID = messageID;
            }
        }

        /// <summary>
        /// Used to hold application specific tranaction information.  This is
        /// stored in the <see cref="BaseTransaction.UserData" /> field.
        /// </summary>
        private sealed class TransInfo
        {
            /// <summary>
            /// Set to <c>true</c> if any messages have been enqueued during the
            /// course of the transaction.
            /// </summary>
            public bool Enqueued = false;

            /// <summary>
            /// The set of <see cref="Guid" />s of the messages locked during
            /// the course of the transaction.
            /// </summary>
            public List<LockInfo> Locks = new List<LockInfo>();
        }

        //---------------------------------------------------------------------
        // Implementation

        // Implementation Note:
        //
        // The class maintains the sessions table with all of the connected
        // duplex sessions.  This table is keyed with a unique long ID and 
        // the DuplexSession.UserData property is set to a SessionInfo instance.

        /// <summary>
        /// The <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.Messaging.Queuing";

        /// <summary>
        /// The default dead letter queue endpoint.
        /// </summary>
        public const string DeadLetterQueueEP = "logical://LillTek/MsgQueue/DeadLetter";

        private const string InternalQueueEP = "logical://LillTek/MsgQueue/Internal";

        private const string NotStartedMsg = "Message queue has not been started.";

        private MsgRouter                           router;             // The message router
        private IMsgQueueStore                      store;              // The backing store implementation
        private MsgQueueEngineSettings              settings;           // Configuration settings
        private bool                                isStarted;          // True if the engine is running
        private Dictionary<string, InternalQueue>   queues;             // Table of message queues keyed by queue endpoint
        private TransactionManager                  transactionManager; // The transaction manager
        private ITransactionLog                     transLog;           // The transaction log implementation
        private Dictionary<long, DuplexSession>     sessions;           // The connected sessions
        private PendingOperations                   pending;            // Table of waiting dequeue or peek requests lists 
                                                                        // keyed by queue endpoint
        private MsgEP[]                             localQueueEPs;      // List of wildcarded local queue base EPs
        private GatedTimer                          flushTimer;         // Queue flush timer
        private GatedTimer                          bkTimer;            // Background task timer
        private DateTime                            nextPendingCheck;   // Scheduled time to check for pending async
                                                                        // completions on the background thread (SYS)
        // Duplex session handlers

        private DuplexReceiveDelegate   onSessionReceive;
        private DuplexQueryDelegate     onSessionQuery;
        private DuplexCloseDelegate     onSessionClose;

        /// <summary>
        /// Raised when a connection is established with the engine.
        /// </summary>
        public event MsgQueueEngineDelegate ConnectEvent;

        /// <summary>
        /// Raised when a message is queued to the engine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// For messages submitted in the context of a transaction, this
        /// event will be raised when the message is submitted to the
        /// engine during the course of the transaction regardless of
        /// whether the transaction is ultimately committed or
        /// rolled back.
        /// </note>
        /// </remarks>
        public event MsgQueueEngineDelegate EnqueueEvent;

        /// <summary>
        /// Raised when a message is dequeued from the engine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// For messages dequeued in the context of a transaction, this
        /// event will be raised when the message is dequeued from the
        /// engine during the course of the transaction regardless of
        /// whether the transaction is ultimately committed or
        /// rolled back.
        /// </note>
        /// </remarks>
        public event MsgQueueEngineDelegate DequeueEvent;

        /// <summary>
        /// Raised when a message is peeked from the engine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// For messages dequeued in the context of a transaction, this
        /// event will be raised when the message is peeked from the
        /// engine during the course of the transaction regardless of
        /// whether the transaction is ultimately committed or
        /// rolled back.
        /// </note>
        /// </remarks>
        public event MsgQueueEngineDelegate PeekEvent;

        /// <summary>
        /// Raised when a message expires and is discarded.
        /// </summary>
        public event MsgQueueEngineDelegate ExpireEvent;

        /// <summary>
        /// Raised when a transaction is committed.
        /// </summary>
        public event MsgQueueEngineDelegate CommitEvent;

        /// <summary>
        /// Raised when a transaction is rolled back.
        /// </summary>
        public event MsgQueueEngineDelegate RollbackEvent;

        /// <summary>
        /// Raised periodically to allow for background processing.
        /// </summary>
        public event MsgQueueEngineDelegate BkTaskEvent;

        /// <summary>
        /// Constructs a <see cref="MsgQueueEngine" /> associating the
        /// <see cref="IMsgQueueStore" /> to be used to persist messages
        /// and metadata to a backing store.
        /// </summary>
        /// <param name="store">An unopened <see cref="IMsgQueueStore" /> instance.</param>
        /// <param name="transactionLog">
        /// The unopened <see cref="ITransactionLog" /> implementation to be used to persist 
        /// transactions made against the engine.
        /// </param>
        public MsgQueueEngine(IMsgQueueStore store, ITransactionLog transactionLog)
        {
            this.isStarted          = false;
            this.store              = store;
            this.transLog           = transactionLog;
            this.queues             = null;
            this.flushTimer         = null;
            this.bkTimer            = null;
            this.transactionManager = null;
            this.onSessionReceive   = new DuplexReceiveDelegate(OnSessionReceive);
            this.onSessionQuery     = new DuplexQueryDelegate(OnSessionQuery);
            this.onSessionClose     = new DuplexCloseDelegate(OnSessionClose);
        }

        /// <summary>
        /// Returns the named internal queue, creating it if necessary.
        /// </summary>
        /// <param name="queueEP">The queue endpoint.</param>
        /// <returns>The queue instance.</returns>
        private InternalQueue GetQueue(string queueEP)
        {
            InternalQueue queue;

            TimedLock.AssertLocked(this);
            if (queues.TryGetValue(queueEP, out queue))
                return queue;

            queue = new InternalQueue(queueEP);
            queues.Add(queueEP, queue);
            return queue;
        }

        /// <summary>
        /// Returns <c>true</c> if the specified queue is one of the queues configured
        /// for the local message queue instance.
        /// </summary>
        /// <param name="queueEP">The queue endpoint being tested.</param>
        /// <returns><c>true</c> if the queue is local.</returns>
        private bool IsLocalQueue(MsgEP queueEP)
        {
            for (int i = 0; i < localQueueEPs.Length; i++)
                if (localQueueEPs[i].LogicalMatch(queueEP))
                    return true;

            return false;
        }

        /// <summary>
        /// Writes a <see cref="NetTrace" /> entry.
        /// </summary>
        /// <param name="message">The trace message string.</param>
        [Conditional("TRACE")]
        private void Trace(string message)
        {
            NetTrace.Write(TraceSubsystem, 0, "QueueEngine: " + message, null, null);
        }

        /// <summary>
        /// Writes a <see cref="NetTrace" /> entry.
        /// </summary>
        /// <param name="message">The trace message string.</param>
        /// <param name="msgInfo">The related message information.</param>
        [Conditional("TRACE")]
        private void Trace(string message, QueuedMsgInfo msgInfo)
        {
            NetTrace.Write(TraceSubsystem, 0, "QueueEngine: " + message, string.Format("msgid={0} queue={1}", msgInfo.ID, msgInfo.TargetEP), null);
        }

        /// <summary>
        /// Writes a <see cref="NetTrace" /> entry.
        /// </summary>
        /// <param name="message">The trace message string.</param>
        /// <param name="summary">Additional summary information.</param>
        [Conditional("TRACE")]
        private void Trace(string message, string summary)
        {
            NetTrace.Write(TraceSubsystem, 0, "QueueEngine: " + message, summary, null);
        }

        /// <summary>
        /// Opens the associated <see cref="IMsgQueueStore" /> implementation and starts
        /// the engine.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be used by the engine.</param>
        /// <param name="settings">The <see cref="MsgQueueEngineSettings" />.</param>
        public void Start(MsgRouter router, MsgQueueEngineSettings settings)
        {
            using (TimedLock.Lock(this))
            {
                if (isStarted)
                    throw new InvalidOperationException("Engine has already been started.");

                Trace("Start");

                this.isStarted = true;
                this.router    = router;
                this.settings  = settings;
                this.queues    = new Dictionary<string, InternalQueue>(StringComparer.OrdinalIgnoreCase);
                this.sessions  = new Dictionary<long, DuplexSession>();
                this.pending   = new PendingOperations();

                // Open the message store and load the message the metadata into the appropriate queues.

                store.Open();
                foreach (var msg in store)
                    GetQueue(msg.TargetEP).Enqueue(null, msg);

                // Crank up the transaction manager and the message store.

                transactionManager = new TransactionManager(true);
                transactionManager.Start(this, transLog, true);

                // Start the timers

                nextPendingCheck = SysTime.Now + settings.PendingCheckInterval;
                flushTimer = new GatedTimer(new TimerCallback(OnFlushTimer), null, settings.FlushInterval);
                bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, settings.BkTaskInterval);

                // Initialize the local queue endpoints and add dispatchers
                // to the router mapping these endpoints as well as the instance
                // endpoint to the OnMsg() method below.

                localQueueEPs = new MsgEP[settings.QueueMap.Length];
                for (int i = 0; i < settings.QueueMap.Length; i++)
                    localQueueEPs[i] = settings.QueueMap[i].HasWildCard
                                           ? settings.QueueMap[i]
                                           : MsgEP.Parse(settings.QueueMap[i].ToString() + "/*");

                SessionHandlerInfo      sessionInfo;
                MsgSessionAttribute     sessionAttr;

                sessionAttr                = new MsgSessionAttribute();
                sessionAttr.Type           = SessionTypeID.Duplex;
                sessionAttr.IsAsync        = true;
                sessionAttr.Idempotent     = true;
                sessionAttr.KeepAlive      = Serialize.ToString(settings.KeepAliveInterval);
                sessionAttr.SessionTimeout = Serialize.ToString(settings.SessionTimeout);
                sessionInfo                = new SessionHandlerInfo(sessionAttr);

                foreach (MsgEP ep in localQueueEPs)
                    router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnSessionConnect), ep, typeof(DuplexSessionMsg), false, sessionInfo, true);

                router.LogicalAdvertise();
            }
        }

        /// <summary>
        /// Stops the engine and closes the associated <see cref="IMsgQueueStore" />.
        /// </summary>
        public void Stop()
        {
            using (TimedLock.Lock(this))
            {
                Trace("Stop");
                isStarted = false;

                if (flushTimer != null)
                {
                    flushTimer.Dispose();
                    flushTimer = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (transactionManager != null)
                {
                    transactionManager.Stop(TimeSpan.FromSeconds(30));
                    transactionManager = null;
                }

                if (store != null)
                {
                    store.Close();
                    store = null;
                }

                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }

                queues = null;
                localQueueEPs = null;
            }
        }

        /// <summary>
        /// Flushes expired messages from the queues.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnFlushTimer(object state)
        {
            // I'm going to do this by quickly scanning through each queue, removing 
            // the messages that have expired and adding them to a separate list.
            //
            // Then I'll go through the deleted message list and add them to
            // the dead letter queue.  If any queue is completly emptied during 
            // this process, it will be removed from the queues table.

            using (TimedLock.Lock(this))
            {
                var             flushed = new List<FlushInfo>();
                var             zeroedQueues = new List<string>();
                QueuedMsgInfo   msgInfo;

                Trace("Flush Begin");

                // Remove any expired messages from the queues.  Note that
                // InternalQueue.Flush() does not remove messages that are
                // currently locked by a transaction.

                foreach (string queueEP in queues.Keys)
                {
                    var queue = queues[queueEP];

                    queue.Flush(flushed);
                    if (queue.Count == 0)
                        zeroedQueues.Add(queueEP);
                }

                // Remove any empty queues

                for (int i = 0; i < zeroedQueues.Count; i++)
                    queues.Remove(zeroedQueues[i]);

                // Run through the messages flushed from the queues.  Delete
                // those messages that came from the dead letter queue.

                foreach (FlushInfo flushInfo in flushed)
                {
                    string          queueEP = flushInfo.QueueEP;
                    InternalQueue   queue;

                    msgInfo = flushInfo.MsgInfo;

                    if (ExpireEvent != null)
                        ExpireEvent(this);

                    if (String.Compare(msgInfo.TargetEP, DeadLetterQueueEP, true) == 0)
                    {
                        // Remove expired dead letters.

                        store.Remove(msgInfo.PersistID);
                        Trace("Flush removing expired dead letter.", msgInfo);
                    }
                    else
                    {
                        Trace("Flush moving message to dead letter queue.", msgInfo);
                        SysLog.LogWarning("Message [mdgid={0} target={1}] added to dead letter queue.", msgInfo.ID, msgInfo.TargetEP);

                        // Requeue the flushed message to the dead letter queue.

                        DateTime now = DateTime.UtcNow;

                        store.Modify(msgInfo.PersistID, DeadLetterQueueEP, now, Helper.Add(now, settings.DeadLetterTTL), DeliveryStatus.Expired);
                        msgInfo.TargetEP = DeadLetterQueueEP;

                        if (!queues.TryGetValue(msgInfo.TargetEP, out queue))
                        {
                            queue = new InternalQueue(DeadLetterQueueEP);
                            queues.Add(msgInfo.TargetEP, queue);
                        }

                        queue.Enqueue(null, msgInfo);

                        if (EnqueueEvent != null)
                            EnqueueEvent(this);
                    }
                }

                Trace("Flush End");
            }
        }

        /// <summary>
        /// Retries a pending operation.
        /// </summary>
        /// <param name="info">The pending operation info.</param>
        /// <returns><c>true</c> if the operation succeeded, <c>false</c> if it is still pending.</returns>
        private bool RetryPending(PendingInfo info)
        {
            MsgQueueCmd         query       = info.Query;
            BaseTransaction     transaction = info.Transaction;
            MsgQueueAck         ack;
            QueueOperation      operation;
            QueuedMsgInfo       msgInfo;
            QueuedMsg           queueMsg;
            InternalQueue       queue;

            TimedLock.AssertLocked(this);
            switch (query.Command)
            {
                case MsgQueueCmd.DequeueCmd:

                    // Try dequeuing a message (if the queue exists).

                    msgInfo = null;
                    if (queues.TryGetValue(query.QueueEP, out queue))
                        msgInfo = queue.Dequeue(transaction);

                    if (msgInfo == null)
                    {
                        // There's no message so we're going to continue waiting.

                        return false;
                    }

                    Trace("Retry Dequeue", msgInfo);

                    queueMsg          = store.Get(msgInfo.PersistID);
                    ack               = new MsgQueueAck();
                    ack.MessageHeader = msgInfo.ToString();
                    ack.MessageBody   = queueMsg.BodyRaw;

                    info.Session.ReplyTo(info.Query, ack);
                    store.Remove(msgInfo.PersistID);

                    if (DequeueEvent != null)
                        DequeueEvent(this);

                    // Log the transaction information

                    if (transaction != null)
                    {
                        var transInfo = (TransInfo)transaction.UserData;

                        operation             = new QueueOperation(OpCmd.Dequeue, "Dequeue: " + msgInfo.ID.ToString());
                        operation.MessageInfo = msgInfo;
                        operation.MessageBody = queueMsg.BodyRaw;

                        transaction.Log(operation);
                        transInfo.Locks.Add(new LockInfo(msgInfo.TargetEP, msgInfo.ID));
                    }

                    return true;

                case MsgQueueCmd.PeekCmd:

                    // Try peeking a message (if the queue exists).

                    msgInfo = null;
                    if (queues.TryGetValue(query.QueueEP, out queue))
                        msgInfo = queue.Peek(transaction);

                    if (msgInfo == null)
                    {
                        // There is no message in the queue so we're going to 
                        // continue waiting.

                        return false;
                    }

                    Trace("Retry Peek", msgInfo);

                    queueMsg          = store.Get(msgInfo.PersistID);
                    ack               = new MsgQueueAck();
                    ack.MessageHeader = msgInfo.ToString();
                    ack.MessageBody   = queueMsg.BodyRaw;

                    info.Session.ReplyTo(info.Query, ack);

                    if (PeekEvent != null)
                        PeekEvent(this);

                    if (transaction != null)
                    {
                        var transInfo = (TransInfo)transaction.UserData;

                        transInfo.Locks.Add(new LockInfo(msgInfo.TargetEP, msgInfo.ID));
                    }
                    return true;

                default:

                    throw new Exception(string.Format("Unexpected command [{0}].", query.Command));
            }
        }

        /// <summary>
        /// Called whenever a message is queued to the server to see if there are any
        /// pending async dequeue or peek requests that can be satisfied with the
        /// new message.
        /// </summary>
        /// <param name="queueEP">The queue endpoint, null otherwise.</param>
        private void ProcessPending(string queueEP)
        {
            List<PendingInfo>   list;
            List<int>           delPending = new List<int>();

            using (TimedLock.Lock(this))
            {
                if (queueEP != null)
                {
                    // The target endpoint of the message enqueued is known so
                    // we can limit the number of pending operations we need to
                    // retry.

                    delPending.Clear();

                    list = pending.GetList(queueEP);
                    for (int i = 0; i < list.Count; i++)
                        if (RetryPending(list[i]))
                            delPending.Add(i);

                    for (int i = delPending.Count - 1; i >= 0; i--)
                        list.RemoveAt(delPending[i]);

                    pending.SetList(queueEP, list);
                }
                else
                {
                    // Retry all pending operations.

                    var targets = new List<string>(pending.Count);

                    foreach (string ep in pending)
                        targets.Add(ep);

                    foreach (string ep in targets)
                        ProcessPending(ep);
                }
            }
        }

        /// <summary>
        /// Used in OnBkTimer() for tracking necessary updates to the pending
        /// operations table.
        /// </summary>
        private struct PendingUpdate
        {
            public string               QueueEP;
            public List<PendingInfo>    List;

            public PendingUpdate(string queueEP, List<PendingInfo> list)
            {
                this.QueueEP = queueEP;
                this.List    = list;
            }
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTimer(object state)
        {
            // Check to see if any messages match any pending dequeue or peek queries.

            if (SysTime.Now <= nextPendingCheck)
            {
                ProcessPending(null);
                nextPendingCheck = SysTime.Now + settings.PendingCheckInterval;
            }

            // Terminate operations that have exceeded their TTL with TimeoutExceptions.

            using (TimedLock.Lock(this))
            {
                var                 now        = SysTime.Now;
                var                 delPending = new List<int>();
                var                 updates    = new List<PendingUpdate>();
                List<PendingInfo>   list;

                foreach (string queueEP in pending)
                {
                    delPending.Clear();

                    list = pending.GetList(queueEP);
                    for (int i = 0; i < list.Count; i++)
                        if (now >= list[i].TTD)
                        {
                            list[i].Session.ReplyTo(list[i].Query, new MsgQueueAck(new TimeoutException()));
                            delPending.Add(i);
                        }

                    if (delPending.Count > 0)
                    {
                        for (int i = delPending.Count - 1; i >= 0; i--)
                            list.RemoveAt(delPending[i]);

                        updates.Add(new PendingUpdate(queueEP, list));
                    }
                }

                foreach (PendingUpdate update in updates)
                    pending.SetList(update.QueueEP, update.List);
            }

            // Give the application a chance to do some background processing.

            if (BkTaskEvent != null)
                BkTaskEvent(this);
        }

        /// <summary>
        /// Returns the current number of client sessions.
        /// </summary>
        /// <see cref="InvalidOperationException">Thrown if the message queue is not running.</see>
        public int SessionCount
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (!isStarted)
                        throw new InvalidOperationException(NotStartedMsg);

                    return sessions.Count;
                }
            }
        }

        /// <summary>
        /// Called when a client <see cref="MsgQueue" /> instance establishes 
        /// a <see cref="DuplexSession" /> connection with this server.
        /// </summary>
        /// <param name="msg">The <see cref="DuplexSessionMsg" />.</param>
        private void OnSessionConnect(Msg msg)
        {
            var duplexMsg = (DuplexSessionMsg)msg;
            var session   = (DuplexSession)duplexMsg._Session;

            Trace("Session Connect", session.ID.ToString());

            if (ConnectEvent != null)
                ConnectEvent(this);

            session.ReceiveEvent += new DuplexReceiveDelegate(onSessionReceive);
            session.QueryEvent += new DuplexQueryDelegate(onSessionQuery);
            session.CloseEvent += new DuplexCloseDelegate(onSessionClose);

            using (TimedLock.Lock(this))
            {
                session.UserData = new SessionInfo(session.ID);
                sessions.Add(session.ID, session);
            }
        }

        /// <summary>
        /// Called when the client sends a message to the server.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
        /// <param name="msg">The message received.</param>
        private void OnSessionReceive(DuplexSession session, Msg msg)
        {
            // This is a NOP for now since all activities are performed
            // via queries at this time.
        }

        /// <summary>
        /// Clears any locks held by a transaction.
        /// </summary>
        /// <param name="transaction">The <see cref="BaseTransaction" />.</param>
        private void ClearLocks(BaseTransaction transaction)
        {
            TransInfo       transInfo;
            QueuedMsgInfo   msgInfo;
            InternalQueue   queue;

            TimedLock.AssertLocked(this);

            if (transaction == null)
                return;

            transInfo = (TransInfo)transaction.UserData;
            foreach (LockInfo lockInfo in transInfo.Locks)
                if (queues.TryGetValue(lockInfo.QueueEP, out queue))
                {
                    msgInfo = queue[lockInfo.MessageID];
                    if (msgInfo != null)
                        msgInfo.LockID = Guid.Empty;
                }
        }

        /// <summary>
        /// Called when the client queries the server.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
        /// <param name="msg">The query message.</param>
        /// <param name="async">Returns as <c>true</c> if the query is being processed asynchronously.</param>
        /// <returns>The response message.</returns>
        private Msg OnSessionQuery(DuplexSession session, Msg msg, out bool async)
        {
            async = false;

            try
            {
                SessionInfo     sessionInfo = (SessionInfo)session.UserData;
                BaseTransaction transaction = sessionInfo.BaseTransaction;
                MsgQueueCmd     query       = (MsgQueueCmd)msg;
                MsgQueueAck     ack         = new MsgQueueAck();
                QueueOperation  operation;
                QueuedMsgInfo   msgInfo;
                QueuedMsg       queueMsg;
                InternalQueue   queue;

                using (TimedLock.Lock(this))
                {
                    switch (query.Command)
                    {
                        case MsgQueueCmd.BeginTransCmd:

                            if (transaction == null)
                            {
                                sessionInfo.BaseTransaction = transaction = transactionManager.BeginTransaction().Base;
                                Trace("Begin Base Transaction");
                                transaction.UserData = new TransInfo();
                            }
                            else
                            {
                                transaction.Peek().BeginTransaction();
                                Trace("Begin Nested Transaction", string.Format("depth={0}", transaction.Count));
                            }
                            break;

                        case MsgQueueCmd.CommitTransCmd:

                            if (transaction == null)
                            {
                                Trace("Transaction commit with no transaction");
                                throw new Exception("Attempting commit without a transaction.");
                            }

                            transaction.Peek().Commit();

                            if (transaction.Count == 0)
                                Trace("Commit Base Transaction");
                            else
                                Trace("Commit Nested Transaction", string.Format("DEPTH={0}", transaction.Count));

                            if (transaction.Count == 0)
                            {
                                var transInfo = (TransInfo)transaction.UserData;

                                sessionInfo.BaseTransaction = null;

                                if (CommitEvent != null)
                                    CommitEvent(this);

                                if (transInfo.Enqueued)
                                    ProcessPending(null);

                                ClearLocks(transaction);
                            }
                            break;

                        case MsgQueueCmd.RollbackTransCmd:

                            if (transaction == null)
                            {
                                Trace("Transaction rollback with no transaction");
                                throw new Exception("Attempting rollback without a transaction.");
                            }

                            transaction.Peek().Rollback();

                            if (transaction.Count == 0)
                                Trace("Rollback Base Transaction");
                            else
                                Trace("Rollback Nested Transaction", string.Format("depth={0}", transaction.Count));

                            if (transaction.Count == 0)
                            {
                                if (RollbackEvent != null)
                                    RollbackEvent(this);

                                sessionInfo.BaseTransaction = null;
                                ClearLocks(transaction);
                            }
                            break;

                        case MsgQueueCmd.RollbackAllTransCmd:

                            if (transaction == null)
                                break;

                            Trace("Rollback All");
                            transaction.RollbackAll();

                            if (RollbackEvent != null)
                                RollbackEvent(this);

                            sessionInfo.BaseTransaction = null;
                            ClearLocks(transaction);
                            break;

                        case MsgQueueCmd.EnqueueCmd:

                            queueMsg = new QueuedMsg(query, false);
                            msgInfo  = new QueuedMsgInfo(null, queueMsg);

                            Trace("Enqueue", msgInfo);

                            // Enqueue the message, creating the queue if necessary.

                            store.Add(msgInfo, queueMsg);

                            using (TimedLock.Lock(this))
                            {
                                if (!queues.TryGetValue(msgInfo.TargetEP, out queue))
                                {
                                    queue = new InternalQueue(msgInfo.TargetEP);
                                    queues.Add(msgInfo.TargetEP, queue);
                                }

                                queue.Enqueue(transaction, msgInfo);
                            }

                            if (EnqueueEvent != null)
                                EnqueueEvent(this);

                            // Log the transaction information

                            if (transaction != null)
                            {
                                var transInfo = (TransInfo)transaction.UserData;

                                operation             = new QueueOperation(OpCmd.Enqueue, "Enqueue: " + msgInfo.ID.ToString());
                                operation.MessageInfo = msgInfo;
                                operation.MessageBody = queueMsg.BodyRaw;

                                transInfo.Enqueued = true;
                                transInfo.Locks.Add(new LockInfo(msgInfo.TargetEP, msgInfo.ID));
                                transaction.Log(operation);
                            }
                            else
                                ProcessPending(msgInfo.TargetEP);

                            break;

                        case MsgQueueCmd.DequeueCmd:

                            using (TimedLock.Lock(this))
                            {
                                // Try dequeuing a message (if the queue exists).

                                if (queues.TryGetValue(query.QueueEP, out queue))
                                    msgInfo = queue.Dequeue(transaction);
                                else
                                    msgInfo = null;

                                if (msgInfo == null)
                                {
                                    if (query.Timeout == TimeSpan.Zero)
                                    {
                                        Trace("Dequeue (none)");
                                        ack = new MsgQueueAck(new TimeoutException());
                                        break;
                                    }

                                    Trace("Dequeue (waiting)");

                                    // There is no message in the queue so we're going to 
                                    // complete the query asynchronously.

                                    async = true;
                                    pending.Add(query.QueueEP, new PendingInfo(session, transaction, query, query.Timeout));
                                    return null;
                                }

                                Trace("Dequeue", msgInfo);

                                queueMsg          = store.Get(msgInfo.PersistID);
                                ack.MessageHeader = msgInfo.ToString();
                                ack.MessageBody   = queueMsg.BodyRaw;

                                store.Remove(msgInfo.PersistID);

                                if (DequeueEvent != null)
                                    DequeueEvent(this);

                                // Log the transaction information

                                if (transaction != null)
                                {
                                    var transInfo = (TransInfo)transaction.UserData;

                                    operation             = new QueueOperation(OpCmd.Dequeue, "Dequeue: " + msgInfo.ID.ToString());
                                    operation.MessageInfo = msgInfo;
                                    operation.MessageBody = queueMsg.BodyRaw;

                                    transInfo.Locks.Add(new LockInfo(msgInfo.TargetEP, msgInfo.ID));
                                    transaction.Log(operation);
                                }
                            }
                            break;

                        case MsgQueueCmd.PeekCmd:

                            using (TimedLock.Lock(this))
                            {
                                // Try peeking a message (if the queue exists).

                                if (queues.TryGetValue(query.QueueEP, out queue))
                                    msgInfo = queue.Peek(transaction);
                                else
                                    msgInfo = null;

                                if (msgInfo == null)
                                {
                                    if (query.Timeout == TimeSpan.Zero)
                                    {
                                        Trace("Peek (none)");
                                        ack = new MsgQueueAck();
                                        break;
                                    }

                                    Trace("Peek (waiting)");

                                    // There is no message in the queue so we're going to 
                                    // complete the query asynchronously.

                                    async = true;
                                    pending.Add(query.QueueEP, new PendingInfo(session, transaction, query, query.Timeout));
                                    return null;
                                }

                                Trace("Peek", msgInfo);

                                queueMsg = store.Get(msgInfo.PersistID);
                                ack.MessageHeader = msgInfo.ToString();
                                ack.MessageBody = queueMsg.BodyRaw;

                                if (PeekEvent != null)
                                    PeekEvent(this);

                                if (transaction != null)
                                {
                                    TransInfo transInfo = (TransInfo)transaction.UserData;

                                    transInfo.Locks.Add(new LockInfo(msgInfo.TargetEP, msgInfo.ID));
                                }
                            }
                            break;

                        default:

                            throw new Exception(string.Format("Unexpected command [{0}].", query.Command));
                    }

                    return ack;
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                return new MsgQueueAck(e);
            }
        }

        /// <summary>
        /// Called when the session is closed explicitly by the client
        /// or is closed implicitly due to a lack of normal or keep-alive
        /// message traffic from the client.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
        /// <param name="timeout"><c>true</c> if the session closed due to a keep-alive timeout.</param>
        private void OnSessionClose(DuplexSession session, bool timeout)
        {
            var     sessionInfo = (SessionInfo)session.UserData;
            bool    done = false;

            Trace("Session Close", session.ID.ToString());

            using (TimedLock.Lock(this))
            {
                // Delete any pending async request information for this session.

                foreach (string queueEP in pending)
                {
                    var list = pending.GetList(queueEP);

                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i].Session.ID == session.ID)
                        {
                            list.RemoveAt(i);
                            done = true;
                            break;      // I can break here because there can only be one
                        }               // outstanding query per DuplexSession.

                    pending.SetList(queueEP, list);

                    if (done)
                        break;
                }

                // Rollback any nested transactions

                if (sessionInfo.BaseTransaction != null)
                    sessionInfo.BaseTransaction.RollbackAll();

                // Remove the session

                if (sessions.ContainsKey(session.ID))
                    sessions.Remove(session.ID);
            }
        }

        //---------------------------------------------------------------------
        // ITransactedResource implementation

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        public string Name
        {
            get { return typeof(MsgQueueEngine).Name; }
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        public void BeginRecovery(UpdateContext context)
        {
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        public void EndRecovery(UpdateContext context)
        {
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool BeginUndo(UpdateContext context)
        {
            return true;
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="operation"></param>
        public void Undo(UpdateContext context, IOperation operation)
        {
            QueueOperation      op       = (QueueOperation)operation;
            string              targetEP = op.MessageInfo.TargetEP;
            InternalQueue       queue;
            object              persistID;
            QueuedMsgInfo       msgInfo;

            using (TimedLock.Lock(this))
            {
                switch (op.Command)
                {
                    case OpCmd.Enqueue:

                        // Remove the message from the store and the queue.

                        Trace("Undo Enqueue", op.MessageInfo);
                        persistID = store.GetPersistID(op.MessageInfo.ID);
                        if (persistID != null)
                            store.Remove(persistID);

                        if (queues.TryGetValue(targetEP, out queue))
                        {
                            queue.Remove(op.MessageInfo);
                            if (queue.Count == 0)
                                queues.Remove(targetEP);
                        }

                        break;

                    case OpCmd.Dequeue:

                        // The message shouldn't be present in the store or
                        // any queues but I'm going to delete it just to be
                        // sure.

                        persistID = store.GetPersistID(op.MessageInfo.ID);
                        if (persistID != null)
                            store.Remove(persistID);

                        if (queues.TryGetValue(targetEP, out queue))
                            queue.Remove(op.MessageInfo);

                        // Add the message to the store and the queue.  Note that
                        // the message will be added to the end of the queue instead
                        // of trying to restore its position.  This is OK since the
                        // messaging queuing semantics do not say anything about message
                        // order other than priority value.

                        msgInfo = op.MessageInfo.Clone();
                        Trace("UndoDeqtete", msgInfo);
                        store.Add(msgInfo, new QueuedMsg(msgInfo, op.MessageBody, false));

                        if (!queues.TryGetValue(targetEP, out queue))
                        {
                            queue = new InternalQueue(targetEP);
                            queues.Add(targetEP, queue);
                        }

                        queue.Enqueue(null, msgInfo);
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        public void EndUndo(UpdateContext context)
        {
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool BeginRedo(UpdateContext context)
        {
            return false;
        }

        /// <summary>
        /// Internal member not intended for application use.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="operation"></param>
        public void Redo(UpdateContext context, IOperation operation)
        {
            QueueOperation      op       = (QueueOperation)operation;
            string              targetEP = op.MessageInfo.TargetEP;
            InternalQueue       queue;
            object              persistID;
            QueuedMsgInfo       msgInfo;

            if (!context.Recovery)
            {
                // We only need to worry about transaction log recoveries
                // from process or system crashes.

                return;
            }

            using (TimedLock.Lock(this))
            {
                switch (op.Command)
                {
                    case OpCmd.Enqueue:

                        // The message shouldn't be present in the store or
                        // any queues but I'm going to delete it just to be
                        // sure.

                        persistID = store.GetPersistID(op.MessageInfo.ID);
                        if (persistID != null)
                            store.Remove(persistID);

                        if (queues.TryGetValue(targetEP, out queue))
                            queue.Remove(op.MessageInfo);

                        // Add the message to the store and the queue.  Note that
                        // the message will be added to the end of the queue instead
                        // of trying to restore its position.  This is OK since the
                        // messaging queuing semantics do not say anything about message
                        // order other than priority value.

                        msgInfo = op.MessageInfo.Clone();
                        Trace("Redo Enqueue", msgInfo);
                        store.Add(msgInfo, new QueuedMsg(msgInfo, op.MessageBody, false));

                        if (!queues.TryGetValue(targetEP, out queue))
                        {
                            queue = new InternalQueue(targetEP);
                            queues.Add(targetEP, queue);
                        }

                        queue.Enqueue(null, msgInfo);
                        break;

                    case OpCmd.Dequeue:

                        // Remove the message from the store and the queue.

                        Trace("Redo Dequeue", op.MessageInfo);
                        persistID = store.GetPersistID(op.MessageInfo.ID);
                        if (persistID != null)
                            store.Remove(persistID);

                        if (queues.TryGetValue(targetEP, out queue))
                        {
                            queue.Remove(op.MessageInfo);
                            if (queue.Count == 0)
                                queues.Remove(targetEP);
                        }
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void EndRedo(UpdateContext context)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public IOperation ReadOperation(EnhancedStream input)
        {
            return new QueueOperation(input);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="output"></param>
        /// <param name="operation"></param>
        public void WriteOperation(EnhancedStream output, IOperation operation)
        {
            ((QueueOperation)operation).Write(output);
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
