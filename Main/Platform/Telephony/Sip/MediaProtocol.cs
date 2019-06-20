//-----------------------------------------------------------------------------
// FILE:        MediaProtocol.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a SDP media protocol.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes a SDP media protocol.
    /// </summary>
    public enum MediaProtocol
    {
        /// <summary>
        /// The protocol cannot be identified by the current implementation
        /// of the SIP stack.
        /// </summary>
        Unknown,

        /// <summary>
        /// An unspecified protocol running over UDP.
        /// </summary>
        Udp,

        /// <summary>
        /// RTP running over UDP (<a href="http://www.ietf.org/rfc/rfc3551.txt?number=3551">RFC 3551</a>).
        /// </summary>
        RtpAvp,

        /// <summary>
        /// Secure Real-time Transport protocol (<a href ="http://www.ietf.org/rfc/rfc3711.txt?number=3711">RFC 3711</a>).
        /// </summary>
        RtpSavp,
    }
}
