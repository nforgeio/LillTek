//-----------------------------------------------------------------------------
// FILE:        ParallelQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the individual query operations to be performed in
//              parallel as well as the results of those operations.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the individual query operations to be performed in
    /// parallel as well as the results of those operations.
    /// </summary>
    public class ParallelQuery
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ParallelQuery()
        {

            this.Operations = new List<ParallelOperation>();
            this.WaitMode   = ParallelWait.ForAll;
        }

        /// <summary>
        /// Returns the set of operations to be performed in parallel.
        /// </summary>
        public List<ParallelOperation> Operations { get; private set; }

        /// <summary>
        /// A <see cref="ParallelWait" /> value specifying how the parallel query
        /// will determine when it has completed.
        /// </summary>
        public ParallelWait WaitMode { get; set; }
    }
}
