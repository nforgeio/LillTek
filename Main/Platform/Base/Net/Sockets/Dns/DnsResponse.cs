//-----------------------------------------------------------------------------
// FILE:        DnsResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a DNS protocol response packet.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// This class encapsulates a DNS response message.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Rather than having the constructor parse the response packet and 
    /// throw an exception on an error, Use the <see cref="ParsePacket" /> method instead. 
    /// The reason for this is that I don't want to incur the overhead of the exception 
    /// in the case where we get DOS-Attacked with malformed packets.
    /// </note>
    /// </remarks>
    public sealed class DnsResponse : DnsMessage
    {
        private TimeSpan    latency;    // The round-trip time for the request/response

        /// <summary>
        /// Constructs an unitialized DnsResponse.  This will typically
        /// be followed by a call to <see cref="ParsePacket" />.
        /// </summary>
        public DnsResponse()
            : base()
        {
        }

        /// <summary>
        /// Constructs an DnsResponse, initializing its fields to
        /// preoare a response to a <see cref="DnsRequest" />.
        /// </summary>
        /// <param name="request">The <see cref="DnsRequest" /> being processed.</param>
        public DnsResponse(DnsRequest request)
            : base()
        {
            this.Opcode = request.Opcode;
            this.QClass = request.QClass;
            this.QID    = request.QID;
            this.QName  = request.QName;
            this.QType  = request.QType;
            this.Flags |= DnsFlag.QR;
        }

        /// <summary>
        /// The round trip time for successful request/response.
        /// </summary>
        public TimeSpan Latency
        {
            get { return latency; }
            set { latency = value; }
        }

        /// <summary>
        /// Returns a shallow clone of this instance.
        /// </summary>
        /// <returns></returns>
        public DnsResponse Clone()
        {
            var response = new DnsResponse();

            response.CopyFrom(this);
            return response;
        }

        /// <summary>
        /// This method parses the raw DNS message packet passed and initializes 
        /// the message properties.
        /// </summary>
        /// <param name="packet">The raw DNS packet received.</param>
        /// <param name="cbPacket">Number of bytes in that packet.</param>
        /// <returns>True on success.</returns>
        public new bool ParsePacket(byte[] packet, int cbPacket)
        {
            if (!base.ParsePacket(packet, cbPacket))
                return false;

            // Perform additional response related validations.

            if ((this.Flags & DnsFlag.QR) == 0)     // response bit is not set
                return false;

            if ((this.Flags & DnsFlag.TC) != 0)     // truncation bit is set
                return false;

            if (this.Opcode != DnsOpcode.QUERY)     // only accept standard queries
                return false;

            return true;
        }
    }
}
