//-----------------------------------------------------------------------------
// FILE:        MercatorIndex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Indexes EntityState instances by location and implements high
//              performance location based queries.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Indexes <see cref="EntityState" /> instances by location and implements high
    /// performance location based queries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Mercator index is a heirarchical index of <see cref="MercatorBlock"/>s that span the global.  
    /// Each of these blocks define a rectangular area and track the entities (and their assigned groups)
    /// that are currently located within the area.  At the topmost level, the <see cref="MercatorIndex" />
    /// maintains a two dimensional array of blocks whose edges are 10 degrees in length (about 691 miles at 
    /// the equator).  This top-level array is 36 blocks wide and 18 blocks high (648 total blocks).  The array 
    /// of blocks are arranged from left to right and top to bottom starting at the upper left corner of the typical
    /// Mercator map project of the world where block (0,0) is aligned at lat/lon (90,-180), block (0,1) at (90,-170),
    /// block (1,0) at (80,-180), etc.
    /// </para>
    /// <para>
    /// Each <see cref="MercatorBlock"/> may directly maintain lists of entity and group information or may consist
    /// of references to <b>16</b> nested <see cref="MercatorBlock" />s formed into a 16x16 block subarray.
    /// The index supports up to 4 levels of nesting beneath the top-level 36,18, where the blocks at each
    /// level will have edge demensions 1/16th the size (in degrees) of the blocks at the level above.  The table 
    /// below provides some insight into block dimensions at each level.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Level</th>        
    /// <th width="1">Degrees</th>
    /// <th width="1">Size at Equator</th>
    /// <th width="90%">Notes</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>Level 0</b></td>
    ///     <td><b>10.0</b></td>
    ///     <td><b>961 miles</b></td>
    ///     <td>
    ///     There are 648 blocks at this level.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>Level 1</b></td>
    ///     <td><b>0.625</b></td>
    ///     <td><b>43 miles</b></td>
    ///     <td>
    ///     There are 165,888 blocks at this level.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>Level 2</b></td>
    ///     <td><b>~0.039063</b></td>
    ///     <td><b>2.7 miles</b></td>
    ///     <td>
    ///     There are 42 million blocks at this level.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>Level 3</b></td>
    ///     <td><b>~0.002441</b></td>
    ///     <td><b>891 feet</b></td>
    ///     <td>
    ///     There is just shy of 11 billion blocks at this level.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>Level 4</b></td>
    ///     <td><b>~0.000153</b></td>
    ///     <td><b>55 feet</b></td>
    ///     <td>
    ///     There are about 2.8 trillion blocks at this level.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// <note>
    /// The horizontal measurements are valid for blocks located near the equator.  The
    /// horizontal dimensions get smaller the closer the block is to one of the poles.
    /// The vertical measurements remain constant for all points across the globe.
    /// </note>
    /// <para>
    /// The index will decide to split a block into sub-blocks when the number of entities
    /// within the exceeds a configurable high watermark limit.  The index may also coalesce sub-blocks
    /// back into the parent block if the number of entites is descreased below a low watermark.
    /// </para>
    /// <note>
    /// Note that I'm implementing support for separate high/low watermark limits to act
    /// as a <i>shock absorber</i> to help prevent undue block splitting/coalescing when
    /// the number of entities being tracked is near the block split/coalescing limit.
    /// </note>
    /// <para>
    /// These limits are specified in the <see cref="GeoTrackerServerSettings" />'
    /// <see cref="GeoTrackerServerSettings.IndexHighWatermarkLimit" /> and
    /// <see cref="GeoTrackerServerSettings.IndexLowWatermarkLimit" /> properties.  The
    /// <see cref="GeoTrackerServerSettings.IndexBalancingInterval" /> property controls
    /// how often the background thread will rebalance the index nodes.
    /// </para>
    /// <para>
    /// Entity location queries work by recursively selecting the set of index blocks that
    /// intersect the region specfied (or implied) by the query and then performing a linear
    /// walk of the entities within those blocks to select the ones that are contained within
    /// the region.  This linear operation will be slow for large numbers of entities and
    /// the purpose for the index is reduce number of entities that will need to be compared,
    /// especially for queries against small regions.
    /// </para>
    /// <para>
    /// The index is also designed to efficently adapt to cities with high concentrations of entities,
    /// to the oceans or poles with very little activity, as well as to situations where large
    /// number of entities migrate from one place to another over a period of time (such as a large
    /// number of people attending a concert or sporting event for a period of time and then leaving
    /// when the event is over).
    /// </para>
    /// <para>
    /// Finally, the index is optimized to quickly be able to determine if a block or its sub-blocks
    /// have any entities that belong to a specific group by having each block maintain a hash table 
    /// of the groups present within that block (or its sub-blocks).  This implementation assumes that
    /// there is a relatively small number of groups as compared to the number of entities and/or that
    /// groups are somewhat geographically localized or it is possible for the memory utilization to
    /// explode.  You can use the <see cref="GeoTrackerServerSettings.IndexMaxGroupTableLevel" />
    /// server setting to control this by specifying the maximum level within the index heirarchy where 
    /// these group hash tables are maintained.  Beyound this level, the query processor will simply 
    /// perform a linear search through the entities.  This setting defaults to <b>level 2</b>.
    /// </para>
    /// <para>
    /// The <see cref="GeoTrackerNode" /> will instantiate a single <see cref="MercatorIndex"/> 
    /// instance on startup, passing the the server settings instance.  The when entities are 
    /// added or updated, the server should call the <see cref="OnEntityUpdated" /> method.  
    /// Likewise, the server should call <see cref="OnEntityPurged" /> method when an entity
    /// is no longer tracked by the server.
    /// </para>
    /// </remarks>
    internal class MercatorIndex
    {
        //---------------------------------------------------------------------
        // Utilities

        /// <summary>
        /// Returns the collection of blocks within a level's block array that potentially
        /// intersect a region.
        /// </summary>
        /// <param name="region">The <see cref="GeoRegion" /> being tested.</param>
        /// <param name="blockArray">The block array.</param>
        /// <returns>The list of potentially interscecing blocks.</returns>
        /// <remarks>
        /// <note>
        /// This method performs simply returns the set of blocks that intersect the bounding
        /// rectangle around the region rather than trying to compute this based on the detailed
        /// region shape: circle, polygon,...
        /// </note>
        /// </remarks>
        public static List<MercatorBlock> GetIntersectingBlocks(GeoRegion region, MercatorBlock[,] blockArray)
        {
            var blocks   = new List<MercatorBlock>();
            var rBounds  = region.Bounds;
            int cRows    = blockArray.Rank + 1;
            int cColumns = blockArray.Length / cRows;
            int topRow, leftColumn;
            int bottomRow, rightColumn;

            if (cColumns == TopLevelColumnCount && cRows == TopLevelRowCount)
            {
                // Special case the top-level block array because we know that the region has to 
                // intersect somewhere on the planet.

                topRow      = (int)((90.0 - rBounds.Northeast.Latitude) / 10.0);
                leftColumn  = (int)((180.0 + rBounds.Southwest.Longitude) / 10.0);
                bottomRow   = (int)((90.0 - rBounds.Southwest.Latitude) / 10.0);
                rightColumn = (int)((180.0 + rBounds.Northeast.Longitude) / 10.0);
            }
            else
            {
                var neBounds   = blockArray[0, 0].Bounds;
                var swBounds   = blockArray[cColumns - 1, cRows - 1].Bounds;
                var edgeLength = neBounds.Northeast.Latitude - neBounds.Southwest.Latitude;

            }

            return blocks;
        }

        //---------------------------------------------------------------------
        // General implementation

        private const int TopLevelColumnCount = 36;
        private const int TopLevelRowCount    = 18;

        private MercatorBlock[,] topLevelBlocks;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">The parent GeoTracker node.</param>
        public MercatorIndex(GeoTrackerNode node)
        {
            // Initialize the top-level blocks.

            this.topLevelBlocks = new MercatorBlock[TopLevelColumnCount, TopLevelRowCount];

            for (int row = 0; row < TopLevelRowCount; row++)
                for (int col = 0; col < TopLevelColumnCount; col++)
                {
                    var neCorner = new GeoCoordinate(90.0 - row * 10, -180.0 + (col + 1) * 10.0);
                    var swCorner = new GeoCoordinate(90.0 - (row + 1) * 10.0, -180.0 + col * 10.0);
                    var bounds   = new GeoRectangle(neCorner, swCorner);

                    topLevelBlocks[row, col] = new MercatorBlock(node.Settings, bounds, 0);
                }
        }

        /// <summary>
        /// Called by the <see cref="GeoTrackerNode" /> when an entity is added or updated.
        /// </summary>
        /// <param name="entity">The entity.</param>
        public void OnEntityUpdated(EntityState entity)
        {
        }

        /// <summary>
        /// Called by the <see cref="GeoTrackerNode" /> when an entity is purged.
        /// </summary>
        /// <param name="entity"></param>
        public void OnEntityPurged(EntityState entity)
        {
        }

        /// <summary>
        /// Performs a location query and returns the result.
        /// </summary>
        /// <param name="query">The location query.</param>
        /// <returns>The query result.</returns>
        /// <remarks>
        /// The format of the query result is designed to be compatible with the <see cref="GeoQueryResults" />
        /// class defined within the <b>LillTek.GeoTracker</b> assembly.  See this class for more details.
        /// </remarks>
        public byte[] Query(GeoQuery query)
        {
            return null;
        }

        //---------------------------------------------------------------------
        // Unit testing related members

        /// <summary>
        /// <b>Unit test only:</b> Returns the two dimensional array of top-level <see cref="MercatorBlock" />s.
        /// </summary>
        internal MercatorBlock[,] TopLevelBlocks 
        {
            get { return topLevelBlocks; }
        }
    }
}
