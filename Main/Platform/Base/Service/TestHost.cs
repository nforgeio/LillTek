//-----------------------------------------------------------------------------
// FILE:        TestHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Class suitable for hosting a service within test suites.

using System;
using System.ServiceProcess;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// This class is suitable for hosting a service within test suites.
    /// </summary>
    public sealed class TestHost : IServiceHost
    {
        /// <summary>
        /// Raised when the service has completed its shut down sequence.
        /// </summary>
        public event MethodDelegate ShutdownEvent;

        private string[]    args;
        private IService    service;

        /// <summary>
        /// Starts the associated service.
        /// </summary>
        public void Start()
        {
            service.Start(this, args);
        }

        /// <summary>
        /// Stops the associated service.
        /// </summary>
        public void Stop()
        {
            service.Stop();
        }

        /// <summary>
        /// Initiates shut down on the associated service.
        /// </summary>
        public void Shutdown()
        {
            service.Shutdown();
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
            this.args    = args;
            this.service = service;

            if (start)
            {
                Start();
            }
        }

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.Console; }
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
        /// typically call the service's Stop() method.
        /// </summary>
        /// <param name="service">The service completing shutdown.</param>
        public void OnShutdown(IService service)
        {
            ShutdownEvent();
            service.Stop();
        }
    }
}
