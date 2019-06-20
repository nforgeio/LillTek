//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the NS DNS protocol resource record.

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Implements the <b>NS</b> DNS protocol resource record.
    /// </summary>
    public sealed class NS_RR : DnsRR
    {
        // Private instance variables

        private string nsname;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NS_RR()
        {
            this.RRType = DnsRRType.NS;
            this.nsname = string.Empty;
        }

        /// <summary>
        /// This constructor initializes the record.
        /// </summary>
        /// <param name="rname">The resource name.</param>
        /// <param name="nsname">The name server name.</param>
        /// <param name="ttl">The time-to-live value for this entry (in seconds).</param>
        public NS_RR(string rname, string nsname, int ttl)
            : base()
        {
            if (!rname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "qname");

            if (!nsname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "nsname");

            this.RRType = DnsRRType.NS;
            this.RName  = rname;
            this.nsname = nsname;
            this.TTL    = ttl;
        }

        /// <summary>
        /// The name server name.
        /// </summary>
        public string NameServer
        {
            get { return nsname; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                nsname = value;
            }
        }

        /// <summary>
        /// This method writes the resource record's data (including its
        /// length to the packet beginning at the offset passed.
        /// </summary>
        /// <param name="message">The DNS message being serialized.</param>
        /// <param name="packet">The packet buffer.</param>
        /// <param name="offset">
        /// The offset in the packet where the data will be written.
        /// This parameter will return set to the offset of the first
        /// byte after the written data.
        /// </param>
        protected override void WriteData(DnsMessage message, byte[] packet, ref int offset)
        {
            int lenPos = offset;    // Remember where the length goes

            offset += 2;
            message.WriteName(packet, ref offset, nsname);
            Helper.WriteInt16(packet, ref lenPos, offset - lenPos - 2);   // Go back and write the length
        }

        /// <summary>
        /// This method parses the resource record's (including its
        /// length from the packet beginning at the offset passed.
        /// </summary>
        /// <param name="message">The message containing the record being parsed.</param>
        /// <param name="packet">The packet buffer.</param>
        /// <param name="offset">
        /// The offset in the packet where the data will be read.
        /// This parameter will return set to the offset of the first
        /// byte after the parsed data.
        /// </param>
        /// <returns>True on success.</returns>
        protected override bool ReadData(DnsMessage message, byte[] packet, ref int offset)
        {
            int     pos;
            int     cb;

            cb = Helper.ReadInt16(packet, ref offset);
            pos = offset;
            if (!message.ReadName(packet, ref offset, out nsname))
                return false;

            return pos + cb == offset;
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        protected override void WriteTraceDetails(StringBuilder sb)
        {
            sb.AppendFormat("nameserver={0}\r\n", nsname);
        }
    }
}
