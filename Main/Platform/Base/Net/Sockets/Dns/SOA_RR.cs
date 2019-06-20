//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the SOA DNS protocol resource record.

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
    /// Implements the <b>SOA</b> DNS protocol resource record.
    /// </summary>
    public sealed class SOA_RR : DnsRR
    {
        // Private instance variables

        private string  primary;    // Primary name server
        private string  adminMail;  // Admin email address
        private uint    serial;     // Version number of the original zone copy
        private uint    refresh;    // Zone refresh interval (seconds)
        private uint    retry;      // Time to wait after a failed refresh (seconds)
        private uint    expire;     // Upper limit on elapsed time before zone data is no longer authoritative (seconds)
        private uint    minimum;    // Minimum TTL for any RR delivered from this zone

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SOA_RR()
        {

            this.RRType    = DnsRRType.SOA;
            this.primary   = string.Empty;
            this.adminMail = string.Empty;
            this.serial    = 0;
            this.refresh   = 0;
            this.retry     = 0;
            this.expire    = 0;
            this.minimum   = 0;
        }

        /// <summary>
        /// This constructor initializes a SOA resource record.
        /// </summary>
        /// <param name="rname">The resource name.</param>
        /// <param name="primary">Primary name server's domain name (this source for this zone info).</param>
        /// <param name="adminMail">Administators email address encoded as a domain name.</param>
        /// <param name="serial">Version number of the original zone copy.</param>
        /// <param name="refresh">Zone refresh interval (seconds).</param>
        /// <param name="retry">Time to wait after a failed refresh (seconds).</param>
        /// <param name="expire">Upper limit on elapsed time before zone data is no longer authoritative (seconds).</param>
        /// <param name="minimum">Minimum TTL for any RR delivered from this zone (seconds).</param>
        public SOA_RR(string rname, string primary, string adminMail,
                      uint serial, uint refresh, uint retry, uint expire, uint minimum)
        {
            if (!rname.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "qname");

            if (!primary.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "primary");

            if (!adminMail.EndsWith("."))
                throw new ArgumentException(DomainDotMsg, "adminEmail");

            this.RRType    = DnsRRType.SOA;
            this.RName     = rname;
            this.primary   = primary;
            this.adminMail = adminMail;
            this.serial    = serial;
            this.refresh   = refresh;
            this.retry     = retry;
            this.expire    = expire;
            this.minimum  = minimum;
        }

        /// <summary>
        /// The primary name server.
        /// </summary>
        public string Primary
        {
            get { return primary; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                primary = value;
            }
        }

        /// <summary>
        /// The administrator's email address encoded as a domain name.
        /// </summary>
        public string AdminEmail
        {
            get { return adminMail; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                adminMail = value;
            }
        }

        /// <summary>
        /// Serial number of the current zone information.
        /// </summary>
        public uint Serial
        {
            get { return serial; }
            set { serial = value; }
        }

        /// <summary>
        /// Zone refresh interval in seconds.
        /// </summary>
        public uint Refresh
        {
            get { return refresh; }
            set { refresh = value; }
        }

        /// <summary>
        /// Time to wait after a failed refresh (seconds).
        /// </summary>
        public uint Retry
        {
            get { return retry; }
            set { retry = value; }
        }

        /// <summary>
        /// Minimum TTL for any RR delivered from this zone (seconds).
        /// </summary>
        public uint Minimum
        {
            get { return minimum; }
            set { minimum = value; }
        }

        /// <summary>
        /// The upper limit on the time interval that can elapse before
        /// the zone is no longer authoritative.
        /// </summary>
        public uint Expire
        {
            get { return expire; }
            set { expire = value; }
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
            message.WriteName(packet, ref offset, primary);
            message.WriteName(packet, ref offset, adminMail);
            Helper.WriteInt32(packet, ref offset, (int)serial);
            Helper.WriteInt32(packet, ref offset, (int)refresh);
            Helper.WriteInt32(packet, ref offset, (int)retry);
            Helper.WriteInt32(packet, ref offset, (int)expire);
            Helper.WriteInt32(packet, ref offset, (int)minimum);

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

            if (!message.ReadName(packet, ref offset, out primary))
                return false;

            if (!message.ReadName(packet, ref offset, out adminMail))
                return false;

            serial  = (uint)Helper.ReadInt32(packet, ref offset);
            refresh = (uint)Helper.ReadInt32(packet, ref offset);
            retry   = (uint)Helper.ReadInt32(packet, ref offset);
            expire  = (uint)Helper.ReadInt32(packet, ref offset);
            minimum = (uint)Helper.ReadInt32(packet, ref offset);

            return pos + cb == offset;
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        protected override void WriteTraceDetails(StringBuilder sb)
        {
            sb.AppendFormat("primary={0}\r\n", primary);
            sb.AppendFormat("adminmail={0}\r\n", adminMail);
            sb.AppendFormat("serial={0}\r\n", serial);
            sb.AppendFormat("refresh={0}\r\n", refresh);
            sb.AppendFormat("retry={0}\r\n", retry);
            sb.AppendFormat("expire={0}\r\n", expire);
            sb.AppendFormat("minimum={0}\r\n", minimum);
        }
    }
}
