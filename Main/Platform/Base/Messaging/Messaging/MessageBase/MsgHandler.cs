//-----------------------------------------------------------------------------
// FILE:        MsgHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an application message handler instance.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using LillTek.Common;

namespace LillTek.Messaging
{

    /// <summary>
    /// Describes an application message handler instance.
    /// </summary>
    public sealed class MsgHandler
    {
        /// <summary>
        /// Used internally for unit testing.
        /// </summary>
        internal static MsgHandler Stub = new MsgHandler();

        /// <summary>
        /// Used internally to create the stub handler.
        /// </summary>
        private MsgHandler() 
        {
        }

        /// <summary>
        /// The target object instance.
        /// </summary>
        public readonly object Target;

        /// <summary>
        /// Information about the instance method that will actually handle the message.
        /// </summary>
        public readonly MethodInfo Method;

        /// <summary>
        /// The method's message parameter type.
        /// </summary>
        public readonly System.Type MsgType;

        /// <summary>
        /// Set to a non-<c>null</c> dynamic scope name if the logical endpoint has the 
        /// potential to be modified dynamically at runtime just before the message 
        /// handler is registered with an <see cref="IMsgDispatcher" />.  This name will
        /// be presented to the <see cref="IDynamicEPMunger.Munge" /> method so that
        /// the method can determine whether the tagged message handler belongs to
        /// the munger our not.  See <see cref="IDynamicEPMunger" /> for more information. 
        /// (Defaults to null).
        /// </summary>
        public readonly string DynamicScope;

        /// <summary>
        /// Session handler information (or <c>null</c>).
        /// </summary>
        public readonly SessionHandlerInfo SessionInfo;

        /// <summary>
        /// Constructs a MsgHandler instance mapped to a reflected 
        /// message handler method.
        /// </summary>
        /// <param name="target">The target object instance.</param>
        /// <param name="method">Information about the instance method that will actually handle the message.</param>
        /// <param name="msgType">The message type accepted by the handler.</param>
        /// <param name="handlerAttr">The [MsgHandler] instance for the handler (or <c>null</c>).</param>
        /// <param name="sessionAttr">The [MsgSession] instance for the handler (or <c>null</c>).</param>
        public MsgHandler(object target, MethodInfo method, System.Type msgType, MsgHandlerAttribute handlerAttr, MsgSessionAttribute sessionAttr)
        {
            this.Target       = target;
            this.Method       = method;
            this.MsgType      = msgType;
            this.DynamicScope = handlerAttr == null ? null : handlerAttr.DynamicScope;

            if (sessionAttr == null)
                this.SessionInfo = SessionHandlerInfo.Default;
            else
                this.SessionInfo = new SessionHandlerInfo(sessionAttr);
        }

        /// <summary>
        /// Constructs a MsgHandler instance mapped to a reflected 
        /// message handler method.
        /// </summary>
        /// <param name="target">The target object instance.</param>
        /// <param name="method">Information about the instance method that will actually handle the message.</param>
        /// <param name="msgType">The message type accepted by the handler.</param>
        /// <param name="handlerAttr">The [MsgHandler] instance for the handler (or <c>null</c>).</param>
        /// <param name="sessionInfo">The session information for the handler (or <c>null</c>).</param>
        public MsgHandler(object target, MethodInfo method, System.Type msgType, MsgHandlerAttribute handlerAttr, SessionHandlerInfo sessionInfo)
        {
            if (sessionInfo == null)
                sessionInfo = SessionHandlerInfo.Default;

            this.Target       = target;
            this.Method       = method;
            this.MsgType      = msgType;
            this.DynamicScope = handlerAttr == null ? null : handlerAttr.DynamicScope;
            this.SessionInfo  = sessionInfo;
        }
    }
}