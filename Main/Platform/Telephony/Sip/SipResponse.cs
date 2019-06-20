//-----------------------------------------------------------------------------
// FILE:        SipResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP response message.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP response message.
    /// </summary>
    public class SipResponse : SipMessage
    {
        private SipStatus status;         // The SIP status
        private string reasonPhrase;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="statusCode">The SIP response status code.</param>
        /// <param name="reasonPhrase">The reason phrase (or <c>null</c>).</param>
        /// <param name="sipVersion">The SIP version string (or <c>null</c>).</param>
        public SipResponse(int statusCode, string reasonPhrase, string sipVersion)
            : base(false, sipVersion)
        {
            if (statusCode < 0)
                throw new SipException("Cannot assign stack specific error codes to a SIP response.");

            if (statusCode < 100 || statusCode >= 700)
                throw new SipException("Invalid status code [{0}].", statusCode);

            this.status       = (SipStatus)statusCode;
            this.reasonPhrase = reasonPhrase != null ? reasonPhrase : SipHelper.GetReasonPhrase(statusCode);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="status">The SIP response status.</param>
        /// <param name="reasonPhrase">The reason phrase (or <c>null</c>).</param>
        /// <param name="sipVersion">The SIP version string (or <c>null</c>).</param>
        public SipResponse(SipStatus status, string reasonPhrase, string sipVersion)
            : base(false, sipVersion)
        {
            int statusCode = (int)status;

            if (statusCode < 0)
                throw new SipException("Cannot assign stack specific error codes to a SIP response.");

            if (statusCode < 100 || statusCode >= 700)
                throw new SipException("Invalid status code [{0}].", statusCode);

            this.status       = status;
            this.reasonPhrase = reasonPhrase != null ? reasonPhrase : SipHelper.GetReasonPhrase(status);
        }

        /// <summary>
        /// Returns a deep clone of the response.
        /// </summary>
        /// <returns>The cloned <see cref="SipResponse" />.</returns>
        public SipResponse Clone()
        {
            var clone = new SipResponse(status, reasonPhrase, base.SipVersion);

            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Returns the response <see cref="SipStatus" />.
        /// </summary>
        public SipStatus Status
        {
            get { return status; }
        }

        /// <summary>
        /// Returns the response status code as an integer.
        /// </summary>
        public int StatusCode
        {
            get { return (int)status; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a final response with <see cref="StatusCode" /> in
        /// the range of 200-699.
        /// </summary>
        public bool IsFinal
        {
            get { return (int)status >= 200; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a final response with <see cref="StatusCode" /> in
        /// the range of 300-699.
        /// </summary>
        public bool IsNonSuccessFinal
        {
            get { return (int)status >= 300; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a successful response with <see cref="StatusCode" /> in
        /// the range of 200-299.
        /// </summary>
        public bool IsSuccess
        {
            get { return 200 <= (int)status && (int)status <= 299; }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SipStatus" /> code is in
        /// the range of 400-699, indicating an error response.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" /> code to check.</param>
        public bool IsError(SipStatus status)
        {
            return (int)status >= 400;
        }

        /// <summary>
        /// Returns <c>true</c> if this is a provisional response with <see cref="StatusCode" /> in
        /// the range of 100-199.
        /// </summary>
        public bool IsProvisional
        {
            get { return (int)status <= 199; }
        }

        /// <summary>
        /// Returns the human readable response reason phrase.
        /// </summary>
        public string ReasonPhrase
        {
            get { return reasonPhrase; }
        }

        /// <summary>
        /// Attempts to return the client side transaction ID from the message.  This is
        /// the <b>branch</b> parameter from the top-most <b>Via</b> header.
        /// </summary>
        /// <param name="transactionID">Returns as the transaction ID on success.</param>
        /// <returns><b><c>true</c> if the transaction ID was returned.</b></returns>
        public bool TryGetTransactionID(out string transactionID)
        {
            SipHeader   via;
            SipValue    viaValue;

            transactionID = null;

            via = base.Headers[SipHeader.Via];
            if (via == null)
                return false;

            viaValue = new SipValue(via.Text);
            return viaValue.Parameters.TryGetValue("branch", out transactionID);
        }

        /// <summary>
        /// Renders the SIP response and headers into the text format suitable for transmission.
        /// </summary>
        /// <returns>The formatted message.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(2048);

            // Make sure that the Content-Length header is initialized.

            base.Headers[SipHeader.ContentLength] = new SipHeader(SipHeader.ContentLength, base.Contents.Length.ToString());

            sb.AppendFormat("{0} {1} {2}\r\n", base.SipVersion, (int)status, reasonPhrase);
            base.Headers.Serialize(sb);
            return sb.ToString();
        }
    }
}
