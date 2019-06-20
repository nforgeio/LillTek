//-----------------------------------------------------------------------------
// FILE:        NullGeoFixArchiver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a do-nothing IGeoFixArchiver.

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
    /// Implements a do-nothing <see cref="IGeoFixArchiver" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This archiver is used for situations where the archival of location
    /// fixes is not required.
    /// </para>
    /// </remarks>
    public class NullGeoFixArchiver : IGeoFixArchiver
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public NullGeoFixArchiver()
        {
        }

        /// <summary>
        /// Initalizes the fix archiver instance.
        /// </summary>
        /// <param name="node">The parent <see cref="GeoTrackerNode" /> instance.</param>
        /// <param name="args">This implementation defines no special arguments.</param>
        /// <remarks>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        public void Start(GeoTrackerNode node, ArgCollection args)
        {
        }

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
        public void Archive(string entityID, string groupID, GeoFix fix)
        {
        }

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
        public void Stop()
        {
        }
    }
}
