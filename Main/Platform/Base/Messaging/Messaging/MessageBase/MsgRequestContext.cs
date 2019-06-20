//-----------------------------------------------------------------------------
// FILE:        MsgRequestContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the server side state of a request/reply transaction
//              for non-session oriented as well as DuplexSession transactions.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds the server side state of a request/reply transaction
    /// for non-session oriented as well as <see cref="DuplexSession" />
    /// transactions, useful when processing requests asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LillTek <see cref="MsgRouter" /> and <see cref="DuplexSession" />
    /// classes provide mechansisms for processing request/reply transactions
    /// both synchronously and asynchronously.  The synchronous methods are
    /// safe and easy to use.  The asynchronous mechanisms are more 
    /// challenging.
    /// </para>
    /// <para>
    /// There are two basic asynchronous implementation problems that 
    /// need to be addressed:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     State about the request needs to be maintained somewhere
    ///     so that when the time comes to send the response, the application
    ///     still knows where to send it.
    ///     </item>
    ///     <item>
    ///     Orphaned transactions need to be detected somehow so that 
    ///     the router can be told to cancel the transaction.
    ///     </item>
    /// </list>
    /// <para>
    /// The first problem isn't too difficult to solve.  One strategy is
    /// for the application to keep a copy of the request message holding
    /// the return endpoint and session ID) during transaction processing.
    /// </para>
    /// <para>
    /// The second problem is more challenging.  At issue here is the fact
    /// that both the basic <see cref="MsgRouter" /> and more advanced
    /// <see cref="DuplexSession" /> request/reply transaction implementations
    /// do no rely on simple timeout mechanisms to detect when a transaction
    /// has failed.  Instead, keep-alive messages are periodically transmitted
    /// back to the client while it appears that the transaction is still
    /// being processed by the server.
    /// </para>
    /// <para>
    /// Servers can process transactions synchronously or asynchronously.
    /// Synchronous transaction processing is considered to be complete
    /// when the message handler returns.  Asynchronous transaction processing 
    /// is completed when the <see cref="MsgRouter.ReplyTo(Msg,Msg)" /> is called
    /// for normal request/reply transactions or  <see cref="DuplexSession.ReplyTo(Msg,Msg)" /> 
    /// is called for transactions within a session.
    /// </para>
    /// <para>
    /// The problem for asynchronous transactions implementations is making
    /// sure that every transaction is ultimately completed, with orphaned
    /// transactions being canceled.  It is very important that this behavior
    /// be implemented correctly to avoid the network, processing, and memory
    /// overhead for orphaned transactions that might accumulate into large
    /// numbers for long running services.
    /// </para>
    /// <para>
    /// The <see cref="MsgRequestContext" /> class provides a solid solution
    /// for managing asynchronous transactions and also abstracts away the 
    /// difference between processing transactions inside or outside of a 
    /// session.
    /// </para>
    /// <para>
    /// The class is easy to use:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Construct a <see cref="MsgRequestContext" /> instance for the
    ///     request message be calling <see cref="Msg" />.<see cref="Msg.CreateRequestContext" />.
    ///     </item>
    ///     <item>
    ///     Maintain a reference to <see cref="MsgRequestContext" /> instance 
    ///     while asynchronously processing the request.
    ///     </item>
    ///     <item>
    ///     When you have successfully completed processing, call <see cref="Reply" />
    ///     to transmit the reply back to the client and then call <see cref="Close" />
    ///     or <see cref="Dispose" />.
    ///     </item>
    ///     <item>
    ///      Call <see cref="Cancel" /> if you want to abort the transaction,
    ///      sending a <see cref="CancelException" /> back to the client, or call 
    ///      <see cref="Abort" /> to abort the transaction without
    ///      sending a reply at all.
    ///     </item>
    ///     <item>
    ///     Orphaned transactions will be addressed when there are no more references to the
    ///     <see cref="MsgRequestContext" /> instance and the CLR garbage collector calls
    ///     its finalizer just before discarding the context.  The finalizer cancels the
    ///     transaction if it hasn't already been completed.
    ///     </item>
    /// </list>
    /// <para>
    /// The <see cref="Abort" /> method can be used to have the message
    /// router discard the session without sending a reply back to the client.
    /// </para>
    /// </remarks>
    public sealed class MsgRequestContext : IDisposable
    {
        /// <summary>
        /// The request message type name to be used internally for tracing.
        /// </summary>
        internal string TraceName;

        /// <summary>
        /// Extended DuplexSession headers (or <c>null</c>).
        /// </summary>
        internal readonly MsgHeader Header;

        /// <summary>
        /// The request's return endpoint.
        /// </summary>
        internal readonly MsgEP FromEP;

        /// <summary>
        /// The request's globally unique session ID.
        /// </summary>
        public readonly Guid SessionID;

        private object                  syncLock = new object();
        private readonly MsgRouter      router;             // The message router (or null)
        private readonly DuplexSession  session;            // The duplex session (or null)
        private bool                    closed = false;     // True if the transaction has completed

        /// <summary>
        /// Constructs a <see cref="MsgRequestContext" /> for transactions that are not within a session.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" />.</param>
        /// <param name="requestMsg">The request <see cref="Msg" />.</param>
        /// <exception cref="ArgumentException">Thrown if the message passed does not have all of the headers necessary to be a request.</exception>
        internal MsgRequestContext(MsgRouter router, Msg requestMsg)
        {
            if (router == null)
                throw new ArgumentNullException("router");

            if (requestMsg._FromEP == null)
                throw new ArgumentException("Message cannot be a request: Null [_FromEP] header.", "requestMsg");

            if (requestMsg._SessionID == Guid.Empty)
                throw new ArgumentException("Message cannot be a request: Empty [_SessionID] header.", "requestMsg");

            this.router    = router;
            this.session   = null;
            this.FromEP    = requestMsg._FromEP.Clone();
            this.SessionID = requestMsg._SessionID;
#if TRACE
            this.TraceName = requestMsg.GetType().Name;
#else
            this.TraceName = "(trace disabled)";
#endif
        }

        /// <summary>
        /// Constructs a <see cref="MsgRequestContext" /> for transactions that are within a session.
        /// </summary>
        /// <param name="session">The <see cref="DuplexSession" />.</param>
        /// <param name="query">The request <see cref="Msg" />.</param>
        /// <exception cref="ArgumentException">Thrown if the message passed does not have all of the headers necessary to be a request.</exception>
        internal MsgRequestContext(DuplexSession session, Msg query)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            if (query._FromEP == null)
                throw new ArgumentException("Message cannot be a request: Null [_FromEP] header.", "requestMsg");

            if (query._SessionID == Guid.Empty)
                throw new ArgumentException("Message cannot be a request: Empty [_SessionID] header.", "requestMsg");

            this.Header = query._ExtensionHeaders[MsgHeaderID.DuplexSession];
            if (this.Header == null)
                throw new ArgumentException("Message is not a DuplexSession query.", "requestMsg");

            this.router    = session.Router;
            this.session   = session;
            this.FromEP    = query._FromEP.Clone();
            this.SessionID = query._SessionID;
#if TRACE
            this.TraceName = query.GetType().Name;
#else
            this.TraceName = "(trace disabled)";
#endif
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~MsgRequestContext()
        {
            if (!closed)
                Cancel();
        }

        /// <summary>
        /// Transmits the transaction reply message.
        /// </summary>
        /// <param name="reply">The reply.</param>
        /// <exception cref="InvalidOperationException">Thrown if the transaction has already been completed.</exception>
        public void Reply(Msg reply)
        {
            lock (syncLock)
            {
                if (closed)
                    throw new InvalidOperationException("Transaction has already been completed.");

                closed = true;
            }

            if (session != null)
                session.ReplyTo(this, reply);
            else
                router.ReplyTo(this, reply);
        }

        /// <summary>
        /// Cancels the transaction if it has not already completed.
        /// </summary>
        public void Cancel()
        {
            lock (syncLock)
            {
                if (closed)
                    return;

                closed = true;
            }

            var ack = new Ack(new CancelException("Transaction has been cancelled."));

            if (session != null)
                session.ReplyTo(this, ack);
            else
                router.ReplyTo(this, ack);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the transaction, cancelling it if it has not already completed.
        /// </summary>
        public void Close()
        {
            Cancel();
        }

        /// <summary>
        /// Closes the transaction, cancelling it if it has not already completed.
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }

        /// <summary>
        /// Cancels the transaction without sending a reply of any kind to the client,
        /// if the transaction has not already completed.
        /// </summary>
        /// <remarks>
        /// This will cause the client to timeout or potentially resubmit the request.
        /// </remarks>
        public void Abort()
        {
            lock (syncLock)
            {
                if (closed)
                    return;

                closed = true;
            }

            router.SessionManager.TerminateServerSession(SessionID);
            GC.SuppressFinalize(this);
        }
    }
}
