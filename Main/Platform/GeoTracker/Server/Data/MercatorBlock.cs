//-----------------------------------------------------------------------------
// FILE:        MercatorBlock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a rectangular area in the hierarchical index of entities and groups.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Implements a rectangular area in the hierarchical index of entities and groups. 
    /// <see cref="MercatorIndex" /> for more information.
    /// </summary>
    internal class MercatorBlock
    {
        //---------------------------------------------------------------------
        // Implementation Note:
        //
        // Blocks at levels 0-2 maintain a dictionary holding the groupIDs with the groups
        // that have entities within the block or below.  This is the groupMap table below.
        // The purpose for this is so that queries with group constraints can quickly determine
        // whether it is necessary to actually examine the membership of entities within the block
        // (or below).
        //
        // Leaf blocks maintain a list of the entities actually located within the block.  Interior
        // index blocks maintain a two dimensional array of sub blocks.

        private const int BlockDimension = 16;              // # of sub-blocks in either direction

        private MercatorBlock[,]            subBlocks;      // Array of sub-blocks (or null)
        private double                      edgeLength;     // Length of this blocks edges in degrees.
        private Dictionary<string, bool>    groupMap;       // Tracks the entity groups for this level and below (or null)
        private List<EntityState>           entities;       // The entities within the block (or null)

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The GeoTracker server settings.</param>
        /// <param name="bounds">The boundry for the area indexed by this instance.</param>
        /// <param name="depth">The depth of of this instance in the hierarchy (0 for the top level).</param>
        /// <remarks>
        /// <note>
        /// This constructor initially creates a leaf index block.  Subsequent calls to <see cref="Balance" /> 
        /// may split this block into sub-blocks or join sub-blocks back into a leaf node.
        /// </note>
        /// </remarks>
        public MercatorBlock(GeoTrackerServerSettings settings, GeoRectangle bounds, int depth)
        {
            this.Bounds    = bounds;
            this.Depth     = depth;
            this.subBlocks = null;
            this.entities  = new List<EntityState>();

            if (depth == 0)
                edgeLength = 10.0;
            else
                edgeLength = 10.0 / (Math.Pow(BlockDimension, depth));

            if (depth <= settings.IndexMaxGroupTableLevel)
                groupMap = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Returns the rectangular geographical bounds of the block.
        /// </summary>
        public GeoRectangle Bounds { get; private set; }

        /// <summary>
        /// The depth of of this instance in the hierarchy (0 for the top level).
        /// </summary>
        public int Depth { get; private set; }

        /// <summary>
        /// Returns the number of entities located within the boundary of the index.
        /// </summary>
        public int EntityCount { get; set; }

        /// <summary>
        /// Returns <c>true</c> if this is a leaf block holding a list of entities or <c>false</c>
        /// if this is an interior block within the index.
        /// </summary>
        public bool IsLeaf
        {
            get { return entities != null; }
        }

        /// <summary>
        /// Determines if there are any entities located within the boundary of the index
        /// that belong to the specified group.
        /// </summary>
        /// <param name="groupID">The group ID.</param>
        /// <returns><c>true</c> if the index contains entities for this group.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="groupID" /> is <c>null</c>.</exception>
        public bool ContainsEntitiesInGroup(string groupID)
        {
            return false;
        }

        /// <summary>
        /// Performs a query on the entities within the block.
        /// </summary>
        /// <param name="context">The executing query context.</param>
        public void Query(QueryContext context)
        {
        }

        /// <summary>
        /// Balances the entities within the block by creating sub-blocks if this is a leaf and the 
        /// number of entities exceeds the high watermark or collapsing sub-blocks back into a leaf
        /// if the number of entities is less than the low watermark.
        /// </summary>
        public void Balance(GeoTrackerServerSettings settings)
        {
        }

        //---------------------------------------------------------------------
        // Members used for whitebox unit testing.

        /// <summary>
        /// <b>Unit test only:</b> Returns the array of sub-blocks or <c>null</c>.
        /// </summary>
        internal MercatorBlock[,] SubBlocks 
        {
            get { return subBlocks; }
        }

        /// <summary>
        /// <b>Unit test only:</b> Returns the set of the groups for the entities 
        /// within the block or sub-blocks (or <c>null</c>).
        /// </summary>
        internal Dictionary<string,bool> GroupMap 
        {
            get { return groupMap; }
        }

        /// <summary>
        /// <b>Unit test only:</b> Returns the entities within the block if it's a 
        /// leaf within the index (<c>null</c> otherwise).
        /// </summary>
        internal List<EntityState> Entities
        {
            get { return entities; }
        }
    }
}
