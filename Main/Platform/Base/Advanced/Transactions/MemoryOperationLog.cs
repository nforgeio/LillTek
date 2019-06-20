//-----------------------------------------------------------------------------
// FILE:        MemoryOperationLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The IOperationLog implementation for in-memory transactions.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// The <see cref="IOperationLog" /> implementation for in-memory transactions.
    /// </summary>
    internal sealed class MemoryOperationLog : IOperationLog
    {
        private const string NotOpenMsg = "MemoryOperationLog is closed.";

        private bool                isOpen;
        private object              syncLock;
        private OperationLogMode    mode;
        private List<IOperation>    operations;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="syncLock">The thread synchronization object.</param>
        /// <param name="mode">The log's current <see cref="OperationLogMode" />.</param>
        internal MemoryOperationLog(object syncLock, OperationLogMode mode)
        {
            this.syncLock   = syncLock;
            this.isOpen     = true;
            this.mode       = mode;
            this.operations = new List<IOperation>();
        }

        /// <summary>
        /// Closes the operation log if it's open.
        /// </summary>
        public void Close()
        {
        }

        /// <summary>
        /// The <see cref="Guid" /> for the base transaction associated with this log.
        /// </summary>
        public Guid TransactionID
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!isOpen)
                        throw new TransactionException(NotOpenMsg);

                    return Guid.Empty;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="OperationLogMode" /> indicating whether the <see cref="IOperationLog" /> 
        /// is an <see cref="OperationLogMode.Undo" /> or <see cref="OperationLogMode.Redo" /> log.
        /// </summary>
        public OperationLogMode Mode
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!isOpen)
                        throw new TransactionException(NotOpenMsg);

                    return mode;
                }
            }

            internal set
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!isOpen)
                        throw new TransactionException(NotOpenMsg);

                    mode = value;
                }
            }
        }

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
        public ILogPosition Position
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!isOpen)
                        throw new TransactionException(NotOpenMsg);

                    return new MemoryLogPosition(operations.Count);
                }
            }
        }

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
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        public void Truncate(ILogPosition position)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (mode != OperationLogMode.Undo)
                    throw new TransactionException("Write is available only when the log is in UNDO mode.");

                var pos = (MemoryLogPosition)position;

                if (pos.Index < 0 || pos.Index > operations.Count)
                    throw new TransactionException("Invalid operation log position [length={0} pos={1}].", operations.Count, pos.Index);

                operations.RemoveRange(pos.Index, operations.Count - pos.Index);
            }
        }

        /// <summary>
        /// Returns the list of the <see cref="ILogPosition" />s with each operation in the log.
        /// </summary>
        /// <param name="reverse">Pass <c>true</c> to return the positions in the reverse order that they were appended to the log.</param>
        /// <returns>The operation position list.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public List<ILogPosition> GetPositions(bool reverse)
        {
            List<ILogPosition> positions;

            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                positions = new List<ILogPosition>(operations.Count);

                if (reverse)
                {
                    for (int i = operations.Count - 1; i >= 0; i--)
                        positions.Add(new MemoryLogPosition(i));
                }
                else
                {
                    for (int i = 0; i < operations.Count; i++)
                        positions.Add(new MemoryLogPosition(i));
                }

                return positions;
            }
        }

        /// <summary>
        /// Returns the set of <see cref="ILogPosition" />s for each operation from the end of the
        /// log to the <paramref name="position" /> passed, in the reverse order that the operations
        /// were added to the log.
        /// </summary>
        /// <param name="position">The limit position.</param>
        /// <returns>The operation position list.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public List<ILogPosition> GetPositionsTo(ILogPosition position)
        {
            int                 index = ((MemoryLogPosition)position).Index;
            List<ILogPosition>  positions;

            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                positions = new List<ILogPosition>(operations.Count - index);
                for (int i = operations.Count - 1; i >= index; i--)
                    positions.Add(new MemoryLogPosition(i));

                return positions;
            }
        }

        /// <summary>
        /// Reads the operation from the specified position in the log.
        /// </summary>
        /// <param name="resource">The parent <see cref="ITransactedResource" /> responsible for deserializing the operation.</param>
        /// <param name="position">See the <see cref="ILogPosition" />.</param>
        /// <returns>The <see cref="IOperation" /> read from the log.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public IOperation Read(ITransactedResource resource, ILogPosition position)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                return operations[((MemoryLogPosition)position).Index];
            }
        }

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
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        public void Write(ITransactedResource resource, IOperation operation)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (mode != OperationLogMode.Undo)
                    throw new TransactionException("Write is available only when the log is in UNDO mode.");

                operations.Add(operation);
            }
        }
    }
}
