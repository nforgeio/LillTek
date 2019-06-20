//-----------------------------------------------------------------------------
// FILE:        SqlSchemaMismatchException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown by SqlContext.VerifySchema() if the actual
//              database schema doesn't match what the application is expecting.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Diagnostics;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Exception thrown by <see cref="SqlContext.VerifySchema" /> if the actual
    /// database schema doesn't match what the application is expecting.
    /// </summary>
    public sealed class SqlSchemaMismatchException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        public SqlSchemaMismatchException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner <see cref="Exception" />.</param>
        public SqlSchemaMismatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public SqlSchemaMismatchException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
