//-----------------------------------------------------------------------------
// FILE:        DnsRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a DNS protocol request packet.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// This class encapsulates a DNS request message.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Rather than having the constructor parse the request packet and 
    /// throw an exception on an error, Use the <see cref="ParsePacket" /> method instead. 
    /// The reason for this is that I don't want to incur the overhead of the exception 
    /// in the case where we get DOS-Attacked with malformed packets.
    /// </note>
    /// </remarks>
    public sealed class DnsRequest : DnsMessage
    {
        /// <summary>
        /// Constructs an unitialized DnsRequest.  This will typically
        /// be followed by a call to <see cref="ParsePacket" />.
        /// </summary>
        public DnsRequest()
            : base()
        {
        }

        /// <summary>
        /// Connstructs a typical Internet class DNS request.
        /// </summary>
        /// <param name="flags">Additional flag bits (the QR and OPCODE sections will be set automatically).</param>
        /// <param name="qname">The query name.</param>
        /// <param name="qtype">The query type.</param>
        /// <remarks>
        /// <para>
        /// By default, this constructor initializes the OPCODE section of the message flags to 
        /// <see cref="DnsOpcode.QUERY" />, the query ID to zero, and the query class to 
        /// <see cref="DnsQClass.IN" />.  These properties can be modified directly after
        /// construction if necessary.  Note that the <see cref="DnsResolver" /> class
        /// handles the assignment of a unique query ID so it generally not necessary
        /// to explicitly set this property.
        /// </para>
        /// </remarks>
        public DnsRequest(DnsFlag flags, string qname, DnsQType qtype)
            : base()
        {
            if (!qname.EndsWith("."))
                throw new ArgumentException("Domain names must end with [.]", "qname");

            this.Flags  = flags;
            this.QName  = qname;
            this.QType  = qtype;
            this.QClass = DnsQClass.IN;
        }

        /// <summary>
        /// Returns a shallow clone of this instance.
        /// </summary>
        /// <returns></returns>
        public DnsRequest Clone()
        {
            var request = new DnsRequest();

            request.CopyFrom(this);
            return request;
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

            // Perform additional request related validations.

            if ((this.Flags & DnsFlag.QR) != 0)  // response bit is set
                return false;

            return true;
        }
    }
}
