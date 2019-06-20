//-----------------------------------------------------------------------------
// FILE:        SessionHandlerInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the information specified by a [MsgSession] attribute for
//              an application's message handler.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds the information specified by a [<see cref="MsgSessionAttribute">MsgSession</see>] attribute for
    /// an application's message handler.
    /// </summary>
    public sealed class SessionHandlerInfo
    {
        //---------------------------------------------------------------------
        // Static members

        private static SessionHandlerInfo defHandlerInfo = new SessionHandlerInfo();

        /// <summary>
        /// Returns the default session handler settings.
        /// </summary>
        public static SessionHandlerInfo Default
        {
            get { return defHandlerInfo; }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// <c>true</c> if the messaging library should implement idempotent
        /// behaviors for this handler.
        /// </summary>
        public readonly bool Idempotent;

        /// <summary>
        /// Interval for which the server side of a session should send
        /// SessionKeepAliveMsgs to the client.
        /// </summary>
        public readonly TimeSpan KeepAliveTime;

        /// <summary>
        /// <c>true</c> if this is an asynchronous server session.
        /// </summary>
        public readonly bool IsAsync;

        /// <summary>
        /// Returns the maximum time an asynchronous session (one marked with <see cref="IsAsync"/><b>=true</b>)
        /// will remain active.  A value of <see cref="TimeSpan.MaxValue" /> indicates
        /// that the operation should never timeout.
        /// </summary>
        public readonly TimeSpan MaxAsyncKeepAliveTime;

        /// <summary>
        /// Maximim time a session should wait for normal message traffic
        /// or a keep-alive from the other end of the session.
        /// </summary>
        public readonly TimeSpan SessionTimeoutTime;

        /// <summary>
        /// Returns the ISession type instance to be created to implement the server
        /// side session for this handler.
        /// </summary>
        public readonly System.Type SessionType;

        /// <summary>
        /// Custom session parameters expressed as a series of name/value pairs.
        /// </summary>
        public readonly ArgCollection Parameters;

        /// <summary>
        /// Copies the fields from the attribute instance passed to this instance.
        /// </summary>
        /// <param name="attr">The [MsgSession] attribute instance.</param>
        public SessionHandlerInfo(MsgSessionAttribute attr)
        {
            this.Idempotent            = attr.Idempotent;
            this.KeepAliveTime         = attr.KeepAliveTime;
            this.IsAsync               = attr.IsAsync;
            this.MaxAsyncKeepAliveTime = attr.MaxAsyncKeepAliveTime;
            this.SessionTimeoutTime    = attr.SessionTimeoutTime;
            this.SessionType           = attr.SessionType;
            this.Parameters            = ArgCollection.Parse(attr.Parameters);
        }

        /// <summary>
        /// Constructs an instance from individual parameters.
        /// </summary>
        /// <param name="itempotent">
        /// <c>true</c> if the messaging library should implement idempotent
        /// behaviors for this handler.
        /// </param>
        /// <param name="keepAliveTime">
        /// Interval for which the server side of a session should send
        /// SessionKeepAliveMsgs to the client.
        /// </param>
        /// <param name="isAsync">
        /// <c>true</c> if this is an asynchronous server session.
        /// </param>
        /// <param name="maxAsyncKeepAliveTime">
        /// Returns the maximum time an asynchronous session (one marked with <see cref="IsAsync"/><b>=true</b>)
        /// will remain active.  A value of <see cref="TimeSpan.MaxValue" /> indicates
        /// that the operation should never timeout.
        /// </param>
        /// <param name="sessionTimeoutTime">
        /// Maximim time a session should wait for normal message traffic
        /// or a keep-alive from the other end of the session.
        /// </param>
        /// <param name="sessionType">
        /// Returns the ISession type instance to be created to implement the server
        /// side session for this handler.
        /// </param>
        /// <param name="parameters">Custom session parameters expressed as a series of name/value pairs.</param>
        public SessionHandlerInfo(bool itempotent,
                                  TimeSpan keepAliveTime,
                                  bool isAsync,
                                  TimeSpan maxAsyncKeepAliveTime,
                                  TimeSpan sessionTimeoutTime,
                                  System.Type sessionType,
                                  ArgCollection parameters)
        {
            this.Idempotent            = itempotent;
            this.KeepAliveTime         = keepAliveTime;
            this.IsAsync               = isAsync;
            this.MaxAsyncKeepAliveTime = maxAsyncKeepAliveTime;
            this.SessionTimeoutTime    = sessionTimeoutTime;
            this.SessionType           = sessionType;
            this.Parameters            = parameters;
        }

        /// <summary>
        /// Initializes the instance with default session properties.
        /// </summary>
        public SessionHandlerInfo()
        {
            this.Idempotent            = false;
            this.KeepAliveTime         = TimeSpan.FromMinutes(2);
            this.IsAsync               = false;
            this.MaxAsyncKeepAliveTime = TimeSpan.MaxValue;
            this.SessionTimeoutTime    = TimeSpan.FromTicks(this.KeepAliveTime.Ticks * 2);
            this.SessionType           = null;
            this.Parameters            = new ArgCollection();
        }
    }
}
