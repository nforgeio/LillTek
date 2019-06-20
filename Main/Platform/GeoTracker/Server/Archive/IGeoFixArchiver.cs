//-----------------------------------------------------------------------------
// FILE:        IGeoFixArchiver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines the behavior of classes capable of archiving GeoFix
//              instances received by a GeoTracker server instance.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Defines the behavior of classes capable of archiving <see cref="GeoFix" /> 
    /// instances received by a GeoTracker server instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GeoTracker servers (via their <see cref="GeoTrackerNode" /> instance) will 
    /// instantiate a <see cref="IGeoFixArchiver" /> when the server starts.  The
    /// <see cref="Start" /> method will be called, passing the node instance as well
    /// as a <see cref="ArgCollection" /> holding archiver-specific parameters (such as
    /// database connection strings) from <see cref="GeoTrackerServerSettings"/>.<see cref="GeoTrackerServerSettings.GeoFixArchiverArgs" />.
    /// </para>
    /// <para>
    /// Once the archiver has started, the GeoTracker server will pass location fixes
    /// it receives to the <see cref="Archive" /> method, along with the ID of the entity
    /// being tracked.  The archiver implementation may persist the fix immediately or
    /// may choose to cache some number of fixes for more efficient archival sometime later.
    /// </para>
    /// <para>
    /// The <see cref="Stop" /> method will be called just before the GeoTracker server
    /// shuts down.  This gives the archiver a chance to flush any cached fixes and perform
    /// any necessary termination related activites.
    /// </para>
    /// <note>
    /// <see cref="IGeoFixArchiver" /> implementations must implement a public parameterless constructor.
    /// </note>
    /// <note>
    /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
    /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
    /// exceptions raised from calls to any of these methods.  Implementations should
    /// catch any exceptions thrown internally and log errors or warnings as necessary.
    /// </note>
    /// </remarks>
    public interface IGeoFixArchiver
    {
        /// <summary>
        /// Initalizes the fix archiver instance.
        /// </summary>
        /// <param name="node">The parent <see cref="GeoTrackerNode" /> instance.</param>
        /// <param name="args">
        /// Archiver-specific parameters (such as database connection strings) from 
        /// <see cref="GeoTrackerServerSettings"/>.<see cref="GeoTrackerServerSettings.GeoFixArchiverArgs" />
        /// </param>
        /// <remarks>
        /// <note>
        /// Implementations of this interface <b>must</b> implement a parameter-less default
        /// constructor.
        /// </note>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        void Start(GeoTrackerNode node, ArgCollection args);

        /// <summary>
        /// Archives a location fix for an entity.
        /// </summary>
        /// <param name="entityID">The entity identifier.</param>
        /// <param name="groupID">The group identifier or <c>null</c>.</param>
        /// <param name="fix">The location fix.</param>
        /// <remarks>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        void Archive(string entityID, string groupID, GeoFix fix);

        /// <summary>
        /// Performs any necessary shut down activites (flushing cached fixes, etc).
        /// </summary>
        /// <remarks>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        void Stop();
    }
}
