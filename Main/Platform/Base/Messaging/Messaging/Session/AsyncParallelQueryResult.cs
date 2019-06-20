//-----------------------------------------------------------------------------
// FILE:        AsyncParallelQueryResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An IAsyncResult implementation for tracking ParallelQuerySessions.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// An IAsyncResult implementation used by <see cref="MsgRouter.BeginParallelQuery" />
    /// for tracking <see cref="ParallelQuerySession" /> instances.
    /// </summary>
    internal sealed class AsyncParallelQueryResult : AsyncResult
    {
        private ParallelQuerySession session;

        /// <summary>
        /// Constructs an <see cref="IAsyncResult" /> instance that
        /// associates a <see cref="ParallelQuerySession" />.
        /// </summary>
        /// <param name="session">The session to be associated.</param>
        /// <param name="owner">The object that owns this instance (or <c>null</c>).</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        public AsyncParallelQueryResult(ParallelQuerySession session, object owner, AsyncCallback callback, object state)
            : base(owner, callback, state)
        {
            this.session = session;
        }

        /// <summary>
        /// Returns the <see cref="ParallelQuerySession" /> instance being tracked.
        /// </summary>
        public ParallelQuerySession Session
        {
            get { return session; }
        }
    }
}
