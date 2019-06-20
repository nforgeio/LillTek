//-----------------------------------------------------------------------------
// FILE:        WcfEnvelopeMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to encapsulate a WCF message in a LillTek message.

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

// $todo(jeff.lill): 
//
// Think about using the .NET Framework BufferManager class to
// manage the payload buffers.

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Used to encapsulate a WCF message in a LillTek message.
    /// </summary>
    internal sealed class WcfEnvelopeMsg : Msg
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static string GetTypeID()
        {
            return ".WcfEnvelope";
        }

        //---------------------------------------------------------------------
        // Instance members

        private ArraySegment<byte> payload;    // The message payload

        /// <summary>
        /// Constructor.
        /// </summary>
        public WcfEnvelopeMsg()
        {
        }

        /// <summary>
        /// The serialized WCF message.
        /// </summary>
        public ArraySegment<byte> Payload
        {
            get { return payload; }
            set { payload = value; }
        }

        /// <summary>
        /// Serializes the payload of the message into the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        protected override void WritePayload(EnhancedStream es)
        {
            es.Write(payload.Array, payload.Offset, payload.Count);
        }

        /// <summary>
        /// Loads the message payload from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <param name="cbPayload">Number of bytes of payload data.</param>
        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            byte[] buffer;

            buffer = new byte[cbPayload];
            es.Read(buffer, 0, cbPayload);

            payload = new ArraySegment<byte>(buffer);
        }
    }
}
