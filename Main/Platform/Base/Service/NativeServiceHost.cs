//-----------------------------------------------------------------------------
// FILE:        NativeServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hosts a service as a native Windows service.

using System;
using System.ServiceProcess;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Hosts a service as a native Windows service.
    /// </summary>
    internal sealed class NativeServiceHost : System.ServiceProcess.ServiceBase, IServiceHost
    {
        private IService service;

        //---------------------------------------------------------------------
        // ServiceBase overrides

        /// <summary>
        /// Starts the service.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            try
            {
                service.Start(this, args);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Stop this service.
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                service.Stop();
                SysLog.Flush();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
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
            {
                SysLog.LogProvider = logProvider;
            }
            else
            {
                SysLog.LogProvider = new NativeSysLogProvider(service.Name);
            }

            this.service = service;
        }

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.Native; }
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
            try
            {
                new ServiceController(service.Name).Stop();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
