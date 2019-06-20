//-----------------------------------------------------------------------------
// FILE:        TransactionManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements core custom application transaction behaviors.

using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

// $todo(jeff.lill): 
//
// I need to think a bit more about the internal transaction
// log model.  The current model assumes that base transactions
// executed in parallel (potentially on different threads)
// can be redone or undone during a recovery in any order.
// This will work fine for simple transaction scenarios like 
// what is required for the message queue service but I'm
// not convinced that this will work in general.  This
// assumes that the transacted resource had locks in place
// to ensure that only those operations that could be applied
// in any order could make it into the logs.  This is probably
// a reasonable assumption, but I need to think about it
// some more.

namespace LillTek.Transactions
{
    /// <summary>
    /// Implements core custom application transaction behaviors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ITransactedResource" />s can expose two different programming
    /// models to their applications.  The first model assumes that a specific
    /// transaction is created and completed on the same thread.  This is a very
    /// convienent model for client applications since it doesn't require the application
    /// to explcitly keep track of transaction instances.  The second programming
    /// model does not have this same thread restriction and is more suitable for
    /// multi-threaded transacted servers.  These programming models are described
    /// in more detail below:
    /// </para>
    /// <para><b><u>Single-Thread Programming Model</u></b></para>
    /// <para>
    /// In the single thread programming model, the <see cref="ITransactedResource" />
    /// implementation passes <b>allowThreadSpanning=false</b> to the constructor.
    /// This signals to <see cref="BeginTransaction" /> that it should check for
    /// an existing base transaction for the current thread before creating a new
    /// one.  A typical single threaded resource's programming model would look 
    /// something like:
    /// </para>
    /// <code language="cs">
    /// using (var transaction = myResource.BeginTransaction()) {
    /// 
    ///     myResource.ChangeSomething();
    ///     myResource.ChangeSomethingElse();
    ///     transaction.Commit();
    /// }
    /// </code>
    /// <para><b><u>Multi-Thread Programming Model</u></b></para>
    /// <para>
    /// The programming model needs to be a bit more complex to
    /// handle transactions that can span multiple threads.  For
    /// this to work, the custom <see cref="ITransactedResource" />
    /// implementation will need to pass <b>allowThreadSpanning=true</b>
    /// to the constructor and also accept <see cref="Transaction" />
    /// instances to all modification methods.  Here's how this
    /// might look:
    /// </para>
    /// <code language="cs">
    /// using (var transaction = myResource.BeginTransaction(true)) {
    /// 
    ///     myResource.ChangeSomething(transaction);
    ///     myResource.ChangeSomethingElse(transaction);
    ///     transaction.Commit();
    /// }
    /// </code>
    /// <para>
    /// This is not a huge difference from the single threading case.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class TransactionManager : ILockable
    {
        /// <summary>
        /// The default <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string DefTraceSubsystem = "LillTek.Transactions";

        private string                              traceSubsystem;     // NetTrace subsystem name
        private bool                                running;            // True if the manager is running
        private bool                                stopPending;        // True if the manager is stopping
        private ITransactedResource                 resource;           // The managed resource
        private ITransactionLog                     log;                // The transaction log
        private Dictionary<Guid, BaseTransaction>   transactions;       // The current set of base transactions managed by
                                                                        // this instance
        private bool                                threadSpanning;     // True if transactions may span threads
        private Dictionary<int, BaseTransaction>    threadTransMap;     // Maps managed thread IDs to current base transactions

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="allowThreadSpanning">
        /// Determines whether the implementation allows transaction activities
        /// to span multiple threads or be restricted to a single thread.
        /// </param>
        public TransactionManager(bool allowThreadSpanning)
        {
            this.traceSubsystem = DefTraceSubsystem;
            this.running        = false;
            this.stopPending    = false;
            this.threadSpanning = allowThreadSpanning;
        }

        /// <summary>
        /// The <see cref="NetTrace" /> subsystem name used when tracing is enabled.
        /// This is set to the default value of <b>"LillTek.Transactions"</b> but may
        /// be customized by applications.
        /// </summary>
        public string TraceSubsystem
        {
            get { return traceSubsystem; }
            set { traceSubsystem = value; }
        }

        /// <summary>
        /// Performs a best efforts recovery of orphaned transactions.
        /// </summary>
        private void Recover()
        {
            var         orphans = log.GetOrphanTransactions();
            var          context = new UpdateContext(this, true, false, false, Guid.Empty);
            IOperation  operation;

            SysLog.LogWarning("Recovering [{0}] transactions for [{1}].", orphans.Count, resource.Name);

            Trace(0, "Begin Recovery", null);
            resource.BeginRecovery(context);

            for (int i = 0; i < orphans.Count; i++)
            {

                IOperationLog opLog = log.OpenOperationLog(orphans[i]);

                context.TransactionID = opLog.TransactionID;
                if (opLog.Mode == OperationLogMode.Redo)
                {
                    if (resource.BeginRedo(context))
                    {
                        foreach (ILogPosition pos in opLog.GetPositions(false))
                        {
                            operation = opLog.Read(resource, pos);
                            Trace(0, "Redo: " + operation.Description, "ID=" + context.TransactionID.ToString());
                            resource.Redo(context, operation);
                        }
                    }

                    resource.EndRedo(context);
                }
                else
                {
                    if (resource.BeginUndo(context))
                    {
                        foreach (ILogPosition pos in opLog.GetPositions(true))
                        {
                            operation = opLog.Read(resource, pos);
                            Trace(0, "Undo: " + operation.Description, "ID=" + context.TransactionID.ToString());
                            resource.Undo(context, opLog.Read(resource, pos));
                        }
                    }

                    resource.EndUndo(context);
                }

                log.CloseOperationLog(opLog);
            }

            context.TransactionID = Guid.Empty;
            resource.EndRecovery(context);
            Trace(0, "End Recovery", null);
        }

        /// <summary>
        /// Starts the transaction manager.
        /// </summary>
        /// <param name="resource">The <see cref="ITransactedResource" /> to be managed.</param>
        /// <param name="log">The unopened <see cref="ITransactionLog" /> implementation to be used.</param>
        /// <param name="recoverCorrupt">Pass as <c>true</c> if recovery of corrupt transaction logs should be attempted.</param>
        public void Start(ITransactedResource resource, ITransactionLog log, bool recoverCorrupt)
        {
            using (TimedLock.Lock(this))
            {
                if (running)
                    throw new TransactionException("Transaction manager has already started for [{0}].", resource.Name);

                switch (log.Open(this))
                {
                    case LogStatus.Ready:

                        break;

                    case LogStatus.Recover:

                        this.resource = resource;
                        this.log = log;

                        Recover();
                        break;

                    case LogStatus.Corrupt:

                        if (!recoverCorrupt)
                            throw new TransactionException("Transaction log for [{0]] is corrupt.", resource.Name);

                        SysLog.LogWarning("Corrupt transaction log detected for [{0}]. Best efforts are being taken to recover.", resource.Name);
                        Recover();
                        break;
                }

                this.running        = true;
                this.resource       = resource;
                this.log            = log;
                this.transactions   = new Dictionary<Guid, BaseTransaction>();
                this.threadTransMap = new Dictionary<int, BaseTransaction>();
            }
        }

        /// <summary>
        /// Stops the transaction manager if it's currently running.
        /// </summary>
        /// <param name="waitTime">
        /// Maximum time to wait for pending transactions to complete or <see cref="TimeSpan.MaxValue" />
        /// to wait indefinitely.
        /// </param>
        public void Stop(TimeSpan waitTime)
        {
            using (TimedLock.Lock(this))
            {
                if (!running)
                    return;

                if (stopPending)
                    throw new TransactionException("Transaction manager stop is already pending for [{0}].", resource.Name);

                stopPending = true;

                if (waitTime <= TimeSpan.Zero)
                {
                    // Quit immediately.

                    if (transactions.Count > 0)
                        SysLog.LogWarning("Transaction manager for [{0}] is stopping with [{1}] transactions still pending.",
                                          resource.Name, transactions.Count);

                    running        = false;
                    stopPending    = false;
                    transactions   = null;
                    threadTransMap = null;

                    log.Close();
                    return;
                }
            }

            // Wait to see if the transactions can be bled off.

            var waitLimit = waitTime == TimeSpan.MaxValue ? DateTime.MaxValue : SysTime.Now + waitTime;

            while (true)
            {
                using (TimedLock.Lock(this))
                {
                    if (transactions.Count == 0)
                    {
                        running      = false;
                        stopPending  = false;
                        transactions = null;

                        log.Close();
                        return;
                    }

                    if (SysTime.Now >= waitLimit)
                    {
                        if (transactions.Count > 0)
                            SysLog.LogWarning("Transaction manager for [{0}] is stopping with [{1}] transactions still pending.",
                                              resource.Name, transactions.Count);

                        running      = false;
                        stopPending  = false;
                        transactions = null;

                        log.Close();
                        return;
                    }
                }

                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Simulates a system crash for unit testing by closing the transaction manager but leaving
        /// any existing transaction logs as they stand.
        /// </summary>
        internal void SimulateCrash() 
        {
            using (TimedLock.Lock(this)) 
            {
                if (!running)
                    return;

                if (stopPending)
                    throw new TransactionException("Transaction manager stop is already pending for [{0}].",resource.Name);

                running      = false;
                stopPending  = false;
                transactions = null;

                foreach (BaseTransaction transaction in transactions.Values)
                    transaction.OperationLog.Close();

                log.Close(true);
            }
        }

        /// <summary>
        /// Returns the object instance to be used for thread synchronization by
        /// the transaction related classes.
        /// </summary>
        public object SyncRoot
        {
            get { return this; }
        }

        /// <summary>
        /// Returns <c>true</c> if the transaction manager is running.
        /// </summary>
        public bool IsRunning
        {
            get { return IsRunning; }
        }

        /// <summary>
        /// Returns the <see cref="ITransactedResource" /> managed by the transaction manager.
        /// </summary>
        public ITransactedResource Resource
        {
            get { return resource; }
        }

        /// <summary>
        /// Returns the transaction manage's <see cref="ITransactionLog" />.
        /// </summary>
        public ITransactionLog Log
        {
            get { return log; }
        }

        /// <summary>
        /// The current <see cref="BaseTransaction" /> for the executing thread (or <c>null</c>).
        /// </summary>
        /// <exception cref="TransactionException">Thrown if transaction manager has not been started.</exception>
        /// <exception cref="TransactionException">Thrown if <b>allowThreadSpanning=true</b> was passed to the constructor.</exception>
        public BaseTransaction CurrentTransaction
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (!running)
                        throw new TransactionException("Transaction manager has not started for [{0}].", resource.Name);

                    if (threadSpanning)
                        throw new TransactionException("Transaction manager for [{0}] was not constructed in single thread mode.", resource.Name);

                    BaseTransaction current;

                    if (threadTransMap.TryGetValue(Thread.CurrentThread.ManagedThreadId, out current))
                        return current;
                    else
                        return null;
                }
            }

            private set
            {
                using (TimedLock.Lock(this))
                {
                    if (!running)
                        throw new TransactionException("Transaction manager has not started for [{0}].", resource.Name);

                    if (threadSpanning)
                        throw new TransactionException("Transaction manager for [{0}] was not constructed in single thread mode.", resource.Name);

                    int threadID = Thread.CurrentThread.ManagedThreadId;

                    if (value == null)
                    {
                        if (threadTransMap.ContainsKey(threadID))
                            threadTransMap.Remove(threadID);
                    }
                    else
                        threadTransMap[threadID] = value;
                }
            }
        }

        /// <summary>
        /// Initiates a new base transaction.
        /// </summary>
        /// <returns>The new <see cref="Transaction" />.</returns>
        /// <exception cref="InvalidOperationException">Thrown if transaction manager has not been started.</exception>
        /// <remarks>
        /// <para>
        /// Note that this method changes its behavior based on whether
        /// <b>allowThreadSpanning</b> was passed as <c>true</c> or <c>false</c>
        /// to the constructor.  If thread spanning is allowed then this method
        /// always creates a new <see cref="BaseTransaction" /> and returns a
        /// new <see cref="Transaction" /> pushed onto it.
        /// </para>
        /// <para>
        /// If single-thread transaction mode is enabled, then this method
        /// will first look to see if there's already a <see cref="BaseTransaction" />
        /// for then current thread.  If there is one, then a new <see cref="Transaction" />
        /// will be pushed onto it and returned.  If there no base transaction associated
        /// with the thread then a new one will be created.
        /// </para>
        /// </remarks>
        public Transaction BeginTransaction()
        {
            BaseTransaction transBase;

            using (TimedLock.Lock(this))
            {
                if (!running)
                    throw new TransactionException("Transaction manager has not started for [{0}].", resource.Name);

                if (stopPending)
                    throw new TransactionException("Transaction manager stop is pending for [{0}].", resource.Name);

                if (threadSpanning)
                {
                    transBase = new BaseTransaction(Helper.NewGuid(), this);
                    transactions.Add(transBase.ID, transBase);

                    return transBase.BeginTransaction();
                }
                else
                {
                    transBase = this.CurrentTransaction;
                    if (transBase != null)
                        return transBase.BeginTransaction();

                    this.CurrentTransaction = transBase = new BaseTransaction(Helper.NewGuid(), this);
                    transactions.Add(transBase.ID, transBase);

                    return transBase.BeginTransaction();
                }
            }
        }

        /// <summary>
        /// Called by <see cref="BaseTransaction" /> when the transaction is complete.
        /// </summary>
        /// <param name="baseTrans"></param>
        internal void EndTransaction(BaseTransaction baseTrans)
        {
            using (TimedLock.Lock(this))
            {
                log.RemoveOperationLog(baseTrans.OperationLog);
                transactions.Remove(baseTrans.ID);

                if (!threadSpanning)
                    this.CurrentTransaction = null;
            }
        }

        /// <summary>
        /// Writes an entry to the trace log.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="summary">The trace summary.</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string tEvent, string summary)
        {
            NetTrace.Write(traceSubsystem, detail, "TransMgr: " + tEvent, summary, null);
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
