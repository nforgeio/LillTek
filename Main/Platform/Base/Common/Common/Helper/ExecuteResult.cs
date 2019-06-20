//-----------------------------------------------------------------------------
// FILE:        ExecuteResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the process exit code and captured standard output from
//              a process execution.

using System;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Holds the process exit code and captured standard output from a process
    /// launched by <see cref="Helper.ExecuteCaptureStreams(string, string, TimeSpan?, Process)"/>.
    /// </summary>
    public class ExecuteResult
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal ExecuteResult()
        {
        }

        /// <summary>
        /// Returns the process exit code.
        /// </summary>
        public int ExitCode { get; internal set; }

        /// <summary>
        /// Returns the captured standard output stream from the process.
        /// </summary>
        public string StandardOutput { get; internal set; }

        /// <summary>
        /// Returns the captured standard error stream from the process.
        /// </summary>
        public string StandardError { get; internal set; }
    }
}
