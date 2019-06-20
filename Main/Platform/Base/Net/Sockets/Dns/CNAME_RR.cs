//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the CNAME DNS protocol resource record.

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
    /// Implements the <b>CNAME DNS</b> protocol resource record.
    /// </summary>
    public sealed class CNAME_RR : DnsRR
    {
        // Private instance variables

        private string  cname;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CNAME_RR()
        {
            this.RRType = DnsRRType.CNAME;
            this.cname  = string.Empty;
        }

        /// <summary>
        /// This constructor initializes the record.
        /// </summary>
        /// <param name="rname">The resource name.</param>
        /// <param name="cname">The cannonical name.</param>
        /// <param name="ttl">The time-to-live value for this entry (in seconds).</param>
        public CNAME_RR(string rname, string cname, int ttl)
            : base()
        {
            if (!rname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "qname");

            if (!cname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "cname");

            this.RRType = DnsRRType.CNAME;
            this.RName  = rname;
            this.RRType = DnsRRType.CNAME;
            this.cname  = cname;
            this.TTL    = ttl;
        }

        /// <summary>
        /// The cannonical name.
        /// </summary>
        public string CName
        {
            get { return cname; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                cname = value;
            }
        }

        /// <summary>
        /// This method writes the resource record's data (including its
        /// length) to the packet beginning at the offset passed.
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
            message.WriteName(packet, ref offset, cname);
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
            if (!message.ReadName(packet, ref offset, out cname))
                return false;

            return pos + cb == offset;
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        protected override void WriteTraceDetails(StringBuilder sb)
        {
            sb.AppendFormat("cname={0}\r\n", cname);
        }
    }
}
