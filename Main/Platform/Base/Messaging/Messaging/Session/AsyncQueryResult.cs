//-----------------------------------------------------------------------------
// FILE:        AsyncQueryResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An IAsyncResult implementation for tracking QuerySessions.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// An IAsyncResult implementation used by <see cref="MsgRouter.BeginQuery" />
    /// for tracking <see cref="QuerySession" /> instances.
    /// </summary>
    internal sealed class AsyncQueryResult : AsyncResult
    {
        private QuerySession    session;
        private Msg             reply;

        /// <summary>
        /// Constructs an <see cref="IAsyncResult" /> instance that
        /// associates a <see cref="QuerySession" />.
        /// </summary>
        /// <param name="session">The session to be associated.</param>
        /// <param name="owner">The object that owns this instance (or <c>null</c>).</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        public AsyncQueryResult(QuerySession session, object owner, AsyncCallback callback, object state)
            : base(owner, callback, state)
        {
            this.session = session;
            this.reply   = null;
        }

        /// <summary>
        /// Returns the <see cref="QuerySession" /> instance being tracked.
        /// </summary>
        public QuerySession Session
        {
            get { return session; }
        }

        /// <summary>
        /// The query reply message (or <c>null</c>).
        /// </summary>
        public Msg Reply
        {
            get { return reply; }
            set { reply = value; }
        }
    }
}
