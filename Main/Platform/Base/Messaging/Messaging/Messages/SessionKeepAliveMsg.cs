//-----------------------------------------------------------------------------
// FILE:        SessionKeepAliveMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sent from the server side of a session back to the client to indicate
//              that the session is still active and that any timeout timers 
//              maintained by the client should be reset.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Sent from one side of a session to the other indicating
    /// that the session is still active and that any timeout timers 
    /// maintained should be reset.
    /// </summary>
    public sealed class SessionKeepAliveMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".SessionKeepAlive";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public SessionKeepAliveMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sessionTTL">
        /// The minumum time the session should wait for another SessionKeepAliveMsg
        /// or a normal session termination message before assuming the session has
        /// failed and timing out.
        /// </param>
        public SessionKeepAliveMsg(TimeSpan sessionTTL)
        {
            this._Flags     = MsgFlag.Priority;
            this.SessionTTL = sessionTTL;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private SessionKeepAliveMsg(Stub param)
            : base(param)
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            SessionKeepAliveMsg clone;

            clone = new SessionKeepAliveMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The minumum time the session should wait for another SessionKeepAliveMsg
        /// or a normal session termination message before assuming the session has
        /// failed and timing out.
        /// </summary>
        public TimeSpan SessionTTL
        {
            get { return base._Get("session-ttl", TimeSpan.FromMinutes(2)); }
            set { base._Set("session-ttl", value); }
        }
    }
}
