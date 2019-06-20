//-----------------------------------------------------------------------------
// FILE:        SessionManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The default implementation of a session manager.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging
{
    /// <summary>
    /// The default implementation of a session manager.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Session managers are used on both the client and server side of a
    /// session to coordinate the execution of a session.  Session
    /// managers provide two basic services:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Provides an easy to use, blocking client side interface for that
    ///     handles the actual messaging to the server, including retries to
    ///     deal with lost message and mapping response messages to particular
    ///     sessions.
    ///     </item> 
    ///     <item>
    ///     On the server side, sessions managers track sessions to 
    ///     provide a progress indication to the client as well as to prevent
    ///     multiple sessions from firing on the server from client side
    ///     retry messages.
    ///     </item>    
    ///     <item>
    ///     If the message handler is tagged with <c>[MsgHandler(Idempotent=true)]</c>
    ///     then the session manager will cache information about the session
    ///     (including any replies) for a period of time after the session has 
    ///     completed implementing a simple idempotent-lite behavior.
    ///     </item>
    /// </list>       
    /// <para>
    /// This SessionManager class provides a default implementation of this
    /// behavior.
    /// </para>
    /// <note>
    /// Session managers may implement their own messages and
    /// handlers.  The message router will walk the session manager for
    /// methods tagged with <c>[MsgHandler]</c> when the session manager is 
    /// associated with the router.
    /// </note>
    /// </remarks>
    public sealed class SessionManager : ISessionManager, ILockable
    {
        private MsgRouter router;             // The associated message router

        // Table of client side ISessions keyed by ID

        private Dictionary<Guid, ISession> clientSessions;

        // Table of server side ISessions keyed by ID

        private Dictionary<Guid, ISession> serverSessions;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SessionManager()
        {
            this.router         = null;
            this.clientSessions = new Dictionary<Guid, ISession>();
            this.serverSessions = new Dictionary<Guid, ISession>();
        }

        /// <summary>
        /// Associates the specified router with this session manager.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <remarks>
        /// This must be called before any of the methods below.
        /// </remarks>
        public void Init(MsgRouter router)
        {
            this.router = router;
        }

        /// <summary>
        /// Returns the message router associated with this session manager.
        /// </summary>
        public MsgRouter Router
        {
            get { return router; }
        }

        /// <summary>
        /// Called to initiate a client side session.
        /// </summary>
        /// <param name="session">The session.</param>
        public void ClientStart(ISession session)
        {
            Assertion.Test(session.SessionID != Guid.Empty);

            using (TimedLock.Lock(router.SyncRoot))
                clientSessions[session.SessionID] = session;
        }

        /// <summary>
        /// Called to dispatch a server side session.
        /// </summary>
        /// <param name="msg">The message initiating the session.</param>
        /// <param name="target">The target object instance.</param>
        /// <param name="method">The target method information.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        /// <remarks>
        /// The target and method parameter will specify the message handler
        /// for the message passed.
        /// </remarks>
        public void ServerDispatch(Msg msg, object target, MethodInfo method, SessionHandlerInfo sessionInfo)
        {
            ISession    session;
            bool        start = false;

            Assertion.Test((msg._Flags & (MsgFlag.OpenSession & MsgFlag.ServerSession)) == (MsgFlag.OpenSession & MsgFlag.ServerSession));
            Assertion.Test(msg._SessionID != Guid.Empty);

            using (TimedLock.Lock(router.SyncRoot))
            {
                // Create a session with this ID if one doesn't already exist.

                serverSessions.TryGetValue(msg._SessionID, out session);

                if (session == null)
                {
                    if (sessionInfo.SessionType == null)
                    {
                        SysLog.LogError("Session creation failed for received [{0}] message: No session type specified in [MsgSession] tag for handler [{1}.{2}({3})}.",
                                        msg.GetType().FullName,
                                        target.GetType().FullName,
                                        method.Name,
                                        method.GetParameters()[0].ParameterType.Name);
                        return;
                    }

                    start   = true;
                    session = Helper.CreateInstance<ISession>(sessionInfo.SessionType);
                    session.InitServer(router, this, router.SessionTimeout, msg, target, method, sessionInfo);
                    serverSessions.Add(msg._SessionID, session);
                }
            }

            // Dispatch the message outside of the lock (to avoid deadlocks)

            if (start)
                session.StartServer();
            else
                session.OnMsg(msg, sessionInfo);
        }

        /// <summary>
        /// Called by the router whenever it receives a message with a non-empty
        /// _SessionID.  This method dispatches the message to the associated session
        /// (if any).
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        public void OnMsg(Msg msg, SessionHandlerInfo sessionInfo)
        {
            ISession session;

            Assertion.Test(msg._SessionID != Guid.Empty);

            using (TimedLock.Lock(router.SyncRoot))
            {
                if ((msg._Flags & MsgFlag.ServerSession) != 0)
                    serverSessions.TryGetValue(msg._SessionID, out session);
                else
                    clientSessions.TryGetValue(msg._SessionID, out session);
            }

            if (session != null)
            {
                msg._Trace(router, 2, "SessionManager: Dispached", null);
                session.OnMsg(msg, sessionInfo);
            }
            else
                msg._Trace(router, 2, "SessionManager: No session", null);
        }

        /// <summary>
        /// Called by the router when ReplyTo() is called in response to a query
        /// or other session pattern.  This gives the session manager and
        /// session the chance to cache server side reply messages. 
        /// </summary>
        /// <param name="msg">The reply message.</param>
        public void OnReply(Msg msg)
        {
            ISession session;

            Assertion.Test(msg._SessionID != Guid.Empty);

            using (TimedLock.Lock(router.SyncRoot))
                serverSessions.TryGetValue(msg._SessionID, out session);

            if (session != null)
                session.OnReply(msg);
        }

        /// <summary>
        /// This method should be called periodically on a background thread
        /// so that the session manager can perform any necessary background
        /// tasks.
        /// </summary>
        /// <remarks>
        /// This should be called fairly frequently, on the order of a 1-10 second
        /// interval.
        /// </remarks>
        public void OnBkTimer()
        {
            var now     = SysTime.Now;
            var delList = new List<ISession>();

            using (TimedLock.Lock(router.SyncRoot))
            {

                // Walk the server sessions table, looking for server side sessions 
                // that have outlived their lifespan and then call the remaining
                // session background task methods.

                delList.Clear();
                foreach (var session in serverSessions.Values)
                {
                    Assertion.Test(session.IsServer);

                    if (session.TTD <= now || (!session.IsRunning && !session.CacheEnable))
                        delList.Add(session);
                    else
                    {
                        session.OnBkTimer();

                        // The session may have scheduled itself for deletion by calling
                        // OnFinished() so retest for deletion.

                        if (session.TTD <= now || (!session.IsRunning && !session.CacheEnable))
                            delList.Add(session);
                    }
                }

                foreach (var session in delList)
                {
                    router.Trace(2, "SessionManager: Remove server session", session.SessionID.ToString(), null);
                    serverSessions.Remove(session.SessionID);
                }

                // Walk the client session table, looking for pending client side sessions
                // that need to be cancelled and then calling the remaining session's
                // background task methods.

                delList.Clear();
                foreach (var session in clientSessions.Values)
                {
                    Assertion.Test(session.IsClient);

                    if (session.TTD <= now)
                        delList.Add(session);
                    else
                    {
                        session.OnBkTimer();

                        // The session may have scheduled itself for deletion by calling
                        // OnFinished() so retest for deletion.

                        if (session.TTD <= now)
                            delList.Add(session);
                    }
                }

                foreach (var session in delList)
                {
                    if (session.IsRunning)
                        session.Cancel();
                    else
                        session.OnBkTimer();

                    router.Trace(2, "SessionManager: Remove client session", session.SessionID.ToString(), null);
                    clientSessions.Remove(session.SessionID);
                }
            }
        }

        /// <summary>
        /// Called when a session is finished.
        /// </summary>
        /// <param name="session">The session.</param>
        public void OnFinished(ISession session)
        {
            session.IsRunning  = false;
            session.FinishTime = SysTime.Now;

            // Note that I'm not going to actually remove session from
            // the appropriate collection.  Instead, I'm going to set 
            // the session's TTD to DateTime.MinValue and let the
            // background task take care of actually removing it.

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (session.IsClient)
                {
                    if (clientSessions.ContainsKey(session.SessionID))
                        session.TTD = DateTime.MinValue;
                }
                else
                {
                    // Server sessions can be removed if the handler wasn't tagged
                    // with [MsgSession(Idempotent=true)].

                    if (!serverSessions.ContainsKey(session.SessionID))
                        return;

                    if (!session.CacheEnable)
                        session.TTD = DateTime.MinValue;
                    else
                        session.TTD = SysTime.Now + router.SessionCacheTime;
                }
            }
        }

        /// <summary>
        /// Terminates a client side session if it is present.
        /// </summary>
        /// <param name="sessionID">The session ID.</param>
        public void TerminateClientSession(Guid sessionID)
        {
            using (TimedLock.Lock(this))
            {
                ISession session;

                clientSessions.TryGetValue(sessionID, out session);

                if (session != null)
                    OnFinished(session);
            }
        }

        /// <summary>
        /// Terminates a server side session if it is present.
        /// </summary>
        /// <param name="sessionID">The session ID.</param>
        public void TerminateServerSession(Guid sessionID)
        {
            using (TimedLock.Lock(this))
            {
                ISession session;

                serverSessions.TryGetValue(sessionID, out session);

                if (session != null)
                    OnFinished(session);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
