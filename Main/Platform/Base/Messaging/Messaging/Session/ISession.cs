//-----------------------------------------------------------------------------
// FILE:        ISession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the basic behavior of a session.

using System;
using System.Reflection;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the basic behavior of a messaging session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Classes implementing the <see cref="ISession" /> interface implement both the client and server sides
    /// of a conversation between multiple endpoints.
    /// </para>
    /// <para><b><u>Client-side Sessions</u></b></para>
    /// <para>
    /// Client sessions are initiated by instantiating the session instance, calling 
    /// <see cref="InitClient" /> to associate the session to a router, session manager, and GUID.
    /// Then send the initiating message to the server endpoint, setting the message's
    /// <see cref="Msg._SessionID" /> property to this session's GUID and the message's
    /// <see cref="Msg._FromEP" /> property to the client router's physical endpoint.
    /// </para>
    /// <para>
    /// The session manager then periodically calls the session's <see cref="OnBkTimer" />
    /// method to give it a chance to handle any background functionality such as timeout
    /// or retry processing.  The session manager will also route any messages it receives with this
    /// session's <see cref="SessionID" /> to the session by passing it to the <see cref="OnMsg" />
    /// method.  Finally, the session manager polls the session's <see cref="TTD" /> property
    /// to determine if the session has expired and should be discarded. (Note that some session 
    /// implementations may choose to set TTD to <see cref="DateTime.MaxValue" /> and perform 
    /// their own lifetime management). 
    /// </para>
    /// <para><b><u>Server-side Sessions</u></b></para>
    /// <para>
    /// Server side sessions will be created automatically by the <see cref="MsgDispatcher" />
    /// for messages whose <see cref="MsgFlag.OpenSession" /> flag is set and whose application handler
    /// is tagged with a <see cref="MsgSessionAttribute" /> attribute.  This attribute specifies
    /// the type of session to create via the <see cref="MsgSessionAttribute.Type" /> or
    /// <see cref="MsgSessionAttribute.TypeRef" /> properties.
    /// </para>
    /// <para>
    /// Server sessions are initiated in the <see cref="MsgDispatcher" /> by instantiating a session 
    /// instance, calling <see cref="InitServer" /> to associate the router, session manager, target 
    /// handler, and the initiating message with the session.  Then call <see cref="StartServer" />
    /// on a worker thread to start the session.
    /// </para>
    /// <para>
    /// The session then performs any necessary activities including executing code and
    /// exchanging addition messages with the client.  The session manager will periodically 
    /// call the session's <see cref="OnBkTimer" /> method to give it a chance to handle 
    /// any background functionality such as timeout or retry.  The session manage will 
    /// also route any messages it receives with this session's <see cref="SessionID" /> to the 
    /// session by passing it to the <see cref="OnMsg" /> method.  Finally, the session 
    /// has expired and should be discarded. (Note that some session implementations may
    /// choose to set TTD to DateTime.MaxValue and perform their own lifetime management). 
    /// </para>
    /// <para>
    /// The session manager is also capable of caching a message representing the session's
    /// final disposition.  This is a relatively simple way to implement idempotent-like
    /// behavior.  Sessions can enable this functionality by having the <see cref="CacheEnable" />
    /// property return true and the <see cref="Reply" /> property return the disposition
    /// message.  Then, when the session manager processes session initiate message for 
    /// a cached session, the session manager will simply respond with the cached reply
    /// message.
    /// </para>
    /// <para><b><u>Asynchronous Server-side Sessions</u></b></para>
    /// <para>
    /// Setting the <see cref="MsgSessionAttribute.IsAsync" /> property of a message handler
    /// to <c>true</c> indicates to a session that the handler will complete the
    /// operation asynchronously.  Session implementations use this to determine how
    /// to interpret a message handler return to the session.
    /// </para>
    /// <para>
    /// Setting <b>IsAsync=true</b> for a session message handler indicates that
    /// the handler may complete the operation asynchronously.  Asynchronous
    /// session message handlers are responsible for indicating that they are
    /// done by calling the <see cref="OnAsyncFinished" /> method of the
    /// session passed in the <see cref="Msg._Session" /> property of the message
    /// originally passed to the handler.  This method <b>MUST</b> be called by at 
    /// some point by the application otherwise the session will remain alive indefinitely.
    /// </para>
    /// <para>
    /// The <see cref="MsgSessionAttribute.MaxAsyncKeepAliveTime" /> property can be
    /// used to specify a maximum lifetime for an async handler.  This indicates how
    /// the maximum time the session should continue to consider the session to be
    /// active.  This is an important safety feature that will prevent sessions from remaining 
    /// active indefinitely, sending keepalive messages, etc.  Prudent
    /// applications will specify a reasonable maximum timeout here or track and manage
    /// timing out sessions internally.
    /// </para>
    /// <para><b><u>Advanced Session Implementation</u></b></para>
    /// <para>
    /// The usage pattern of the <see cref="QuerySession" /> is pretty straight forward.  The client
    /// sends a query message to the server.  The message is routed to a message handler method
    /// which simply sends a reply message.  More advanced session scenarios are also possible.
    /// <see cref="DuplexSession" /> and <see cref="ReliableTransferSession" /> are a good examples.
    /// </para>
    /// <para>
    /// <see cref="DuplexSession" /> can be used to establish a potentially long-lived
    /// session between two specific service instances on the network where messages
    /// can be sent and queries can be initiated from either side of the session.
    /// </para>
    /// <para>
    /// <see cref="ReliableTransferSession" /> is used to transfer byte streams from the
    /// client to the server or from the server to the client.  This transfer is
    /// performed by sending multiple payload messages from one end of the session to
    /// the other rather than sending a single potentially huge message.  A single
    /// message handler method is still used in these scenarios, but rather than
    /// sending a reply message as with <see cref="QuerySession" />, the message
    /// handler will allocate an object implementing <see cref="ISessionHandler" />
    /// and assign it to the <see cref="SessionHandler" /> property of the session
    /// associated with the received message.  From this point, the message handler
    /// can return and all subsequent application interaction with the session will
    /// be done via the session handler.
    /// </para>
    /// <para> 
    /// Advanced session types require specific <see cref="ISessionHandler" /> 
    /// implementations.  For example, <see cref="ReliableTransferSession" /> requires
    /// that an <see cref="ReliableTransferHandler" /> class be passed.  Session implementions
    /// must verify that the handler object passed is reasonable and throw an
    /// <see cref="InvalidOperationException" /> otherwise.  Sessions that don't
    /// make use of a handler should throw an exception if any object is assigned.
    /// </para>
    /// <para>
    /// Client side sessions are created using the <see cref="MsgRouter" />'s <see cref="MsgRouter.CreateSession" />
    /// method.  This method creates the client side session and registers it with the
    /// router's <see cref="ISessionManager" /> implementation.  The client then instantiates
    /// the proper <see cref="ISessionHandler" /> type, registers any event handlers,
    /// assigns the handler instance to the session's <see cref="SessionHandler" /> property,
    /// and then calls a session type specific method to initiate the session.  Here's
    /// an client side example for the reliable transfer session:
    /// </para>
    /// <code language="cs">
    /// MsgRouter               router;     // Already initialized and started
    /// ReliableTransferSession session;
    /// ReliableTransferHandler handler;
    /// 
    /// handler                = new ReliableTransferHandler();
    /// handler.SendEvent     += new ReliableTransferDelegate(MySendHandler);
    /// 
    /// session                = router.CreateSession(typeof(ReliableTransferSession,TimeSpan.FromMinutes(2));
    /// session.SessionHandler = handler;
    /// session.Transfer("logical://MyApp/Transfer",TransferDirection.Upload);
    /// </code>
    /// <para>
    /// Here's a partial example of of the code necessary to
    /// handle the server side of this transfer:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler(LogicalEP="logical://MyApp/Transfer")]
    /// [MsgSession(Type=SessionTypeID.ReliableTransfer)]
    /// public void OnMsg(ReliableTransferMsg msg)
    /// {
    ///     ReliableTransferHandler handler;
    ///     ISession                session = msg._Session;
    /// 
    ///     handler                = new ReliableTransferHandler();
    ///     handler.ReceiveEvent  += new ReliableTransferDelegate(MyReceiveHandler);
    ///     session.SessionHandler = handler;
    /// }
    /// </code>
    /// <para><b><u>Query Specific Session Parameters</u></b></para>
    /// <para>
    /// The <see cref="QuerySession" />, <see cref="DuplexSession" /> or <see cref="ReliableTransferSession" /> 
    /// classes require no additional query specific parameters to be specified in the 
    /// <see cref="MsgSessionAttribute.Parameters" /> property of the <see cref="MsgSessionAttribute" /> 
    /// tagging the application's message handler.
    /// </para>
    /// </remarks>
    public interface ISession
    {
        /// <summary>
        /// Initializes a client side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="sessionID">The ID to be assigned to this session</param>
        void InitClient(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Guid sessionID);

        /// <summary>
        /// Initializes a server side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="msg">The message that triggered this session.</param>
        /// <param name="target">The dispatch target instance.</param>
        /// <param name="method">The dispatch method.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        /// <remarks>
        /// The implementation of this method should verify that the message passed is
        /// a valid session initiation message.  If this is not the case, then the method
        /// should throw a MsgException describing the problem.
        /// </remarks>
        void InitServer(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Msg msg, object target,
                        MethodInfo method, SessionHandlerInfo sessionInfo);

        /// <summary>
        /// Starts the server session initialized with <see cref="InitServer" />.
        /// </summary>
        void StartServer();

        /// <summary>
        /// Returns the session's globally unique ID.
        /// </summary>
        Guid SessionID { get; }

        /// <summary>
        /// The <see cref="ISessionHandler" /> used to implement advanced multi-message
        /// session scenarios.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <see cref="ISession" /> implementations must verify that
        /// the session assigned is valid for the implementation and throw an
        /// <see cref="InvalidOperationException" /> if this is not the case.
        /// </note>
        /// </remarks>
        ISessionHandler SessionHandler { get; set; }

        /// <summary>
        /// Returns the router associated with the session.
        /// </summary>
        MsgRouter Router { get; }

        /// <summary>
        /// Returns the session manager that owns this session.
        /// </summary>
        ISessionManager SessionManager { get; }

        /// <summary>
        /// True for a client session, false for a server session.
        /// </summary>
        bool IsClient { get; set; }

        /// <summary>
        /// True for a server session, false for a client session.
        /// </summary>
        bool IsServer { get; set; }

        /// <summary>
        /// Returns <c>true</c> if this session should be cached by the session manager
        /// for a period of time after it completes to make the session idempotent.
        /// </summary>
        bool CacheEnable { get; }

        /// <summary>
        /// The session time-to-live.
        /// </summary>
        TimeSpan TTL { get; }

        /// <summary>
        /// <c>true</c> if the session is still running.
        /// </summary>
        bool IsRunning { get; set; }

        /// <summary>
        /// The time the session was initiated (SYS).
        /// </summary>
        DateTime StartTime { get; set; }

        /// <summary>
        /// The time the session was completed (SYS).
        /// </summary>
        DateTime FinishTime { get; set; }

        /// <summary>
        /// The session's scheduled time-to-die if the session
        /// is still pending or the time to remove cached completed or 
        /// cancelled sessions (SYS).
        /// </summary>
        DateTime TTD { get; set; }

        /// <summary>
        /// Handles any received messages (not including the first message directed to the server) associated with this session.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        /// <remarks>
        /// <note>
        /// The first message sent to server side session will result in a
        /// call to <see cref="StartServer" /> rather than a call to this method.
        /// </note>
        /// </remarks>
        void OnMsg(Msg msg, SessionHandlerInfo sessionInfo);

        /// <summary>
        /// Called when <see cref="MsgRouter.ReplyTo(LillTek.Messaging.Msg, LillTek.Messaging.Msg)" /> 
        /// is called in response to a query or other session pattern.  This gives the session 
        /// the chance to cache server side reply messages. 
        /// </summary>
        /// <param name="msg">The reply message.</param>
        void OnReply(Msg msg);

        /// <summary>
        /// Returns the session's reply message if one is
        /// cached, null otherwise.
        /// </summary>
        Msg Reply { get; }

        /// <summary>
        /// Cancels the session.  This may be called on both the client and the server.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Called periodically on a worker thread providing a mechanism
        /// for the sessions to perform any background work.
        /// </summary>
        void OnBkTimer();

        /// <summary>
        /// Called by server side session message handlers whose <see cref="MsgSessionAttribute.IsAsync" />
        /// property is set to <c>true</c> to indicate that the asynchronous operation
        /// has been completed and the session should no longer be tracked.
        /// </summary>
        void OnAsyncFinished();
    }
}
