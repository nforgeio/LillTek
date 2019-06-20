//-----------------------------------------------------------------------------
// FILE:        GeoHeatmapQueryOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the options for a GeoTracker heatmap query.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker.Msgs;
using LillTek.Messaging;

namespace LillTek.GeoTracker
{
    /// <summary>
    /// Defines the options for a GeoTracker heatmap query.
    /// </summary>
    public class GeoHeatmapQueryOptions : GeoQueryOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoHeatmapQueryOptions()
        {
            this.ResolutionMiles = null;
            this.MapBounds       = new GeoRectangle(0, 0, 0, 0);
        }

        /// <summary>
        /// The optional rectangular boundry for the heatmap to be generated (defaults
        /// to the empty rectangle (0,0)-(0,0).
        /// </summary>
        /// <remarks>
        /// If this is not empty then it acts as an additional region filter for the
        /// query.  If this is empty and the query has a non-empty region, then the
        /// boundry of the region will be used for generating the heat map.  If this is
        /// empty and the query has an empty region, then a heat map for the entire
        /// planet will be generated.
        /// </remarks>
        public GeoRectangle MapBounds { get; set; }

        /// <summary>
        /// The optional approximate resolution in miles for generated heatmap points
        /// (defaults to <c>null</c>).
        /// </summary>
        public double? ResolutionMiles { get; set; }

        /// <summary>
        /// The optional approximate resolution in kilometers for generated heatmap points
        /// (defaults to <c>null</c>).
        /// </summary>
        public double? ResolutionKilometers
        {
            get
            {
                if (!ResolutionMiles.HasValue)
                    return null;

                return GeoHelper.ToKilometers(ResolutionMiles.Value);
            }

            set
            {
                if (!value.HasValue)
                {
                    ResolutionMiles = null;
                    return;
                }

                ResolutionMiles = GeoHelper.ToMiles(value.Value);
            }
        }

        /// <summary>
        /// Derived classes must return the query key constant that identifies the type
        /// of query being performed.
        /// </summary>
        protected override string QueryKey
        {
            get { return GeoQueryOptions.HeatmapQueryKey; }
        }

        /// <summary>
        /// Derived classes must implement this to save the option fields to the 
        /// query message passed.
        /// </summary>
        /// <param name="queryMsg">The target message.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        protected override void SaveTo(GeoQueryMsg queryMsg)
        {
            if (!MapBounds.IsEmpty)
                queryMsg["Option.MapBounds"] = MapBounds.ToString();

            if (ResolutionMiles.HasValue)
                queryMsg["Option.ResolutionMiles"] = ResolutionMiles.Value.ToString();
        }

        /// <summary>
        /// Derived classes must implement this to load the option fields from the 
        /// query passed.
        /// </summary>
        /// <param name="queryMsg">The source message.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        protected override void LoadFrom(GeoQueryMsg queryMsg)
        {
            string  value;
            double  d;

            value = queryMsg._Get("Option.MapBounds", (string)null);
            if (!string.IsNullOrWhiteSpace(value))
                MapBounds = (GeoRectangle)GeoRegion.Parse(value);

            value = queryMsg._Get("Option.ResolutionMiles", (string)null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (double.TryParse(value, out d))
                    ResolutionMiles = d;
            }
        }

        /// <summary>
        /// Used by unit tests to save the option fields to the query message passed.
        /// </summary>
        /// <param name="queryMsg">The target message.</param>
        /// <param name="stub">Pass a <see cref="Stub"/> value.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        internal void SaveTo(GeoQueryMsg queryMsg, Stub stub)
        {
            SaveTo(queryMsg);
        }

        /// <summary>
        /// Used by unit tests to load the option fields from the query passed.
        /// </summary>
        /// <param name="queryMsg">The source message.</param>
        /// <param name="stub">Pass a <see cref="Stub"/> value.</param>
        /// <remarks>
        /// The derived class must store its values as <see cref="PropertyMsg" /> fields
        /// using <b>"Option."</b> as the key prefix for each value's property name.
        /// </remarks>
        internal void LoadFrom(GeoQueryMsg queryMsg, Stub stub)
        {
            LoadFrom(queryMsg);
        }
    }
}
