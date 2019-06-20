//-----------------------------------------------------------------------------
// FILE:        SipRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP request message.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP request message.
    /// </summary>
    public class SipRequest : SipMessage
    {

        private string      methodText;         // The SIP method string (uppercase)
        private SipMethod   method;             // The SIP method
        private string      uri;                // The request URI

        /// <summary>
        /// Initializes a SIP message.
        /// </summary>
        /// <param name="methodText">The SIP method text.</param>
        /// <param name="uri">The SIP request URI.</param>
        /// <param name="sipVersion">The SIP version string (or <c>null</c>).</param>
        public SipRequest(string methodText, string uri, string sipVersion)
            : base(true, sipVersion)
        {
            this.methodText = methodText.ToUpper();
            this.method     = SipHelper.ParseMethod(methodText);
            this.uri        = uri;
        }

        /// <summary>
        /// Initializes a SIP message.
        /// </summary>
        /// <param name="method">The SIP method.</param>
        /// <param name="uri">The SIP request URI.</param>
        /// <param name="sipVersion">The SIP version string (or <c>null</c>).</param>
        public SipRequest(SipMethod method, string uri, string sipVersion)
            : base(true, sipVersion)
        {
            this.methodText = method.ToString().ToUpper();
            this.method     = method;
            this.uri        = uri;
        }

        /// <summary>
        /// Returns a deep clone of the request.
        /// </summary>
        /// <returns>The cloned <see cref="SipRequest" />.</returns>
        public SipRequest Clone()
        {
            var clone = new SipRequest(method, uri, base.SipVersion);

            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Returns the request SIP method as uppercase text.
        /// </summary>
        public string MethodText
        {
            get { return methodText; }
        }

        /// <summary>
        /// Returns the request <see cref="SipMethod" />.
        /// </summary>
        public SipMethod Method
        {
            get { return method; }
        }

        /// <summary>
        /// The request URI.
        /// </summary>
        public string Uri
        {
            get { return uri; }
            set { uri = value; }
        }

        /// <summary>
        /// Sets the message's <b>CSeq</b> header using using the requests current method value.
        /// </summary>
        /// <param name="cseq">The sequence number.</param>
        public void SetCSeq(int cseq)
        {
            base.Headers[SipHeader.CSeq] = new SipHeader(SipHeader.CSeq, string.Format("{0} {1}", cseq, methodText));
        }

        /// <summary>
        /// Attempts to return the server side transaction ID from the message.  This is
        /// the <b>branch</b> parameter from the top-most <b>Via</b> header.
        /// </summary>
        /// <param name="transactionID">Returns as the transaction ID on success.</param>
        /// <returns><b><c>true</c> if the transaction ID was returned.</b></returns>
        /// <remarks>
        /// <para>
        /// Transaction IDs are formed from the "branch" parameter of the
        /// topmost "Via" header, plus the "sent-by" parameter on the "Via" (if present)
        /// plus the method as upper case (except for the ACK method which maps to INVITE)
        /// </para>
        /// <para>
        /// This method also implements the alternative RFC 2543 compatible procedure
        /// if the Via branch parameter doesn't start with the magic cookie (not
        /// implemented yet).
        /// </para>
        /// <para>
        /// See RFC 3261 17.2.3 on page 138 for more information.
        /// </para>
        /// </remarks>
        public bool TryGetTransactionID(out string transactionID)
        {
            SipHeader   via;
            SipViaValue viaValue;
            string      branch;
            string      sentBy;

            transactionID = null;

            via = base[SipHeader.Via];
            if (via == null)
                return false;

            viaValue = new SipViaValue(via.Text);
            branch = viaValue.Branch;

            if (branch == null || !branch.StartsWith("z9hG4bK"))
            {
                SysLog.LogWarning("SIP Request received with non-compliant Via branch parameter.");
                return false;
            }

            transactionID = branch + ":";

            sentBy = viaValue.SentBy;
            if (sentBy != null)
                transactionID += sentBy;

            transactionID += ":";

            if (method == SipMethod.Ack)
                transactionID += "INVITE";
            else
                transactionID += methodText.ToUpper();

            return true;
        }

        /// <summary>
        /// Creates a <see cref="SipResponse" /> for this request, copying the minimum
        /// required headers from the request to the response.
        /// </summary>
        /// <param name="status">The status code.</param>
        /// <param name="reasonPhrase">The reason phrase (or <c>null</c>).</param>
        /// <returns>The <see cref="SipResponse" />.</returns>
        /// <remarks>
        /// <para>
        /// The procedure for doing this is described in RFC 3261 on page 50.
        /// </para>
        /// <note>
        /// This method assumes that the <b>tag</b> parameter on the <b>To</b>
        /// header has already been added (if necessary).
        /// </note>
        /// </remarks>
        public SipResponse CreateResponse(SipStatus status, string reasonPhrase)
        {
            SipResponse     response = new SipResponse(status, reasonPhrase, this.SipVersion);
            SipHeader       header;

            header = this[SipHeader.Via];
            if (header != null)
                response.Headers.Add(SipHeader.Via, header.Clone());

            header = this[SipHeader.To];
            if (header != null)
                response.Headers.Add(SipHeader.To, header.Clone());

            header = this[SipHeader.From];
            if (header != null)
                response.Headers.Add(SipHeader.From, header.Clone());

            header = this[SipHeader.CallID];
            if (header != null)
                response.Headers.Add(SipHeader.CallID, header.Clone());

            header = this[SipHeader.CSeq];
            if (header != null)
                response.Headers.Add(SipHeader.CSeq, header.Clone());

            return response;
        }

        /// <summary>
        /// Creates a CANCEL <see cref="SipRequest" /> for this request.
        /// </summary>
        /// <returns>The CANCEL <see cref="SipRequest" />.</returns>
        public SipRequest CreateCancelRequest()
        {
            SipRequest      cancelRequest = new SipRequest(SipMethod.Cancel, this.uri, this.SipVersion);
            SipHeader       header;
            SipCSeqValue    vCSeq;

            header = this[SipHeader.Via];
            if (header == null || header.Values.Length == 0)
                throw new SipException("[Via] header is required when generating a CANCEL.");

            cancelRequest.SetHeader(SipHeader.Via, header.Text);

            header = this[SipHeader.Route];
            if (header != null)
                cancelRequest.Headers.Add(SipHeader.Route, header);

            header = this[SipHeader.To];
            if (header != null)
                cancelRequest.Headers.Add(SipHeader.To, header.Clone());

            header = this[SipHeader.From];
            if (header != null)
                cancelRequest.Headers.Add(SipHeader.From, header.Clone());

            header = this[SipHeader.CallID];
            if (header == null)
                throw new SipException("[Call-ID] header is required when generating a CANCEL.");

            cancelRequest.Headers.Add(SipHeader.CallID, header.Clone());

            vCSeq = this.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            if (vCSeq == null)
                throw new SipException("[CSeq] header is required when generating a CANCEL.");

            cancelRequest.SetCSeq(vCSeq.Number);

            return cancelRequest;
        }

        /// <summary>
        /// Renders the SIP request and headers into the text format suitable for transmission.
        /// </summary>
        /// <returns>The formatted message.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(2048);

            // Make sure that the Content-Length header is initialized.

            base.Headers[SipHeader.ContentLength] = new SipHeader(SipHeader.ContentLength, base.Contents.Length.ToString());

            sb.AppendFormat("{0} {1} {2}\r\n", methodText, uri, base.SipVersion);
            base.Headers.Serialize(sb);
            return sb.ToString();
        }
    }
}

