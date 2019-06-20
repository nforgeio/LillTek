//-----------------------------------------------------------------------------
// FILE:        GeoCompositeRegion.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a geographical region that is composed of other regions.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// $todo(jeff.lill):
//
// I don't currently support composing a a composite region as a subtractive subregion.
// I'd either have to retain the heirarchy of composite regions or implement region
// intersect to make this work.  Not a high priority at the moment.

namespace LillTek.Common
{
    /// <summary>
    /// Implements a geographical region that is composed of other regions.
    /// </summary>
    public sealed class GeoCompositeRegion : GeoRegion
    {
        private IList<GeoRegion>    additiveRegions;        // Read-only
        private IList<GeoRegion>    subtractiveRegions;     // Read-only

        /// <summary>
        /// Constructs a composite region from the union of a set of regions.
        /// </summary>
        /// <param name="regions">The regions.</param>
        /// <exception cref="ArgumentException">Thrown if <c>null</c> is passed or one of the regions being added is a <see cref="GeoCompositeRegion" />.</exception>
        /// <remarks>
        /// <note>
        /// <see cref="GeoCompositeRegion" />s may be composed but will be flattened by adding their
        /// component regions by adding them to the <see cref="AdditiveRegions" /> and <see cref="SubtractiveRegions" />.
        /// collections.
        /// </note>
        /// </remarks>
        public GeoCompositeRegion(IEnumerable<GeoRegion> regions)
            : this(regions, null)
        {
        }

        /// <summary>
        /// Constructs a composite region from the union of a set of regions and then
        /// removing a set of other regions.
        /// </summary>
        /// <param name="additiveRegions">The regions to be added.</param>
        /// <param name="subtractiveRegions">The regions to be subtracted (or <c>null</c>).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="additiveRegions" /> is <c>null</c> or one of the subtractive regions being 
        /// added is a <see cref="GeoCompositeRegion" /> with one or more subtractive subregions of its own.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <see cref="GeoCompositeRegion" />s may be composed but will be flattened by adding their
        /// component regions by adding them to the <see cref="AdditiveRegions" /> and <see cref=" SubtractiveRegions" />.
        /// collections.  Composite regions with subtractive subregions may not be composed as a subtractive 
        /// region.
        /// </note>
        /// </remarks>
        public GeoCompositeRegion(IEnumerable<GeoRegion> additiveRegions, IEnumerable<GeoRegion> subtractiveRegions)
        {
            List<GeoRegion>     addList = new List<GeoRegion>();
            List<GeoRegion>     subList = null;

            if (additiveRegions == null)
                throw new ArgumentNullException("additiveRegions");

            foreach (var region in additiveRegions)
            {
                var composite = region as GeoCompositeRegion;

                if (composite != null)
                {
                    foreach (var compRegion in composite.additiveRegions)
                        addList.Add(compRegion);

                    if (composite.subtractiveRegions != null)
                    {
                        subList = new List<GeoRegion>();
                        foreach (var compRegion in composite.subtractiveRegions)
                            subList.Add(compRegion);
                    }
                }
                else
                    addList.Add(region);
            }

            this.additiveRegions = addList.AsReadOnly();

            if (subtractiveRegions != null)
            {
                if (subList == null)
                    subList = new List<GeoRegion>();

                foreach (var region in subtractiveRegions)
                {
                    var composite = region as GeoCompositeRegion;

                    if (composite != null)
                    {
                        foreach (var compRegion in composite.additiveRegions)
                            addList.Add(compRegion);

                        if (composite.subtractiveRegions != null && composite.subtractiveRegions.Count > 0)
                            throw new ArgumentException("Composite regions with subtractive subregions may not be composed.");
                    }
                    else
                        subList.Add(region);
                }
            }

            if (subList != null && subList.Count > 0)
                this.subtractiveRegions = subList.AsReadOnly();

            ComputeBounds();
        }

        /// <summary>
        /// Deserializes the instance from a string generated by a previous call to <see cref="ToString" />.
        /// </summary>
        /// <param name="input">The serialized region.</param>
        /// <exception cref="ArgumentException">Thrown if the input is not valid.</exception>
        internal GeoCompositeRegion(string input)
        {
            List<GeoRegion>     additiveRegions    = new List<GeoRegion>();
            List<GeoRegion>     subtractiveRegions = null;
            string              key;
            int                 pos;
            string[]            regions;
            string              region;

            try
            {
                pos = input.IndexOf(':');
                if (pos == -1)
                    throw new ArgumentException();

                key = input.Substring(0, pos);
                if (String.Compare(key, GeoRegion.CompositeRegionKey, StringComparison.OrdinalIgnoreCase) != 0)
                    throw new ArgumentException();

                regions = input.Substring(pos + 1).Split('|');

                for (int i = 0; i < regions.Length; i++)
                {
                    region = regions[i].Trim();
                    if (region.Length == 0)
                        break;      // Tolerate an extra pipe (|) at the end

                    switch (region[0])
                    {
                        case '+':

                            additiveRegions.Add(GeoRegion.Parse(region.Substring(1)));
                            break;

                        case '-':

                            if (subtractiveRegions == null)
                                subtractiveRegions = new List<GeoRegion>();

                            subtractiveRegions.Add(GeoRegion.Parse(region.Substring(1)));
                            break;

                        default:

                            throw new ArgumentException();
                    }
                }

                this.additiveRegions = additiveRegions.AsReadOnly();

                if (subtractiveRegions != null)
                    this.subtractiveRegions = subtractiveRegions.AsReadOnly();
            }
            catch (Exception e)
            {
                throw new ArgumentException(string.Format("GeoCompositeRegion: Cannot deserialize [{0}].", input), e);
            }

            ComputeBounds();
        }

        /// <summary>
        /// Returns a read-only list of the composed additive regions
        /// </summary>
        public IList<GeoRegion> AdditiveRegions
        {
            get { return additiveRegions; }
        }

        /// <summary>
        /// Returns a read-only list of the composed subtractive regions or <c>null</c> if there are none.
        /// </summary>
        public IList<GeoRegion> SubtractiveRegions
        {
            get { return subtractiveRegions; }
        }

        /// <summary>
        /// Computes the bounding rectangle.
        /// </summary>
        private void ComputeBounds()
        {
            if (additiveRegions.Count == 0)
            {
                base.Bounds = new GeoRectangle(0, 0, 0, 0);
                return;
            }

            double left    = 180;
            double right   = -180;
            double top    = -90;
            double bottom = 90;

            foreach (var region in additiveRegions)
            {
                var r = region.Bounds;

                left = Math.Min(r.Southwest.Longitude, left);
                right = Math.Max(r.Northeast.Longitude, right);
                top = Math.Max(r.Northeast.Latitude, top);
                bottom = Math.Min(r.Southwest.Longitude, bottom);
            }

            base.Bounds = new GeoRectangle(top, right, bottom, left);
        }

        /// <summary>
        /// Determines whether a point is within the region.
        /// </summary>
        /// <param name="point">The point being tested.</param>
        /// <returns><c>true</c> if the point is within the region.</returns>
        /// <remarks>
        /// This method works by first determining whether the point is within any
        /// of the subtractive regions (if any).  If this is the case, then the point
        /// will not be considered to be within the region and the method will return
        /// <c>false</c>.  If the point passes this screen, then the method will return
        /// <c>true</c> if the point is within any of the additive regions.
        /// </remarks>
        public override bool Contains(GeoCoordinate point)
        {
            if (!Bounds.Contains(point))
                return false;

            if (subtractiveRegions != null)
            {
                foreach (var region in subtractiveRegions)
                    if (region.Contains(point))
                        return false;
            }

            foreach (var region in additiveRegions)
                if (region.Contains(point))
                    return true;

            return false;
        }

        /// <summary>
        /// Serializes the region into a form suitable for transmission or writing
        /// to persistant storage.
        /// </summary>
        /// <returns>The serialized region.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            var first = true;

            sb.AppendFormat("{0}:", GeoRegion.CompositeRegionKey);

            foreach (var region in additiveRegions)
            {
                if (first)
                    first = false;
                else
                    sb.Append('|');

                sb.AppendFormat("+{0}", region);
            }

            if (subtractiveRegions != null)
            {
                foreach (var region in subtractiveRegions)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append('|');

                    sb.AppendFormat("-{0}", region);
                }
            }

            return sb.ToString();
        }
    }
}
