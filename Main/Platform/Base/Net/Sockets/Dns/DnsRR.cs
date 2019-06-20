//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a base DNS protocol resource record.

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
    /// Implements a base DNS protocol resource record.
    /// </summary>
    public class DnsRR
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DomainDotMsg = "Domain names must end with [.]";

        /// <summary>
        /// Parses a resource record from the packet passed.
        /// </summary>
        /// <param name="message">The message containing the record being parsed.</param>
        /// <param name="packet">The raw DNS message packet.</param>
        /// <param name="offset">
        /// The current offset in the packet.  Returns as the offset of the first byte
        /// after the record.
        /// </param>
        /// <returns>The resource record or <c>null</c> if there was an error.</returns>
        public static DnsRR Parse(DnsMessage message, byte[] packet, ref int offset)
        {
            string      qname;
            DnsRRType   rrtype;
            DnsRR       rr;
            int         savePos;

            // Peek the qname and record type

            savePos = offset;

            if (!message.ReadName(packet, ref offset, out qname))
                return null;

            rrtype = (DnsRRType)Helper.ReadInt16(packet, ref offset);
            offset = savePos;

            switch (rrtype)
            {
                case DnsRRType.A:

                    rr = new A_RR();
                    break;

                case DnsRRType.CNAME:

                    rr = new CNAME_RR();
                    break;

                case DnsRRType.NS:

                    rr = new NS_RR();
                    break;

                case DnsRRType.MX:

                    rr = new MX_RR();
                    break;

                case DnsRRType.SOA:

                    rr = new SOA_RR();
                    break;

                default:

                    rr = new DnsRR();
                    break;
            }

            if (!rr.Read(message, packet, ref offset))
                return null;

            return rr;
        }

        //---------------------------------------------------------------------
        // Instance members

        private DnsRRType   rrtype;         // Record type
        private string      rname;          // Record's resource name
        private DnsQClass   qclass;         // Record class
        private int         ttl;            // Record's time-to-live (seconds)
        private byte[]      data;           // Resource record data

        /// <summary>
        /// This constructor initializes an empty DNS resource
        /// record.
        /// </summary>
        public DnsRR()
        {
            this.rrtype = DnsRRType.UNKNOWN;
            this.rname  = string.Empty;
            this.qclass = DnsQClass.IN;
            this.ttl    = 0;
            this.data   = new byte[0];
        }

        /// <value>
        /// This property returns the resource record name.
        /// </value>
        public string RName
        {
            get { return this.rname; }

            set
            {
                if (!value.EndsWith("."))
                    throw new ArgumentException(DomainDotMsg);

                this.rname = value;
            }
        }

        /// <value>
        /// This property returns the resource record type.
        /// </value>
        public DnsRRType RRType
        {
            get { return this.rrtype; }
            set { this.rrtype = value; }
        }

        /// <value>
        /// This property returns the resource record class.
        /// </value>
        public DnsQClass QClass
        {
            get { return this.qclass; }
            set { this.qclass = value; }
        }

        /// <value>
        /// This property returns the resource record time-to-live in seconds.
        /// </value>
        public int TTL
        {
            get { return this.ttl; }
            set { this.ttl = value; }
        }

        /// <summary>
        /// The resource record data.  Note that this is not valid for classes
        /// that derive from DnsRR.  For those classes, use the class specific
        /// fields to gain access to the resource contents.
        /// </summary>
        public byte[] Data
        {
            get { return data; }
            set { data = value; }
        }

        /// <value>
        /// This property returns <c>true</c> if this is a NAK record.  A NAK record
        /// indicates that the corresponding query failed and that this fact
        /// is record in the DNS cache.  NAK records should never be included
        /// in DNS messages.
        /// </value>
        public bool IsNAK
        {
            get { return this is NAK_RR; }
        }

        /// <summary>
        /// This method writes the resource record to the packet beginning
        /// at the offset passed.
        /// </summary>
        /// <param name="message">The DNS message being serialized.</param>
        /// <param name="packet">The packet buffer.</param>
        /// <param name="offset">
        /// The offset in the packet where the record will be written.
        /// This parameter will return set to the offset of the first
        /// byte after the written record.
        /// </param>
        public void Write(DnsMessage message, byte[] packet, ref int offset)
        {
            Assertion.Test(rrtype != DnsRRType.UNKNOWN, "RRType is not set.");
            message.WriteName(packet, ref offset, rname);
            Helper.WriteInt16(packet, ref offset, (int)rrtype);
            Helper.WriteInt16(packet, ref offset, (int)qclass);
            Helper.WriteInt32(packet, ref offset, (int)ttl);

            WriteData(message, packet, ref offset);
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
        protected virtual void WriteData(DnsMessage message, byte[] packet, ref int offset)
        {
            Helper.WriteBytes16(packet, ref offset, data);
        }

        /// <summary>
        /// This method parses the resource record from the packet beginning
        /// at the offset passed.
        /// </summary>
        /// <param name="message">The message containing the record being parsed.</param>
        /// <param name="packet">The packet buffer.</param>
        /// <param name="offset">
        /// The offset in the packet where the record is located.
        /// This parameter will return set to the offset of the first
        /// byte after the parsed record.
        /// </param>
        /// <returns>True on success.</returns>
        public bool Read(DnsMessage message, byte[] packet, ref int offset)
        {
            if (!message.ReadName(packet, ref offset, out rname))
                return false;

            rrtype = (DnsRRType)Helper.ReadInt16(packet, ref offset);
            qclass = (DnsQClass)Helper.ReadInt16(packet, ref offset);
            ttl    = Helper.ReadInt32(packet, ref offset);

            return ReadData(message, packet, ref offset);
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
        protected virtual bool ReadData(DnsMessage message, byte[] packet, ref int offset)
        {
            data = Helper.ReadBytes16(packet, ref offset);
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the resource records type and qname
        /// match the parameters passed.
        /// </summary>
        /// <param name="rrType">The resource type to be matched.</param>
        /// <param name="qname">The qname to be matched.</param>
        /// <returns><c>true</c> if there's a match.</returns>
        public bool Match(DnsRRType rrType, string qname)
        {
            return this.RRType == rrType && String.Compare(this.RName, qname, true) == 0;
        }

        /// <summary>
        /// Writes NetTrace information to a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        [Conditional("TRACE")]
        public void WriteTrace(StringBuilder sb)
        {
            sb.AppendFormat("[type={0} name={1} ttl={2}]\r\n", rrtype, rname, ttl);
            WriteTraceDetails(sb);
        }

        /// <summary>
        /// Writes details about the resource record to the a StringBuilder.
        /// </summary>
        /// <param name="sb">The output StringBuilder.</param>
        /// <remarks>
        /// <note>
        /// Resources that implement <see cref="ReadData" /> must
        /// override and implement this as well.
        /// </note>
        /// </remarks>
        protected virtual void WriteTraceDetails(StringBuilder sb)
        {
            if (data == null)
            {
                sb.AppendFormat("Error: {0} must implement WriteTraceDetails()\r\n", this.GetType().FullName);
                return;
            }

            sb.AppendFormat("size={0} data:\r\n", data.Length);
            sb.Append(Helper.HexDump(data, 16, HexDumpOption.ShowAll));
        }
    }
}
