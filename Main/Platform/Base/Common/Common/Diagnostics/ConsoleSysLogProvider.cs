//-----------------------------------------------------------------------------
// FILE:        ConsoleSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Console system log provider implementation.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Implements system log provider for DEBUG builds that logs
    /// entries to the console.
    /// </summary>
    public sealed class ConsoleSysLogProvider : SysLogProvider
    {
        //---------------------------------------------------------------------
        // Instance members

        private object  syncLock = new object();

        /// <summary>
        /// Constructs a logger that writes output to the console.
        /// </summary>
        public ConsoleSysLogProvider()
            : base()
        {
        }

        /// <summary>
        /// Appends the log entry passed to the in-memory list if in-memory
        /// logging is enabled, otherwise writes the entry to the output.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            Console.Write(entry.ToString());
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
        }
    }
}
