//-----------------------------------------------------------------------------
// FILE:        SipMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base implementation of a SIP message.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// The SIP message parser/lexer is very primitive at this point
// as I really haven't made a real attempt to understand and implement
// the grammer defined in RFC 3261.  I've basically hacked this so
// that I can get enough of a SIP stack working quickly so I can
// get Microsoft Speech Server to talk to the outside world.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Base implementation of a SIP message.  <see cref="SipRequest" /> and <see cref="SipResponse" />
    /// both derive from this class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SIP messages are not created directly by applications; <see cref="SipRequest" /> and
    /// <see cref="SipResponse" /> instances will be created instead.  The constructors for
    /// these classes include the parameters necessary to initialize the first line of the
    /// SIP message: the <b>method</b>, <b>URI</b>, <b>SIP version</b> for <see cref="SipRequest" />
    /// and <b>Sip version</b>, <b>status</b>, and <b>reason phrase</b> for <see cref="SipResponse" />.
    /// </para>
    /// <para>
    /// This <see cref="SipMessage" /> base class implementation defines the methods and
    /// properties common to both SIP requests and responses.  This includes properties such as
    /// <see cref="Headers" /> which holds the message's headers, <see cref="Contents" /> which
    /// references the message payload, <see cref="IsRequest" /> and <see cref="IsResponse" />
    /// which indicate the message type.  The <see cref="ToString" /> method renders the
    /// message envelope and the <see cref="ToArray" /> method renders the message into a
    /// byte array suitable for transmision to a remote SIP element.
    /// </para>
    /// <para>
    /// Applications send SIP messages by instantiating a <see cref="SipRequest" /> or
    /// <see cref="SipResponse" /> instance, intializing its headers, and then passing
    /// it to the <see cref="ISipTransport.Send" /> method of an <see cref="ISipTransport" />
    /// implementation.
    /// </para>
    /// <para>
    /// <see cref="ISipTransport" /> implementations are responsible for parsing messages
    /// received from remote SIP elements.  These transports do this by calling the static
    /// <see cref="Parse" /> method, passing the byte data received.
    /// </para>
    /// </remarks>
    public class SipMessage
    {
        //---------------------------------------------------------------------
        // Static Members

        private static byte[] emptyPayload = new byte[0];
        private static byte[] CRLFCRLF     = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A };

        /// <summary>
        /// Parses a <see cref="SipRequest" /> or <see cref="SipResponse" /> from the
        /// <paramref name="buffer"/> byte array passed.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded SIP message data.</param>
        /// <param name="isCompletePacket"><c>true</c> if <paramref name="buffer" /> includes both the message header and payload.</param>
        /// <returns>A <see cref="SipRequest" /> or <see cref="SipResponse" />.</returns>
        /// <remarks>
        /// <para>
        /// This method is used internally by <see cref="ISipTransport" /> implementations to
        /// convert raw data read from the transport into SIP messages.  This method supports
        /// two basic situations:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The transport is packet oriented (aka UDP) and the buffer passed includes
        ///     both the SIP message header and content information.  In this case, you
        ///     need to pass <paramref name="isCompletePacket"/> as <c>true</c>, and the 
        ///     SIP message returned will be complete, with the <see cref="Contents" />
        ///     property set to the payload bytes.
        ///     </item>
        ///     <item>
        ///     The transport is stream oriented (aka TCP or TLS) and the buffer passed
        ///     includes only the message header information from the message start line
        ///     up to and including the empty line terminating the header section.  In this
        ///     case, the <see cref="Contents" /> property of the SIP message returned will
        ///     be set to <c>null</c> and <see cref="ContentLength" /> will be set to the
        ///     parsed value.  The transport is then responsible for extracting the message
        ///     payload from the stream and explictly setting the <see cref="Contents" />
        ///     property.
        ///     </item>
        /// </list>
        /// </remarks>
        public static SipMessage Parse(byte[] buffer, bool isCompletePacket)
        {
            string      text;
            int         dataPos;
            int         p, pEnd, pColon;
            string      firstLine;
            string      line;
            string      reasonPhrase;
            string[]    fields;
            SipMessage  message;
            SipHeader   header;
            int         cbContents;

            dataPos = Helper.IndexOf(buffer, CRLFCRLF);
            if (dataPos == -1)
                throw new SipException("Malformed SIP message: header termination missing.");

            dataPos += 4;
            text     = Helper.FromUTF8(buffer, 0, dataPos);

            // Look at the first line of text to determine whether we have a 
            // request or a response.

            p    = 0;
            pEnd = text.IndexOf("\r\n");

            firstLine = text.Substring(p, pEnd - p);
            fields    = firstLine.Split(' ');
            if (fields.Length < 3)
                throw new SipException("Malformed SIP message: invalid first line.");

            p = firstLine.IndexOf(' ');
            p = firstLine.IndexOf(' ', p + 1);
            reasonPhrase = firstLine.Substring(p + 1);

            if (fields[0].ToUpper().StartsWith("SIP/"))
            {
                int statusCode;

                if (!int.TryParse(fields[1], out statusCode))
                    throw new SipException("Malformed SIP message: invalid response status code.");

                if (statusCode < 100 || statusCode >= 700)
                    throw new SipException("Invalid status code [{0}].", statusCode);

                message = new SipResponse(statusCode, reasonPhrase, fields[0]);
            }
            else
                message = new SipRequest(fields[0], fields[1], reasonPhrase);

            // Parse the headers

            header = null;

            p = pEnd + 2;
            while (true)
            {
                pEnd = text.IndexOf("\r\n", p);
                line = text.Substring(p, pEnd - p);
                if (line.Length == 0)
                    break;

                if (line[0] == ' ' || line[0] == '\t')
                {
                    // Header folding

                    if (header == null)
                        throw new SipException("Malformed SIP message: invalid header folding.");

                    header.FullText += " " + line.Trim();
                }
                else
                {
                    string name;
                    string value;

                    // Parse a normal header: <field-name> ":" <field-value>

                    pColon = line.IndexOf(':');
                    if (pColon == -1)
                        throw new SipException("Malformed SIP message: header missing a colon.");

                    name = line.Substring(0, pColon).Trim();
                    value = line.Substring(pColon + 1).Trim();
                    header = message.headers.Add(name, value);
                }

                p = pEnd + 2;
            }

            // Handle the payload

            if (isCompletePacket)
            {
                // Extract the contents from the buffer.  If we have a Content-Length header
                // then use that to determine how much data we have, otherwise extract the
                // data from the end of the headers to the end of the buffer.

                header = message[SipHeader.ContentLength];
                if (header != null)
                {
                    if (!int.TryParse(header.Text, out cbContents) || cbContents < 0 || cbContents > buffer.Length - dataPos)
                        throw new SipException("Malformed SIP message: invalid Content-Length.");

                    message.contents = Helper.Extract(buffer, dataPos, cbContents);
                }
                else
                    message.contents = Helper.Extract(buffer, dataPos);
            }
            else
            {
                // Messages received from streaming transports must have a valid 
                // Content-Length header.

                header = message[SipHeader.ContentLength];
                if (header == null)
                    throw new SipException("Content-Length required for messages received on stream transports.");

                if (!int.TryParse(header.Text, out cbContents) || cbContents < 0)
                    throw new SipException("Malformed SIP message: invalid Content-Length.");
            }

            return message;
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool                    isRequest;          // True if the message is a SIP request, false for a response
        private string                  sipVersion;         // SIP version string (typically "SIP/2.0")
        private SipHeaderCollection     headers;            // The message headers
        private byte[]                  contents;           // The message payload
        private SipTransaction          sourceTransaction;  // The message source transaction
        private ISipTransport           sourceTransport;    // The message source transport
        private NetworkBinding          remoteEP;           // The message source endpoint

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isRequest"><c>true</c> if the message is a SIP request, false for a response</param>
        /// <param name="sipVersion">The SIP version string (or <c>null</c>).</param>
        internal SipMessage(bool isRequest, string sipVersion)
        {
            this.isRequest         = isRequest;
            this.sipVersion        = sipVersion == null ? "SIP/2.0" : sipVersion.ToUpper();
            this.headers           = new SipHeaderCollection();
            this.contents          = emptyPayload;
            this.sourceTransaction = null;
            this.sourceTransport   = null;
            this.remoteEP          = null;
        }

        /// <summary>
        /// Performs a deep copy of the internal properties of the message passed 
        /// to this instance.
        /// </summary>
        /// <param name="message">The message to be copied.</param>
        internal void CopyFrom(SipMessage message)
        {
            this.isRequest         = message.isRequest;
            this.sipVersion        = message.sipVersion;
            this.sourceTransaction = null;
            this.sourceTransport   = message.sourceTransport;
            this.remoteEP          = message.remoteEP;

            this.contents = null;
            if (message.contents != null)
            {
                this.contents = new byte[message.contents.Length];
                Array.Copy(message.contents, this.contents, message.contents.Length);
            }

            foreach (SipHeader header in message.headers.Values)
                this.headers.Add(header.Name, header.Clone());
        }

        /// <summary>
        /// Returns the SIP version for this message (typically "SIP/2.0").
        /// </summary>
        public string SipVersion
        {
            get { return sipVersion; }
        }

        /// <summary>
        /// Returns <c>true</c> if the message is a <see cref="SipRequest" />.
        /// </summary>
        public bool IsRequest
        {
            get { return isRequest; }
        }

        /// <summary>
        /// Returns <c>true</c> if the message is a <see cref="SipResponse" />.
        /// </summary>
        public bool IsResponse
        {
            get { return !isRequest; }
        }

        /// <summary>
        /// Returns the <see cref="ISipTransport" /> the message was received on,
        /// <c>null</c> otherwise. 
        /// </summary>
        public ISipTransport SourceTransport
        {
            get { return sourceTransport; }
            internal set { sourceTransport = value; }
        }

        /// <summary>
        /// Returns the <see cref="SipTransaction" /> that handled the reception
        /// of this message.
        /// </summary>
        public SipTransaction SourceTransaction
        {
            get { return sourceTransaction; }
            internal set { sourceTransaction = value; }
        }

        /// <summary>
        /// Returns the <see cref="NetworkBinding" /> for the message source if
        /// it was received on a <see cref="ISipTransport" />, <c>null</c> otherwise.
        /// </summary>
        public NetworkBinding RemoteEndpoint
        {
            get { return remoteEP; }
            internal set { remoteEP = value; }
        }

        /// <summary>
        /// Returns the message's <see cref="SipHeaderCollection" />.
        /// </summary>
        public SipHeaderCollection Headers
        {
            get { return headers; }
        }

        /// <summary>
        /// Returns <c>true</c> if the message includes the specified header.
        /// </summary>
        /// <param name="name">Case insensitive name of the desired header.</param>
        /// <returns><c>true</c> if the header is present.</returns>
        public bool ContainsHeader(string name)
        {
            return headers.ContainsKey(name);
        }

        /// <summary>
        /// Adds a header to the message.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <remarks>
        /// <note>
        /// If a header with this name already exists then this method will
        /// add a new value to it.
        /// </note>
        /// </remarks>
        public void AddHeader(string name, string value)
        {
            headers.Add(name, value);
        }

        /// <summary>
        /// Adds a header to the message.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <remarks>
        /// <note>
        /// If a header with this name already exists then this method will
        /// add a new value to it.
        /// </note>
        /// </remarks>
        public void AddHeader(string name, SipValue value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            headers.Add(name, value.ToString());
        }

        /// <summary>
        /// Adds a header to the message, replacing any existing header
        /// with this name.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value (or <c>null</c> to remove any existing header).</param>
        /// <remarks>
        /// This method is equivalent to calling the <b>this[string]</b> indexer's setter.
        /// </remarks>
        public void SetHeader(string name, SipHeader value)
        {
            if (value == null)
            {
                if (headers.ContainsKey(name))
                    headers.Remove(name);

                return;
            }

            this[name] = value;
        }

        /// <summary>
        /// Adds a header to the message, replacing any existing header
        /// with this name.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value (or <c>null</c> to remove any existing header).</param>
        public void SetHeader(string name, string value)
        {
            if (value == null)
            {
                if (headers.ContainsKey(name))
                    headers.Remove(name);

                return;
            }

            this[name] = new SipHeader(name, value);
        }

        /// <summary>
        /// Adds a header to the message, replacing any existing header
        /// with this name.
        /// </summary>
        /// <param name="header">The new header.</param>
        public void SetHeader(SipHeader header)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            this[header.Name] = header;
        }

        /// <summary>
        /// Adds a header to the message, <b>appending</b> the value to the
        /// end of any existing values if the header already exists.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value.</param>
        public void AppendHeader(string name, string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            headers.Append(name, value);
        }

        /// <summary>
        /// Adds a header to the message, <b>appending</b> the value to the
        /// end of any existing values if the header already exists.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value.</param>
        public void AppendHeader(string name, SipValue value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            headers.Append(name, value.ToString());
        }

        /// <summary>
        /// Adds a header to the message, <b>prepending</b> the value before
        /// any existing values if the header already exists.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value.</param>
        public void PrependHeader(string name, string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            headers.Prepend(name, value);
        }

        /// <summary>
        /// Adds a header to the message, <b>prepending</b> the value before
        /// any existing values if the header already exists.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The new value.</param>
        public void PrependHeader(string name, SipValue value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            headers.Prepend(name, value.ToString());
        }

        /// <summary>
        /// Returns a header's <see cref="SipHeader" /> from the message.
        /// </summary>
        /// <param name="name">The desired header's name.</param>
        /// <returns>The <see cref="SipHeader" /> if the header is present, <c>null</c> otherwise.</returns>
        /// <remarks>
        /// This method is equivalent to calling the <b>this[string]</b> indexer's getter.
        /// </remarks>
        public SipHeader GetHeader(string name)
        {
            return this[name];
        }

        /// <summary>
        /// Returns the first value of the specified header, if it is present.
        /// </summary>
        /// <typeparam name="T">Specifies the desired <see cref="SipValue" /> result type.</typeparam>
        /// <param name="name">The header name.</param>
        /// <returns>The header value or <c>null</c> if the header doesn't exist.</returns>
        public T GetHeader<T>(string name)
            where T : SipValue, new()
        {
            SipHeader   h;
            T           v;

            h = this[name];
            if (h == null)
                return null;

            v = new T();
            v.Parse(h.Text);

            return v;
        }

        /// <summary>
        /// Returns a header's text from the message, if the header is present.
        /// </summary>
        /// <param name="name">The desired header's name.</param>
        /// <returns>The header text if the header is present, <c>null</c> otherwise.</returns>
        /// <remarks>
        /// If the header has multiple values, this method will return the text
        /// for the first value.
        /// </remarks>
        public string GetHeaderText(string name)
        {
            var header = this[name];

            if (header == null)
                return null;

            return header.Text;
        }

        /// <summary>
        /// Accesses the named <see cref="SipHeader" />.
        /// </summary>
        /// <param name="name">Case insensitive name of the desired header.</param>
        /// <returns>The header instance or <c>null</c> if the header is not present.</returns>
        public SipHeader this[string name]
        {
            get { return headers[name]; }

            set
            {
                headers.Remove(name);
                headers.Add(name, new SipHeader(name, value.Values));
            }
        }

        /// <summary>
        /// Removes a header if it is present in the message.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <remarks>
        /// <note>
        /// No exception will be thrown if the header doesn't exist.
        /// </note>
        /// </remarks>
        public void RemoveHeader(string name)
        {
            headers.Remove(name);
        }

        /// <summary>
        /// Compares the <b>CSeq</b> header in this message with the message
        /// passed.
        /// </summary>
        /// <param name="message">The message to be compared.</param>
        /// <returns><c>true</c> if both headers have <b>CSeq</b> headers and their values match.</returns>
        public bool MatchCSeq(SipMessage message)
        {
            var v1 = this.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            var v2 = message.GetHeader<SipCSeqValue>(SipHeader.CSeq);

            if (v1 == null || v2 == null)
                return false;

            return v1.Number == v2.Number && v1.Method == v2.Method;
        }

        /// <summary>
        /// Returns the size of the message payload in bytes.
        /// </summary>
        public int ContentLength
        {
            get { return contents.Length; }
        }

        /// <summary>
        /// The message payload encoded as an array of bytes.
        /// </summary>
        public byte[] Contents
        {
            get { return contents; }
            set { contents = value; }
        }

        /// <summary>
        /// Determines whether the message has a <b>Content-Type</b> header
        /// that matches the MIM type passed.
        /// </summary>
        /// <param name="mimeType">The MIME type.</param>
        /// <returns><c>true</c> if the message has the specified MIME type.</returns>
        public bool HasContentType(string mimeType)
        {
            var header = GetHeader("Content-Type");

            if (header == null)
                return false;

            return String.Compare(mimeType, header.Text, true) == 0;
        }

        /// <summary>
        /// Renders the message into a byte array suitable for transmission to another SIP element.
        /// </summary>
        /// <returns>The encoded SIP message.</returns>
        public byte[] ToArray()
        {
            return Helper.Concat(Helper.ToUTF8(this.ToString()), contents);
        }

        /// <summary>
        /// Renders the message into a string suitable for transmitting to another SIP element.
        /// </summary>
        /// <returns>The message string.</returns>
        public override string ToString()
        {
            throw new NotImplementedException("ToString() must be implemented by derived classes.");
        }
    }
}
