//-----------------------------------------------------------------------------
// FILE:        DuplexSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a general purpose symmetric two sided session that 
//              supports sending messages and queries from either side.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

// $todo(jeff.lill): Implement support form concurrent queries.

namespace LillTek.Messaging
{
    /// <summary>
    /// Delegate called when <see cref="DuplexSession" /> raises its <see cref="DuplexSession.ReceiveEvent" />
    /// when a message is received from the remote side of the session.
    /// </summary>
    /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
    /// <param name="msg">The message received.</param>
    public delegate void DuplexReceiveDelegate(DuplexSession session, Msg msg);

    /// <summary>
    /// Delegate called when <see cref="DuplexSession" /> raises its <see cref="DuplexSession.QueryEvent" />
    /// when a query is received from the remote side of the session.
    /// </summary>
    /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
    /// <param name="query">The query message received.</param>
    /// <param name="async">Returns as <c>true</c> if the query will be processed asynchronously.</param>
    /// <returns>The query response or <c>null</c> if the query is being processed asynchronously.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="DuplexSession"/> supports both synchronous and asynchronrous 
    /// query processing via calls to a <see cref="DuplexQueryDelegate" />.  Synchronous 
    /// implmentations are simple: the delegate performs any necessary work and
    /// returns the query response messages.  
    /// </para>
    /// <para>
    /// Asynchronous processing is slightly more complex.  In this case, the
    /// delegate initiates the necessary work, retaining a copy of the
    /// <paramref name="session" /> parameter.  When processing completes,
    /// the query code will need to call the  session's <see cref="DuplexSession.ReplyTo(Msg,Msg)" /> 
    /// method, passing the response message.
    /// </para>
    /// </remarks>
    public delegate Msg DuplexQueryDelegate(DuplexSession session, Msg query, out bool async);

    /// <summary>
    /// Delegate called when a <see cref="DuplexSession" /> is closed.
    /// </summary>
    /// <param name="session">The <see cref="DuplexSession" /> firing the event.</param>
    /// <param name="timeout">
    /// <c>true</c> if the session closed due to a keep-alive timeout,
    /// <c>false</c> if the remote side explicitly closed its side
    /// of the session.
    /// </param>
    public delegate void DuplexCloseDelegate(DuplexSession session, bool timeout);

    /// <summary>
    /// Implements a general purpose symmetric two sided session that 
    /// supports sending messages and queries from either side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Duplex sessions are used to establish a persistent session between two 
    /// endpoints on the network where multiple messages can be sent in 
    /// each direction.  The session is created by the client application
    /// and the session is established using the <see cref="Connect" /> or
    /// <see cref="BeginConnect" /> and <see cref="EndConnect" /> methods,
    /// passing the logical or physical endpoint of the server.  An <b>open</b>
    /// message is submitted to the message router with a unique <b>SessionID</b>
    /// and the message's <b>FromEP</b> set to the physical endpoint of the
    /// client.  The message will be routed to a server in the usual way.  The
    /// server will respond with an an <b>open-ack</b> message with the 
    /// <b>FromEP</b> set to the physical endpoint of the server.  From this
    /// point on, all traffic between the two ends of the session will be
    /// addressed to these physical endpoints using the unique session ID.
    /// </para>
    /// <para>
    /// Here's an example establishing a session on the client side:
    /// </para>
    /// <code language="cs">
    /// MsgRouter           router;     // Already initialized and started
    /// DuplexSession       session;
    /// 
    /// session               = router.CreateDuplexSession();
    /// session.ReceiveEvent += new DuplexReceiveDelegate(MyReceiveHandler);
    /// session.QueryEvent   += new DuplexQueryDelegate(MyQueryHandler);
    /// session.CloseEvent   += new DuplexCloseDelegate(MyCloseHandler);
    /// session.Connect("logical://MyApp/Server");
    /// </code>
    /// <para>
    /// To expose a session on the server side, you need to add a 
    /// message handler for the <see cref="DuplexSessionMsg" /> to obtain
    /// the <see cref="DuplexSession" /> instance.  To abort the connection
    /// the handler needs only to throw an exception.  Here is a server 
    /// side example:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler(LogicalEP="logical://MyApp/Server")]
    /// [MsgSession(Type=SessionTypeID.Duplex,KeepAlive="15s",SessionTimeout="120s")]
    /// public void OnMsg(DuplexSessionMsg msg) {
    /// 
    ///     DuplexSession   session = msg.Session;
    /// 
    ///     session.ReceiveEvent += new DuplexReceiveDelegate(MyReceiveHandler);
    ///     session.QueryEvent   += new DuplexQueryDelegate(MyQueryHandler);
    ///     session.CloseEvent   += new DuplexCloseDelegate(MyCloseHandler);
    /// }
    /// </code>
    /// <para>
    /// <see cref="DuplexSession" /> exposes three events: <see cref="ReceiveEvent" />,
    /// <see cref="QueryEvent" />, and <see cref="CloseEvent" />.  These events
    /// are raised by the session when a non-query application message is received
    /// by the session, when a query message is received, and when the session
    /// is closed explicitly or due to a timeout.  Applications will need to enlist
    /// in these events to process them.
    /// </para>
    /// <para>
    /// Once a session is established, either side can send a message to the other
    /// via <see cref="Send" /> or perform a query using <see cref="Query" /> or
    /// <see cref="BeginQuery" /> and <see cref="EndQuery" />.  Note that duplex
    /// sessions support only one query at a time in each direction.  Either side
    /// can disconnect the session by calling <see cref="Close()" />.  The <see cref="IsConnected" />
    /// property returns <c>true</c> when a connection is currently established.
    /// </para>
    /// <para>
    /// Each side of the session sends periodic keep-alive messages to the other
    /// side if there's no other message traffic.  Session ends that don't see
    /// any messages for some period of time will assume that the session has
    /// been closed or crashed on the other side.  The keep-alive and maximum
    /// wait time periods are specified as the <b>KeepAlive</b> and <b>SessionTimeout</b>
    /// settings in the <see cref="MsgSessionAttribute" /> tagging the server
    /// side message handler.  These are set to <c>KeepAlive="15s"</c> and 
    /// <c>SessionTimeout="120s"</c> in the <c>[MsgSession]</c> attribute in
    /// the example above.  These settings are communicated back to the client
    /// in the <b>open-ack</b> message.
    /// </para>
    /// <note>
    /// The <b>SessionTimeout</b> setting actually used by a duplex session will
    /// be the maximum of the <b>SessionTimeout</b> setting specified and 
    /// <b>three times</b> the router's <see cref="MsgRouter.BkInterval" />.
    /// </note>
    /// <para>
    /// The session implements reasonable support for session queries.  As was
    /// mentioned above, only one outstanding query is allowed in each direction
    /// on a session.  This means that the client and server can each have a
    /// query running against the other side, but that the client or server can't 
    /// have more than one query running in parallel.  The duplex session
    /// protocol currently implements a simple but effective mechanism for ensuring 
    /// the retransmission of query and response messages if necessary.  Here's
    /// how this works:
    /// </para>
    /// <list type="number">
    ///     <item>The application initiates a query.</item>
    ///     <item>
    ///     The query side <see cref="DuplexSession" /> generates an internal outbound query ID
    ///     and adds this to the application's query message as the <see cref="MsgHeaderID.DuplexSession" />
    ///     extension header and then sends the message to the response side of
    ///     the session.
    ///     </item>
    ///     <item>
    ///     The query side <see cref="DuplexSession" /> starts a timer that 
    ///     periodically resends the request message in case it got lost during 
    ///     transmission.
    ///     </item>
    ///     <item>
    ///     The response side <see cref="DuplexSession" /> receives the request message
    ///     and sends a <b>query-request-ack</b> back to the query side indicating
    ///     that it received the message and also raises the <see ref="QueryEvent" />
    ///     so that the application can process the query (note that this event
    ///     is not raised if the query received was a retransmission).
    ///     </item>
    ///     <item>
    ///     The query side <see cref="DuplexSession" /> stops its retransmission
    ///     timer when it receives the <b>query-request-ack</b> and starts a
    ///     new timer that sends <b>query-status</b> messages.
    ///     </item>
    ///     <item>
    ///     When application completes processing the query, the response side
    ///     <see cref="DuplexSession" /> transmits the response back to the 
    ///     query side and also caches a copy of the response.
    ///     </item>
    ///     <item>
    ///     When the query side receives the response, it stops the <b>query-status</b>
    ///     timer, sends a <b>query-response-ack</b> to the response side and
    ///     returns the response to the calling application.
    ///     </item>
    ///     <item>
    ///     When the response side receives a <b>query-status</b> it checks to
    ///     see if the query is still being processed.  If this is the case, then
    ///     the message is ignored.  Otherwise, if the query matches the cached
    ///     query response then the response will be retransmitted to the query
    ///     side.
    ///     </item>
    ///     <item>
    ///     When the response side receives a <b>query-response-ack</b> it
    ///     clears the cached response message.
    ///     </item>
    /// </list>
    /// <para>
    /// Both the client and server side of a session can process queries by
    /// enlisting in the <see cref="QueryEvent" />.  This event calls a <see cref="DuplexQueryDelegate" />
    /// when raised which will handle the actual query processing.  
    /// <see cref="DuplexSession"/> supports both synchronous and asynchronrous 
    /// query processing via calls to a <see cref="DuplexQueryDelegate" />.  Synchronous 
    /// implmentations are simple: the delegate performs any necessary work and
    /// returns the query response messages.  Here's an example:
    /// </para>
    /// <code language="cs">
    /// private Msg MyQueryProcessor(DuplexSession session,Msg query,out bool isAsync) {
    /// 
    ///     // Perform query processing
    /// 
    ///     isAsync = true;     // Indicate that queuy is being processed synchronously
    ///     return new Msg();   // Create and return the query response
    /// }
    /// 
    /// [MsgHandler(LogicalEP="logical://MyApp/Server")]
    /// [MsgSession(Type=SessionTypeID.Duplex,KeepAlive="15s",SessionTimeout="120s")]
    /// public void OnMsg(DuplexSessionMsg msg) {
    /// 
    ///     DuplexSession   session = msg.Session;
    /// 
    ///     session.ReceiveEvent += new DuplexReceiveDelegate(MyQueryProcessor);
    /// }
    /// </code>
    /// <para>
    /// Asynchronous processing is slightly more complex.  In this case, the
    /// query event handler initiates the necessary work, retaining copies of the
    /// <b>session</b> and <b>query</b> parameters.  When processing completes,
    /// the query code will need to call the session's <see cref="DuplexSession.ReplyTo(Msg,Msg)" /> 
    /// method, passing the response message.  Here's an example:
    /// </para>
    /// <code language="cs">
    /// private Msg MyQueryProcessor(DuplexSession session,Msg query,out bool isAsync) {
    /// 
    ///     // Initiate async query processing, saving a
    ///     // reference to "session"
    /// 
    ///     isAsync = true;     // Indicate that queuy is being processed asynchronously
    ///     return null;        // Indicate that processing continues asynchronously
    /// }
    /// 
    /// // Internal method called when asynchronous processing is complete.
    /// private void OnDone(DuplexSession session,Msg query,Msg response) {
    /// 
    ///     session.ReplyTo(query,response);
    /// }
    /// 
    /// [MsgHandler(LogicalEP="logical://MyApp/Server")]
    /// [MsgSession(Type=SessionTypeID.Duplex,KeepAlive="15s",SessionTimeout="120s")]
    /// public void OnMsg(DuplexSessionMsg msg) {
    /// 
    ///     DuplexSession   session = msg.Session;
    /// 
    ///     session.ReceiveEvent += new DuplexReceiveDelegate(MyQueryProcessor);
    /// }
    /// </code>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class DuplexSession : SessionBase, ISession
    {
        //---------------------------------------------------------------------
        // Private types

        private enum State
        {
            Idle,
            Connecting,
            Connected,
            Disconnected
        }

        private enum QueryState
        {
            Idle,
            Submitting,
            Waiting,
            Complete
        }

        //---------------------------------------------------------------------
        // Static members

        private static long nextID = 0;         // The next available session ID

        /// <summary>
        /// Returns a process unique ID.
        /// </summary>
        private static long GetNextID()
        {
            return Interlocked.Increment(ref nextID);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The trace subsystem name for the <see cref="DuplexSession" /> and related classes.
        /// </summary>
        public const string TraceSubsystem   = "Messaging.DuplexSession";

        private const string NotConnectedMsg = "Duplex session is not connected.";

        private object          syncLock;           // The thread sync instance
        private long            id;                 // Process unique connection ID
        private MsgRouter       router;             // The associated router
        private MsgEP           remoteEP;           // The physical endpoint of the other side
                                                    // of the session.
        private State           state;              // The current connection state
        private AsyncResult     arConnect;          // Non-null if a connect is pending
        private TimeSpan        keepAliveTime;      // Interval between keep-alive transmissions
        private TimeSpan        sessionTTL;         // Maximum time to wait for a keep-alive
        private DateTime        nextKeepAlive;      // Scheduled time to send the next keep-alive (SYS)
        private DateTime        sessionTTD;         // Scheduled time to kill the session if no messages (SYS)
        private DateTime        nextConRetry;       // Scheduled time to resend the open message (SYS)
        private int             cConMsgs;           // # of open messages sent so far
        private object          userData;           // Application-specific data

        // Query related members

        private QueryState      queryState;         // Indicates the query client side state
        private AsyncResult     arQuery;            // Non-null if a query is pending
        private Guid            inQueryID;          // ID of the current inbound query
        private Guid            outQueryID;         // ID of the current outbound query
        private Msg             cachedRequest;      // Cached query request message (or null)
        private Msg             cachedResponse;     // Cached query response message (or null)
        private DateTime        nextQueryTimer;     // Scheduled time for the next query client
        private NetFailMode     networkMode;        // Set this to simulate various network failures

        /// <summary>
        /// Raised when application messages are received from the remote session.
        /// </summary>
        public event DuplexReceiveDelegate ReceiveEvent;

        /// <summary>
        /// Raised when an application query is received from the remote session.
        /// </summary>
        public event DuplexQueryDelegate QueryEvent;

        /// <summary>
        /// Raised when the session is closed.
        /// </summary>
        public event DuplexCloseDelegate CloseEvent;

        /// <summary>
        /// Constructor.
        /// </summary>
        public DuplexSession()
            : base()
        {
            this.syncLock       = null;
            this.state          = State.Idle;
            this.arConnect      = null;
            this.nextKeepAlive  = DateTime.MaxValue;
            this.sessionTTD     = DateTime.MaxValue;

            this.queryState     = QueryState.Idle;
            this.arQuery        = null;
            this.id             = 0;
            this.inQueryID      = Guid.Empty;
            this.outQueryID     = Guid.Empty;
            this.cachedRequest  = null;
            this.cachedResponse = null;
            this.nextQueryTimer = DateTime.MaxValue;
            this.userData       = null;
            this.networkMode    = NetFailMode.Normal;
        }

        //---------------------------------------------------------------------
        // Server side implementation

        /// <summary>
        /// Initializes a server side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="msg">The message that triggered this session.</param>
        /// <param name="target">The dispatch target instance.</param>
        /// <param name="method">The dispatch method.</param>
        /// <param name="sessionInfo">
        /// The session information associated with the handler or <c>null</c>
        /// to use session defaults.
        /// </param>
        public override void InitServer(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Msg msg, object target,
                                        MethodInfo method, SessionHandlerInfo sessionInfo)
        {
            base.InitServer(router, sessionMgr, ttl, msg, target, method, sessionInfo);

            base.IsRunning = true;
            base.IsAsync   = true;
            this.router    = router;
            this.syncLock  = router.SyncRoot;
            base.TTD       = DateTime.MaxValue;
        }

        /// <summary>
        /// Starts the server session initialized with InitServer().
        /// </summary>
        public override void StartServer()
        {
            DuplexSessionMsg ack;

            // Peform some initialization

            Assertion.Test(state == State.Idle);

            base.IsRunning     = true;
            this.id            = GetNextID();
            this.inQueryID     = Guid.Empty;
            this.remoteEP      = base.ServerInitMsg._FromEP;
            this.keepAliveTime = base.SessionInfo.KeepAliveTime;
            this.sessionTTL    = Helper.Max(base.SessionInfo.SessionTimeoutTime, Helper.Multiply(router.BkInterval, 3));
            this.nextKeepAlive = SysTime.Now + keepAliveTime;

            Assertion.Test(remoteEP != null);
            Assertion.Test(remoteEP.IsPhysical);

            Trace(0, "Start Server");

            // Call the message target.  If it throws an exception then
            // we'll abort the connection, if is doesn't, we'll acknowledge it.

            try
            {
                base.Method.Invoke(base.Target, new object[] { base.ServerInitMsg });

                ack               = new DuplexSessionMsg(DuplexSessionMsg.OpenAck);
                ack.KeepAliveTime = keepAliveTime;
                ack.SessionTTL    = sessionTTL;

                this.sessionTTD = SysTime.Now + sessionTTL;

                if (base.SessionInfo.MaxAsyncKeepAliveTime == TimeSpan.MaxValue)
                    base.TTD = DateTime.MaxValue;
                else
                    base.TTD = SysTime.Now + SessionInfo.MaxAsyncKeepAliveTime;

                SetState(State.Connected);
                SendInternal(ack);
            }
            catch (TargetInvocationException e)
            {
                ack           = new DuplexSessionMsg(DuplexSessionMsg.OpenAck);
                ack.Exception = e.InnerException.Message;

                SetState(State.Disconnected);
                SendInternal(ack);
            }
            catch (Exception e)
            {
                ack           = new DuplexSessionMsg(DuplexSessionMsg.OpenAck);
                ack.Exception = e.Message;

                SetState(State.Disconnected);
                SendInternal(ack);
            }
        }

        /// <summary>
        /// Called by server side session message handlers whose <see cref="MsgSessionAttribute.IsAsync" />
        /// property is set to <c>true</c> to indicate that the asynchronous operation
        /// has been completed and the session should no longer be tracked.
        /// </summary>
        public override void OnAsyncFinished()
        {
            base.OnAsyncFinished();
            Close(true);
        }

        //---------------------------------------------------------------------
        // Client side implementation

        /// <summary>
        /// Initializes a client side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="sessionID">The session ID to assign to this session</param>
        public override void InitClient(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Guid sessionID)
        {
            base.InitClient(router, sessionMgr, ttl, sessionID);

            this.router    = router;
            this.syncLock = router.SyncRoot;
        }

        /// <summary>
        /// Synchronously establishes a duplex session with a remote endpoint.
        /// </summary>
        /// <param name="serverEP">The server endpoint.</param>
        public void Connect(MsgEP serverEP)
        {
            var ar = BeginConnect(serverEP, null, null);

            EndConnect(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to establish a duplex session with
        /// a remote endpoint.
        /// </summary>
        /// <param name="serverEP">The remote endpoint.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application-defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the status of the operation.</returns>
        /// <remarks>
        /// <note>
        /// A successsful calls to <see cref="BeginConnect" /> must be matched with a call to <see cref="EndConnect" />.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if a connection is already established or if a connection attempt is pending.</exception>
        public IAsyncResult BeginConnect(MsgEP serverEP, AsyncCallback callback, object state)
        {
            DuplexSessionMsg msg;

            using (TimedLock.Lock(syncLock))
            {
                Trace(0, "BeginConnect");

                if (this.router == null)
                    throw new InvalidOperationException("Session is not associated with a router. Verify that the session is constructed via MsgRouter.CreateSession().");

                if (this.state == State.Connected)
                    throw new InvalidOperationException("A connection is already established.");

                if (this.state == State.Connecting)
                    throw new InvalidOperationException("A connection attempt is already pending.");

                if (this.state == State.Disconnected)
                    throw new InvalidOperationException("DuplexSessions cannot be reused.");

                base.TTD = DateTime.MaxValue;
                base.SessionManager.ClientStart(this);

                this.IsRunning    = true;
                this.id           = GetNextID();
                this.inQueryID    = Guid.Empty;
                this.remoteEP     = serverEP.Clone(true);
                this.arConnect    = new AsyncResult(null, callback, state);
                this.cConMsgs     = 1;
                this.nextConRetry = SysTime.Now + router.SessionTimeout;
                this.sessionTTL   = Helper.Max(router.SessionTimeout, Helper.Multiply(router.BkInterval, 3));
                this.sessionTTD   = SysTime.Now + sessionTTL;

                SetState(State.Connecting);

                msg         = new DuplexSessionMsg(DuplexSessionMsg.OpenCmd);
                msg._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession;

                SendInternal(msg);
                arConnect.Started();
            }

            return arConnect;
        }

        /// <summary>
        /// Completes an asynchronous connection operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginConnect" />.</param>
        public void EndConnect(IAsyncResult ar)
        {
            var arConnect = (AsyncResult)ar;

            arConnect.Wait();
            try
            {
                Trace(0, string.Format("EndConnect: {0}", arConnect.Exception == null ? "OK" : arConnect.Exception.Message));
                if (arConnect.Exception != null)
                {
                    SetState(State.Disconnected);
                    throw arConnect.Exception;
                }
                else
                    SetState(State.Connected);
            }
            finally
            {
                arConnect.Dispose();
                this.arConnect = null;
            }
        }

        //---------------------------------------------------------------------
        // Common implementation

        /// <summary>
        /// Used by unit tests to simulate a network failure.
        /// </summary>
        internal NetFailMode NetworkMode
        {
            get { return networkMode; }
            set { networkMode = value; }
        }

        /// <summary>
        /// Returns the session's process unique connection ID.
        /// </summary>
        /// <remarks>
        /// A new process unique ID is assigned to a <see cref="DuplexSession" />
        /// whenever a a session connection is established.  Otherwise, the
        /// ID is set to 0.  This is important in connection pooling scenarios
        /// where application code needs to determine if a connection has been
        /// pooled or recycled.
        /// </remarks>
        public long ID
        {
            get { return id; }
        }

        /// <summary>
        /// Available for applications to store application-specific information.
        /// </summary>
        public object UserData
        {
            get { return userData; }
            set { userData = value; }
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
            const string headerFmt = "state={0} qstate={0}\r\n----------\r\n";
            string header;

            header = string.Format(headerFmt, state, queryState, "na");
            if (details == null)
                details = string.Empty;

            NetTrace.Write(TraceSubsystem, detail, string.Format("Duplex: [state={0} qstate={1}]",
                                                                 state.ToString().ToUpper(),
                                                                 queryState.ToString().ToUpper()),
                           Helper.ToTrace(SysTime.Now) + "  " + (base.IsClient ? "CLIENT: " : "SERVER: ") + summary, header + details);
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
        /// Writes information about the message passed to the <see cref="NetTrace" />
        /// </summary>
        /// <param name="summary">The summary string.</param>
        /// <param name="msg">The message.</param>
        [Conditional("TRACE")]
        internal void Trace(string summary, Msg msg)
        {
#if TRACE
            var duplexMsg = msg as DuplexSessionMsg;

            if (duplexMsg == null)
                Trace(0, string.Format("{0}: {1}", summary, msg.GetType().Name));
            else
                Trace(1, string.Format("{0}: Duplex: {1}", summary, duplexMsg.Command.ToUpper()), duplexMsg.GetTrace());
#endif  // TRACE
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

        /// <summary>
        /// Returns <c>true</c> if the session is currently connected.
        /// </summary>
        public bool IsConnected
        {
            get { return state == State.Connected; }
        }

        /// <summary>
        /// Closes the session if it's currently open.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        /// <summary>
        /// Closes the session if it's currently open.
        /// </summary>
        /// <param name="timeout"><b><c>true</c> if the session is being closed due to a timeout.</b></param>
        private void Close(bool timeout)
        {
            Trace(0, string.Format("Close: timeout={0}", timeout));

            switch (this.state)
            {
                case State.Connecting:
                case State.Connected:

                    using (TimedLock.Lock(syncLock))
                    {
                        if (arConnect != null)
                            arConnect.Notify(new CancelException());

                        if (arQuery != null)
                            arQuery.Notify(new TimeoutException());
                    }

                    SendInternal(new DuplexSessionMsg(DuplexSessionMsg.CloseCmd));
                    RaiseClose(timeout);

                    SetState(State.Disconnected);
                    break;
            }
        }

        /// <summary>
        /// Sets the session state, dumping trace information.
        /// </summary>
        /// <param name="newState">The new <see cref="State" />.</param>
        private void SetState(State newState)
        {
            if (state == newState)
                return;

            Trace(0, string.Format("State: {0} --> {1}", state.ToString().ToUpper(), newState.ToString().ToUpper()), new CallStack(1, true).ToString());

            state = newState;
            if (state == State.Disconnected)
            {
                this.id        = 0;
                this.inQueryID = Guid.Empty;
                base.SessionManager.OnFinished(this);
            }
        }

        /// <summary>
        /// Sets the session query state, dumping trace information.
        /// </summary>
        /// <param name="newState">The new <see cref="QueryState" />.</param>
        private void SetQueryState(QueryState newState)
        {
            if (queryState == newState)
                return;

            Trace(0, string.Format("Query State: {0} --> {1}", queryState.ToString().ToUpper(), newState.ToString().ToUpper()), new CallStack(1, true).ToString());

            queryState = newState;
        }

        /// <summary>
        /// Used internally to handle message transmission to the remote side.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void SendInternal(Msg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state == State.Connected)
                    nextKeepAlive = SysTime.Now + keepAliveTime;    // Advance the next scheduled keep-alive transmission
                                                                    // whenever we send something to the remote side
                if (base.IsClient)
                    msg._Flags |= MsgFlag.ServerSession;

                msg._SessionID = base.SessionID;

                switch (networkMode) 
                {
                    case NetFailMode.Normal :

                        break;

                    case NetFailMode.Intermittent :

                        if (Helper.Rand()%4 == 0) 
                        {
                            Trace("Send: Simulated intermittent net failure",msg);
                            return;
                        }

                        break;

                    case NetFailMode.Duplicate :

                        Msg         clone;

                        Trace("Send: Simulated duplicate packet",msg);

                        clone = msg.Clone();
                        if (base.IsClient)
                            clone._Flags |= MsgFlag.ServerSession;

                        clone._SessionID = SessionID;
                        router.SendTo(remoteEP,router.RouterEP,clone);
                        break;

                    case NetFailMode.Disconnected :

                        Trace("Send: Simulated network failure",msg);
                        return;

                    case NetFailMode.Delay:

                        Thread.Sleep(100);
                        break;
                }

                Trace("Send", msg);
                router.SendTo(remoteEP, router.RouterEP, msg);
            }
        }

        /// <summary>
        /// Sends a message to the remote side of the session.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// The message passed must not be reused by the application.
        /// </note>
        /// <para>
        /// The <see cref="DuplexSession" /> class <b>does not</b> implement any sort
        /// of confirmation mechanism for messages sent via this method to the 
        /// remote side.  It is possible for messages to be lost in transit.
        /// Use <see cref="Query" /> if it is necessary to ensure that messages
        /// are delivered.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the connection is closed.</exception>
        public void Send(Msg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state != State.Connected)
                    throw new InvalidOperationException(NotConnectedMsg);

                SendInternal(msg);
            }
        }

        /// <summary>
        /// Performs a synchronous query against the remote side of the session.
        /// </summary>
        /// <param name="query">The query message.</param>
        /// <returns>The query response.</returns>
        /// <remarks>
        /// <para>
        /// The <see cref="DuplexSession" /> goes to a lot of effort to ensure that
        /// queries and responses are delivered between the endpoints.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the connection is closed.</exception>
        public Msg Query(Msg query)
        {
            var ar = BeginQuery(query, null, null);

            return EndQuery(ar);
        }

        /// <summary>
        /// Initiates an asynchronous query against the remote side of the session.
        /// </summary>
        /// <param name="query">The query message.</param>
        /// <param name="callback">The <see cref="AsyncCallback" /> to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">The application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance used to track the status of the operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the connection is closed or another query is in progress.</exception>
        /// <remarks>
        /// <para>
        /// The <see cref="DuplexSession" /> goes to a lot of effort to ensure that
        /// queries and responses are delivered between the endpoints.
        /// </para>
        /// <note>
        /// All successful calls to <see cref="BeginQuery" /> must be matched with a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginQuery(Msg query, AsyncCallback callback, object state)
        {
            var arQuery = new AsyncResult(null, callback, state);

            using (TimedLock.Lock(syncLock))
            {
                Trace("BeginQuery", query);

                if (this.state != State.Connected)
                    throw new InvalidOperationException(NotConnectedMsg);

                if (queryState != QueryState.Idle)
                    throw new InvalidOperationException("A query is already in progress. DuplexSession supports only one outstanding query from each side of the session.");

                outQueryID    = Helper.NewGuid();
                cachedRequest = query;

                query._ExtensionHeaders.Set(MsgHeaderID.DuplexSession, new DuplexSessionHeader(outQueryID, DuplexMessageType.Query).ToArray());

                SetQueryState(QueryState.Submitting);

                nextQueryTimer = SysTime.Now + router.SessionTimeout;
                this.arQuery   = arQuery;

                SendInternal(query);
                arQuery.Started();

                return arQuery;
            }
        }

        /// <summary>
        /// Completes an asynchronous query operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The query response.</returns>
        /// <exception cref="InvalidOperationException">Thrown if there is no query pending.</exception>
        /// <exception cref="CancelException">Thrown if the connection was closed while the query was in progress.</exception>
        /// <exception cref="TimeoutException">Thrown if the remote side of the sesssion did not respond in time.</exception>
        /// <exception cref="SessionException">Thrown if an exception was thrown by the remote side.</exception>
        public Msg EndQuery(IAsyncResult ar)
        {
            var     arQuery = (AsyncResult)ar;
            Msg     response;
            IAck    ack;

            arQuery.Wait();
            try
            {
                Trace(0, "EndQuery");
                if (arQuery.Exception != null)
                    throw arQuery.Exception;

                response = (Msg)arQuery.Result;
                ack = response as IAck;

                if (ack != null && ack.Exception != null)
                {
                    if (ack.ExceptionTypeName == typeof(TimeoutException).FullName)
                        throw new TimeoutException(ack.Exception);
                    else
                        throw SessionException.Create(ack.ExceptionTypeName, ack.Exception);
                }

                return response;
            }
            finally
            {
                using (TimedLock.Lock(syncLock))
                {
                    SetQueryState(QueryState.Idle);

                    this.arQuery        = null;
                    this.nextQueryTimer = DateTime.MaxValue;
                    this.outQueryID     = Guid.Empty;
                }

                arQuery.Dispose();
            }
        }

        /// <summary>
        /// Raises <see cref="ReceiveEvent" /> when a message is received.
        /// </summary>
        /// <param name="msg">The message.</param>
        private void RaiseReceive(Msg msg)
        {
            if (ReceiveEvent != null && state == State.Connected)
            {
                msg._Session = this;
                ReceiveEvent(this, msg);
            }
        }

        /// <summary>
        /// Raises <see cref="QueryEvent" /> when a message is received.
        /// </summary>
        /// <param name="msg">The query message.</param>
        /// <param name="async">Returns as <c>true</c> if the query is being processed asynchronously.</param>
        /// <returns>The query response message or <c>null</c> if there is no event handler.</returns>
        private Msg RaiseQuery(Msg msg, out bool async)
        {
            async = false;

            if (QueryEvent != null && state == State.Connected)
            {
                Msg response;

                msg._Session = this;
                response = QueryEvent(this, msg, out async);
                async = response == null;

                return response;
            }
            else
                return null;
        }

        /// <summary>
        /// Raises <see cref="CloseEvent" />.
        /// </summary>
        /// <param name="timeout"><c>true</c> if the session is being closed due to a keep-alive timeout.</param>
        private void RaiseClose(bool timeout)
        {
            if (CloseEvent != null && (state == State.Connected || state == State.Connecting))
                CloseEvent(this, timeout);
        }

        /// <summary>
        /// Completes asynchronous query processing by sending the query response message.
        /// </summary>
        /// <param name="query">The original query message.</param>
        /// <param name="response">The query response.</param>
        /// <remarks>
        /// <note>
        /// This method is smart enough to handle situations where the session
        /// has been closed during asynchronous query processing and even if it
        /// has been recycled from a connection pool.  In either case, the method
        /// simply returns without performing any operations.
        /// </note>
        /// </remarks>
        public void ReplyTo(Msg query, Msg response)
        {
            using (TimedLock.Lock(syncLock))
            {
                MsgHeader           header;
                DuplexSessionHeader duplexHeader;

                header = query._ExtensionHeaders[MsgHeaderID.DuplexSession];
                if (header == null)
                    throw new ArgumentException("Message is not a DuplexSession query.", "query");

                duplexHeader = new DuplexSessionHeader(header.Contents);

                if (state != State.Connected)
                    return;     // Session is not connected

                if (duplexHeader.QueryID != inQueryID)
                    return;     // Looks like the session has been recycled

                response._ExtensionHeaders.Set(MsgHeaderID.DuplexSession,
                                               new DuplexSessionHeader(duplexHeader.QueryID, DuplexMessageType.Response).ToArray());
                cachedResponse = response;
                SendInternal(response);
            }
        }

        /// <summary>
        /// Initiates the delivery of a reply to a request whose
        /// <see cref="MsgRequestContext" /> is passed.
        /// </summary>
        /// <param name="context">The <see cref="MsgRequestContext" /> holding state about the original request message.</param>
        /// <param name="response">The response message.</param>
        /// <remarks>
        /// <note>
        /// This is not a general purpose method.  It is designed to be
        /// called only by the <see cref="MsgRequestContext" /> type.
        /// </note>
        /// <para>
        /// Use of this override helps to save memory since the entire
        /// request message doesn't need to be retained throughout the
        /// time that the request is being processed since <see cref="MsgRequestContext" />
        /// copies only the few message header fields necessary to submit
        /// the reply.
        /// </para>
        /// <para>
        /// This method works by routing the reply to the FromEP
        /// of the original message, after copying the original
        /// session ID into the reply.
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        internal void ReplyTo(MsgRequestContext context, Msg response)
        {
            using (TimedLock.Lock(syncLock))
            {
                MsgHeader           header;
                DuplexSessionHeader duplexHeader;

                header = context.Header;
                Assertion.Test(header != null);

                duplexHeader = new DuplexSessionHeader(header.Contents);

                if (state != State.Connected)
                    return;     // Session is not connected

                if (duplexHeader.QueryID != inQueryID)
                    return;     // Looks like the session has been recycled

                response._ExtensionHeaders.Set(MsgHeaderID.DuplexSession,
                                               new DuplexSessionHeader(duplexHeader.QueryID, DuplexMessageType.Response).ToArray());
                cachedResponse = response;
                SendInternal(response);
            }
        }

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
            try
            {
                // $todo(jeff.lill): 
                //
                // I'm not super happy with my thread locking strategy
                // in this method.  I think I may be leaving myself
                // open to problems for intensely multi-threaded
                // applications.

                DuplexSessionMsg duplexMsg = msg as DuplexSessionMsg;
                DuplexSessionMsg reply;

                Trace("Receive", msg);

                using (TimedLock.Lock(syncLock))
                    sessionTTD = SysTime.Now + sessionTTL;      // Extend the session life whenever we see traffic
                                                                // from the remote side
                switch (state)
                {
                    case State.Idle:

                        break;

                    case State.Connecting:

                        if (base.IsClient && duplexMsg != null && duplexMsg.Command == DuplexSessionMsg.OpenAck)
                        {
                            // Looks like we have a connection response from the server.

                            if (duplexMsg.Exception == null)
                            {
                                using (TimedLock.Lock(syncLock))
                                {
                                    this.remoteEP      = duplexMsg._FromEP;
                                    this.keepAliveTime = duplexMsg.KeepAliveTime;
                                    this.sessionTTL    = Helper.Max(duplexMsg.SessionTTL, Helper.Multiply(router.BkInterval, 3));
                                    this.sessionTTD    = SysTime.Now + sessionTTL;
                                    this.nextKeepAlive = SysTime.Now + keepAliveTime;

                                    SetState(State.Connected);
                                }

                                arConnect.Notify();
                            }
                            else
                            {
                                arConnect.Notify(SessionException.Create(null, duplexMsg.Exception));
                                Close(false);
                            }
                        }
                        break;

                    case State.Connected:

                        if (duplexMsg == null)
                        {
                            MsgHeader           header = msg._ExtensionHeaders[MsgHeaderID.DuplexSession];
                            DuplexSessionHeader duplexHeader;
                            Msg                 response;

                            if (header == null)
                            {
                                // The message is not part of a query so send it directly to the application.

                                Trace(1, "Normal Message");
                                RaiseReceive(msg);
                                break;
                            }

                            // The message is part of a query.

                            Trace(1, "Query Message");
                            duplexHeader = new DuplexSessionHeader(header.Contents);
                            switch (duplexHeader.Type)
                            {
                                case DuplexMessageType.Query:

                                    // Send a QUERY-REQUEST-ACK back to query side

                                    SendInternal(new DuplexSessionMsg(DuplexSessionMsg.QueryRequestAck, duplexHeader.QueryID));

                                    using (TimedLock.Lock(syncLock))
                                    {
                                        // Exit if this query is currently being processed or has been completed.

                                        if (inQueryID == duplexHeader.QueryID)
                                            break;

                                        // Process the query

                                        inQueryID      = duplexHeader.QueryID;
                                        cachedResponse = null;
                                    }

                                    try
                                    {
                                        bool async;

                                        response = RaiseQuery(msg, out async);
                                        if (async)
                                            return;

                                        if (response == null)
                                            throw SessionException.Create(null, "DuplexSession has no QueryEvent handler.");

                                        response._ExtensionHeaders.Set(MsgHeaderID.DuplexSession,
                                                                       new DuplexSessionHeader(duplexHeader.QueryID, DuplexMessageType.Response).ToArray());
                                        cachedResponse = response;
                                        SendInternal(response);
                                    }
                                    catch (TargetInvocationException e)
                                    {
                                        response = new Ack(e.InnerException);
                                        response._ExtensionHeaders.Set(MsgHeaderID.DuplexSession,
                                                                       new DuplexSessionHeader(duplexHeader.QueryID, DuplexMessageType.Response).ToArray());
                                        cachedResponse = response;
                                        SendInternal(response);
                                    }
                                    catch (Exception e)
                                    {
                                        response = new Ack(e);
                                        response._ExtensionHeaders.Set(MsgHeaderID.DuplexSession,
                                                                       new DuplexSessionHeader(duplexHeader.QueryID, DuplexMessageType.Response).ToArray());
                                        cachedResponse = response;
                                        SendInternal(response);
                                    }
                                    break;

                                case DuplexMessageType.Response:

                                    if (duplexHeader.QueryID != outQueryID || queryState == QueryState.Idle || arQuery == null)
                                        break;      // Ignore responses that don't correlate to the current query.

                                    using (TimedLock.Lock(syncLock))
                                    {
                                        var ar = arQuery;

                                        if (duplexHeader.QueryID != outQueryID || arQuery == null)
                                        {
                                            // Acknowledge the query response even though it appears that
                                            // we've already done so because there's no query outstanding or
                                            // the response query ID doesn't match the current query.  We'll
                                            // see this situation if messages are lost or duplicated so
                                            // acknowledging this will add some robustness to the protocol.

                                            SendInternal(new DuplexSessionMsg(DuplexSessionMsg.QueryResponseAck, outQueryID));
                                            break;
                                        }

                                        SendInternal(new DuplexSessionMsg(DuplexSessionMsg.QueryResponseAck, outQueryID));
                                        SetQueryState(QueryState.Complete);

                                        cachedRequest = null;
                                        nextQueryTimer = DateTime.MaxValue;

                                        ar.Result = msg;
                                        ar.Notify();
                                    }
                                    break;

                                default:

                                    return;     // Ignore
                            }
                            break;
                        }

                        switch (duplexMsg.Command)
                        {
                            case DuplexSessionMsg.OpenCmd:

                                if (base.IsServer)
                                {
                                    // The server has received another OPEN message.  This can happen if
                                    // the client missed the first response so simply resend another.

                                    reply               = new DuplexSessionMsg(DuplexSessionMsg.OpenAck);
                                    reply.KeepAliveTime = sessionInfo.KeepAliveTime;
                                    reply.SessionTTL    = sessionInfo.SessionTimeoutTime;

                                    SendInternal(reply);
                                }
                                break;

                            case DuplexSessionMsg.OpenAck:

                                break;  // Ignore

                            case DuplexSessionMsg.CloseCmd:

                                Close(false);
                                break;

                            case DuplexSessionMsg.QueryStatusCmd:

                                using (TimedLock.Lock(syncLock))
                                {
                                    if (inQueryID == duplexMsg.QueryID && cachedResponse != null)
                                    {
                                        // The requesting side must have missed the response so
                                        // resend it.

                                        SendInternal(cachedResponse.Clone());
                                        break;
                                    }

                                    if (inQueryID != duplexMsg.QueryID)
                                    {
                                        SendInternal(new Ack("Unable to correlate status request to a query."));
                                        break;
                                    }
                                }
                                break;

                            case DuplexSessionMsg.QueryRequestAck:

                                using (TimedLock.Lock(syncLock))
                                {
                                    if (queryState == QueryState.Submitting)
                                    {
                                        SetQueryState(QueryState.Waiting);
                                        nextQueryTimer = SysTime.Now + router.SessionTimeout;
                                    }
                                }
                                break;

                            case DuplexSessionMsg.QueryResponseAck:

                                using (TimedLock.Lock(syncLock))
                                {
                                    inQueryID      = Guid.Empty;
                                    cachedResponse = null;
                                }
                                break;

                            case DuplexSessionMsg.KeepAliveCmd:

                                break;
                        }

                        break;

                    case State.Disconnected:

                        break;
                }
            }
            catch (Exception e)
            {
                Trace("OnMsg", e);
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Cancels the session.  This may be called on both the client and the server.
        /// </summary>
        public override void Cancel()
        {
            // This is NOP.
        }

        /// <summary>
        /// Called periodically on a worker thread providing a mechanism
        /// for the sessions to perform any background work.
        /// </summary>
        public override void OnBkTimer()
        {
            DateTime            now = SysTime.Now;
            DuplexSessionMsg    duplex;

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    switch (state)
                    {
                        case State.Idle:
                        case State.Disconnected:

                            break;

                        case State.Connecting:

                            Assertion.Test(base.IsClient);
                            if (now >= nextConRetry)
                            {
                                // Time to retry sending the OPEN message

                                if (cConMsgs >= router.SessionRetries)
                                {
                                    arConnect.Notify(new TimeoutException());
                                    break;
                                }

                                cConMsgs++;
                                nextConRetry = now + router.SessionTimeout;
                                sessionTTD = now + sessionTTL;

                                duplex = new DuplexSessionMsg(DuplexSessionMsg.OpenCmd);
                                duplex._Flags |= MsgFlag.OpenSession | MsgFlag.ServerSession;

                                SendInternal(duplex);
                            }
                            break;

                        case State.Connected:

                            if (now >= sessionTTD)
                            {
                                // The session has timed-out

                                Close(true);
                                break;
                            }

                            if (now >= nextQueryTimer)
                            {
                                switch (queryState)
                                {
                                    case QueryState.Idle:

                                        break;

                                    case QueryState.Submitting:

                                        if (cachedRequest != null)
                                            SendInternal(cachedRequest.Clone());

                                        break;

                                    case QueryState.Waiting:

                                        SendInternal(new DuplexSessionMsg(DuplexSessionMsg.QueryStatusCmd, outQueryID));
                                        break;
                                }

                                nextQueryTimer = SysTime.Now + router.SessionTimeout;
                            }

                            if (now >= nextKeepAlive)
                                SendInternal(new DuplexSessionMsg(DuplexSessionMsg.KeepAliveCmd));

                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Trace("BkTimer", e);
                SysLog.LogException(e);
            }
        }
    }
}
