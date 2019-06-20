//-----------------------------------------------------------------------------
// FILE:        SessionBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a base class for most ISession behaviors.

using System;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a base class for most <see cref="ISession" /> behaviors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Classes implementing the <see cref="ISession" /> inferface implement both 
    /// the client and server sides of a conversation between multiple endpoints.
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
    /// or retry.  The session manage will also route any messages it receives with this
    /// session's <see cref="SessionID" /> to the session by passing it to the <see cref="OnMsg" />
    /// method.  Finally, the session manager polls the session's <see cref="TTD" /> property
    /// to determine if the session has expired and should be discarded. (Note that some session 
    /// implementations may choose to set TTD to DateTime.MaxValue and perform their own lifetime management). 
    /// </para>
    /// <para><b><u>Server-side Sessions</u></b></para>
    /// <para>
    /// Server side sessions will be created automatically by the <see cref="MsgDispatcher" />
    /// for messages whose MsgFlag.OpenSession flag is set and whose application handler is
    /// tagged with a <see cref="MsgSessionAttribute" /> attribute.  This attribute specifies
    /// the type of session to create via the <see cref="MsgSessionAttribute.Type" /> or
    /// <see cref="MsgSessionAttribute.TypeRef" /> properties.
    /// </para>
    /// <para>
    /// Server sessions are initiated in the MsgDispatcher by instantiating a session instance, 
    /// calling <see cref="InitServer" /> to associate the router, session manager, target handler,
    /// and the initiating message with the session.  Then call <see cref="StartServer" />
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
    /// </remarks>
    public class SessionBase
    {
        //-------------------------------------------------------------------------------
        // Static members

        private static SessionHandlerInfo defSessionInfo;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SessionBase()
        {
            MsgSessionAttribute attr;

            attr = new MsgSessionAttribute();
            attr.Type = SessionTypeID.Unknown;
            defSessionInfo = new SessionHandlerInfo(attr);
        }

        //-------------------------------------------------------------------------------
        // Instance members

        private MsgRouter           router;         // The associated message router
        private ISessionManager     sessionMgr;     // The associated session manager
        private Guid                sessionID;      // The session's globally unique ID
        private bool                isClient;       // True for a client implemenation, false for server
        private TimeSpan            ttl;            // Session time-to-live
        private bool                isAsync;        // True if the session is async
        private bool                isRunning;      // True if the session is still running
        private DateTime            startTime;      // Time the session was started (SYS)
        private DateTime            finishTime;     // Time the session was completed (SYS)
        private DateTime            ttd;            // Session time-to-die (SYS)
        private Msg                 serverInitMsg;  // The message that initiated a server session (or null)
        private MsgEP               clientEP;       // The client's endpoint (or null)
        private object              target;         // The dispatch target
        private MethodInfo          method;         // The dispatch method
        private SessionHandlerInfo  sessionInfo;    // Session information associated with the handler
                                                    // for the server side (null for the client side)
        private Msg                 cachedReply;    // The cached reply message (or null).

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SessionBase()
        {
        }

        /// <summary>
        /// Initializes a client side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="sessionID">The session ID to assign to this session</param>
        public virtual void InitClient(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Guid sessionID)
        {
            this.isClient      = true;
            this.router        = router;
            this.sessionMgr    = sessionMgr;
            this.sessionID     = sessionID;
            this.startTime     = SysTime.Now;
            this.finishTime    = DateTime.MaxValue;
            this.ttd           = startTime + ttl;
            this.ttl           = ttl;
            this.serverInitMsg = null;
            this.clientEP      = null;
            this.target        = null;
            this.method        = null;
            this.sessionInfo   = null;
            this.cachedReply   = null;
        }

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
        public virtual void InitServer(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Msg msg, object target,
                                       MethodInfo method, SessionHandlerInfo sessionInfo)
        {
            if (sessionInfo == null)
                sessionInfo = SessionHandlerInfo.Default;

            this.isClient      = false;
            this.router        = router;
            this.sessionMgr    = sessionMgr;
            this.sessionID     = msg._SessionID;
            this.isAsync       = sessionInfo.IsAsync;
            this.isRunning     = true;
            this.startTime     = SysTime.Now;
            this.finishTime    = DateTime.MaxValue;
            this.ttd           = startTime + ttl;
            this.ttl           = ttl;
            this.serverInitMsg = msg;
            this.clientEP      = msg._FromEP;
            this.target        = target;
            this.method        = method;
            this.sessionInfo   = sessionInfo != null ? sessionInfo : defSessionInfo;
            this.cachedReply   = null;

            msg._Session       = (ISession)this;
        }

        /// <summary>
        /// Starts the server session initialized with InitServer().
        /// </summary>
        public virtual void StartServer()
        {
        }

        /// <summary>
        /// True for a client session, false for a server
        /// session.
        /// </summary>
        public bool IsClient
        {
            get { return isClient; }
            set { isClient = value; }
        }

        /// <summary>
        /// True for a server session, false for a client
        /// session.
        /// </summary>
        public bool IsServer
        {
            get { return !isClient; }
            set { isClient = !value; }
        }

        /// <summary>
        /// Set to <c>true</c> if this is an async server session.
        /// </summary>
        public bool IsAsync
        {

            get { return isAsync; }
            set { isAsync = value; }
        }

        /// <summary>
        /// Returns the router associated with the session.
        /// </summary>
        public MsgRouter Router
        {
            get { return router; }
        }

        /// <summary>
        /// Returns the session manager that owns this session.
        /// </summary>
        public ISessionManager SessionManager
        {
            get { return sessionMgr; }
        }

        /// <summary>
        /// Returns the session's globally unique ID.
        /// </summary>
        public Guid SessionID
        {
            get { return sessionID; }
        }

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
        public virtual ISessionHandler SessionHandler
        {
            get { return null; }

            set
            {
                if (value != null)
                    throw new InvalidOperationException(string.Format("Session type [{0}] does not require an [ISessionHandler].", this.GetType().Name));
            }
        }

        /// <summary>
        /// Returns the session handler information associated with
        /// the application message handler (null for client side sessions). 
        /// </summary>
        public SessionHandlerInfo SessionInfo
        {
            get { return sessionInfo; }
        }

        /// <summary>
        /// Returns <c>true</c> if this session should be cached by the session manager
        /// for a period of time after it completes to make the session idempotent.
        /// </summary>
        public bool CacheEnable
        {
            get { return sessionInfo.Idempotent; }
        }

        /// <summary>
        /// Returns the interval to be used when transmitting <see cref="SessionKeepAliveMsg" />
        /// messages from the session (defaults to 5s).
        /// </summary>
        public TimeSpan KeepAliveTime
        {
            get { return sessionInfo.KeepAliveTime; }
        }

        /// <summary>
        /// The session time-to-live.
        /// </summary>
        public TimeSpan TTL
        {
            get { return ttl; }
        }

        /// <summary>
        /// <c>true</c> if the session is still running.
        /// </summary>
        public bool IsRunning
        {
            get { return isRunning; }
            set { isRunning = value; }
        }

        /// <summary>
        /// The time the session was initiated (SYS).
        /// </summary>
        public DateTime StartTime
        {
            get { return startTime; }
            set { startTime = value; }
        }

        /// <summary>
        /// The time the session was finished (SYS).
        /// </summary>
        public DateTime FinishTime
        {
            get { return finishTime; }
            set { finishTime = value; }
        }

        /// <summary>
        /// The session's scheduled time-to-die if the session
        /// is still pending or the time to remove cached completed or 
        /// cancelled sessions (SYS).
        /// </summary>
        public DateTime TTD
        {

            get { return ttd; }
            set { ttd = value; }
        }

        /// <summary>
        /// Returns the client endpoint for server side sessions,
        /// null for client sessions.
        /// </summary>
        public MsgEP ClientEP
        {
            get { return clientEP; }
        }

        /// <summary>
        /// Returns the initiating message for server side sessions, null
        /// for client side session instances.
        /// </summary>
        public Msg ServerInitMsg
        {
            get { return serverInitMsg; }
        }

        /// <summary>
        /// Returns the dispatch target instance for server side sessions.
        /// </summary>
        public object Target
        {
            get { return target; }
        }

        /// <summary>
        /// Returns the dispatch target method for server side sessions.
        /// </summary>
        public MethodInfo Method
        {
            get { return method; }
        }

        /// <summary>
        /// Handles any received messages associated with this session.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        public virtual void OnMsg(Msg msg, SessionHandlerInfo sessionInfo)
        {
        }

        /// <summary>
        /// Called when MsgRouter.ReplyTo() is called in response to a query/reponse
        /// or other session pattern.  This gives the session 
        /// the chance to cache server side reply messages. 
        /// </summary>
        /// <param name="msg">The reply message.</param>
        public void OnReply(Msg msg)
        {
            this.cachedReply = msg;
        }

        /// <summary>
        /// Returns the session's reply message if one is
        /// cached, null otherwise.
        /// </summary>
        public Msg Reply
        {
            get { return cachedReply; }
        }

        /// <summary>
        /// Cancels the session.  This may be called on both the client and the server.
        /// </summary>
        public virtual void Cancel()
        {
        }

        /// <summary>
        /// Called periodically on a worker thread providing a mechanism
        /// for the sessions to perform any background work.
        /// </summary>
        public virtual void OnBkTimer()
        {
        }

        /// <summary>
        /// Called by server side session message handlers whose <see cref="MsgSessionAttribute.IsAsync" />
        /// property is set to <c>true</c> to indicate that the asynchronous operation
        /// has been completed and the session should no longer be tracked.
        /// </summary>
        public virtual void OnAsyncFinished()
        {
            isRunning = false;
            sessionMgr.OnFinished((ISession)this);
        }
    }
}
