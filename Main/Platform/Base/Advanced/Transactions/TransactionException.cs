//-----------------------------------------------------------------------------
// FILE:        ITransactionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Indicates that a transaction error has occured.

using System;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Indicates that a transaction error has occured.
    /// </summary>
    public class TransactionException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        public TransactionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The error message format string.</param>
        /// <param name="args">The error message arguments.</param>
        public TransactionException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
