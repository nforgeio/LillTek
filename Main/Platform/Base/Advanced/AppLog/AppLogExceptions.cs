//-----------------------------------------------------------------------------
// FILE:        AppLogExceptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the application log exceptions.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Base class for the application log exceptions.
    /// </summary>
    public class LogException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The exception message text.</param>
        public LogException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Indicates that the log file is corrupted.
    /// </summary>
    public sealed class LogCorruptedException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public LogCorruptedException()
            : base("Corrupted log file.")
        {
        }
    }

    /// <summary>
    /// Indicates that the log file has a format version that is not
    /// supported by the current class.
    /// </summary>
    public sealed class LogVersionException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public LogVersionException()
            : base("Log file's format version is not supported by this implementation.")
        {
        }
    }

    /// <summary>
    /// Indicates that the log file is closed.
    /// </summary>
    public sealed class LogClosedException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public LogClosedException()
            : base("Log is closed.")
        {
        }
    }

    /// <summary>
    /// Indicates that another <see cref="AppLog" /> has already opened the
    /// requested application log for the requested access.
    /// </summary>
    public sealed class LogLockedException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public LogLockedException()
            : base("Another AppLog instance has already opened this log.")
        {
        }
    }

    /// <summary>
    /// Indicates that a operation was attempted on an application log
    /// cannot support due to the mode (reader or writer) by which it was opened.
    /// </summary>
    public sealed class LogAccessException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="readMode">Indicates whether the log was opened for reading or writing.</param>
        public LogAccessException(bool readMode)
            : base(readMode ? "Operation not available for reader logs." : "Operation not available for writer logs.")
        {
        }
    }

    /// <summary>
    /// Indicates that an invalid position string was passed to an
    /// appliction log.
    /// </summary>
    public sealed class LogPositionException : LogException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public LogPositionException()
            : base("Invalid position string.")
        {
        }
    }
}
