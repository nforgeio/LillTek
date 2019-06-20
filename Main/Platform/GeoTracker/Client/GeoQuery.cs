//-----------------------------------------------------------------------------
// FILE:        GeoQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a query to be presented to GeoTracker.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using LillTek.Common;
using LillTek.GeoTracker.Msgs;
using LillTek.Messaging;

namespace LillTek.GeoTracker
{
    /// <summary>
    /// Describes a query to be presented to GeoTracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GeoTracker platform implements a reasonably flexible query processor that supports
    /// two basic types of querys: <b>Entity</b> and <b>Heatmap</b>.  Both query types filter 
    /// entities based on the following criteria:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>Entity Filter</b></term>
    ///         <description>
    ///         Entity IDs can be filtered using a simple wildcarding scheme.  Set the
    ///         <see cref="EntityFilters" /> to one or more filter strings with <b>(*)</b>
    ///         or <b>(?)</b> wildcard characters where <b>(*)</b> matches zero or more
    ///         characters and <b>(?)</b> matches a single character.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Group Filter</b></term>
    ///         <description>
    ///         Group IDs can be filtered using a simple wildcarding scheme.  Set the
    ///         <see cref="GroupFilters" /> to one or more filter strings with <b>(*)</b>
    ///         or <b>(?)</b> wildcard characters where <b>(*)</b> matches zero or more
    ///         characters and <b>(?)</b> matches a single character.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Region</b></term>
    ///         <description>
    ///         Entities can be filtered based on their current location.  Set the
    ///         <see cref="Region" /> property to a <see cref="GeoCircle" />, <see cref="GeoRectangle" />,
    ///         <see cref="GeoPolygon" />, or <see cref="GeoCompositeRegion" /> and the
    ///         query will return information about only those entities currently located
    ///         within the region.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>Entity Update Time</b></term>
    ///         <description>
    ///         Entities can also be filtered based on the time (UTC) of the last location update
    ///         received for the entity by setting the <see cref="MinTimeUtc" /> and/or <see cref="MaxTimeUtc" />
    ///         properties.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// The <see cref="Options" /> property determines the type of query results returned as 
    /// well as any query result specific options.  This may be set to either a <see cref="GeoEntityQueryOptions" />
    /// or <see cref="GeoHeatmapQueryOptions" /> instance or it may be left as <c>null</c>
    /// (which implies a <see cref="GeoEntityQueryOptions" /> instance with default settings).
    /// </para>
    /// <para>
    /// Client application perform queries by passing a <see cref="GeoQuery" /> to the
    /// <see cref="GeoTrackerClient" />'s <see cref="GeoTrackerClient.Query" /> or 
    /// <see cref="GeoTrackerClient.BeginQuery" /> methods.  The query will be transmitted 
    /// to the GeoTracker cluster for processing.  Query processing proceeds logically by 
    /// the query processor selecting the set of entities being tracked by the cluster that 
    /// satisfy the various query filters.  The next step is to generate the desired results 
    /// from the entities found and finally, the results are transmitted back to the client.
    /// </para>
    /// <note>
    /// The entities included in the results must satisfy <b>all</b> of the filter conditions.
    /// </note>
    /// <para>
    /// GeoTracker currently supports two types of query results: <b>Entity</b> and <b>Heatmap</b>.
    /// Entity results return information about filtered entities including their current <see cref="GeoFix"/>
    /// and optionally historical <see cref="GeoFix"/>es.  Heatmaps summarize the number of filtered
    /// entities located near geographical points on a grid.  <see cref="GeoEntityQueryOptions" />
    /// and <see cref="GeoHeatmapQueryOptions" /> for information customizing the results and
    /// <see cref="GeoEntityResults" /> and <see cref="GeoHeatmapResults" /> for details on
    /// what queries return.
    /// </para>
    /// </remarks>
    public class GeoQuery
    {
        //---------------------------------------------------------------------
        // Static members

        internal static GeoQuery FromMessage(GeoQueryMsg queryMsg)
        {

            GeoQuery    query = new GeoQuery();
            string      value;

            query.Options       = GeoQueryOptions.Load(queryMsg);
            query.EntityFilters = queryMsg._GetArray("EntityFilter");
            query.GroupFilters  = queryMsg._GetArray("QueryFilter");

            value = queryMsg._Get("Region", (string)null);
            if (!string.IsNullOrWhiteSpace(value))
                query.Region = GeoRegion.Parse(value);

            if (!string.IsNullOrWhiteSpace(queryMsg["MinTimeUtc"]))
                query.MinTimeUtc = queryMsg._Get("MinTimeUtc", DateTime.MinValue);

            if (!string.IsNullOrWhiteSpace(queryMsg["MaxTimeUtc"]))
                query.MaxTimeUtc = queryMsg._Get("MaxTimeUtc", DateTime.MaxValue);

            return query;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GeoQuery()
        {
        }

        /// <summary>
        /// The entity ID filters or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Set this property to one or more filter patterns optionally containing <b>(*)</b> or <b>(?)</b>
        /// wildcard characters.  The query will return results only for entities whose IDs match this pattern.
        /// </remarks>
        public string[] EntityFilters { get; set; }

        /// <summary>
        /// The group ID filters or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Set this property to one or more filter patterns optionally containing <b>(*)</b> or <b>(?)</b>
        /// wildcard characters.  The query will return results only for entities belonging to groups with
        /// IDs that match this pattern.
        /// </remarks>
        public string[] GroupFilters { get; set; }

        /// <summary>
        /// The region filter or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Set this to a <see cref="GeoRegion" /> to be used to filter the set of results returned based
        /// on the entity's current position.
        /// </remarks>
        public GeoRegion Region { get; set; }

        /// <summary>
        /// The minimum current fix time filter or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// When set, the GeoTracker query processor will filter out any entities whose most recent
        /// received fix is older than the value of the property.
        /// </remarks>
        public DateTime? MinTimeUtc { get; set; }

        /// <summary>
        /// When set, the GeoTracker query processor will filter out any entities whose most recent
        /// received fix is newer than the value of the property.
        /// </summary>
        public DateTime? MaxTimeUtc { get; set; }

        /// <summary>
        /// Identifies the type desired query as well as any type specific query options.
        /// </summary>
        /// <remarks>
        /// <para>
        /// GeoTracker currently supports <b>Entity</b> and <b>Heatmap</b> queries.  Entity
        /// queries return a <see cref="GeoEntityResults" /> holding information about the set 
        /// of entities that satisfy the filter criteria.  Heatmap queries return a
        /// <see cref="GeoHeatmapResults" /> instance with information about the distribution
        /// of entities in a rectangular area.
        /// </para>
        /// <para>
        /// Pass a <see cref="GeoQueryOptions" /> instance for an entity query or a
        /// <see cref="GeoHeatmapQueryOptions" /> instance for a heatmap query.  If property is
        /// not set an entity query will be performed with default options.
        /// </para>
        /// </remarks>
        public GeoQueryOptions Options { get; set; }

        /// <summary>
        /// Generates a <see cref="GeoQueryMsg" /> from the instance.
        /// </summary>
        /// <returns>The query message.</returns>
        internal GeoQueryMsg ToMessage()
        {
            var queryMsg = new GeoQueryMsg();
            var options  = Options;

            if (options == null)
                options = new GeoEntityQueryOptions();

            options.Save(queryMsg);

            if (EntityFilters != null)
            {
                for (int i = 0; i < EntityFilters.Length; i++)
                    queryMsg[string.Format("EntityFilter[{0}]", i)] = EntityFilters[i];
            }

            if (GroupFilters != null)
            {
                for (int i = 0; i < GroupFilters.Length; i++)
                    queryMsg[string.Format("GroupFilter[{0}]", i)] = GroupFilters[i];
            }

            if (Region != null)
                queryMsg["Region"] = Region.ToString();

            if (MinTimeUtc.HasValue)
                queryMsg._Set("MinTimeUtc", MinTimeUtc.Value);

            if (MaxTimeUtc.HasValue)
                queryMsg._Set("MaxTimeUtc", MaxTimeUtc.Value);

            return queryMsg;
        }
    }
}
