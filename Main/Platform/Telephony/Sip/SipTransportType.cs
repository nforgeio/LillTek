//-----------------------------------------------------------------------------
// FILE:        SipTransport.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible SIP transport types.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Enumerates the possible SIP transport types.
    /// </summary>
    public enum SipTransportType
    {
        /// <summary>
        /// Returned by <see cref="SipHelper.TryGetRemoteBinding" /> if the transport type
        /// was not specified in the contact information.
        /// </summary>
        Unspecified,

        /// <summary>
        /// SIP over UDP.
        /// </summary>
        UDP,

        /// <summary>
        /// SIP over TCP.
        /// </summary>
        TCP,

        /// <summary>
        /// SIP over TLS.
        /// </summary>
        TLS
    }
}
