//-----------------------------------------------------------------------------
// FILE:        StdErrorCreateDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Merges the .NET concepts of a SqlConnection and SqlTransaction.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Delegate called by <see cref="SqlContext" /> to construct the
    /// appropriate exception instance for an error code returned
    /// during query processing.
    /// </summary>
    /// <param name="code">The code returned by the database sproc.</param>
    /// <param name="sproc">The stored procedure name.</param>
    /// <param name="message">The error message.</param>
    public delegate Exception StdErrorCreateDelegate(int code, string sproc, string message);
}
