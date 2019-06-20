//-----------------------------------------------------------------------------
// FILE:        SysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An abstract class that implements most ISysLogProvider methods
//              to simplify the implenmtation of a custom log provider.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// An abstract class that implements most ISysLogProvider methods
    /// to simplify the implenmtation of a custom log provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses this as the base class for your custom log provider.  The
    /// class provides implementations for all of the <see cref="ISysLogProvider" />
    /// event logging methoods: <see cref="ISysLogProvider.LogInformation" />
    /// thru <see cref="ISysLogProvider.Trace" />.  Your class will need
    /// to implement <see cref="Flush()" /> and <see cref="Append(SysLogEntry)" />.
    /// </para>
    /// </remarks>
    public abstract class SysLogProvider : ISysLogProvider
    {
        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public abstract void Flush();

        /// <summary>
        /// Appends a <see cref="SysLogEntry" /> to the event log.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected abstract void Append(SysLogEntry entry);

        /// <summary>
        /// Logs a <see cref="SysLogEntry" />
        /// </summary>
        /// <param name="entry">The log entry.</param>
        public void Log(SysLogEntry entry)
        {
            Append(entry);
        }

        /// <summary>
        /// Logs an informational entry.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogInformation(ISysLogEntryExtension extension, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.Information, message));
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogWarning(ISysLogEntryExtension extension, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.Warning, message));
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogError(ISysLogEntryExtension extension, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.Error, message));
        }

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e)
        {
            Append(new SysLogEntry(extension, e, null));
        }

        /// <summary>
        /// Logs an exception with additional information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        /// <param name="message">The message.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e, string message)
        {
            Append(new SysLogEntry(extension, e, message));
        }

        /// <summary>
        /// Logs a successful security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecuritySuccess(ISysLogEntryExtension extension, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.SecuritySuccess, message));
        }

        /// <summary>
        /// Logs a failed security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecurityFailure(ISysLogEntryExtension extension, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.SecurityFailure, message));
        }

        /// <summary>
        /// Logs debugging related information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="category">Used to group debugging related log entries.</param>
        /// <param name="message">The message.</param>
        public void Trace(ISysLogEntryExtension extension, string category, string message)
        {
            Append(new SysLogEntry(extension, SysLogEntryType.Trace, category, message));
        }
    }
}
