//-----------------------------------------------------------------------------
// FILE:        ISessionManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface necessary to implement a messaging
//              session manager.

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
    /// Defines the behavior of a message session manager.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Session managers are used on both the client and server side of a
    /// transaction to coordinate the execution of a session.  Session
    /// managers provide two basic services:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Provides an easy to use, blocking client side interface for that
    ///     handles the actual messaging to the server, including retries to
    ///     deal with lost message and mapping response messages to particular
    ///     session transactions.
    ///     </item>
    ///     <item>       
    ///     On the server side, session managers track transactions to 
    ///     provide a progress indication to the client as well as to prevent
    ///     multiple sessions from firing on the server from client side
    ///     retry messages.
    ///     </item>
    /// </list>       
    /// <para>
    /// The <see cref="SessionManager" /> class provides a default implementation 
    /// of this behavior.
    /// </para>
    /// <note>
    /// Session managers may implement their own messages and
    /// handlers.  The message router will walk the session manager for
    /// methods tagged with <c>[MsgHandler]</c> when the session manager is 
    /// associated with the router.
    /// </note>
    /// </remarks>
    public interface ISessionManager
    {
        /// <summary>
        /// Associates the specified with this router.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <remarks>
        /// This must be called before any of the methods below.
        /// </remarks>
        void Init(MsgRouter router);

        /// <summary>
        /// Called to initiate a client side session.
        /// </summary>
        /// <param name="session">The session.</param>
        void ClientStart(ISession session);

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
        void ServerDispatch(Msg msg, object target, MethodInfo method, SessionHandlerInfo sessionInfo);

        /// <summary>
        /// Called by the router whenever it receives a message with a non-empty
        /// _SessionID.  This method dispatches the message to the associated session
        /// (if any).
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        void OnMsg(Msg msg, SessionHandlerInfo sessionInfo);

        /// <summary>
        /// Called by the router when <see cref="MsgRouter.ReplyTo(LillTek.Messaging.Msg, LillTek.Messaging.Msg)" /> 
        /// is called in response to a query or other session pattern.  This gives the session manager
        /// and session the chance to cache server side reply messages. 
        /// </summary>
        /// <param name="msg">The reply message.</param>
        void OnReply(Msg msg);

        /// <summary>
        /// This method should be called periodically on a background thread
        /// so that the session manager can perform any necessary background
        /// tasks.
        /// </summary>
        /// <remarks>
        /// This should be called fairly frequently, on the order of a 1-10 second
        /// interval.
        /// </remarks>
        void OnBkTimer();

        /// <summary>
        /// Called when a session is finished.
        /// </summary>
        /// <param name="session">The session.</param>
        void OnFinished(ISession session);

        /// <summary>
        /// Terminates a client side session if it is present.
        /// </summary>
        /// <param name="sessionID">The session ID.</param>
        void TerminateClientSession(Guid sessionID);

        /// <summary>
        /// Terminates a server side session if it is present.
        /// </summary>
        /// <param name="sessionID">The session ID.</param>
        void TerminateServerSession(Guid sessionID);
    }
}
