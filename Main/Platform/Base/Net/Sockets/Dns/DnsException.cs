//-----------------------------------------------------------------------------
// FILE:        DnsException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the detailed exceptions thrown by DnsResolver.

using System;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Implement the detailed exceptions thrown by <see cref="DnsResolver" />.
    /// </summary>
    public sealed class DnsException : ApplicationException
    {
        //---------------------------------------------------------------------
        // Static members

        private static string[] errMsgs;    // Maps rcodes to error messages

        /// <summary>
        /// Static constructor
        /// </summary>
        static DnsException()
        {
            errMsgs = new string[(int)DnsFlag.RCODE_MAX + 1];
            for (int i = 0; i < errMsgs.Length; i++)
                errMsgs[i] = string.Format("Unknown error [{0}]", i);

            errMsgs[(int)DnsFlag.RCODE_OK]         = "OK";
            errMsgs[(int)DnsFlag.RCODE_FORMAT]     = "Request format error";
            errMsgs[(int)DnsFlag.RCODE_SERVER]     = "Server error";
            errMsgs[(int)DnsFlag.RCODE_NAME]       = "Name does not exist";
            errMsgs[(int)DnsFlag.RCODE_NOTIMPL]    = "Not implemented";
            errMsgs[(int)DnsFlag.RCODE_REFUSED]    = "Refused";
            errMsgs[(int)DnsFlag.RCODE_NOTANSWER]  = "Invalid answer";
            errMsgs[(int)DnsFlag.RCODE_QUERYLIMIT] = "Query limit exceeded";
        }

        //---------------------------------------------------------------------
        // Instance members

        private DnsFlag rcode;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rcode">The DNS response code.</param>
        public DnsException(DnsFlag rcode)
            : base(errMsgs[(int)rcode])
        {
            this.rcode = rcode;
        }

        /// <summary>
        /// Returns the DNS response code.
        /// </summary>
        public DnsFlag RCode
        {
            get { return rcode; }
        }
    }
}
