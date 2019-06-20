//-----------------------------------------------------------------------------
// FILE:        QuerySession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client and server sides of a query/response session.

using System;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging.Internal;

// $todo(jeff.lill): 
//
// I think that I'd better send an explicit SessionTimeoutMsg
// back to the client when an async session on the server
// side times out.  This will prevent the client from automatically
// retrying the query.  I'm not completely convinced that this
// is a problem though.  Another way to prevent this is to make
// sure that the MsgSession.MaxAsyncKeepAlive property is set
// to a duration large enough to the client will timeout 
// naturally.

// $todo(jeff.lill): 
//
// I'd like to figure out a way to delay sending the initial 
// SessionKeepAliveMsg for a brief period of time < 500ms
// so that if the server side sends the response very quickly,
// we won't waste resources on sending and processing the
// keep alive message.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the client and server sides of a query/response session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Query sessions implement the basic query/response pattern where the
    /// client sends a query message to an endpoint and then waits for
    /// a reply message.  Past implementations of this session implemented
    /// pretty much exactly this, with the addition of the concepts of a
    /// timeout and resending the query message in an effort to implement
    /// some robustness in the face of message delivery failures.
    /// </para>
    /// <para>
    /// Query responses implement the <see cref="IAck" /> interface.  The main
    /// purpose of this interface is to provide a standard way of encoding
    /// information about any exceptions thrown on the server while processing
    /// the query.
    /// </para>
    /// <para>
    /// The problem with this simplistic implementation is that there's no
    /// real way to tell if the server hasn't responded yet due to some sort
    /// of execution or message delivery failure or simply because the server 
    /// operation is still in progress.  The only way to deal with the latter is 
    /// to use a timeout value that should be long enough to allow the functioning
    /// servers to complete the operation successfully.  The problem with this
    /// though, is that long timeouts mean that it will take that much longer
    /// for the messaging library to discover that an endpoint is no longer
    /// live and failover to another instance.
    /// </para>
    /// <para>
    /// The current implementation addresses this dilemma by the use of 
    /// <see cref="SessionKeepAliveMsg" /> messages.  When the server side
    /// of the session receives the query message, it begins sending
    /// periodic SessionKeepAliveMsg messages back to the client so
    /// that the client will be able to much more quickly determine
    /// whether the server has failed.  The SessionKeepAliveMsg messages
    /// will be sent during the entire time the query is being processed.
    /// </para>
    /// <code language="none">
    ///     Client                  Server
    /// ------------------------------------------------
    ///             ----&gt;          Begins processing the query 
    ///        query message
    /// 
    ///            &lt;----           Sends periodic keepalives to
    ///     SessionKeepAliveMsg    the client
    /// 
    ///            &lt;----
    ///     SessionKeepAliveMsg
    /// 
    ///            &lt;----
    ///     SessionKeepAliveMsg
    /// 
    ///            &lt;----           Server sends the final response
    ///      IAck reply message
    /// </code>
    /// <para>
    /// The session begins with the client calling <see cref="MsgRouter.Query(MsgEP,Msg)" />
    /// to send a message to an application message handler that is tagged with <c>[MsgSession(Type=SessionType.Query)]</c>.
    /// <see cref="MsgRouter.Query(MsgEP,Msg)" /> generates a new session ID GUID, saves it
    /// in the message's <see cref="Msg._SessionID" /> property, adds a client side <see cref="QuerySession" />
    /// instance to the client router's <see cref="MsgRouter.SessionManager" />, and finally
    /// sets the message's <see cref="MsgFlag.ReceiptRequest" /> flag bit to enable dead router detection
    /// (if dead router detection is enabled for the router). The message is then sent to specified endpoint.
    /// </para>
    /// <para>
    /// Upon receiving the message on the server, a server side <see cref="QuerySession" />
    /// is added to the server router's <see cref="MsgRouter.SessionManager" /> and a 
    /// <see cref="SessionKeepAliveMsg" /> is immediately sent back to the client to
    /// indicate that the query is being processed and also to communicate the timeout
    /// values the client session should use.
    /// </para>
    /// <para>
    /// The session then invokes the server side message handler.  While the handler
    /// continues executing, the server side session will send periodic <see cref="SessionKeepAliveMsg" />
    /// messages back to the client.  The interval between these messages will be approximately
    /// 1/3 the session timeout communicated to the client.
    /// </para>
    /// <para>
    /// The server's message handler should pass the reply message (which must implement
    /// <see cref="IAck" />) in a call to <see cref="MsgRouter.ReplyTo(Msg,Msg)" /> and then
    /// return.  The message router will route the <see cref="IAck" /> message back to the
    /// client router which will then unblock the thread that called 
    /// <see cref="MsgRouter.Query(MsgEP,Msg)" />, returning the <see cref="IAck" /> message.
    /// </para>
    /// <para>
    /// If the server's message handler throws an exception, an <see cref="IAck" /> message
    /// with the exception information will be sent back to the client.  The thread that
    /// called <see cref="MsgRouter.Query(MsgEP,Msg)" /> will be unblocked and a 
    /// <see cref="SessionException" /> will be thrown.
    /// </para>
    /// <para>
    /// If the server dies in the middle of executing the query, or if the network fails,
    /// then the client session will eventually timeout, unblock the  <see cref="MsgRouter.Query(MsgEP,Msg)" />
    /// thread, and then throw a <see cref="TimeoutException" />.
    /// </para>
    /// <para><b><u>Asynchronous Query Message Handlers</u></b></para>
    /// <para>
    /// Setting the <see cref="MsgSessionAttribute.IsAsync" /> property of a message handler
    /// to <c>true</c> indicates to the QuerySession that the handler will complete the
    /// operation asynchronously.
    /// </para>
    /// <para>
    /// When <b>IsAsync</b> is set to false (the default) for a message handler the
    /// QuerySession instance will stop sending <see cref="SessionKeepAliveMsg" />
    /// messages to the query source.  This means that if the message handler returned
    /// without replying to the query message or throwing an exception that the client
    /// side of the query session will eventially timeout and throw an exception that
    /// can be caught and handled by the application.
    /// </para>
    /// <para>
    /// Setting <b>IsAsync=true</b> for a QuerySession message handler indicates that
    /// the handler may complete the operation asynchronously.  This means that the
    /// QuerySession will continue to send <see cref="SessionKeepAliveMsg" /> messages
    /// to the query source, even after the message handler has returned.  Asynchronous
    /// QuerySession message handlers are responsible for indicating that they are
    /// done by calling the <see cref="SessionBase.OnAsyncFinished" /> method of the
    /// session passed in the <see cref="Msg._Session" /> property of the message
    /// originally passed to the handler.  This method <b>MUST</b> be called by at 
    /// some point by the application otherwise the session will remain alive indefinitely,
    /// sending <see cref="SessionKeepAliveMsg" /> messages.
    /// </para>
    /// <para>
    /// The <see cref="MsgSessionAttribute.MaxAsyncKeepAliveTime" /> property can be
    /// used to specify a maximum lifetime for an async handler.  This indicates how
    /// the maximum time the QuerySession should continue to send <see cref="SessionKeepAliveMsg" />
    /// messages back to the client.  This is an important safety feature that will prevent
    /// sessions from remaining active indefinitely, sending keepalive messages.  Prudent
    /// applications will specify a reasonable maximum timeout here or track and manage
    /// timing out sessions internally.
    /// </para>
    /// <para><b><u>Query Specific Session Parameters</u></b></para>
    /// <para>
    /// The <see cref="QuerySession" /> class requires no additional query specific parameters 
    /// to be specified in the <see cref="MsgSessionAttribute.Parameters" /> property
    /// of the <see cref="MsgSessionAttribute" /> tagging the application's message handler.
    /// </para>
    /// </remarks>
    public class QuerySession : SessionBase, ISession
    {
        /// <summary>
        /// Constructs the client side implementation of a QueryResponse session.
        /// </summary>
        public QuerySession()
            : base()
        {
            this.arQuery = null;
        }

        //-------------------------------------------------------------------
        // Client-side implementation members

        private int             retry;          // Current retry count
        private DateTime        retryTime;      // Time-to-retry (SYS)
        private Msg             query;          // The query message
        private Msg             response;       // The response (or null)
        private Exception       error;          // The exception to throw
        private AsyncResult     arQuery;        // The query async result

        /// <summary>
        /// Initiates an asynchronous query operation by sending the message
        /// passed to the target endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The async result used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// Each call to <see cref="BeginQuery" /> must be matched with a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginQuery(MsgEP toEP, Msg query, AsyncCallback callback, object state)
        {
            Assertion.Test(arQuery == null, "Cannot reuse a query session.");

            this.query = query;

            query._ToEP      = toEP.Clone(true);
            query._FromEP    = base.Router.RouterEP.Clone(true);
            query._SessionID = base.SessionID;
            query._Flags    |= MsgFlag.OpenSession | MsgFlag.ServerSession;

            if (base.Router.DeadRouterDetection)
                query._Flags |= MsgFlag.ReceiptRequest;

            retry     = 0;
            retryTime = base.StartTime + TimeSpan.FromTicks(base.Router.SessionTimeout.Ticks);
            arQuery   = new AsyncResult(base.SessionManager, callback, state);
            response  = null;
            error     = null;

            base.TTD       = DateTime.MaxValue;   // Client side QuerySessions handle their own lifespan
            base.IsRunning = true;

            try
            {
                query._Trace(base.Router, 2, "Q/R Query", null);
                base.SessionManager.ClientStart(this);
                base.Router.Send(query);
            }
            catch (Exception e)
            {
                AsyncResult arTemp;

                arQuery.Notify(e);
                arTemp = arQuery;
                arQuery = null;

                base.IsRunning = false;

                return arTemp;
            }

            arQuery.Started();
            return arQuery;
        }

        /// <summary>
        /// Completes the execution of an asynchronous query operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginQuery" />.</param>
        /// <returns>The query response message.</returns>
        /// <remarks>
        /// <note>
        /// Each call to <see cref="BeginQuery" /> must be matched with
        /// a call to EndQuery.
        /// </note>
        /// </remarks>
        public Msg EndQuery(IAsyncResult ar)
        {
            var arQuery = (AsyncResult)ar;

            arQuery.Wait();

            try
            {
                if (arQuery.Exception != null)
                    throw arQuery.Exception;
#if TRACE
                if (error != null)
                    query._Trace(base.Router, 2, "Q/R Finish", null, "Exception: {0}", error.ToString());
                else
                {
                    var sb = new StringBuilder(512);

                    sb.Append("\r\nResponse:\r\n\r\n");
                    response._TraceDetails(base.Router, sb);

                    query._Trace(base.Router, 2, "Q/R Finish", "", sb.ToString());
                }
#endif
                Assertion.Test(error != null || response != null, "Either an error or a response should be present.");

                if (error != null)
                {
                    Helper.Rethrow(error);
                    throw null;
                }
                else
                    return response;
            }
            finally
            {
                arQuery.Dispose();
            }
        }

        /// <summary>
        /// Performs a synchronous query operation by sending the message
        /// passed to the target endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <returns>The query response.</returns>
        /// <remarks>
        /// <note>
        /// The endpoint passed may be either a physical or logical
        /// endpoint.
        /// </note>
        /// </remarks>
        public Msg Query(MsgEP toEP, Msg query)
        {
            var ar = BeginQuery(toEP, query, null, null);

            return EndQuery(ar);
        }

        /// <summary>
        /// Handles client side messages.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void OnClientMsg(Msg msg)
        {
            msg._Trace(base.Router, 2, "Q/R Client Recv", null);
            Assertion.Test((msg._Flags & MsgFlag.OpenSession) == 0);

            using (TimedLock.Lock(base.Router.SyncRoot))
            {
                if (arQuery == null)
                    return;

                // If the message is a SessionKeepAliveMsg then reset
                // the time-to-retry timer.

                var keepAliveMsg = msg as SessionKeepAliveMsg;

                if (keepAliveMsg != null)
                {
                    retryTime = SysTime.Now + keepAliveMsg.SessionTTL;
                    return;
                }

                // I'm going to assume that any other message received that is
                // associated with this session is the response so
                // set the response field and signal that the session
                // is finished.

                response = msg;
                arQuery.Notify();
                arQuery = null;

                base.IsRunning = false;

                // Tell the session manager

                base.SessionManager.OnFinished(this);
            }
        }

        /// <summary>
        /// Handles client side cancelation.
        /// </summary>
        private void ClientCancel()
        {
            query._Trace(base.Router, 2, "Q/R Timeout", null);

            using (TimedLock.Lock(base.Router.SyncRoot))
            {
                if (arQuery == null)
                    return;

                Router.Metrics.SessionTimeouts.Increment();

                // Set the error field and signal that the session
                // is finished.

                error = new TimeoutException();
                arQuery.Notify();
                arQuery = null;

                base.IsRunning = false;

                // Tell the session manager

                base.SessionManager.OnFinished(this);
            }
        }

        /// <summary>
        /// Handles client side background activities.
        /// </summary>
        private void OnClientBkTimer()
        {
            DateTime    now = SysTime.Now;
            Msg         clone;

            using (TimedLock.Lock(base.Router.SyncRoot))
            {
                if (arQuery == null)
                    return;

                // See if it's time to send a retry message

                if (now >= retryTime)
                {
                    retry++;
                    if (retry >= base.Router.SessionRetries)
                    {
                        ClientCancel();
                        return;
                    }

                    Router.Metrics.SessionRetries.Increment();
                    query._Trace(base.Router, 2, "Q/R Retry", null);

                    retryTime        = now + base.Router.SessionTimeout;
                    clone            = query.Clone();
                    clone._SessionID = query._SessionID;

                    base.Router.Send(clone);
                }
            }
        }

        //---------------------------------------------------------------------
        // Server-side implementation members

        private TimeSpan    keepAliveInterval;  // Interval between keep-alive transmissions
        private DateTime    nextKeepAlive;      // Time to next keep-alive transmission (SYS)

        /// <summary>
        /// Starts the server session initialized with InitServer().
        /// </summary>
        public override void StartServer()
        {
            SessionKeepAliveMsg     keepAliveMsg;
            bool                    exceptionThrown = false;
            DateTime                now             = SysTime.Now;

            try
            {
                base.IsRunning = true;
                base.TTD       = DateTime.MaxValue;
                this.query     = base.ServerInitMsg;

                // Send the first keep alive and schedule the next.

                keepAliveInterval = TimeSpan.FromTicks(base.SessionInfo.KeepAliveTime.Ticks / 3);
                nextKeepAlive     = now + keepAliveInterval;

                keepAliveMsg = new SessionKeepAliveMsg(base.SessionInfo.KeepAliveTime);
                keepAliveMsg._SessionID = base.SessionID;

                base.Router.SendTo(base.ClientEP, keepAliveMsg);

                // This simply drops through to the message handler.  Unhandled
                // exceptions will cause an Ack to be sent in response to the
                // original message.

                base.Method.Invoke(base.Target, new object[] { base.ServerInitMsg });
            }
            catch (TargetInvocationException eInvoke)
            {
                var e = eInvoke.InnerException;
                var ack = new Ack();

                exceptionThrown = true;
                ack.Exception = e.Message;
                ack.ExceptionTypeName = e.GetType().FullName;

                base.Router.ReplyTo(query, ack);
            }
            catch (Exception e)
            {
                var ack = new Ack();

                exceptionThrown = true;
                ack.Exception = e.Message;
                ack.ExceptionTypeName = e.GetType().FullName;

                base.Router.ReplyTo(query, ack);
            }
            finally
            {
                if (!base.IsAsync || exceptionThrown)
                {
                    base.IsRunning = false;

                    if (base.CacheEnable)
                    {
                        base.TTD = now + base.Router.SessionCacheTime;
                        base.SessionManager.OnFinished(this);
                    }
                    else
                        base.TTD = now;
                }
                else
                {
                    if (base.SessionInfo.MaxAsyncKeepAliveTime == TimeSpan.MaxValue)
                        base.TTD = DateTime.MaxValue;
                    else
                        base.TTD = SysTime.Now + SessionInfo.MaxAsyncKeepAliveTime;
                }
            }
        }

        /// <summary>
        /// Called by server side session message handlers whose <see cref="MsgSessionAttribute.IsAsync" />
        /// property is set to <c>true</c> to indicate that the asynchronous operation
        /// has been completed and the session should no longer be tracked.
        /// </summary>
        public override void OnAsyncFinished()
        {
            var now = SysTime.Now;

            if (base.CacheEnable)
                base.TTD = now + base.Router.SessionCacheTime;
            else
                base.TTD = now;

            base.OnAsyncFinished();
        }

        /// <summary>
        /// Handles server side messages.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void OnServerMsg(Msg msg)
        {
            // If we haved a cached reply for this session then
            // resend it, otherwise ignore this message.

            if (base.Reply != null)
            {
                base.Reply._Trace(base.Router, 2, "Q/R Cached Reply", null);
                base.Router.Send(base.Reply.Clone());
            }
            else
                msg._Trace(base.Router, 2, "Q/R Msg Ignored", null);
        }

        /// <summary>
        /// Handles server side cancellation.
        /// </summary>
        private void ServerCancel()
        {
            // Ignored
        }

        /// <summary>
        /// Handles server side background activities.
        /// </summary>
        private void OnServerBkTimer()
        {
            DateTime                now;
            SessionKeepAliveMsg     keepAliveMsg;

            // Send periodic keep-alives back to the client as long as 
            // the session is still running.

            if (!IsRunning)
                return;

            // If the session is async and has exceeded its lifespan
            // then stop sending keep-alives.

            now = SysTime.Now;

            if (base.IsAsync && now >= base.TTD)
            {
                IsRunning = false;
                return;
            }

            // Send a keep-alive if it's time.

            if (now >= nextKeepAlive)
            {
                nextKeepAlive           = now + keepAliveInterval;
                keepAliveMsg            = new SessionKeepAliveMsg(base.SessionInfo.KeepAliveTime);
                keepAliveMsg._SessionID = base.SessionID;

                base.Router.SendTo(base.ClientEP, keepAliveMsg);
            }
        }

        //---------------------------------------------------------------------
        // Common implementation members

        /// <summary>
        /// Handles any received messages (not including the first message directed to the server) 
        /// associated with this session.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        /// <remarks>
        /// <note>
        /// The first message sent to server side session will result in a
        /// call to <see cref="StartServer" /> rather than a call to this method.
        /// </note>
        /// </remarks>
        public override void OnMsg(Msg msg, SessionHandlerInfo sessionInfo)
        {
            if (base.IsClient)
                OnClientMsg(msg);
            else
                OnServerMsg(msg);
        }

        /// <summary>
        /// Cancels the session.  This may be called on both the client and the server.
        /// </summary>
        public override void Cancel()
        {
            if (base.IsClient)
                ClientCancel();
            else
                ServerCancel();
        }

        /// <summary>
        /// Called periodically on a worker thread providing a mechanism
        /// for the sessions to perform any background work.
        /// </summary>
        public override void OnBkTimer()
        {
            if (base.IsClient)
                OnClientBkTimer();
            else
                OnServerBkTimer();
        }
    }
}
