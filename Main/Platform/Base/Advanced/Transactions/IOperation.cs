//-----------------------------------------------------------------------------
// FILE:        IOperation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates an operation performed within the context of
//              a transaction.

using System;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Encapsulates an application-specific operation performed within 
    /// the context of a transaction.
    /// </summary>
    public interface IOperation
    {
        /// <summary>
        /// The human-readable description of the operation.  This is
        /// useful for tracing and debugging purposes.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The parent <see cref="ITransactedResource" /> implementation is <b>not</b> 
        /// responsible for persisting this value.  This is taken care of by the 
        /// <see cref="TransactionManager" />.
        /// </note>
        /// <para>
        /// Implementations may set this to <c>null</c> if they wish.
        /// </para>
        /// </remarks>
        string Description { get; set; }
    }
}
