//-----------------------------------------------------------------------------
// FILE:        IService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a service.

using System;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Defines the behavior of a service.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Returns the unique name of the service.  Note that this name must
        /// conform to the limitations of a Win32 file name.  It should not include
        /// any special characters such as colons, forward slashes (/), or back
        /// slashes (\) etc.  The name may include periods.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns the display name of the service.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Returns human readable status text.  This will be called periodically
        /// by the service host user interface.
        /// </summary>
        string DisplayStatus { get; }

        /// <summary>
        /// Indicates how the service was started.
        /// </summary>
        StartAs StartedAs { get; }

        /// <summary>
        /// Returns the current state of the service.
        /// </summary>
        ServiceState State { get; }

        /// <summary>
        /// Loads/reloads the service's configuration settings.
        /// </summary>
        void Configure();

        /// <summary>
        /// Returns <c>true</c> if the service actually implements <see cref="Configure" />.
        /// </summary>
        bool IsConfigureImplemented { get; }

        /// <summary>
        /// Starts the service, associating it with the service host passed.
        /// </summary>
        /// <param name="serviceHost">The service user interface.</param>
        /// <param name="args">Command line arguments.</param>
        /// <remarks>
        /// <note>
        /// The method should ignore any command line arguments it doesn't 
        /// understand since these may have been targeted at the ServiceHost.
        /// </note>
        /// </remarks>
        void Start(IServiceHost serviceHost, string[] args);

        /// <summary>
        /// Begins a graceful shut down process by disallowing any new user
        /// connections and monitoring the users still using the system.
        /// Once the last user has disconnected, the service will call the
        /// associated service host's <see cref="IServiceHost.OnShutdown" /> method.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Stops the service immediately, terminating any user activity.
        /// </summary>
        void Stop();
    }
}
