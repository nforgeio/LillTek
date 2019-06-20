//-----------------------------------------------------------------------------
// FILE:        IServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the basic behavior of a service handler.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the basic behavior of a service handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A service handler is essentially a class that implements
    /// handlers for one or more logical or physical message endpoints.
    /// A service handler is typically associated with a message
    /// router instance before (or just after) the router is started
    /// via a call to <see cref="Start" />.  This method performs any
    /// necessary initialization including registering the class'
    /// message handlers with the router's message dispatcher.
    /// </para>
    /// <para>
    /// Service handlers will typically specify abstract endpoints for
    /// their message handlers so these endpoints can be reconfigured
    /// using the <b>MsgRouter.AbstractMap</b> settings.  Although doing
    /// this is probably the best practice, fixed logical and even
    /// physical endpoints can be registered by service handlers.
    /// </para>
    /// <para>
    /// The <see cref="Stop" /> method is designed to immediately terminate
    /// any all activities being performed by the service handler on
    /// behalf of clients and the <see cref="Shutdown" /> method is designed
    /// to gracefully terminate these activities by not allowing any new
    /// client requests to be processed but to let existing requests to
    /// complete.  <see cref="PendingCount" /> returns the current number
    /// of pending client requests.
    /// </para>
    /// <para>
    /// Services can use <see cref="Shutdown" /> and <see cref="PendingCount" />
    /// to implement a graceful shutdown.  This will typically be performed
    /// by first calling <see cref="Shutdown" /> and then waiting any pending
    /// client requests to be completed by polling <see cref="PendingCount" />,
    /// waiting for this to return 0.  Then <see cref="Stop" /> will be called.
    /// Robust services may limit the amount of time they'll poll <see cref="PendingCount" />
    /// and eventually give up and call <see cref="Stop" /> if it looks like
    /// one or more client transactions has hung.
    /// </para>
    /// <para>
    /// Although, <see cref="Stop" />, <see cref="Shutdown" />, and 
    /// <see cref="PendingCount" /> must be present in classes implementing
    /// this interface, it's not required that these members actually do
    /// anything (it's perfectly reasonable for the methods to do nothing
    /// and for <see cref="PendingCount" /> to return zero.
    /// </para>
    /// </remarks>
    public interface IServiceHandler
    {
        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix (or <c>null</c> for the service's default prefix.)</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix);

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        void Stop();

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        int PendingCount { get; }
    }
}
