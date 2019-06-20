//-----------------------------------------------------------------------------
// FILE:        InternalServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hosts a service internally within application process.

using System;
using System.ServiceProcess;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Hosts a service internally within application process.
    /// </summary>
    /// <remarks>
    /// This class is used to explicitly host a service within an application.
    /// The application is responsible for handling the service start/stop
    /// method calls.
    /// </remarks>
    public sealed class ApplicationServiceHost : IServiceHost
    {
        private IService service;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ApplicationServiceHost()
        {
        }

        //---------------------------------------------------------------------
        // IServiceHost implementations

        /// <summary>
        /// Initializes the service user interface.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="service">The service to associate with this instance.</param>
        /// <param name="logProvider">The optional system log provider (or <c>null</c>).</param>
        /// <param name="start"><c>true</c> to start the service.</param>
        public void Initialize(string[] args, IService service, ISysLogProvider logProvider, bool start)
        {
            if (logProvider != null)
                SysLog.LogProvider = logProvider;
            else
                SysLog.LogProvider = new NativeSysLogProvider(service.Name);

            this.service = service;
        }

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.Application; }
        }

        /// <summary>
        /// Returns the service instance managed by the host.
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
        }

        /// <summary>
        /// Writes the message passed to a log area of the user interface.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Log(string message)
        {
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
        }

        /// <summary>
        /// Called by a service in ServiceState.Shutdown mode when the last
        /// user disconnects from the service.  The service host will then
        /// typically call the service's <see cref="IService.Stop" /> method.
        /// </summary>
        /// <param name="service">The service completing shutdown.</param>
        public void OnShutdown(IService service)
        {
        }
    }
}
