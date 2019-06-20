//-----------------------------------------------------------------------------
// FILE:        SipInviteRequest.cs
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
    /// Encapsulates a SIP INVITE request message.
    /// </summary>
    public sealed class SipInviteRequest : SipRequest
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="to">Populates the request's <b>To</b> header.</param>
        /// <param name="from">Populates the request's <b>From</b> header.</param>
        /// <param name="sdp">The SDP information describing this side's session media.</param>
        public SipInviteRequest(string requestUri, string to, string from, SdpPayload sdp)
            : base(SipMethod.Invite, requestUri, SipHelper.SIP20)
        {
            base.AddHeader(SipHeader.To, to);
            base.AddHeader(SipHeader.From, from);
            base.AddHeader(SipHeader.CallID, SipHelper.GenerateCallID());
            base.AddHeader("Allow", "ACK, CANCEL, BYE");
            base.AddHeader("Accept", SipHelper.SdpMimeType);
            base.AddHeader("Content-Disposition", "session");

            base.Contents = Helper.ToUTF8(sdp.ToString());
        }
    }
}
