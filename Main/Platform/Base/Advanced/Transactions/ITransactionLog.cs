//-----------------------------------------------------------------------------
// FILE:        ITransactionLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of persistent transaction undo and redo logs.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Describes the transaction log status just after the log is
    /// opened.
    /// </summary>
    public enum LogStatus
    {
        /// <summary>
        /// The log is ready to begin handling transactions.
        /// </summary>
        Ready,

        /// <summary>
        /// The log was not closed properly and there are transactions that
        /// need to be recovered.  Call <see cref="ITransactionLog.GetOrphanTransactions" />
        /// to get the <see cref="Guid" />s of the transactions that need to be
        /// recovered.
        /// </summary>
        Recover,

        /// <summary>
        /// The log is corrupt and cannot be recovered with the potential for
        /// losing information.
        /// </summary>
        Corrupt,
    }

    /// <summary>
    /// Defines the behavior of persistent transaction undo and redo logs.
    /// </summary>
    public interface ITransactionLog
    {
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
        LogStatus Open(TransactionManager manager);

        /// <summary>
        /// Closes the transaction log if it is open.
        /// </summary>
        void Close();

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
        void Close(bool simulateCrash);

        /// <summary>
        /// Returns the orphaned transaction <see cref="Guid" />s discovered after
        /// <see cref="Open" /> returns <see cref="LogStatus.Recover" />.
        /// </summary>
        /// <returns>The list of transaction <see cref="Guid" />s.</returns>
        /// <remarks>
        /// This method returns only the IDs for valid, non-corrupted transaction
        /// operation logs.  It will delete any corrupted logs it finds.
        /// </remarks>
        List<Guid> GetOrphanTransactions();

        /// <summary>
        /// Opens an existing <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="transactionID">The operation's base transaction <see cref="Guid" />.</param>
        /// <returns>The <see cref="IOperationLog" /> instance.</returns>
        IOperationLog OpenOperationLog(Guid transactionID);

        /// <summary>
        /// Closes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        void CloseOperationLog(IOperationLog operationLog);

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
        IOperationLog CreateOperationLog(Guid transactionID);

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
        void CommitOperationLog(IOperationLog operationLog);

        /// <summary>
        /// Closes and deletes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <remarks>
        /// This is called after the all of the transactions have been applied to
        /// the underlying resource and the log is no longer necessary.
        /// </remarks>
        void RemoveOperationLog(IOperationLog operationLog);
    }
}
