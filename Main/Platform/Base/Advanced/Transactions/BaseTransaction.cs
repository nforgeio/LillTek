//-----------------------------------------------------------------------------
// FILE:        BaseTransaction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a base transaction including any nested transactions.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Implements a base transaction including any nested transactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ITransactedResource" />s main use of this class will be to append
    /// <see cref="IOperation" /> instances within resource modification methods using
    /// the <see cref="Log" /> method.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class BaseTransaction : StackArray<Transaction>
    {
        private const string EmptyMsg      = "Transaction stack is empty.";
        private const string NotPresentMsg = "The transaction being committed or rolled back is not part of the base transaction.";

        private object              syncLock;       // The sync root
        private Guid                id;             // The transaction ID;
        private TransactionManager  manager;        // The transaction manager
        private IOperationLog       operationLog;   // The operation log for this transaction
        private object              userData;       // Application specific information

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="id">The base transaction <see cref="Guid" />.</param>
        /// <param name="manager">The <see cref="TransactionManager" /> responsible for this transaction.</param>
        internal BaseTransaction(Guid id, TransactionManager manager)
            : base()
        {
            manager.Trace(0, "Begin BASE transaction", "ID=" + id.ToString());

            this.syncLock     = manager.SyncRoot;
            this.id           = id;
            this.manager      = manager;
            this.operationLog = manager.Log.CreateOperationLog(id);
            this.userData     = null;
        }

        /// <summary>
        /// Constructor available for unit testing.
        /// </summary>
        internal BaseTransaction(Stub param)
        {
            this.id = Helper.NewGuid();
        }

        /// <summary>
        /// Returns the base transaction <see cref="Guid" />.
        /// </summary>
        public Guid ID
        {
            get { return id; }
        }

        /// <summary>
        /// Returns the <see cref="TransactionManager" /> responsible for this base transaction.
        /// </summary>
        public TransactionManager Manager
        {
            get { return manager; }
        }

        /// <summary>
        /// Returns the <see cref="OperationLog" /> used for the base transaction.
        /// </summary>
        internal IOperationLog OperationLog
        {
            get { return operationLog; }
        }

        /// <summary>
        /// This property is available for application use.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The default value is <c>null</c>.
        /// </note>
        /// </remarks>
        public object UserData
        {
            get { return userData; }
            set { userData = value; }
        }

        /// <summary>
        /// Calls the <see cref="TransactionManager" /> responsble for this base transaction
        /// to construct a new <see cref="Transaction" /> instance pushing the transaction
        /// onto the base transaction's nesting stack.
        /// </summary>
        /// <returns>The <see cref="Transaction" /> created.</returns>
        internal Transaction BeginTransaction()
        {
            Transaction transaction;

            using (TimedLock.Lock(syncLock))
            {
                manager.Trace(0, string.Format("Begin NESTED transaction [{0}]", base.Count), "ID=" + id.ToString());

                transaction = new Transaction(this, operationLog.Position);
                base.Push(transaction);
                return transaction;
            }
        }

        /// <summary>
        /// Appends the operation to the end of the current transaction.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to be appended.</param>
        /// <exception cref="TransactionException">Thrown if the stack is empty.</exception>
        public void Log(IOperation operation)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (base.Count == 0)
                    throw new TransactionException(EmptyMsg);

                operationLog.Write(manager.Resource, operation);
            }
        }

        /// <summary>
        /// Commits the current transaction.  If this is the base transaction then
        /// the operation will be commited to the <see cref="ITransactedResource" />.
        /// </summary>
        /// <exception cref="TransactionException">Thrown if the stack is empty.</exception>
        internal void Commit()
        {
            Transaction transaction;

            using (TimedLock.Lock(syncLock))
            {
                if (base.Count == 0)
                    throw new TransactionException(EmptyMsg);

                transaction = base.Pop();

                if (base.Count == 0)
                {
                    manager.Trace(0, "Begin commit BASE", "ID=" + id.ToString());

                    // We're committing the base transaction so play the redo log
                    // to the resource and then delete the log when we're finished.

                    ITransactedResource resource = manager.Resource;
                    UpdateContext context = new UpdateContext(manager, false, true, false, ID);
                    List<ILogPosition> positions;

                    positions = operationLog.GetPositions(false);

                    if (resource.BeginRedo(context))
                    {
                        for (int i = 0; i < positions.Count; i++)
                        {
                            var operation = operationLog.Read(manager.Resource, positions[i]);

                            manager.Trace(0, "Redo: " + operation.Description, "ID=" + id.ToString());
                            resource.Redo(context, operation);
                        }
                    }

                    resource.EndRedo(context);
                    manager.EndTransaction(this);

                    manager.Trace(0, "End commit BASE", "ID=" + id.ToString());
                }
                else
                    manager.Trace(0, string.Format("Commit NESTED [{0}]", base.Count), "ID=" + id.ToString());
            }
        }

        /// <summary>
        /// Commits a specific transaction as well as any nested transactions.  If this is 
        /// the base transaction then the operation will be commited to the <see cref="ITransactedResource" />.
        /// </summary>
        /// <param name="transaction">The <see cref="Transaction" /> to be committed.</param>
        /// <exception cref="TransactionException">Thrown if the transaction being committed or rolled back is not part of the base transaction.</exception>
        internal void Commit(Transaction transaction)
        {
            using (TimedLock.Lock(syncLock))
            {
                // Commit all nested transactions.

                if (base.IndexOf(transaction) == -1)
                    throw new TransactionException(NotPresentMsg);

                while (!base.Peek().Equals(transaction))
                    base.Pop().Commit();

                // Commit the transaction.

                Commit();
            }
        }

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        /// <exception cref="TransactionException">Thrown if the stack is empty.</exception>
        internal void Rollback()
        {
            Transaction transaction;

            using (TimedLock.Lock(syncLock))
            {
                if (base.Count == 0)
                    throw new TransactionException(EmptyMsg);

                transaction = base.Pop();

                if (base.Count == 0)
                    manager.Trace(0, "Begin rollback BASE", "ID=" + id.ToString());
                else
                    manager.Trace(0, string.Format("Begin rollback NESTED [{0}]", base.Count), "ID=" + id.ToString());

                // Submit the operations being undone back to the
                // resource in the reverse order that they were
                // originally performed.

                ITransactedResource resource = manager.Resource;
                UpdateContext context = new UpdateContext(manager, false, false, true, ID);
                List<ILogPosition> positions;

                positions = operationLog.GetPositionsTo(transaction.Position);

                if (resource.BeginUndo(context))
                {
                    for (int i = 0; i < positions.Count; i++)
                    {
                        var operation = operationLog.Read(manager.Resource, positions[i]);

                        manager.Trace(0, "Undo: " + operation.Description, "ID=" + id.ToString());
                        resource.Undo(context, operation);
                    }
                }

                resource.EndUndo(context);

                if (base.Count == 0)
                {
                    manager.EndTransaction(this);
                    manager.Trace(0, "End rollback BASE", "ID=" + id.ToString());
                }
                else
                {
                    operationLog.Truncate(transaction.Position);
                    manager.Trace(0, string.Format("End rollback NESTED [{0}]", base.Count), "ID=" + id.ToString());
                }
            }
        }

        /// <summary>
        /// Rolls a specific transaction back.
        /// </summary>
        /// <param name="transaction">The <see cref="Transaction" /> to be rolled back.</param>
        /// <exception cref="TransactionException">Thrown if the transaction being committed or rolled back is not part of the base transaction.</exception>
        internal void Rollback(Transaction transaction)
        {
            using (TimedLock.Lock(syncLock))
            {
                // Roll back nested transactions

                if (base.IndexOf(transaction) == -1)
                    throw new TransactionException(NotPresentMsg);

                while (!base.Peek().Equals(transaction))
                    base.Peek().Rollback();

                // Rollback the transaction.

                Rollback();
            }
        }

        /// <summary>
        /// Rolls back all transactions nested within the base transaction.
        /// </summary>
        public void RollbackAll()
        {
            while (base.Count > 0)
                Rollback();
        }
    }
}
