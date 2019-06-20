//-----------------------------------------------------------------------------
// FILE:        ISysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a system log provider.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the behavior of a system log provider.
    /// </summary>
    public interface ISysLogProvider
    {
        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        void Flush();

        /// <summary>
        /// Logs a <see cref="SysLogEntry" />
        /// </summary>
        /// <param name="entry">The log entry.</param>
        void Log(SysLogEntry entry);

        /// <summary>
        /// Logs an informational entry.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        void LogInformation(ISysLogEntryExtension extension, string message);

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        void LogWarning(ISysLogEntryExtension extension, string message);

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        void LogError(ISysLogEntryExtension extension, string message);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        void LogException(ISysLogEntryExtension extension, Exception e);

        /// <summary>
        /// Logs an exception with additional information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        /// <param name="message">The message.</param>
        void LogException(ISysLogEntryExtension extension, Exception e, string message);

        /// <summary>
        /// Logs a successful security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        void LogSecuritySuccess(ISysLogEntryExtension extension, string message);

        /// <summary>
        /// Logs a failed security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        void LogSecurityFailure(ISysLogEntryExtension extension, string message);

        /// <summary>
        /// Logs debugging related information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="category">Used to group debugging related log entries.</param>
        /// <param name="message">The message.</param>
        void Trace(ISysLogEntryExtension extension, string category, string message);
    }
}
