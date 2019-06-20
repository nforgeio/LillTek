//-----------------------------------------------------------------------------
// FILE:        QueryDisposition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Returns information about the disposition of a query
//              run by a ScriptRunner instance.

using System;
using System.IO;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Returns information about the disposition of a query run by a 
    /// ScriptRunner instance.
    /// </summary>
    public sealed class QueryDisposition
    {
        private Exception   exception;      // The query exception thrown or null on success
        private string      message;        // The exception message with adjusted line numbers (or null)

        /// <summary>
        /// Constructs a success QueryDisposition instance.
        /// </summary>
        internal QueryDisposition()
        {
            this.exception = null;
            this.message   = null;
        }

        /// <summary>
        /// Constructs an error QueryDisposition instance.
        /// </summary>
        /// <param name="exception">The query exception thrown or <c>null</c> on success.</param>
        /// <param name="message">The exception message with adjusted line numbers (or <c>null</c>).</param>
        internal QueryDisposition(Exception exception, string message)
        {
            this.exception = exception;
            this.message   = message;
        }

        /// <summary>
        /// Returns <c>true</c> if the query failed.
        /// </summary>
        public bool Failed
        {
            get { return exception != null; }
        }

        /// <summary>
        /// Returns the raw exception thrown for a failed query or <c>null</c> for
        /// a successful query.
        /// </summary>
        public Exception Exception
        {
            get { return exception; }
        }

        /// <summary>
        /// Returns a formatted error message with the errors thrown for
        /// a failed query or <c>null</c> for a successful query.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The error line numbers in this message are adjusted
        /// to refer to the offending line of the query in the original 
        /// consolidated script file.
        /// </note>
        /// </remarks>
        public string Message
        {
            get { return message; }
        }
    }
}
