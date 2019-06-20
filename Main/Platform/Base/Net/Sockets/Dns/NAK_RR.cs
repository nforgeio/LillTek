//-----------------------------------------------------------------------------
// FILE:        DnsRR.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the NAK DNS protocol resource record.

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
    /// Implements the <b>NAK</b> DNS protocol resource record.  The presence of
    /// this kind of record in the DNS record cache indicates that desired
    /// record is not present.
    /// </summary>
    public sealed class NAK_RR : DnsRR
    {
        /// <summary>
        /// This constructor initializes the NAK record.
        /// </summary>
        /// <param name="rname">The resource name.</param>
        public NAK_RR(string rname)
            : base()
        {
            this.RRType = DnsRRType.NAK;
            this.RName  = rname;
            this.QClass = DnsQClass.IN;
            this.TTL    = 0;            // $todo: I may want to set this
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
            Assertion.Test(false, "NAK's should never be sent in DNS messages.");
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
            Assertion.Test(false, "NAK's should never be sent in DNS messages.");
            return false;
        }
   }
}
