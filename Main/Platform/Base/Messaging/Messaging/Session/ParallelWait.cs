//-----------------------------------------------------------------------------
// FILE:        ParallelWait.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies how a ParallelQuery determines when it is complete.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Specifies how a <see cref="ParallelQuery" /> determines when it has completed.
    /// </summary>
    public enum ParallelWait
    {
        /// <summary>
        /// The parallel query will wait for all of the component queries to complete or 
        /// fail before the overall operation will be considered to be complete.
        /// </summary>
        ForAll,

        /// <summary>
        /// The parallel query will be considered to be complete when any single query 
        /// completes successfully or all of the operations fail.
        /// </summary>
        ForAny,
    }
}
