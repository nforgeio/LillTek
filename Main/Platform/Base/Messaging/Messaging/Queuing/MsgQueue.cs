//-----------------------------------------------------------------------------
// FILE:        MsgQueue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements client side access to the message queuing system.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

// $todo(jeff.lill): Implement session pooling.

// $todo(jeff.lill): 
//
// Implement some sort of mechanism where a clustered
// queue notices that a message is received by another
// queue instance that a client is waiting on this
// instances for.  Perhaps the queue can dequeue this
// message from the other queue on behalf of the client.

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Implements client side access to the message queuing system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="MsgQueue" /> class implements client side access to a LillTek message
    /// queuing cluster composed of Message Queue Service instances.  Message queues are
    /// identified using logical messaging endpoints.  Applications use <see cref="MsgQueue" />
    /// to enqueue and dequeue messages from queue services.
    /// </para>
    /// <para>
    /// Applications that need to send or receive queued messages need to instantiate
    /// a <see cref="MsgQueue" /> instance, establishing connection to a message queue
    /// service instance.  The class provides three constructors for doing this.  They
    /// all accept the <see cref="MsgRouter" /> to be used for sending and receivig messages.
    /// An optional <see cref="MsgQueueSettings" /> instance used to customize the instance 
    /// settings, including the base endpoint to use when connecting to the message queue service
    /// and also for enqueuing and dequeuing messages.  <see cref="MsgQueue.Close()" /> or <see cref="MsgQueue.Dispose" />
    /// should be called promptly once a queue is no longer needed top ensure that any 
    /// resources held are released.
    /// </para>
    /// <para>
    /// The <b>queueEP</b> parameter can be either a fully qualified logical ar abstract <see cref="MsgEP" />
    /// string or a relative queue name.  Relative queue names will be appended to the 
    /// queue base endpoint <see cref="MsgQueueSettings.BaseEP" /> from the <see cref="MsgQueueSettings" />.
    /// The fully qualified queue endpoint is used to establish a connection with a message queue
    /// service instance as well as to specify that target message queue when one isn't explicitly
    /// specified to one of the enqueue, dequeue, or peek methods.
    /// </para>
    /// <note>
    /// Once a <see cref="MsgQueue" /> establishes a session with queue service instance, all 
    /// subsequent messaging operations will be directed to that instance regardless of whether
    /// it is configured to manage the target queue or not.
    /// </note>
    /// <para>
    /// Messages are sent synchronously to the queue by creating a <see cref="QueuedMsg" /> 
    /// and setting its <see cref="QueuedMsg.Body" /> property to a serializable object instance
    /// and then calling <see cref="MsgQueue.Enqueue" />.  Messages can be read synchronously from the queue
    /// by calling <see cref="MsgQueue.Dequeue()" /> or <see cref="MsgQueue.Dequeue(TimeSpan)" />.  
    /// Messages can be sent asynchronously using <see cref="MsgQueue.BeginEnqueue" /> and 
    /// <see cref="MsgQueue.EndEnqueue" /> and received asynchronously using <see cref="MsgQueue.BeginDequeue" /> 
    /// and <see cref="MsgQueue.EndDequeue" />.
    /// </para>
    /// <para>
    /// Here's a simple example that opens a queue and enqueues a message.
    /// </para>
    /// <code language="cs">
    /// void Test() 
    /// {
    ///     LeafRouter      router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (var queue = new MsgQueue(router,"logical://MyQueues/Test"))
    ///     {
    ///         queue.Enqueue(new QueuedMsg("Hello World!"));
    ///     }
    /// }
    /// </code>
    /// <para>
    /// It is possible to enqueue and dequeue messages from any arbitrary message
    /// queue using the <see cref="MsgQueue.EnqueueTo" />, <see cref="MsgQueue.EnqueueTo" />,
    /// <see cref="MsgQueue.DequeueFrom(string)" /> and <see cref="MsgQueue.DequeueFrom(string,TimeSpan)" />
    /// methods and their asynchronous equivalents: <see cref="MsgQueue.BeginEnqueueTo" />,
    /// <see cref="MsgQueue.EndEnqueueTo" />, <see cref="MsgQueue.BeginDequeueFrom" />, and
    /// <see cref="MsgQueue.EndDequeueFrom" />. 
    /// </para>
    /// <para>
    /// <see cref="MsgQueue" /> also provides <see cref="MsgQueue.Peek()" />,
    /// <see cref="MsgQueue.Peek(TimeSpan)" />, <see cref="MsgQueue.PeekFrom(string)" />
    /// <see cref="MsgQueue.PeekFrom(string,TimeSpan)" />" /> to check to see if 
    /// there's a message waiting in a queue without removing it.  Note that applications should not
    /// assume that just because <see cref="MsgQueue.Peek()" /> returned a message that
    /// the next call to <see cref="MsgQueue.Dequeue()" /> will succeed or return the
    /// same message (unless the operation is performed within a transaction).  This class 
    /// implements some asynchronous peek methods: <see cref="MsgQueue.BeginPeek" />, 
    /// <see cref="MsgQueue.EndPeek" />, <see cref="MsgQueue.BeginPeekFrom" />, and 
    /// <see cref="MsgQueue.EndPeekFrom" />.
    /// </para>
    /// <para><b><u>Transaction Support</u></b></para>
    /// <para>
    /// <see cref="MsgQueue" /> supports the .NET Framework <see cref="TransactionScope" />
    /// defined in the <b>System.Transactions</b> namespace so message queuing can 
    /// implictly particpate in distributed transactions. <see cref="MsgQueue" /> instances 
    /// check to whether <see cref="MsgQueue.Enqueue" />, <see cref="MsgQueue.Dequeue()" />, 
    /// or <see cref="MsgQueue.Peek()" />   are being called within an ambient transaction.  If 
    /// this is the case, the <see cref="MsgQueue" /> enlists the operation in the transaction and
    /// then handles the transaction callbacks to prepare, commit or rollback the transaction.
    /// </para>
    /// <para>
    /// Here's a transacted example showing the dequeuing of a message from one queue 
    /// and forwarding it to two other queues.  If any of these operations fail then 
    /// the dequeued message will be restored and the enqueued messages will be removed.
    /// </para>
    /// <code language="cs">
    /// using System;
    /// using System.Transactions;
    /// 
    /// using LillTek.Common;
    /// using LillTek.Messaging;
    /// 
    /// void ForwardMessage() 
    /// {
    ///     LeafRouter  router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (var scope = new TransactionScope())
    ///     {
    ///         using (MsgQueue queue = new MsgQueue(router,"logical://queues/*")) 
    ///         {
    ///             QueuedMsg   msg;
    /// 
    ///             msg = queue.DequeueFrom("Input);
    ///             queue.EnqueueTo("output1",msg);
    ///             queue.EnqueueTo("output2",msg);
    /// 
    ///             scope.Commit();
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// The example above dequeues a message from <b>logical://queues/Input</b> and copies it to 
    /// <b>logical://queues/Output1</b> and <b>logical://queues/Output2</b>
    /// </para>
    /// <para>
    /// <see cref="MsgQueue" /> also implement the simple built-in transaction
    /// methods: <see cref="MsgQueue.BeginTransaction" />, <see cref="MsgQueue.Commit" />,  
    /// <see cref="MsgQueue.Rollback" />, and <see cref="MsgQueue.RollbackAll" />.  These methods are used 
    /// internally to implement <see cref="TransactionScope" /> support but they can also be used 
    /// explicitly.  Note that these explicit methods do allow the asynchronous enqueue, dequeue, and
    /// peek methods to be included within transactions (as opposed to  <see cref="TransactionScope" />
    /// which does not support asynchronous methods).  Here's a transaction example using the
    /// built-in methods:
    /// </para>
    /// <code language="cs">
    /// void ForwardMessage()
    /// {
    ///     LeafRouter  router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (var queue = new MsgQueue(router,"logical://queues/*"))
    ///     {
    ///         QueuedMsg   msg;
    ///         bool        commited = false;
    /// 
    ///         queue.BeginTransaction();
    /// 
    ///         try 
    ///         {
    ///             msg = queue.DequeueFrom("Input);
    ///             queue.EnqueueTo("output1",msg);
    ///             queue.EnqueueTo("output2",msg);
    ///             queue.Commit();
    ///             comitted = true;
    ///         }
    ///         finally
    ///         {
    ///             if (!committed)
    ///                 queue.RollbackAll();
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para><b><u>Delivery Order Guarantees</u></b></para>
    /// <para>
    /// The LillTek Message Queuing platform currently makes no guarantees as to the order
    /// in which messages will be ultimately delivered.  In general, messages with the
    /// same <see cref="DeliveryPriority" /> will be delivered in the order they were
    /// submitted and messages with higher priorities will be delivered before messages
    /// with lower priorities but applications should not expect this behavior to be
    /// enforced for all messages.
    /// </para>
    /// <para><b><u>Asynchronous Method Limitations</u></b></para>
    /// <para>
    /// Only one operation may be outstanding at any given
    /// time on a <see cref="MsgQueue" /> instance.  This restriction
    /// is due to the fact that <see cref="MsgQueue" /> uses <see cref="DuplexSession" />
    /// to communicate with the message queue service instances and
    /// duplex sessions support only one query at a time.  You'll see an
    /// <see cref="InvalidOperationException" /> if you attempt to perform
    /// more than one operation in parallel on a queue.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// Since the <see cref="TransactionScope" /> implementation is inherently synchronous in
    /// design, the asynchronous queue methods (<see cref="MsgQueue.BeginEnqueue" /> etc) <b>do not</b>
    /// participate in transactions in these transactions.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class MsgQueue : IDisposable, ILockable, IEnlistmentNotification
    {
        private MsgRouter           router;         // Associated message router
        private MsgEP               queueEP;        // The queue's logical endpoint
        private AsyncCallback       onEnqueue;      // Delegate that handles enqueue completions
        private AsyncCallback       onDequeue;      // Delegate that handles dequeue completions
        private AsyncCallback       onPeek;         // Delegate that handles peek completions
        private MsgQueueSettings    settings;       // The queue settings
        private DuplexSession       session;        // The server sessions
        private Stack<string>       transStack;     // Stack of ambient transaction IDs

        /// <summary>
        /// The default message queue service base endpoint.
        /// </summary>
        public const string AbstractBaseEP = "abstract://LillTek/DataCenter/MsgQueue";

        /// <summary>
        /// Constructs a queue using default <see cref="MsgQueueSettings" />.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be used by the queue for messaging.</param>
        /// <remarks>
        /// This constructor establishes a connection to a queue server that matches the 
        /// the logical base endpoint specified in the <see cref="MsgQueueSettings" /> with
        /// a wildcard added.  For example, if <see cref="MsgQueueSettings.BaseEP" /> is
        /// <b>logical://MyQueues</b> then a connection will be established with a queue
        /// service that exposes endpoints that match <b>logical://MyQueues/*</b>.
        /// </remarks>
        public MsgQueue(MsgRouter router)
            : this(router, null, new MsgQueueSettings())
        {
        }

        /// <summary>
        /// Constructs a queue with the <paramref name="queueEP" /> passed and using default <see cref="MsgQueueSettings" />.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be used by the queue for messaging.</param>
        /// <param name="queueEP">The relative or absolute logical queue endpoint (or <c>null</c>).</param>
        /// <remarks>
        /// You can pass <paramref name="queueEP" /> as <c>null</c>, indicating that  a connection 
        /// to a queue server that matches the logical base endpoint specified in the 
        /// <see cref="MsgQueueSettings" /> with  a wildcard added.  For example, if 
        /// <see cref="MsgQueueSettings.BaseEP" /> is <b>logical://MyQueues</b> then a connection 
        /// will be established with a queue service that exposes endpoints that match
        /// <b>logical://MyQueues/*</b>.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="queueEP" /> is not <c>null</c> and is not a valid absolute or
        /// relative logical endpoint.
        /// </exception>
        public MsgQueue(MsgRouter router, string queueEP)
            : this(router, queueEP, new MsgQueueSettings())
        {
        }

        /// <summary>
        /// Opens the queue with the <paramref name="queueEP" /> and <see cref="MsgQueueSettings" /> passed.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be used by the queue for messaging.</param>
        /// <param name="queueEP">The relative or absolute logical queue endpoint (or <c>null</c>).</param>
        /// <param name="settings">The <see cref="MsgQueueSettings" />.</param>
        /// <remarks>
        /// You can pass <paramref name="queueEP" /> as <c>null</c>, indicating that  a connection 
        /// to a queue server that matches the logical base endpoint specified in the 
        /// <see cref="MsgQueueSettings" /> with  a wildcard added.  For example, if 
        /// <see cref="MsgQueueSettings.BaseEP" /> is <b>logical://MyQueues</b> then a connection 
        /// will be established with a queue service that exposes endpoints that match
        /// <b>logical://MyQueues/*</b>.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="queueEP" /> is not <c>null</c> and is not a valid absolute or
        /// relative logical endpoint.
        /// </exception>
        public MsgQueue(MsgRouter router, string queueEP, MsgQueueSettings settings)
        {
            string conEP;

            if (router == null)
                throw new ArgumentNullException("router");

            if (settings == null)
                throw new ArgumentNullException("settings");

            using (TimedLock.Lock(this))
            {
                this.onEnqueue  = new AsyncCallback(OnEnqueue);
                this.onDequeue  = new AsyncCallback(OnDequeue);
                this.onPeek     = new AsyncCallback(OnPeek);
                this.router     = router;
                this.settings   = settings;
                this.queueEP    = NormalizeQueueEP(queueEP);
                this.transStack = new Stack<string>();

                conEP = this.queueEP;
                if (queueEP == null)
                    conEP += "/*";

                this.session = router.CreateDuplexSession();
                this.session.Connect(conEP);
            }
        }

        /// <summary>
        /// Normalizes the endpoint string passed into an absolute queue endpoint.
        /// </summary>
        /// <param name="queueEP">The endpoint string.</param>
        /// <returns>The normalized queue <see cref="MsgEP" />.</returns>
        private MsgEP NormalizeQueueEP(string queueEP)
        {
            string      ep;
            MsgEP       result;

            if (queueEP != null &&
                (queueEP.StartsWith("abstract://", StringComparison.OrdinalIgnoreCase) ||
                 queueEP.StartsWith("logical://", StringComparison.OrdinalIgnoreCase)))
            {
                // Looks like we have an absolute queue endpoint,
                // so just use it.

                ep = queueEP;
            }
            else
            {
                // Must be a relative endpoint.

                ep = settings.BaseEP;

                // Strip off the base wildcard (if there is one) before appending
                // the relative part.

                if (ep.EndsWith("*"))
                    ep = ep.Substring(0, ep.Length - 1);

                if (queueEP != null && queueEP != string.Empty)
                {
                    // Append a trailing "/" if necessary.

                    if (!ep.EndsWith("/"))
                        ep += "/";

                    // Strip any leading "/" from the relative part (if necessary.)

                    if (queueEP.StartsWith("/"))
                        queueEP = queueEP.Substring(1);

                    ep += queueEP;
                }
            }

            try
            {
                result = (MsgEP)ep;
            }
            catch
            {
                throw new ArgumentException("Improperly formated QueueEP.");
            }

            if (!result.IsLogical)
                throw new ArgumentException("Endpoint is not logical.", "queueEP");

            return result;
        }

        /// <summary>
        /// Returns <c>true</c> if the message queue is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return session != null && session.IsConnected; }
        }

        /// <summary>
        /// Closes the queue if it is open.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (session != null)
                {
                    session.Close();
                    session = null;
                }

                router = null;
            }
        }

        /// <summary>
        /// Handles ambient transaction entlistment.
        /// </summary>
        private void EnlistAmbientTransaction()
        {
            var ambientTransaction = Transaction.Current;

            if (ambientTransaction == null)
                return;

            if (transStack.Count == 0)
            {
                BeginTransaction();
                transStack.Push(ambientTransaction.TransactionInformation.LocalIdentifier);
                ambientTransaction.EnlistVolatile(this, EnlistmentOptions.None);
                return;
            }

            if (transStack.Peek() != ambientTransaction.TransactionInformation.LocalIdentifier)
            {
                BeginTransaction();
                transStack.Push(ambientTransaction.TransactionInformation.LocalIdentifier);
                ambientTransaction.EnlistVolatile(this, EnlistmentOptions.None);
            }
        }

        /// <summary>
        /// Synchronously sends a message to the message queue specified to the constuctor.
        /// </summary>
        /// <param name="msg">The <see cref="QueuedMsg" /> instance to be queued.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if the queue service doesn't respond in time.</exception>
        public void Enqueue(QueuedMsg msg)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginEnqueue(msg, null, null);
            EndEnqueue(ar);
        }

        /// <summary>
        /// Synchronously sends a message to the specified message queue.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="msg">The <see cref="QueuedMsg" /> instance to be queued.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if the queue service doesn't respond in time.</exception>
        public void EnqueueTo(string queueEP, QueuedMsg msg)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginEnqueueTo(queueEP, msg, null, null);
            EndEnqueueTo(ar);
        }

        /// <summary>
        /// Initiates an asynchronous transmission of a message to the queue specified
        /// to the constructor.
        /// </summary>
        /// <param name="msg">The <see cref="QueuedMsg" /> instance to be queued.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginEnqueue" /> should be matched with
        /// a call to <see cref="EndEnqueue" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public IAsyncResult BeginEnqueue(QueuedMsg msg, AsyncCallback callback, object state)
        {
            AsyncResult     arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd     cmd;

            msg.SendTime = DateTime.UtcNow;
            msg.TargetEP = queueEP;

            cmd = new MsgQueueCmd(MsgQueueCmd.EnqueueCmd);
            cmd.QueueEP = queueEP;
            cmd.MessageHeader = msg.GetMessageHeader(settings);
            cmd.MessageBody = msg.GetMessageBody(settings.Compress);

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onEnqueue, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Initiates an asynchronous transmission of a message to the specified queue.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="msg">The <see cref="QueuedMsg" /> instance to be queued.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginEnqueueTo" /> should be matched with
        /// a call to <see cref="EndEnqueueTo" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public IAsyncResult BeginEnqueueTo(string queueEP, QueuedMsg msg, AsyncCallback callback, object state)
        {
            AsyncResult     arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd     cmd;

            queueEP = NormalizeQueueEP(queueEP);
            msg.SendTime = DateTime.UtcNow;
            msg.TargetEP = queueEP;

            cmd = new MsgQueueCmd(MsgQueueCmd.EnqueueCmd);
            cmd.QueueEP = queueEP;
            cmd.MessageHeader = msg.GetMessageHeader(settings);
            cmd.MessageBody = msg.GetMessageBody(settings.Compress);

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onEnqueue, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Handles enqueue query completions.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private void OnEnqueue(IAsyncResult ar)
        {
            AsyncResult     arOp = (AsyncResult)ar.AsyncState;
            MsgQueueAck     ack  = null;
            Exception       err;

            try
            {
                ack = (MsgQueueAck)session.EndQuery(ar);
                err = null;
            }
            catch (Exception e)
            {
                err = e;
            }

            arOp.Result = ack;
            arOp.Notify(err);
        }

        /// <summary>
        /// Completes an asynchronous message transmission initiated by <see cref="BeginEnqueue" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if the queue service doesn't respond in time.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public void EndEnqueue(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar;

            arOp.Wait();
            try
            {
                if (arOp.Exception != null)
                    throw arOp.Exception;
            }
            finally
            {
                arOp.Dispose();
            }
        }

        /// <summary>
        /// Completes an asynchronous message transmission initiated by <see cref="BeginEnqueueTo" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if the queue service doesn't respond in time.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public void EndEnqueueTo(IAsyncResult ar)
        {
            EndEnqueue(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from the queue specified to the constructor,
        /// using the default timeout specified by <see cref="MsgQueueSettings.Timeout" />
        /// when the queue was opened.
        /// </summary>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg Dequeue()
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginDequeue(settings.Timeout, null, null);
            return EndDequeue(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from the queue specified to the constructor,
        /// using the timeout specified.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg Dequeue(TimeSpan timeout)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginDequeue(timeout, null, null);
            return EndDequeue(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from a specific queue,
        /// using the default timeout specified by <see cref="MsgQueueSettings.Timeout" />
        /// when the queue was opened.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg DequeueFrom(string queueEP)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginDequeueFrom(queueEP, settings.Timeout, null, null);
            return EndDequeueFrom(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from a specific queue,
        /// using the default timeout specified by <see cref="MsgQueueSettings.Timeout" />
        /// when the queue was opened.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg DequeueFrom(string queueEP, TimeSpan timeout)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginDequeueFrom(queueEP, timeout, null, null);
            return EndDequeueFrom(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to retrieve a message from the queue
        /// specified to the constructor.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginDequeueFrom" /> should be matched with
        /// a call to <see cref="EndDequeueFrom" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no queue endpoint was specified when the queue was opened.</exception>
        public IAsyncResult BeginDequeue(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult     arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd     cmd;

            cmd         = new MsgQueueCmd(MsgQueueCmd.DequeueCmd);
            cmd.Timeout = timeout;
            cmd.QueueEP = queueEP;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onDequeue, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Initiates an asynchronous operation to retrieve a message from a specific message queue.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginDequeue" /> should be matched with
        /// a call to <see cref="EndDequeue" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public IAsyncResult BeginDequeueFrom(string queueEP, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult         arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd cmd;

            queueEP     = NormalizeQueueEP(queueEP);
            cmd         = new MsgQueueCmd(MsgQueueCmd.DequeueCmd);
            cmd.QueueEP = queueEP;
            cmd.Timeout = timeout;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onDequeue, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Handles dequeue query completions.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private void OnDequeue(IAsyncResult ar)
        {
            AsyncResult     arOp = (AsyncResult)ar.AsyncState;
            MsgQueueAck     ack = null;
            Exception       err;

            try
            {
                ack = (MsgQueueAck)session.EndQuery(ar);
                err = null;
            }
            catch (Exception e)
            {
                err = e;
            }

            arOp.Result = ack;
            arOp.Notify(err);
        }

        /// <summary>
        /// Completes the asynchronous retrieval of a message from the message queue
        /// specified to the constructor.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <returns>The <see cref="QueuedMsg" /> retrieved.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public QueuedMsg EndDequeue(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar;

            arOp.Wait();
            try
            {
                if (arOp.Exception != null)
                    throw arOp.Exception;

                return new QueuedMsg((MsgQueueAck)arOp.Result, true);
            }
            finally
            {
                arOp.Dispose();
            }
        }

        /// <summary>
        /// Completes the asynchronous retrieval of a message from a specific message queue.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <returns>The <see cref="QueuedMsg" /> retrieved.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if the a message cannot be dequeued before the operation times-out.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public QueuedMsg EndDequeueFrom(IAsyncResult ar)
        {
            return EndDequeue(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from the specified to the constructor
        /// without removing the message from the queue, using the default timeout 
        /// specified by <see cref="MsgQueueSettings.Timeout" /> when the queue was opened.
        /// </summary>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg Peek()
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginPeek(settings.Timeout, null, null);
            return EndPeek(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from the specified to the constructor
        /// without removing the message from the queue.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout" /> is <see cref="TimeSpan.Zero" /> and there's no
        /// message available to be dequeued then this method will return <c>null</c>
        /// instead of throwing a <see cref="TimeoutException" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be dequeued before the operation times-out.</exception>
        public QueuedMsg Peek(TimeSpan timeout)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginPeek(timeout, null, null);
            return EndPeek(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from a specific queue without removing the message from 
        /// the queue, using the default timeout specified by <see cref="MsgQueueSettings.Timeout" /> 
        /// when the queue was opened.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <remarks>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be returned before the operation times-out.</exception>
        public QueuedMsg PeekFrom(string queueEP)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginPeekFrom(queueEP, settings.Timeout, null, null);
            return EndPeekFrom(ar);
        }

        /// <summary>
        /// Synchronously retrieves a message from a specific queue without removing the message from 
        /// the queue.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The <see cref="QueuedMsg" /> returned from the queue.</returns>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout" /> is <see cref="TimeSpan.Zero" /> and there's no
        /// message available to be dequeued then this method will return <c>null</c>
        /// instead of throwing a <see cref="TimeoutException" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be returned before the operation times-out.</exception>
        public QueuedMsg PeekFrom(string queueEP, TimeSpan timeout)
        {
            IAsyncResult ar;

            EnlistAmbientTransaction();
            ar = BeginPeekFrom(queueEP, timeout, null, null);
            return EndPeekFrom(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to retrieve a message from the queue
        /// specified to the constructor without removing the message from the queue.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginDequeueFrom" /> should be matched with
        /// a call to <see cref="EndDequeueFrom" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public IAsyncResult BeginPeek(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult     arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd     cmd;

            cmd         = new MsgQueueCmd(MsgQueueCmd.PeekCmd);
            cmd.QueueEP = queueEP;
            cmd.Timeout = timeout;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onPeek, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Initiates an asynchronous operation to retrieve a message from a specific message queue
        /// without removing the message from the queue.
        /// </summary>
        /// <param name="queueEP">The absolute or relative queue endpoint.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> delegate to be when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginDequeue" /> should be matched with
        /// a call to <see cref="EndDequeue" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public IAsyncResult BeginPeekFrom(string queueEP, TimeSpan timeout, AsyncCallback callback, object state)
        {
            AsyncResult     arOp = new AsyncResult(null, callback, state);
            MsgQueueCmd     cmd;

            queueEP     = NormalizeQueueEP(queueEP);
            cmd         = new MsgQueueCmd(MsgQueueCmd.PeekCmd);
            cmd.QueueEP = queueEP;
            cmd.Timeout = timeout;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.BeginQuery(cmd, onPeek, arOp);
                arOp.Started();
            }

            return arOp;
        }

        /// <summary>
        /// Handles peek query completions.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private void OnPeek(IAsyncResult ar)
        {
            AsyncResult     arOp = (AsyncResult)ar.AsyncState;
            MsgQueueAck     ack = null;
            Exception       err;

            try
            {
                ack = (MsgQueueAck)session.EndQuery(ar);
                err = null;
            }
            catch (Exception e)
            {
                err = e;
            }

            arOp.Result = ack;
            arOp.Notify(err);
        }

        /// <summary>
        /// Completes the asynchronous retrieval of a message from the message queue
        /// specified to the constructor without removing the message from the queue.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <returns>The <see cref="QueuedMsg" /> retrieved.</returns>
        /// <remarks>
        /// <note>
        /// If <b>timeout</b> was passed as <see cref="TimeSpan.Zero" /> to <see cref="BeginPeek" />
        /// and there's no message available to be dequeued then this method will return <c>null</c>
        /// instead of throwing a <see cref="TimeoutException" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be returned before the operation times-out.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public QueuedMsg EndPeek(IAsyncResult ar)
        {
            AsyncResult     arOp = (AsyncResult)ar;
            MsgQueueAck     ack;

            arOp.Wait();
            try
            {
                if (arOp.Exception != null)
                    throw arOp.Exception;

                ack = (MsgQueueAck)arOp.Result;
                if (ack.MessageBody == null)
                    return null;
                else
                    return new QueuedMsg(ack, true);
            }
            finally
            {
                arOp.Dispose();
            }
        }

        /// <summary>
        /// Completes the asynchronous retrieval of a message from a specific message queue
        ///  without removing the message from the queue.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginEnqueue" />.</param>
        /// <returns>The <see cref="QueuedMsg" /> retrieved.</returns>
        /// <remarks>
        /// <note>
        /// If <b>timeout</b> was passed as <see cref="TimeSpan.Zero" /> to <see cref="BeginPeekFrom" />
        /// and there's no message available to be dequeued then this method will return <c>null</c>
        /// instead of throwing a <see cref="TimeoutException" />.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        /// <exception cref="TimeoutException">Thrown if a message cannot be returned before the operation times-out.</exception>
        /// <exception cref="CancelException">Thrown if <see cref="Close" /> was called during this operation.</exception>
        public QueuedMsg EndPeekFrom(IAsyncResult ar)
        {
            return EndPeek(ar);
        }

        //---------------------------------------------------------------------
        // Transaction support

        /// <summary>
        /// Initiates a new or nested transaction against the message queue.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All enqueue, dequeue, and peek operations performed from this point
        /// until <see cref="Commit" /> or <see cref="Rollback" /> is called will
        /// be part of the transaction.
        /// </para>
        /// <note>
        /// Transactions may be nested to an arbitrary depth.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public void BeginTransaction()
        {
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.Query(new MsgQueueCmd(MsgQueueCmd.BeginTransCmd));
            }
        }

        /// <summary>
        /// Commits all changes made at the current transaction nesting level to
        /// the message queue.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public void Commit()
        {
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.Query(new MsgQueueCmd(MsgQueueCmd.CommitTransCmd));
            }
        }

        /// <summary>
        /// Rolls back all changes made at the current transaction nesting level
        /// from the message queue.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the queue is not open.</exception>
        public void Rollback()
        {
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.Query(new MsgQueueCmd(MsgQueueCmd.RollbackTransCmd));
            }
        }

        /// <summary>
        /// Rolls back any and all transactions made on the queue.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method when there's no transaction open.
        /// If this is the case, then this method does nothing.
        /// </note>
        /// </remarks>
        public void RollbackAll()
        {
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new ObjectDisposedException(typeof(MsgQueue).Name);

                session.Query(new MsgQueueCmd(MsgQueueCmd.RollbackAllTransCmd));
            }
        }

        //-----------------------------------------------------------------
        // IEnlistmentNotification implementation

        void IEnlistmentNotification.Commit(Enlistment enlistment)
        {
            transStack.Pop();
            Commit();
            enlistment.Done();
        }

        void IEnlistmentNotification.InDoubt(Enlistment enlistment)
        {
            enlistment.Done();
        }

        void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment)
        {
            preparingEnlistment.Prepared();
        }

        void IEnlistmentNotification.Rollback(Enlistment enlistment)
        {
            transStack.Pop();
            Rollback();
            enlistment.Done();
        }

        //---------------------------------------------------------------------
        // IDisposable implementation

        /// <summary>
        /// Closes the queue if it's open.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Any uncommitted transactions will be rolled back.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            Close();
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
