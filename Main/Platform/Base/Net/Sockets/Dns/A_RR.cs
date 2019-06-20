//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the A DNS protocol resource record.

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
    /// Implements the <b>A</b> DNS protocol resource record.
    /// </summary>
    public sealed class A_RR : DnsRR
    {
        // Private instance variables

        private IPAddress ipAddr;     // The 32-bit IP address

        /// <summary>
        /// Default constructor.
        /// </summary>
        public A_RR()
        {

            this.RRType = DnsRRType.A;
            this.ipAddr = IPAddress.Any;
        }

        /// <summary>
        /// This constructor initializes the record.
        /// </summary>
        /// <param name="rname">The resource name.</param>
        /// <param name="ipAddr">The IP address.</param>
        /// <param name="ttl">The time-to-live value for this entry (in seconds).</param>
        public A_RR(string rname, IPAddress ipAddr, int ttl)
            : base()
        {
            if (!rname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "qname");

            this.RRType = DnsRRType.A;
            this.RName  = rname;
            this.ipAddr = ipAddr;
            this.TTL    = ttl;
        }

        /// <summary>
        /// The IP address associated with the qname.
        /// </summary>
        public IPAddress Address
        {
            get { return ipAddr; }
            set { ipAddr = value; }
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
            byte[] buf;

            Helper.WriteInt16(packet, ref offset, 4);
            buf = ipAddr.GetAddressBytes();
            Assertion.Test(buf.Length == 4);
            Array.Copy(buf, 0, packet, offset, 4);
            offset += 4;
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
            byte[]  buf;
            int     pos;
            int     cb;

            cb = Helper.ReadInt16(packet, ref offset);
            pos = offset;
            if (cb != 4)
                return false;

            buf = new byte[4];
            for (int i = 0; i < 4; i++)
                buf[i] = packet[offset++];

            ipAddr = new IPAddress(buf);

            return pos + cb == offset;
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        protected override void WriteTraceDetails(StringBuilder sb)
        {
            sb.AppendFormat("address={0}\r\n", Address);
        }
    }
}
