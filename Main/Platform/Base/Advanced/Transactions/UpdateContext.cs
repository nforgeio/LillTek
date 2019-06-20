//-----------------------------------------------------------------------------
// FILE:        UpdateContext.cs
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
    /// Global state communicated to <see cref="ITransactedResource" /> implementations
    /// while committing, rolling back, or recovering transactions.
    /// </summary>
    public sealed class UpdateContext
    {

        private TransactionManager  manager;
        private bool                recovery;
        private bool                commit;
        private bool                rollback;
        private Guid                transactionID;
        private object              state;

        /// <summary>
        /// The <see cref="TransactionManager" /> performing the operation.
        /// </summary>
        public TransactionManager Manager
        {
            get { return manager; }
        }

        /// <summary>
        /// Set to <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of recovering transactions.
        /// </summary>
        public bool Recovery
        {
            get { return recovery; }
        }

        /// <summary>
        /// Set to <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of committing a transaction.
        /// </summary>
        public bool Commit
        {
            get { return commit; }
            internal set { commit = value; }
        }

        /// <summary>
        /// Set to <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of rolling back a transaction.
        /// </summary>
        public bool Rollback
        {
            get { return rollback; }
            internal set { rollback = value; }
        }

        /// <summary>
        /// The <see cref="Guid" /> of the transaction being committed or rolled back
        /// or <see cref="Guid.Empty" /> for recovery operations.
        /// </summary>
        public Guid TransactionID
        {
            get { return transactionID; }
            internal set { transactionID = value; }
        }

        /// <summary>
        /// This property is available so that the <see cref="ITransactedResource" />
        /// implementation can save its own state.
        /// </summary>
        public object State
        {
            get { return state; }
            set { state = value; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="manager">The <see cref="TransactionManager" /> handling the operation.</param>
        /// <param name="recovery">
        /// Pass as <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of recovering transactions.
        /// </param>
        /// <param name="commit">
        /// Pass as <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of committing a transaction.
        /// </param>
        /// <param name="rollback">
        /// Pass as <c>true</c> if the <see cref="TransactionManager" /> is in the process
        /// of rolling back a transaction.
        /// </param>
        /// <param name="transactionID">
        /// Pass as the <see cref="Guid" /> of the transaction being committed or rolled back
        /// or <see cref="Guid.Empty" /> for recovery operations.
        /// </param>
        internal UpdateContext(TransactionManager manager, bool recovery, bool commit, bool rollback, Guid transactionID)
        {

            this.manager       = manager;
            this.recovery      = recovery;
            this.commit        = commit;
            this.rollback      = rollback;
            this.transactionID = transactionID;
        }
    }
}
