//-----------------------------------------------------------------------------
// FILE:        RouterStopMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Broadcast by a hub or leaf router just before shutting down.

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
    /// Broadcast by a hub or leaf router just before shutting down.
    /// </summary>
    public sealed class RouterStopMsg : PropertyMsg
    {

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".RouterStop";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public RouterStopMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">The stopping router's physical endpoint.</param>
        public RouterStopMsg(MsgEP routerEP)
        {
            this._Flags   = MsgFlag.Priority;
            this.RouterEP = routerEP;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private RouterStopMsg(Stub param)
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
            RouterStopMsg clone;

            clone = new RouterStopMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The router's physical endpoint.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return MsgEP.Parse(base["router-ep"]); }

            set
            {
                Assertion.Test(value.ChannelEP == null, "ChannelEP must be null.");
                base["router-ep"] = value.ToString();
            }
        }
    }
}
