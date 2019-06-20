//-----------------------------------------------------------------------------
// FILE:        ITransactionContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an implemention combining the concepts of a connection to 
//              a database with transaction behavior.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

namespace LillTek.Common
{
    /// <summary>
    /// Describes an implemention combining the concepts of a connection to 
    /// a database with transaction behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances of this interface map to a database connection that
    /// supports transacted operations.
    /// </para>
    /// <para>
    /// Transaction support is integrated into the LillTek.Data.SqlContext implementation.
    /// Simply call BeginTransaction() to start a transaction and Commit()
    /// or RollBack() to complete it.  Transactions may be nested.  Note that
    /// instances automatically handle the release of the transaction resources 
    /// when a transaction is completed or the context is closed.
    /// </para>
    /// </remarks>
    public interface ITransactionContext : IDisposable
    {
        /// <summary>
        /// Closes the database connection.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this on a closed context.
        /// </note>
        /// </remarks>
        /// <remarks>
        /// If there are any transactions pending on this context, the
        /// method will roll them back and then throw an InvalidOperationException.
        /// </remarks>
        void Close();

        /// <summary>
        /// Initiates a database transaction.
        /// </summary>
        /// <param name="iso">The isolation level to use.</param>
        /// <remarks>
        /// <para>
        /// Database transactions may be nested.  This class implements this
        /// via save points.  Note that every call to BeginTransaction()
        /// must be matched with a call to Commit() or Rollback().
        /// </para>
        /// <note>
        /// Note that the isolation level will be ignored for nested
        /// transactions.
        /// </note>
        /// </remarks>
        void BeginTransaction(IsolationLevel iso);

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        void Rollback();

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        void Commit();
    }
}
