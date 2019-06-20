//-----------------------------------------------------------------------------
// FILE:        IServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a service host.

using System;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Defines the behavior of a service host which provides the connection
    /// between a service instance and the Windows forms UI or native Windows
    /// service hosting the service.
    /// </summary>
    public interface IServiceHost
    {
        /// <summary>
        /// Initializes the service user interface.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="service">The service to associate with this instance.</param>
        /// <param name="logProvider">The optional system log provider (or <c>null</c>).</param>
        /// <param name="start"><c>true</c> to start the service.</param>
        void Initialize(string[] args, IService service, ISysLogProvider logProvider, bool start);

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        StartAs StartedAs { get; }

        /// <summary>
        /// Returns the service instance managed by the service host.
        /// </summary>
        IService Service { get; }

        /// <summary>
        /// Sets the status text to the message built by formatting the arguments
        /// passed.  This uses the same formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        void SetStatus(string format, params object[] args);

        /// <summary>
        /// Writes the message passed to a log area of the user interface.
        /// </summary>
        /// <param name="message">The message.</param>
        void Log(string message);

        /// <summary>
        /// Writes the  to the message built by formatting the arguments
        /// passed to the log area of the user interface.  This uses the same 
        /// formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        void Log(string format, params object[] args);

        /// <summary>
        /// Called by a service in ServiceState.Shutdown mode when the last
        /// user disconnects from the service.  The service host will then
        /// typically call the service's <see cref="IService.Stop" /> method.
        /// </summary>
        /// <param name="service">The service completing shutdown.</param>
        void OnShutdown(IService service);
    }
}
