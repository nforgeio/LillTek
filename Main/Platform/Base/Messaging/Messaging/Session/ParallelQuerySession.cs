//-----------------------------------------------------------------------------
// FILE:        ParallelQuerySession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client and server sides of a parallel query/response session.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the client sides of a parallel query/response session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This session is designed issue multiple query/response operations in parallel to
    /// a set of endpoints.  The session can be configured to complete when and any of the
    /// parallel queries complete of when the first query completes.
    /// </para>
    /// <para>
    /// The session begins with the client creating a <see cref="LillTek.Messaging.ParallelQuery" /> specifying the
    /// query endpoints and the query messages to be sent and then calling <see cref="MsgRouter.ParallelQuery(ParallelQuery)" />
    /// to initiate parallel query/response sessions.  The message router will create a new
    /// <see cref="ParallelQuerySession" /> with a unique session ID and then start
    /// the individual queries via <see cref="QuerySession" /> instances.  The <see cref="ParallelQuerySession" />
    /// will track the progress of the individual query operations.
    /// </para>
    /// <para>
    /// The <see cref="LillTek.Messaging.ParallelQuery" />.<see cref="LillTek.Messaging.ParallelQuery.WaitMode" /> property specifies 
    /// when the parallel query considers itself to be completed.  This defaults to <see cref="ParallelWait.ForAll" />
    /// which indicates that the query won't be completed until all of the individual operations
    /// have completed.  This can also be set to <see cref="ParallelWait.ForAny" /> which indicates
    /// that the parallel query will complete when any of the operations complete or if they
    /// all fail.
    /// </para>
    /// <para>
    /// Note that parallel query sessions exist only on the client side.  Target endpoints will
    /// still implement regular <see cref="QuerySession" />s.
    /// </para>
    /// </remarks>
    public class ParallelQuerySession : SessionBase, ISession
    {
        /// <summary>
        /// The trace subsystem name for the <see cref="DuplexSession" /> and related classes.
        /// </summary>
        public const string TraceSubsystem = "Messaging.DuplexSession";

        private ParallelQuery   parallelQuery;  // The query state
        private AsyncResult     arParallel;     // The parallel query async result
        private int             cCompleted;     // Number of completed operations

        /// <summary>
        /// Constructs the client side implementation of a QueryResponse session.
        /// </summary>
        public ParallelQuerySession()
            : base()
        {

            this.arParallel = null;
        }

        /// <summary>
        /// Returns the associated <see cref="LillTek.Messaging.ParallelQuery" /> instance.
        /// </summary>
        public ParallelQuery Query
        {
            get { return parallelQuery; }
        }

        /// <summary>
        /// Initiates an asynchronous parallel query operation by sending the message
        /// passed to the target endpoint.
        /// </summary>
        /// <param name="parallelQuery">Specifies the parallel query operations to be performed.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The async result used to track the operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no query operations are specified.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a query operation or query message is passed more than once.</exception>
        /// <remarks>
        /// <note>
        /// Each call to <see cref="BeginParallelQuery" /> must be matched with a call to <see cref="EndParallelQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginParallelQuery(ParallelQuery parallelQuery, AsyncCallback callback, object state)
        {
            if (parallelQuery.Operations.Count == 0)
                throw new InvalidOperationException("At least one query operation must be requested.");

            Assertion.Test(arParallel == null, "Cannot reuse a parallel query session.");
#if DEBUG
            // Make sure that no single query operation or message is passed more than once.

            for (int i = 0; i < parallelQuery.Operations.Count; i++)
                for (int j = 0; j < parallelQuery.Operations.Count; j++)
                {
                    if (i == j)
                        continue;

                    if (object.ReferenceEquals(parallelQuery.Operations[i], parallelQuery.Operations[j]))
                        throw new InvalidOperationException(string.Format("The same ParallelOperation instance is passed in a ParallelQuery (slots [{0}] and [{1}]).", i, j));

                    if (object.ReferenceEquals(parallelQuery.Operations[i].QueryMsg, parallelQuery.Operations[j].QueryMsg))
                        throw new InvalidOperationException(string.Format("The same ParallelOperation query instance is passed in a ParallelQuery (slots [{0}] and [{1}]).", i, j));
                }
#endif
            this.parallelQuery = parallelQuery;
            this.arParallel    = new AsyncResult(base.SessionManager, callback, state);
            base.TTD           = DateTime.MaxValue;   // We'll let the underlying  QuerySessions handle this
            base.IsRunning     = true;

            try
            {
                Trace(2, "Query Start", null);
                base.SessionManager.ClientStart(this);

                var onOperationComplete = new AsyncCallback(OnOperationComplete);

                foreach (var operation in parallelQuery.Operations)
                    Router.BeginQuery(operation.QueryEP, operation.QueryMsg, onOperationComplete, operation);
            }
            catch (Exception e)
            {
                AsyncResult arTemp;

                arParallel.Notify(e);

                arTemp     = arParallel;
                arParallel = null;

                base.IsRunning = false;

                return arTemp;
            }

            arParallel.Started();
            return arParallel;
        }

        /// <summary>
        /// Handles underlying query/response operation completions.
        /// </summary>
        /// <param name="ar">The operation's <see cref="IAsyncResult"/>.</param>
        private void OnOperationComplete(IAsyncResult ar)
        {
            var operation = (ParallelOperation)ar.AsyncState;

            using (TimedLock.Lock(base.Router.SyncRoot))
            {
                if (!base.IsRunning)
                    return;     // Ignore query completions if the parallel query has already finished.

                try
                {
                    operation.ReplyMsg = Router.EndQuery(ar);
                }
                catch (Exception e)
                {
                    operation.Error = e;
                }

                // Determine if the parallel query is consider to be complete.

                cCompleted++;

                switch (parallelQuery.WaitMode)
                {
                    case ParallelWait.ForAll:

                        if (cCompleted < parallelQuery.Operations.Count)
                            return;     // Still more queries pending.

                        arParallel.Notify();
                        arParallel = null;

                        base.IsRunning = false;
                        break;

                    case ParallelWait.ForAny:

                        if (cCompleted >= parallelQuery.Operations.Count || operation.ReplyMsg != null)
                        {
                            // All of the queries have completed or we have a response message.

                            arParallel.Notify();
                            arParallel = null;

                            base.IsRunning = false;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Completes the execution of an asynchronous parallel query operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginParallelQuery" />.</param>
        /// <returns>The parallel queries.</returns>
        /// <remarks>
        /// <note>
        /// Each call to <see cref="BeginParallelQuery" /> must be matched with
        /// a call to <see cref="EndParallelQuery" />.
        /// </note>
        /// </remarks>
        public ParallelQuery EndParallelQuery(IAsyncResult ar)
        {
            var arParallel = (AsyncResult)ar;

            arParallel.Wait();

            try
            {
                Trace(2, "Query Finish", null);

                if (arParallel.Exception != null)
                    throw arParallel.Exception;

                return parallelQuery;
            }
            finally
            {
                arParallel.Dispose();
            }
        }

        /// <summary>
        /// Performs a synchronous parallel query operation.
        /// </summary>
        /// <param name="query">Specifies the parallel query operations to be performed.</param>
        /// <returns>The parallel queries.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no query operations are specified.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a query operation or query message is passed more than once.</exception>
        /// <remarks>
        /// <note>
        /// The endpoint passed may be either a physical or logical
        /// endpoint.
        /// </note>
        /// </remarks>
        public ParallelQuery ParallelQuery(ParallelQuery query)
        {
            var ar = BeginParallelQuery(query, null, null);

            return EndParallelQuery(ar);
        }

        /// <summary>
        /// Starts the server session initialized with InitServer().
        /// </summary>
        public override void StartServer()
        {
            throw new NotImplementedException("ParallelQuerySession does not implement server side behaviors.");
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        /// <param name="details">The trace details (or <c>null</c>).</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary, string details)
        {
            if (details == null)
                details = string.Empty;

            NetTrace.Write(TraceSubsystem, detail, "ParallelQuery", summary, details);
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary)
        {
            Trace(detail, summary, (string)null);
        }

        /// <summary>
        /// Writes the exception passed out to the NetTrace.
        /// </summary>
        /// <param name="tEvent">The event text.</param>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        internal void Trace(string tEvent, Exception e)
        {
            const string format =
@"Exception: {0}
Message:   {1}
Stack:

";
            var     sb = new StringBuilder();
            string  summary;

            summary = this.GetType().Name + ": " + e.GetType().Name;

            sb.AppendFormat(null, format, e.GetType().ToString(), e.Message);
            sb.AppendFormat(e.StackTrace);

            NetTrace.Write(TraceSubsystem, 0, tEvent, summary, sb.ToString());
        }
    }
}
