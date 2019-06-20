//-----------------------------------------------------------------------------
// FILE:        ProcessLimiter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enforces limits on the resources consumed by a process by via a notification
//              or by forcibly terminating the process.  This class currently supports limiting
//              memory usage.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// Enforces limits on the resources consumed by processes by via a notification
    /// or by forcibly terminating the process.  This class currently supports limiting
    /// memory usage.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    public static class ProcessLimiter
    {
        private static TimeSpan             defPollTime         = TimeSpan.FromMinutes(1);
        private static TimeSpan             defLogFlushInterval = TimeSpan.FromMinutes(1);
        private static object               syncLock            = new object();
        private static List<ProcessLimits>  limits              = new List<ProcessLimits>();
        private static Thread               pollThread          = null;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ProcessLimiter()
        {
            PollInterval     = defPollTime;
            LogFlushInterval = defLogFlushInterval;
        }

        /// <summary>
        /// The interval at which the class polls processes for resource consumption.
        /// This defaults to 1 minute by may be adjusted by the application.
        /// </summary>
        public static TimeSpan PollInterval { get; set; }

        /// <summary>
        /// The interval wait after a process has exceeded its limits and an error
        /// has been logged before the process is terminated.  This time gives the
        /// logging subsystem a chance to persist the error.  This defaults to
        /// 1 minute.
        /// </summary>
        public static TimeSpan LogFlushInterval { get; set; }

        /// <summary>
        /// Adds a new process limitation to be monitored.
        /// </summary>
        /// <param name="processLimits">The process limitation information.</param>
        /// <remarks>
        /// This method silently ignores the addition of new limits for a process
        /// that is already being monitored.
        /// </remarks>
        public static void Add(ProcessLimits processLimits)
        {
            lock (syncLock)
            {
                foreach (var limit in limits)
                {
                    if (limit.Process == processLimits.Process)
                        return;     // We already have a limit for this process.
                }

                limits.Add(processLimits);

                // Start the polling thread if it is not already running.

                if (pollThread == null)
                {

                    pollThread      = new Thread(new ThreadStart(PollLimits));
                    pollThread.Name = "LillTek-ProcessLimiter";
                    pollThread.Start();
                }
            }
        }

        /// <summary>
        /// Stops monitoring a process for limits.
        /// </summary>
        /// <param name="process">The process.</param>
        public static void Remove(Process process)
        {
            lock (syncLock)
            {
                limits.Remove(limit => limit.Process == process);

                // Abort the polling thread if there are no more processes.

                if (limits.Count == 0)
                {
                    if (pollThread != null)
                    {
                        pollThread.Abort();
                        pollThread = null;
                    }
                }
            }
        }

        /// <summary>
        /// Stops monitoring a process for limits.
        /// </summary>
        /// <param name="processLimits">The limitation information.</param>
        public static void Remove(ProcessLimits processLimits)
        {
            Remove(processLimits.Process);
        }

        /// <summary>
        /// Remove all process limits.
        /// </summary>
        public static void RemoveAll()
        {
            lock (syncLock)
            {
                limits.Clear();

                if (pollThread != null)
                {
                    pollThread.Abort();
                    pollThread = null;
                }
            }
        }

        /// <summary>
        /// Restores the process limiter to its default state.
        /// </summary>
        public static void Reset()
        {
            RemoveAll();

            PollInterval = defPollTime;
            LogFlushInterval = defLogFlushInterval;
        }

        /// <summary>
        /// The default process limit handler.
        /// </summary>
        /// <param name="limits">The process limits.</param>
        /// <param name="message">The error message.</param>
        internal static void DefaultLimitHandler(ProcessLimits limits, string message)
        {
            // Add diagnostic information about each thread in the process,
            // including the stack trace, to the error message.

            // $todo(jeff.lill):
            //
            // There isn't a way to get the managed threads for the current
            // AppDomain so I'm going to punt on the additional diagnostics
            // for now.
            //
            // One place to look is the Managed Stack Explorer project
            // on CodePlex.  This may have a low-level way of doing this.

            // We're going to log the error and then give the logging
            // system some additional time to flush it to persistent 
            // storage before terminating the process.

            SysLog.LogError(message);
            SysLog.Flush();
            Thread.Sleep(LogFlushInterval);

            limits.Process.Kill();
        }

        /// <summary>
        /// Background thread handler used for polling for limit voliations.
        /// </summary>
        private static void PollLimits()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(PollInterval);

                    lock (syncLock)
                    {
                        // Remove any records for processes that have exited.

                        limits.Remove(limit => limit.Process.HasExited);

                        // Verify the limits.

                        foreach (var limit in limits)
                        {
                        retry:

                            limit.Process.Refresh();

                            var pagedMemorySize = limit.Process.PagedMemorySize64;
                            var oneMB = 1024 * 1024;

                            if (limit.PagedMemorySize != -1 && pagedMemorySize > limit.PagedMemorySize)
                            {
                                // If the offending process is the current process, then force a garbage collection
                                // as a last chance to reduce the memory footprint before terminating the process.

                                if (limit.Process == Process.GetCurrentProcess() || !limit.GCPending)
                                {
                                    SysLog.LogWarning("Process [{0}: {1}] is using [{2}] MB working set memory exceeding the [{3}] MB limit.\r\n\r\nAttempting a garbage collection.",
                                                      limit.Process.Id, limit.Process.ProcessName, pagedMemorySize / oneMB, limit.PagedMemorySize / oneMB);

                                    limit.GCPending = true;
                                    GC.Collect();
                                    Thread.Sleep(PollInterval);
                                    goto retry;
                                }

                                var message = string.Format("Process [{0}: {1}] is using [{2}] MB working set memory exceeding the [{3}] MB limit.\r\n\r\nThe process will be terminated automatically.",
                                                            limit.Process.Id, limit.Process.ProcessName, pagedMemorySize / oneMB, limit.PagedMemorySize / oneMB);

                                limit.OnLimitExceeded(limit, message);
                            }
                            else
                                limit.GCPending = false;    // Reset this
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Intentionally ignoring this.
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                pollThread = null;
            }
        }
    }
}
