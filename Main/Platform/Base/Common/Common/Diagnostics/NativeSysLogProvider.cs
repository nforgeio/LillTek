//-----------------------------------------------------------------------------
// FILE:        NativeSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an ISysLogProvider for the Windows system event log.

using System;
using System.Diagnostics;
using System.Security;

// $todo(jeff.lill): 
// 
// I'm having trouble with obtaining write access to the security
// in some services when running under the LocalSystem account.
// For now, I'm going to just write security related events to the
// application event log as a work-around.

namespace LillTek.Common
{
    /// <summary>
    /// Implements an <see cref="ISysLogProvider" /> for the Windows system event log.
    /// </summary>
    public sealed class NativeSysLogProvider : SysLogProvider
    {
        //---------------------------------------------------------------------
        // Static members

        private static bool securityWarningLogged = false;

        /// <summary>
        /// Returns the native Windows event log source name to use for
        /// creating the security log for the named application source.
        /// </summary>
        /// <param name="source">The application event source.</param>
        /// <returns></returns>
        private static string GetSecuritySource(string source)
        {
            return source + ".Security";
        }

        /// <summary>
        /// Creates the event logs for the source passed.  This will
        /// typically be called during an application's installation
        /// because it requires elevated security rights.
        /// </summary>
        /// <param name="source">The application event source.</param>
        public static void CreateLogs(string source)
        {
            string securitySource = GetSecuritySource(source);

            if (!EventLog.SourceExists(source))
                EventLog.CreateEventSource(source, "application");

            // if (!EventLog.SourceExists(securitySource))
            //     EventLog.CreateEventSource(securitySource,"security");
        }

        /// <summary>
        /// Removes all of the event logs created for the source passed.
        /// This will typically be called during an application's uninstall
        /// process because it requires elevated security rights.
        /// </summary>
        /// <param name="source">The application event source.</param>
        public static void RemoveLogs(string source)
        {
            string securitySource = GetSecuritySource(source);
            string logName;

            if (EventLog.SourceExists(source))
            {

                logName = EventLog.LogNameFromSourceName(source, ".");
                EventLog.DeleteEventSource(source);
            }

            // if (EventLog.SourceExists(securitySource)) {
            // 
            //     logName = EventLog.LogNameFromSourceName(securitySource,".");
            //     EventLog.DeleteEventSource(securitySource);
            // }
        }

        //---------------------------------------------------------------------
        // Instance members

        private EventLog appEventLog;

        /// <summary>
        /// Constructs an <see cref="ISysLogProvider" /> for the system event log.
        /// </summary>
        /// <param name="source">The application event source.</param>
        /// <remarks>
        /// <para>
        /// The source parameter specifies the unique system wide name
        /// of the event source creating this instance.
        /// </para>
        /// <note>
        /// This constructor fails silently if the current process does not have 
        /// sufficient rights to intialize the event log.
        /// </note>
        /// </remarks>
        public NativeSysLogProvider(string source)
            : base()
        {
            string securitySource = GetSecuritySource(source);

            try
            {
                if (!EventLog.SourceExists(source))
                    EventLog.CreateEventSource(source, "application");

                appEventLog = new EventLog("application", ".", source);
            }
            catch (SecurityException)
            {
                if (!securityWarningLogged)
                {
                    // We're only going to log the security warning once per process.

                    SysLog.LogWarning("Current process does not have sufficient rights to access the native event log.");
                    securityWarningLogged = false;
                }

                appEventLog = null;
            }
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Appends the log entry passed to the output log.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            EventLogEntryType winEventType;

            if (appEventLog == null)
                return;

            switch (entry.Type)
            {
                case SysLogEntryType.Information:       winEventType = EventLogEntryType.Information; break;
                case SysLogEntryType.Warning:           winEventType = EventLogEntryType.Warning; break;
                case SysLogEntryType.Error:             winEventType = EventLogEntryType.Error; break;
                case SysLogEntryType.SecuritySuccess:   winEventType = EventLogEntryType.SuccessAudit; break;
                case SysLogEntryType.SecurityFailure:   winEventType = EventLogEntryType.FailureAudit; break;
                case SysLogEntryType.Exception:         winEventType = EventLogEntryType.Error; break;
                case SysLogEntryType.Trace:             winEventType = EventLogEntryType.Information; break;
                default:                                winEventType = EventLogEntryType.Information; break;
            }

            try
            {
                appEventLog.WriteEntry(entry.ToString(SysLogEntryFormat.ShowExtended), winEventType);
            }
            catch
            {
                // The event log can throw an exception when it's full.  Ignore these.
            }
        }
    }
}
