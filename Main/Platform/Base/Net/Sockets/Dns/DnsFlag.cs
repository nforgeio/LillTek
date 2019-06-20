//-----------------------------------------------------------------------------
// FILE:        DnsEnums.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the DNS message header flags

// Note that the values defined in these enums map directly to the values
// defined by the DNS protocol.

using System;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines the DNS message header flags (after being converted from network to machine ordering).
    /// </summary>
    [Flags]
    public enum DnsFlag : int
    {
        /// <summary>
        /// Indicates that all flag bits are zeros.
        /// </summary>
        NONE = 0x0000,

        /// <summary>
        /// 0=query, 1=response
        /// </summary>
        QR = 0x8000,

        /// <summary>
        /// Mask for opcode
        /// </summary>
        OPCODE_MASK = 0x7800,

        /// <summary>
        /// Bits to shift opcode right
        /// </summary>
        OPCODE_SHIFT = 11,

        /// <summary>
        /// Authoritative answer
        /// </summary>
        AA = 0x0400,

        /// <summary>
        /// Truncated
        /// </summary>
        TC = 0x0200,

        /// <summary>
        /// Recursion desired
        /// </summary>
        RD = 0x0100,

        /// <summary>
        /// Recursion available
        /// </summary>
        RA = 0x0080,

        /// <summary>
        /// Response code mask
        /// </summary>
        RCODE_MASK = 0x000F,

        /// <summary>
        /// Success
        /// </summary>
        RCODE_OK = 0x0000,

        /// <summary>
        /// Format error
        /// </summary>
        RCODE_FORMAT = 0x0001,

        /// <summary>
        /// Server failure
        /// </summary>
        RCODE_SERVER = 0x0002,

        /// <summary>
        /// Name error: domain name doesn't exist
        /// </summary>
        RCODE_NAME = 0x0003,

        /// <summary>
        /// Not implemented
        /// </summary>
        RCODE_NOTIMPL = 0x0004,

        /// <summary>
        /// Request refused
        /// </summary>
        RCODE_REFUSED = 0x0005,

        //---------------------------------------------------------------------
        // The following error codes are not part of the RFC 1035 standard 
        // and are for internal use only to communicate extended error information.
        // These codes will never appaear on the wire.

        /// <summary>
        /// Indicates that the response received from the server does not
        /// answer the request.  This is not a standard RFC 1035 error
        /// code and will never be sent on the wire.
        /// </summary>
        RCODE_NOTANSWER = 16,

        /// <summary>
        /// The maximum number of DNS queries allow to process a single DNS
        /// request has been exceeded.
        /// </summary>
        RCODE_QUERYLIMIT = 17,

        /// <summary>
        /// The maximum return code value 
        /// </summary>
        RCODE_MAX = 17
    }
}
