//-----------------------------------------------------------------------------
// FILE:        MemoryTransactionLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an in-memory ITransactionLog.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Implements an in-memory <see cref="ITransactionLog" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This <see cref="ITransactionLog" /> is suitable for transaction implemenations
    /// that require only non-durable transaction commit and rollback support.  This
    /// log implementation is very fast and requires no additional configuration.
    /// The only downside is that the transaction log is lost when if the 
    /// process terminates abnormally before the operation completes.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class MemoryTransactionLog : ITransactionLog
    {
        private const string NotOpenMsg = "Transaction log is not open.";

        private object                                  syncLock;
        private bool                                    isOpen;
        private TransactionManager                      manager;
        private Dictionary<Guid, MemoryOperationLog>    transactions;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MemoryTransactionLog()
        {

            this.isOpen       = false;
            this.transactions = null;
            this.manager      = null;
        }

        /// <summary>
        /// Opens the transaction log, returning a <see cref="LogStatus" />
        /// value indicating the state of the transaction log.
        /// </summary>
        /// <param name="manager">The <see cref="TransactionManager" />.</param>
        /// <returns>The log's <see cref="LogStatus" /> code.</returns>
        /// <remarks>
        /// <para>
        /// This method will return <see cref="LogStatus.Ready" /> if the log is ready to
        /// begin handling transactions.
        /// </para>
        /// <para>
        /// <see cref="LogStatus.Recover" /> will be returned if the log was not 
        /// closed properly (probably due to a system or application failure) and 
        /// there are transactions that need to be recovered.  The <see cref="TransactionManager" />
        /// will call <see cref="GetOrphanTransactions" /> to get the <see cref="Guid" />s of
        /// the orphaned transactions and will then call <see cref="OpenOperationLog" />
        /// to open each transaction operation log and then undo or redo the transaction
        /// operations depending on the state of the operation log.
        /// </para>
        /// <para>
        /// The method returns <see cref="LogStatus.Corrupt" /> if the transaction
        /// log is corrupt and that there's the potential for the resource to 
        /// be in an inconsistent state after recovering the remaining transactions.
        /// </para>
        /// </remarks>
        public LogStatus Open(TransactionManager manager)
        {

            this.manager      = manager;
            this.syncLock     = manager.SyncRoot;
            this.isOpen       = true;
            this.transactions = new Dictionary<Guid, MemoryOperationLog>();

            return LogStatus.Ready;
        }

        /// <summary>
        /// Closes the transaction log if it is open.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        /// <summary>
        /// Closes the transaction log if it is open, optionally simulating a system crash.
        /// </summary>
        /// <param name="simulateCrash">
        /// Pass as <c>true</c> to simulate a system crash by leaving the transaction
        /// log state as is.
        /// </param>
        /// <remarks>
        /// <note>
        /// Implementations may choose to ignore the <paramref name="simulateCrash" />
        /// parameter is this is used only for UNIT testing.
        /// </note>
        /// </remarks>
        public void Close(bool simulateCrash)
        {
            using (TimedLock.Lock(syncLock))
            {
                isOpen       = false;
                transactions = null;
            }
        }

        /// <summary>
        /// Returns the orphaned transaction <see cref="Guid" />s discovered after
        /// <see cref="Open" /> returns <see cref="LogStatus.Recover" />.
        /// </summary>
        /// <returns>The list of transaction <see cref="Guid" />s.</returns>
        /// <remarks>
        /// This method returns only the IDs for valid, non-corrupted transaction
        /// operation logs.  It will delete any corrupted logs it finds.
        /// </remarks>
        public List<Guid> GetOrphanTransactions()
        {
            return new List<Guid>();
        }

        /// <summary>
        /// Opens an existing <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="transactionID">The operation's base transaction <see cref="Guid" />.</param>
        /// <returns>The <see cref="IOperationLog" /> instance.</returns>
        public IOperationLog OpenOperationLog(Guid transactionID)
        {
            MemoryOperationLog operationLog;

            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (!transactions.TryGetValue(transactionID, out operationLog))
                    throw new TransactionException("Operation log not found.");

                return operationLog;
            }
        }

        /// <summary>
        /// Closes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        public void CloseOperationLog(IOperationLog operationLog)
        {
        }

        /// <summary>
        /// Creates an <see cref="IOperationLog" /> in <see cref="OperationLogMode.Undo" /> mode.
        /// </summary>
        /// <param name="transactionID">The base transaction <see cref="Guid" />.</param>
        /// <returns>The <see cref="IOperationLog" /> instance.</returns>
        /// <remarks>
        /// This is called when a transaction is first created and operations need
        /// to be persisted to an undo log so that they can be undone if the process
        /// crashes before all of the operations have been persisted.
        /// </remarks>
        public IOperationLog CreateOperationLog(Guid transactionID)
        {
            MemoryOperationLog operationLog;

            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (transactions.ContainsKey(transactionID))
                    throw new TransactionException("Operation log already exists.");

                operationLog = new MemoryOperationLog(manager.SyncRoot, OperationLogMode.Undo);
                transactions.Add(transactionID, operationLog);

                return operationLog;
            }
        }

        /// <summary>
        /// Sets an <see cref="IOperationLog" />'s mode to <see cref="OperationLogMode.Redo" />
        /// and then closes the log.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <remarks>
        /// This is called when the base transaction is committed and all of the
        /// operations that compose the transaction have been persisted to the
        /// log.  The method sets the operation log mode to <see cref="OperationLogMode.Redo" />
        /// so that the operations will be reapplied after a process crash.
        /// </remarks>
        public void CommitOperationLog(IOperationLog operationLog)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                ((MemoryOperationLog)operationLog).Mode = OperationLogMode.Redo;
            }
        }

        /// <summary>
        /// Closes and deletes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <remarks>
        /// This is called after the all of the transactions have been applied to
        /// the underlying resource and the log is no longer necessary.
        /// </remarks>
        public void RemoveOperationLog(IOperationLog operationLog)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (transactions.ContainsKey(operationLog.TransactionID))
                    transactions.Remove(operationLog.TransactionID);
            }
        }
    }
}
