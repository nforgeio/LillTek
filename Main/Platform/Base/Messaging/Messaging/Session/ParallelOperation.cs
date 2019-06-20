//-----------------------------------------------------------------------------
// FILE:        ParallelOperation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an individual parallel query operation and result.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes an individual parallel query operation and results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used by <see cref="ParallelQuerySession" /> to track an
    /// individual query that is one of a larger set of parallel queries.  This
    /// class holds the target query endpoint and the query message as well as the
    /// result of the operation: the response message or an exception.
    /// </para>
    /// </remarks>
    public class ParallelOperation
    {
        /// <summary>
        /// Constructs a query to a known endpoint.
        /// </summary>
        /// <param name="queryEP">The target query endpoint (or <c>null</c>).</param>
        /// <param name="queryMsg">The query message.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryEP" /> is <c>null</c>.</exception>
        public ParallelOperation(MsgEP queryEP, Msg queryMsg)
        {
            if (queryMsg == null)
                throw new ArgumentNullException("queryMsg");

            this.QueryEP  = queryEP;
            this.QueryMsg = queryMsg;
        }

        /// <summary>
        /// Constructs a query where the endpoint will be determined later
        /// (e.g. by a <see cref="ITopologyProvider" />).
        /// </summary>
        /// <param name="queryMsg">The query message.</param>
        public ParallelOperation(Msg queryMsg)
        {
            if (queryMsg == null)
                throw new ArgumentNullException("queryMsg");

            this.QueryMsg = queryMsg;
        }

        /// <summary>
        /// Returns the query message endpoint.
        /// </summary>
        public MsgEP QueryEP { get; set; }

        /// <summary>
        /// Returns the query message.
        /// </summary>
        public Msg QueryMsg { get; private set; }

        /// <summary>
        /// Returns the response message if the query completely successfully or <c>null</c>
        /// if the operation is still in progress or if there was an error.
        /// </summary>
        public Msg ReplyMsg { get; internal set; }

        /// <summary>
        /// Returns the exception if the query failed or <c>null</c> if the operation
        /// is still in progress or completed successfully.
        /// </summary>
        public Exception Error { get; internal set; }

        /// <summary>
        /// Returns <c>true</c> if the operation has completed, <c>false</c> if it is
        /// still in progress or was silently cancelled due to the <see cref="ParallelQuery" />.<see cref="ParallelQuery.WaitMode" />.
        /// </summary>
        public bool IsComplete
        {
            get { return ReplyMsg != null || Error != null; }
        }
    }
}
