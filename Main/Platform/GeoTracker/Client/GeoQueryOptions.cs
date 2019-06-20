//-----------------------------------------------------------------------------
// FILE:        GeoQueryOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The base class for all GeoTracker query options.

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
    /// Defines the common behaviors for all GeoTracker query options.
    /// </summary>
    public abstract class GeoQueryOptions
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Key used to identify <see cref="GeoEntityQueryOptions" /> type for serialization.
        /// </summary>
        internal const string EntityQueryKey = "Entity";

        /// <summary>
        /// Key used to identify <see cref="GeoHeatmapQueryOptions" /> type for serialization.
        /// </summary>
        internal const string HeatmapQueryKey = "Heatmap";

        /// <summary>
        /// Deserializes the query options from a <see cref="GeoQueryMsg" /> if any options
        /// are present.
        /// </summary>
        /// <param name="queryMsg">The source message.</param>
        /// <returns>The deserialized options or <c>null</c> if none are present.</returns>
        internal static GeoQueryOptions Load(GeoQueryMsg queryMsg)
        {
            string          queryType = queryMsg._Get("QueryType", (string)null);
            GeoQueryOptions options;

            switch (queryType)
            {
                case EntityQueryKey:

                    options = new GeoEntityQueryOptions();
                    break;

                case HeatmapQueryKey:

                    options = new GeoHeatmapQueryOptions();
                    break;

                default:

                    return null;
            }

            options.LoadFrom(queryMsg);
            return options;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        internal GeoQueryOptions()
        {
        }

        /// <summary>
        /// Serializes the query options to a <see cref="GeoQueryMsg" />.
        /// </summary>
        /// <param name="queryMsg">The target message.</param>
        internal void Save(GeoQueryMsg queryMsg)
        {
            queryMsg["QueryType"] = this.QueryKey;
            this.SaveTo(queryMsg);
        }

        /// <summary>
        /// Derived classes must return the query key constant that identifies the type
        /// of query being performed.
        /// </summary>
        protected abstract string QueryKey { get; }

        /// <summary>
        /// Derived classes must implement this to save the option fields to the 
        /// query message passed.
        /// </summary>
        /// <param name="queryMsg">The target message.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        protected abstract void SaveTo(GeoQueryMsg queryMsg);

        /// <summary>
        /// Derived classes must implement this to load the option fields from the 
        /// query passed.
        /// </summary>
        /// <param name="queryMsg">The source message.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        protected abstract void LoadFrom(GeoQueryMsg queryMsg);
    }
}
