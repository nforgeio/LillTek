//-----------------------------------------------------------------------------
// FILE:        SipClientCore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a basic SIP client core binding a SipClientAgent
//              and a SipServerAgent to one or more SipTransports.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// I'm bacically just hacking this for now.  I need to come back
// and really do a clean implementation.

// $todo(jeff.lill): 
//
// I don't handle the case where the ACK confirming an INVITE
// has an SDP payload.  This code will continue deliverying
// its media using the SDP sent in the original 2xx response
// to the INVITE.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a basic SIP client core binding a <see cref="SipClientAgent" /> and a 
    /// <see cref="SipServerAgent" /> to one or more <see cref="ISipTransport" />s.
    /// See <see cref="SipCore" /> for information on the basic operation of a core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="SipClientCore" /> is designed for use in client applications
    /// such as Soft Phones or IM applications.  It includes one <see cref="SipClientAgent" />
    /// and one <see cref="SipServerAgent" /> used to implement transactions.
    /// </para>
    /// <para>
    /// Once a <see cref="SipClientCore" /> has been bound to one or more <see cref="ISipTransport" />s
    /// and started using the base class <see cref="SipCore.Start" /> method, this core will
    /// begin routing messages received by the transports to the two agents as well as
    /// messages from the agent to the appropriate outbound transports.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SipClientCore : SipBasicCore
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="settings">The <see cref="SipCoreSettings" />.</param>
        public SipClientCore(SipCoreSettings settings)
            : base(settings)
        {
        }
    }
}
