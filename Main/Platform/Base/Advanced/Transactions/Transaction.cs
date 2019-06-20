//-----------------------------------------------------------------------------
// FILE:        Transaction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a transaction.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Provides applications with a mechanism to commit or roll back transactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transaction are typically started by calling a <see cref="ITransactedResource" />
    /// implementation's <b>BeginTransaction()</b> method.  This first call creates an
    /// internal <see cref="BaseTransaction" /> which manages a stack of <see cref="Transaction" />
    /// instances, the first of which returned as the <b>BeginTransaction()</b> result.
    /// </para>
    /// <para>
    /// Nested transactions can be created by calling the current <see cref="Transaction" />'s
    /// <see cref="BeginTransaction" /> method.  This creates a new <see cref="Transaction" />
    /// pushes it on the <see cref="BaseTransaction" />'s stack and returns it.
    /// </para>
    /// <para>
    /// Individual transactions can be committed or rolled back by calling <see cref="Commit" />
    /// or <see cref="Rollback" />.  <see cref="Transaction" /> implements <see cref="IDisposable" />
    /// and the <see cref="Dispose" /> implementation will roll back the transaction.  This makes
    /// it convenient to include transactions in <b>using</b> blocks that automatically roll back
    /// transactions when exceptions are thrown.
    /// </para>
    /// <note>
    /// The transaction committed or rolled back does not have to be on the top of the
    /// <see cref="BaseTransaction" /> stack.  When a transaction deeper within the stack
    /// is committed or rolled back, any transactions above it will first be committed or
    /// rolled back before committing or rolling back the original transaction.
    /// </note>
    /// <para>
    /// The <see cref="ID" /> property returns the base transaction <see cref="Guid" />,
    /// <see cref="IsOpen" /> returns <c>true</c> if the transaction is still open
    /// (that is, it has not yet been committed or rolled back).  <see cref="Manager" />
    /// returns the <see cref="TransactionManager" /> responsible for the transaction,
    /// <see cref="Base" /> returns the <see cref="BaseTransaction" /> whose stack holds
    /// this transaction, and <see cref="ResourceData" /> available for <see cref="ITransactedResource" />
    /// implementations for saving implementation specific information.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class Transaction : IDisposable
    {
        private const string NotOpenMsg = "Transaction is not open.";

        private object              syncLock;
        private BaseTransaction     transBase;
        private ILogPosition        position;
        private bool                isOpen;
        private object              resourceData;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="transBase">The parent <see cref="BaseTransaction" />.</param>
        /// <param name="position">The <see cref="ILogPosition" /> for the first <see cref="IOperation" /> for this transaction.</param>
        internal Transaction(BaseTransaction transBase, ILogPosition position)
        {

            this.syncLock     = transBase.Manager.SyncRoot;
            this.transBase    = transBase;
            this.position     = position;
            this.isOpen       = true;
            this.resourceData = null;
        }

        /// <summary>
        /// Returns the base transaction <see cref="Guid" />.
        /// </summary>
        public Guid ID
        {
            get { return transBase.ID; }
        }

        /// <summary>
        /// Returns <c>true</c> if the transaction is open.
        /// </summary>
        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>
        /// Available for use by <see cref="ITransactedResource" /> implementations to hold
        /// custom transaction information.
        /// </summary>
        public object ResourceData
        {
            get { return resourceData; }
            set { resourceData = value; }
        }

        /// <summary>
        /// Returns the <see cref="ILogPosition" /> of the first <see cref="IOperation" /> for this transaction.
        /// </summary>
        internal ILogPosition Position
        {
            get { return position; }
        }

        /// <summary>
        /// Appends an <see cref="IOperation" /> onto the end of the transaction's
        /// redo operation log.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" />.</param>
        /// <exception cref="TransactionException">Thrown if the transaction is not open.</exception>
        public void Log(IOperation operation)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                transBase.Log(operation);
            }
        }

        /// <summary>
        /// Initiates a nested transaction.
        /// </summary>
        /// <returns>The nested <see cref="Transaction" />.</returns>
        /// <exception cref="TransactionException">Thrown if the transaction is not open.</exception>
        /// <exception cref="TransactionException">Thrown if the transaction is not on the top of the stack.</exception>
        public Transaction BeginTransaction()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                if (transBase.Count == 0 || !object.ReferenceEquals(this, transBase.Peek()))
                    throw new TransactionException("This transaction is not on the top of the stack.");

                return transBase.BeginTransaction();
            }
        }

        /// <summary>
        /// Commits the transaction's changes.
        /// </summary>
        /// <exception cref="TransactionException">Thrown if the transaction is not open.</exception>
        public void Commit()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                transBase.Commit(this);
                isOpen = false;
            }
        }

        /// <summary>
        /// Rolls back the transaction's changes.
        /// </summary>
        /// <exception cref="TransactionException">Thrown if the transaction is not open.</exception>
        public void Rollback()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                transBase.Rollback(this);
                isOpen = false;
            }
        }

        /// <summary>
        /// Returns the <see cref="TransactionManager" /> managing this transaction.
        /// </summary>
        public TransactionManager Manager
        {
            get { return transBase.Manager; }
        }

        /// <summary>
        /// Returns the <see cref="BaseTransaction" /> managing this transaction.
        /// </summary>
        public BaseTransaction Base
        {
            get { return transBase; }
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The integer hash code.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals this instance.
        /// </summary>
        /// <param name="obj">The instance to be compared.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }

        //---------------------------------------------------------------------
        // IDisposable implementation.

        /// <summary>
        /// Releases any resources associated with the transaction if it
        /// is open, rolling back any partually completed operations
        /// against the associated resource.
        /// </summary>
        public void Dispose()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (isOpen)
                    Rollback();
            }
        }
    }
}
