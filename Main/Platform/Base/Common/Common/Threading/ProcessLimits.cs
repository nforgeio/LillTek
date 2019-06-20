//-----------------------------------------------------------------------------
// FILE:        ProcessLimits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used by ProcessLimiter to describe the resource limits for a
//              process as well as the action to take when a limit is exceeded.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// Used by <see cref="ProcessLimiter" /> to describe the resource limits
    /// for a process as well as the action to take when a limit is exceeded.
    /// </summary>
    /// <remarks>
    /// This class currently supports only memory consumption limits.  It may make
    /// sense to support other limits in the future.
    /// </remarks>
    public class ProcessLimits
    {
        /// <summary>
        /// Constructs a process limit that terminates the process when a
        /// a limit is exceeded.
        /// </summary>
        /// <param name="process">The process being limited.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="process" /> is <c>null</c>.</exception>
        public ProcessLimits(Process process)
            : this(process, ProcessLimiter.DefaultLimitHandler)
        {
        }

        /// <summary>
        /// Constructs a process limit that invokes an action when a process limit
        /// a limit is exceeded.
        /// </summary>
        /// <param name="process">The process being limited.</param>
        /// <param name="onLimitExceeded">The action to be invoked when the limit is exceeded.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="process" /> or <paramref name="onLimitExceeded" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <paramref name="onLimitExceeded" /> will be invoked when the a process limit is exceeded.
        /// The first parameter will be a reference to the <see cref="ProcessLimits" /> instance
        /// and the second parameter will be a string suitable for logging that describes the problem.
        /// </remarks>
        public ProcessLimits(Process process, Action<ProcessLimits, string> onLimitExceeded)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            if (onLimitExceeded == null)
                throw new ArgumentNullException("onLimitExceeded");

            this.Process         = process;
            this.OnLimitExceeded = onLimitExceeded;
            this.PagedMemorySize = -1;
        }

        /// <summary>
        /// Returns the process being limited.
        /// </summary>
        public Process Process { get; private set; }

        /// <summary>
        /// The action to be invoked when a limit is exceeded.
        /// </summary>
        internal Action<ProcessLimits, string> OnLimitExceeded { get; private set; }

        /// <summary>
        /// The maximum amount of working set memory allowed for the process or
        /// -1 if memory utilization is to be unconstrained.
        /// </summary>
        public Int64 PagedMemorySize { get; set; }

        /// <summary>
        /// Used by <see cref="ProcessLimiter "/> to indicate that it has tried performing
        /// a garbage collection for the process the last time the memory limit was exceeded
        /// so it won't try another GC before terminating the process.
        /// </summary>
        internal bool GCPending { get; set; }
    }
}
