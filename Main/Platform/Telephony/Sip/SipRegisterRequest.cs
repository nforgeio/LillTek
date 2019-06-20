//-----------------------------------------------------------------------------
// FILE:        SipRegisterRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP INVITE request message.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP REGISTER request message.
    /// </summary>
    public sealed class SipRegisterRequest : SipRequest
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="to">Populates the request's <b>To</b> header.</param>
        /// <param name="from">Populates the request's <b>From</b> header.</param>
        /// <param name="desiredTTL">The requested lifetime of the registration.</param>
        /// <remarks>
        /// <para>
        /// The RFC 3261 requires that UACs use the same Call-ID for all REGISTER requests
        /// made to a registrar and also that that UA must increment the CSeq value by
        /// one for each request.  The UAC will need to track these values and pass them
        /// to this constructor.
        /// </para>
        /// </remarks>
        public SipRegisterRequest(string requestUri, string to, string from, TimeSpan desiredTTL)
            : base(SipMethod.Register, requestUri, SipHelper.SIP20)
        {
            base.AddHeader(SipHeader.To, to);
            base.AddHeader(SipHeader.From, from);
            base.AddHeader(SipHeader.CallID, SipHelper.GenerateCallID());
            base.AddHeader(SipHeader.Expires, ((int)desiredTTL.TotalSeconds).ToString());
        }
    }
}
