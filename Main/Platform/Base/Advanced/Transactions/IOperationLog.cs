//-----------------------------------------------------------------------------
// FILE:        IOperationLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Abstracts access to the set of logged IOperation instances associated
//              with a base transaction to be used when recovering transactions.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Indicates whether an <see cref="IOperationLog" /> is an <see cref="Undo" /> or <see cref="Redo" /> log.
    /// </summary>
    public enum OperationLogMode
    {
        /// <summary>
        /// The log contains operations that need to be
        /// undone during a transaction recovery.
        /// </summary>
        Undo = 0,

        /// <summary>
        /// The log contains operations that need to be redone
        /// during a transaction recovery.
        /// </summary>
        Redo = 1
    }

    /// <summary>
    /// Abstracts access to the set of logged <see cref="IOperation" /> instances associated
    /// with a base transaction to be used when recovering transactions.
    /// </summary>
    public interface IOperationLog
    {
        /// <summary>
        /// Closes the operation log if it's open.
        /// </summary>
        void Close();

        /// <summary>
        /// The <see cref="Guid" /> for the base transaction associated with this log.
        /// </summary>
        Guid TransactionID { get; }

        /// <summary>
        /// Returns the <see cref="OperationLogMode" /> indicating whether the <see cref="IOperationLog" /> 
        /// is an <see cref="OperationLogMode.Undo" /> or <see cref="OperationLogMode.Redo" /> log.
        /// </summary>
        OperationLogMode Mode { get; }

        /// <summary>
        /// Returns the position of the next operation to be written to the log.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        ILogPosition Position { get; }

        /// <summary>
        /// Truncates the log to the position passed.
        /// </summary>
        /// <param name="position">The <see cref="ILogPosition" /> defining where the truncation should occur.</param>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// <para>
        /// This method is used in combination with the <see cref="Position" /> property to roll
        /// back operations within a base transaction.
        /// </para>
        /// </remarks>
        void Truncate(ILogPosition position);

        /// <summary>
        /// Returns the list of the <see cref="ILogPosition" />s for each operation in the log.
        /// </summary>
        /// <param name="reverse">Pass <c>true</c> to return the positions in the reverse order that they were appended to the log.</param>
        /// <returns>The operation position list.</returns>
        List<ILogPosition> GetPositions(bool reverse);

        /// <summary>
        /// Returns the set of <see cref="ILogPosition" />s for each operation from the end of the
        /// log to the <paramref name="position" /> passed, in the reverse order that the operations
        /// were added to the log.
        /// </summary>
        /// <param name="position">The limit position.</param>
        /// <returns>The operation position list.</returns>
        List<ILogPosition> GetPositionsTo(ILogPosition position);

        /// <summary>
        /// Reads the operation from the specified position in the log.
        /// </summary>
        /// <param name="resource">The parent <see cref="ITransactedResource" /> responsible for deserializing the operation.</param>
        /// <param name="position">See the <see cref="ILogPosition" />.</param>
        /// <returns>The <see cref="IOperation" /> read from the log.</returns>
        IOperation Read(ITransactedResource resource, ILogPosition position);

        /// <summary>
        /// Writes an <see cref="IOperation" /> to the log.
        /// </summary>
        /// <param name="resource">The parent <see cref="ITransactedResource" /> responsible for serializing the operation.</param>
        /// <param name="operation">The operation to be written.</param>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// </remarks>
        void Write(ITransactedResource resource, IOperation operation);
    }
}
