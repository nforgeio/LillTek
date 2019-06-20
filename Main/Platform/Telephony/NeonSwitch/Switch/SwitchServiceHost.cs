//-----------------------------------------------------------------------------
// FILE:        SwitchServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An IServiceHost implementation designed to host LillTek based
//              services within a NeonSwitch instance.

using System;

using LillTek.Common;
using LillTek.Service;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// An <see cref="IServiceHost" /> implementation designed to host LillTek
    /// based services within a NeonSwitch instance.
    /// </summary>
    public class SwitchServiceHost : IServiceHost
    {
        private IService service;

        /// <summary>
        /// Initializes the service user interface.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="service">The service to associate with this instance.</param>
        /// <param name="logProvider">The optional system log provider (or <c>null</c>).</param>
        /// <param name="start"><c>true</c> to start the service.</param>
        /// <remarks>
        /// If a non-<c>null</c> <paramref name="logProvider" /> is passed then it will be
        /// setup as the global log provider.  If <c>null</c> is passed then the service
        /// host will initialize a  provider that logs events to the FreeSWITCH logging
        /// subsystem.
        /// </remarks>
        public void Initialize(string[] args, IService service, ISysLogProvider logProvider, bool start)
        {
            this.service = service;

            if (logProvider == null)
                SysLog.LogProvider = new SwitchLogProvider();

            if (start)
                Service.Start(this, args);
        }

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.NeonSwitch; }
        }

        /// <summary>
        /// Returns the service instance managed by the service host.
        /// </summary>
        public IService Service
        {
            get { return service; }
        }

        /// <summary>
        /// Sets the status text to the message built by formatting the arguments
        /// passed.  This uses the same formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void SetStatus(string format, params object[] args)
        {
            // This is NOP for NeonSwitch application.
        }

        /// <summary>
        /// Writes the message passed to a log area of the user interface.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Log(string message)
        {
            Switch.Log(SwitchLogLevel.Info, message);
        }

        /// <summary>
        /// Writes the  to the message built by formatting the arguments
        /// passed to the log area of the user interface.  This uses the same 
        /// formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void Log(string format, params object[] args)
        {
            Switch.Log(SwitchLogLevel.Info, format, args);
        }

        /// <summary>
        /// Called by a service in ServiceState.Shutdown mode when the last
        /// user disconnects from the service.  The service host will then
        /// typically call the service's <see cref="IService.Stop" /> method.
        /// </summary>
        /// <param name="service">The service completing shutdown.</param>
        public void OnShutdown(IService service)
        {
            service.Stop();
        }
    }
}
