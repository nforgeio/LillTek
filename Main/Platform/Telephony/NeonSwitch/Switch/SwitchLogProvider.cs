//-----------------------------------------------------------------------------
// FILE:        SwitchLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An ISysLogProvider implementation designed to integrate LillTek
//              logging with the NeonSwitch infrastructure.

using System;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// An <see cref="ISysLogProvider" /> implementation designed to integrate LillTek
    /// logging with the NeonSwitch infrastructure.
    /// </summary>
    public class SwitchLogProvider : ISysLogProvider
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Maps a LillTek log entry type into the corresponding FreeSWITCH log level.
        /// </summary>
        /// <param name="entryType">The LillTek log level.</param>
        /// <returns>The corresponding FreeSWITCH log level.</returns>
        private static SwitchLogLevel MapLogLevel(SysLogEntryType entryType)
        {

            switch (entryType)
            {

                case SysLogEntryType.Information:       return SwitchLogLevel.Info;
                case SysLogEntryType.Warning:           return SwitchLogLevel.Warning;
                case SysLogEntryType.Error:             return SwitchLogLevel.Error;
                case SysLogEntryType.SecuritySuccess:   return SwitchLogLevel.Notice;
                case SysLogEntryType.SecurityFailure:   return SwitchLogLevel.Warning;
                case SysLogEntryType.Exception:         return SwitchLogLevel.Error;
                case SysLogEntryType.Trace:             return SwitchLogLevel.Debug;
                default:                                return SwitchLogLevel.Info;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs a logger that logs directly to the IDE debug output.
        /// </summary>
        public SwitchLogProvider()
        {
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public void Flush()
        {
        }

        /// <summary>
        /// Logs a <see cref="SysLogEntry" />
        /// </summary>
        /// <param name="entry">The log entry.</param>
        public void Log(SysLogEntry entry)
        {
            Switch.Log(MapLogLevel(entry.Type), entry.Message);
        }

        /// <summary>
        /// Logs an informational entry.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogInformation(ISysLogEntryExtension extension, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Information), message);
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogWarning(ISysLogEntryExtension extension, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Warning), message);
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogError(ISysLogEntryExtension extension, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Error), message);
        }

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Exception), "Exception [{0}] : {1}", e.GetType().Name, e.Message);
        }

        /// <summary>
        /// Logs an exception with additional information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        /// <param name="message">The message.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Exception), "Exception [{0}] : {1}", e.GetType().Name, e.Message);
        }

        /// <summary>
        /// Logs a successful security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecuritySuccess(ISysLogEntryExtension extension, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.SecuritySuccess), message);
        }

        /// <summary>
        /// Logs a failed security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecurityFailure(ISysLogEntryExtension extension, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.SecurityFailure), message);
        }

        /// <summary>
        /// Logs debugging related information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="category">Used to group debugging related log entries.</param>
        /// <param name="message">The message.</param>
        public void Trace(ISysLogEntryExtension extension, string category, string message)
        {
            Switch.Log(MapLogLevel(SysLogEntryType.Trace), message);
        }
    }
}
