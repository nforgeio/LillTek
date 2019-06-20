//-----------------------------------------------------------------------------
// FILE:        DebugSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Debug system log provider implementation.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Implements system log provider for DEBUG builds that logs
    /// entries to the diagnostics output.
    /// </summary>
    public sealed class DebugSysLogProvider : SysLogProvider
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Configures the current application instance with a debug log.
        /// </summary>
        /// <param name="inMemory"><c>true</c> to enable in-memory caching.</param>
        /// <param name="format">Log display format.</param>
        /// <param name="fileName">
        /// Pass as the file name where log entries are to be appended
        /// when Flush() is called or <c>null</c> to append these thes to the
        /// IDE debug output.
        /// </param>
        /// <remarks>
        /// This method will delete any existing log file if present.
        /// </remarks>
        public static void SetDebugLog(bool inMemory, SysLogEntryFormat format, string fileName)
        {
            if (fileName != null)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch
                {
                }
            }

            SysLog.LogProvider = new DebugSysLogProvider(inMemory, format, fileName);
        }

        //---------------------------------------------------------------------
        // Instance members

        private object              syncLock = new object();
        private SysLogEntryFormat   format;     // Log display format
        private bool                inMemory;   // True to log to memory
        private string              fileName;   // The log file name (or null)
        private SysLogEntry         head;       // Head of the in-memory log
        private SysLogEntry         tail;       // Tail of the in-memory log

        /// <summary>
        /// Constructs a logger that logs directly to the IDE debug output.
        /// </summary>
        public DebugSysLogProvider()
            : base()
        {
            this.format   = SysLogEntryFormat.AllButTime;
            this.inMemory = false;
            this.fileName = null;
        }

        /// <summary>
        /// Constructs a logger that caches the log in-memory and flushes
        /// the output to the IDE debug output or a file.
        /// </summary>
        /// <param name="inMemory"><c>true</c> to enable in-memory caching.</param>
        /// <param name="format">The log entry format.</param>
        /// <param name="fileName">
        /// Pass as the file name where log entries are to be appended
        /// when Flush() is called or <c>null</c> to append these thes to the
        /// IDE debug output.
        /// </param>
        public DebugSysLogProvider(bool inMemory, SysLogEntryFormat format, string fileName)
            : base()
        {
            this.format   = format;
            this.inMemory = inMemory;
            this.fileName = fileName;
            this.head     = null;
            this.tail     = null;
        }

        /// <summary>
        /// Appends the log entry passed to the in-memory list if in-memory
        /// logging is enabled, otherwise writes the entry to the output.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            if (!inMemory)
            {
                if (fileName == null)
                    Debug.Write(entry.ToString());
                else
                {
                    lock (syncLock)
                    {
                        FileStream  output;
                        byte[]      buf;

                        output = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None);
                        buf = Encoding.UTF8.GetBytes(entry.ToString(format));
                        output.Write(buf, 0, buf.Length);
                        output.Close();
                    }
                }

                return;
            }

            lock (syncLock)
            {
                if (head == null)
                    head = tail = entry;
                else
                    tail.Next = entry;
            }
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
            if (!inMemory)
                return;

            lock (syncLock)
            {
                if (fileName != null)
                {
                    FileStream      output;
                    SysLogEntry     entry;
                    byte[]          buf;

                    output = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None);

                    entry = head;
                    while (entry != null)
                    {
                        buf = Encoding.UTF8.GetBytes(entry.ToString(format));
                        output.Write(buf, 0, buf.Length);

                        entry = entry.Next;
                    }

                    output.Close();
                }
                else
                {
                    SysLogEntry entry;

                    entry = head;
                    while (entry != null)
                    {
                        Debug.Write(entry.ToString(format));
                        entry = entry.Next;
                    }
                }

                head = tail = null;
            }
        }
    }
}
