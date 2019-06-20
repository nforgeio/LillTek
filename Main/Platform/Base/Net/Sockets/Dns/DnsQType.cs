//-----------------------------------------------------------------------------
// FILE:        DnsQType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible DNS query types.

// Note that the values defined in these enums map directly to the values
// defined by the DNS protocol.

using System;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines the possible DNS query types.
    /// </summary>
    public enum DnsQType : uint
    {
        /// <summary>
        /// Host address
        /// </summary>
        A = 1,

        /// <summary>
        /// Authoritative name server
        /// </summary>
        NS = 2,

        /// <summary>
        /// Mail destination (obsolete)
        /// </summary>
        MD = 3,

        /// <summary>
        /// Mail forwarder (obsolete)
        /// </summary>
        MF = 4,

        /// <summary>
        /// Cannonical name for an alias
        /// </summary>
        CNAME = 5,

        /// <summary>
        /// Marks the start of a zone of authority
        /// </summary>
        SOA = 6,

        /// <summary>
        /// Mail box domain name (experimental)
        /// </summary>
        MB = 7,

        /// <summary>
        /// Mail group member (experimental)
        /// </summary>
        MG = 8,

        /// <summary>
        /// Mail rename domain (experimental)
        /// </summary>
        MR = 9,

        /// <summary>
        /// Null (experimental)
        /// </summary>
        NULL = 10,

        /// <summary>
        /// Wll known service description
        /// </summary>
        WKS = 11,

        /// <summary>
        /// Domain name pointer
        /// </summary>
        PTR = 12,

        /// <summary>
        /// Hist information
        /// </summary>
        HINFO = 13,

        /// <summary>
        /// Mailbox or mail list information
        /// </summary>
        MINFO = 14,

        /// <summary>
        /// Mail exchange
        /// </summary>
        MX = 15,

        /// <summary>
        /// Text strings
        /// </summary>
        TXT = 16,

        /// <summary>
        /// Physical location (RFC 1876)
        /// </summary>
        LOC = 29,

        /// <summary>
        /// Host IPv6 address (RFC 1886)
        /// </summary>
        AAAA = 28,

        /// <summary>
        /// Service location (RFC 2782)
        /// </summary>
        SRV = 33,

        /// <summary>
        /// Naming authority pointer (RFC 3403)
        /// </summary>
        NAPTR = 35,

        /// <summary>
        /// Host IPv6 address (RFC 2874, Obsoletes AAAA)
        /// </summary>
        A6 = 38,

        /// <summary>
        /// Map an subtree of the namespace to another domain (RFC 2672)
        /// </summary>
        DNAM = 39,

        /// <summary>
        /// Returns all record types
        /// </summary>
        ALL = 255,
    }
}
