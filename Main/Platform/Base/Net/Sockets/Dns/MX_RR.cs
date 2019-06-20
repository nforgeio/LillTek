//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the MX DNS protocol resource record.

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
    /// Implements the <b>MX</b> DNS protocol resource record.
    /// </summary>
    public sealed class MX_RR : DnsRR
    {
        // Private instance variables

        private ushort  pref;       // Perference value
        private string  exchange;   // Mail exchange domain name

        /// <summary>
        /// Default constructor.
        /// </summary>
        public MX_RR()
        {
            this.RRType   = DnsRRType.MX;
            this.pref     = 0;
            this.exchange = string.Empty;
        }

        /// <summary>
        /// This constructor initializes the mail exchange resource record.
        /// </summary>
        /// <param name="pref">
        /// The "preference" value.  MX records with lower preference values
        /// will bew preferred over those with higher values by mailers.
        /// </param>
        /// <param name="rname">The resource name.</param>
        /// <param name="exchange">The domain name of the mail exchange server.</param>
        /// <param name="ttl">The time-to-live value for this entry (in seconds).</param>
        public MX_RR(string rname, ushort pref, string exchange, int ttl)
            : base()
        {
            if (!rname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "qname");

            if (!exchange.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "exchange");

            this.RRType   = DnsRRType.MX;
            this.RName    = rname;
            this.pref     = pref;
            this.exchange = exchange;
            this.TTL      = ttl;
        }

        /// <summary>
        /// The "preference" value.  MX records with lower preference values
        /// will bew preferred over those with higher values by mailers.
        /// </summary>
        public ushort Preference
        {
            get { return pref; }
            set { pref = value; }
        }

        /// <summary>
        /// The domain name of the mail exchange server.
        /// </summary>
        public string Exchange
        {
            get { return exchange; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                exchange = value;
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
            Helper.WriteInt16(packet, ref offset, pref);
            message.WriteName(packet, ref offset, exchange);
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

            cb   = Helper.ReadInt16(packet, ref offset);
            pos  = offset;
            pref = (ushort)Helper.ReadInt16(packet, ref offset);
            if (!message.ReadName(packet, ref offset, out exchange))
                return false;

            return pos + cb == offset;
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        protected override void WriteTraceDetails(StringBuilder sb)
        {
            sb.AppendFormat("preference={0} exchange={1} ttl{2}\r\n", pref, exchange, TTL);
        }
    }
}
