//-----------------------------------------------------------------------------
// FILE:        GeoEntityQueryOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the options for a GeoTracker entity query.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker.Msgs;
using LillTek.Messaging;

namespace LillTek.GeoTracker
{
    /// <summary>
    /// Defines the options for a GeoTracker entity query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GeoTracker entity queries are used to return information on selected entities
    /// being tracked by the platform.  The entity filter conditions are specified
    /// by the <see cref="GeoQuery" /> properties.  An instance of this class may
    /// be assigned to the <see cref="GeoQuery.Options" /> property to control how
    /// the entity results are returned.
    /// </para>
    /// </remarks>
    public class GeoEntityQueryOptions : GeoQueryOptions
    {
        /// <summary>
        /// Constructs an instance with reasonable default property values.
        /// </summary>
        public GeoEntityQueryOptions()
        {

            this.FixCount      = 1;
            this.MinFixTimeUtc = DateTime.MinValue;
            this.FixFields     = GeoFixField.All;
        }

        /// <summary>
        /// The maximum number of <see cref="GeoFix" />es to be returned for each
        /// entity in the results.  The most recent fixes will be returned first.
        /// </summary>
        public int FixCount { get; set; }

        /// <summary>
        /// The minimum <see cref="GeoFix.TimeUtc" /> for <see cref="GeoFix" />es to be
        /// returned in the result set.
        /// </summary>
        public DateTime MinFixTimeUtc { get; set; }

        /// <summary>
        /// A bitmap specifying <see cref="GeoFixField" />s to be returned in the result set.
        /// </summary>
        public GeoFixField FixFields { get; set; }

        /// <summary>
        /// Derived classes must return the query key constant that identifies the type
        /// of query being performed.
        /// </summary>
        protected override string QueryKey
        {
            get { return GeoQueryOptions.EntityQueryKey; }
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
            queryMsg._Set("Option.FixCount", this.FixCount);
            queryMsg._Set("Option.MinFixTimeUtc", this.MinFixTimeUtc);
            queryMsg._Set("Option.FixFields", (int)this.FixFields);
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
            this.FixCount = queryMsg._Get("Option.FixCount", this.FixCount);
            this.MinFixTimeUtc = queryMsg._Get("Option.MinFixTimeUtc", this.MinFixTimeUtc);
            this.FixFields = (GeoFixField)queryMsg._Get("Option.FixFields", (int)this.FixFields);
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
