//-----------------------------------------------------------------------------
// FILE:        ServiceSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Log provider that writes to the service host.

using System;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Implements a custom <see cref="ISysLogProvider" /> that writes log entries to service's
    /// windows user interface as well as the native Windows event log.
    /// </summary>
    public sealed class ServiceSysLogProvider : SysLogProvider
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Configures the current application to write system log entries
        /// to the specified service host.
        /// </summary>
        /// <param name="serviceHost">The service host.</param>
        public static void SetDebugLog(IServiceHost serviceHost)
        {
            SysLog.LogProvider = new ServiceSysLogProvider(serviceHost);
        }

        //---------------------------------------------------------------------
        // Instance members

        private IServiceHost            serviceHost;
        private NativeSysLogProvider    nativeLogProvider;

        /// <summary>
        /// Constructs a log provider that writes entries to the specified service host.
        /// </summary>
        /// <param name="serviceHost">The service host.</param>
        public ServiceSysLogProvider(IServiceHost serviceHost)
            : base()
        {
            this.serviceHost       = serviceHost;
            this.nativeLogProvider = new NativeSysLogProvider(serviceHost.Service.Name);
        }

        /// <summary>
        /// Appends the log entry passed to the output logs.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            nativeLogProvider.Log(entry);
            serviceHost.Log(entry.ToString());
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
        }
    }
}
