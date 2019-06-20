//-----------------------------------------------------------------------------
// FILE:        EnvelopeMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to represent a message that can't be unserialized.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds the serialized contents of a message type that is
    /// unknown to the current <see cref="Msg" /> context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Message routers may have to route messages whose types are
    /// unknown to the current router instance.  This can happen, for
    /// example, when two leaf routers route a message to each other
    /// via a hub router that has no knowledge of the specific message
    /// type.
    /// </para>
    /// <para>
    /// Instances of this class will be created by <see cref="Msg.Load" /> when
    /// it cannot map the message typeID to a type in the internal
    /// type map.
    /// </para>
    /// </remarks>
    public sealed class EnvelopeMsg : Msg
    {
        private string  typeID;     // The serialized message typeID
        private byte[]  payload;    // The message payload

        /// <summary>
        /// Constructor.
        /// </summary>
        public EnvelopeMsg()
        {
            this.typeID = null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="typeID">The message's serialized typeID.</param>
        public EnvelopeMsg(string typeID)
        {
            this.typeID = typeID;
        }

        /// <summary>
        /// Returns the type ID to be used when reserializing this message.
        /// </summary>
        public string TypeID
        {
            get { return typeID; }
        }

        /// <summary>
        /// Serializes the payload of the message into the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        protected override void WritePayload(EnhancedStream es)
        {
            es.Write(payload, 0, payload.Length);
        }

        /// <summary>
        /// Loads the message payload from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <param name="cbPayload">Number of bytes of payload data.</param>
        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            payload = new byte[cbPayload];
            es.Read(payload, 0, cbPayload);
        }
    }
}
