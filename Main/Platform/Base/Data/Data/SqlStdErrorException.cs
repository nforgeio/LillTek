//-----------------------------------------------------------------------------
// FILE:        SqlStdErrorException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a generic standard error exception.

using System;
using System.Data;
using System.Data.SqlClient;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Implements a generic standard error exception.
    /// </summary>
    public sealed class SqlStdErrorException : ApplicationException
    {
        private int         reason;
        private string      sproc;

        /// <summary>
        /// Constructs an exception from an error reason code and message string.
        /// </summary>
        /// <param name="reason">The error reason code.</param>
        /// <param name="sproc">The stored procedure name.</param>
        /// <param name="message">The message.</param>
        public SqlStdErrorException(int reason, string sproc, string message)
            : base(message)
        {
            this.reason = reason;
            this.sproc  = sproc;
        }

        /// <summary>
        /// Identifies the reason behind the exception.
        /// </summary>
        public int Reason
        {
            get { return reason; }
        }

        /// <summary>
        /// Returns the name of the stored procedure that generated the
        /// error (or <c>null</c>).
        /// </summary>
        public string SProc
        {
            get { return sproc; }
        }
    }
}
