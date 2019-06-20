//-----------------------------------------------------------------------------
// FILE:        HttpResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a light-weight HTTP response.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a light-weight HTTP response.
    /// </summary>
    public sealed class HttpResponse : HttpHeaderCollection
    {
        //---------------------------------------------------------------------
        // Static members

        private const string BadParseMsg = "Invalid parsing operation.";

        /// <summary>
        /// Indicates the current parsing state.
        /// </summary>
        private enum ParseState
        {
            Disabled,
            None,
            Headers,
            Content,
            GotAll,
            Done
        }

        //---------------------------------------------------------------------
        // Instance members

        private ParseState          parseState;         // Indicates where we are in parsing
        private HttpContentParser   contentParser;      // The content parser
        private BlockArray          content;            // The content data
        private int                 cbContentMax;       // Maximum content size (or -1)

        /// <summary>
        /// Constructs a response to be parsed from a network stream.
        /// </summary>
        public HttpResponse()
            : base(false)
        {
            this.parseState    = ParseState.None;
            this.contentParser = null;
            this.content       = null;
            this.cbContentMax  = -1;
        }

        /// <summary>
        /// Constructs a response to be parsed from a network stream.
        /// </summary>
        /// <param name="cbContentMax">Maximum allowed content size or <b>-1</b> to disable checking.</param>
        public HttpResponse(int cbContentMax)
            : base(false)
        {
            this.parseState    = ParseState.None;
            this.contentParser = null;
            this.content       = null;
            this.cbContentMax  = cbContentMax;
        }

        /// <summary>
        /// Constructs a HTTP response.
        /// </summary>
        /// <param name="version">HTTP protocol version.</param>
        /// <param name="status">HTTP response status code.</param>
        /// <param name="reason">Response reason phrase.</param>
        public HttpResponse(Version version, HttpStatus status, string reason)
            : base(version, status, reason)
        {
            this.parseState   = ParseState.Disabled;
            this.cbContentMax = -1;

            if (content == null)
                content = new BlockArray();
        }

        /// <summary>
        /// Constructs a HTTP 1.1 response.
        /// </summary>
        /// <param name="status">HTTP response status code.</param>
        /// <param name="reason">Response reason phrase.</param>
        public HttpResponse(HttpStatus status, string reason)
            : base(status, reason)
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();
        }

        /// <summary>
        /// Constructs a HTTP 1.1 response.
        /// </summary>
        /// <param name="status">HTTP response status code.</param>
        /// <remarks>
        /// The reason phrase will be initialized to the standard phrase
        /// from HttpStack.GetReasonPhrase().
        /// </remarks>
        public HttpResponse(HttpStatus status)
            : base(status, HttpStack.GetReasonPhrase(status))
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();
        }

        /// <summary>
        /// Returns the response status code.
        /// </summary>
        public new HttpStatus Status
        {
            get { return base.Status; }
        }

        /// <summary>
        /// Returns the response reason phrase.
        /// </summary>
        public new string Reason
        {
            get { return base.Reason; }
        }

        /// <summary>
        /// The content data as a block array.
        /// </summary>
        public BlockArray Content
        {
            get { return content; }

            set
            {
                if (value == null)
                    content = new BlockArray();
                else
                    content = value;
            }
        }

        /// <summary>
        /// Serializes the response into a binary form suitiable for network transmission.
        /// </summary>
        /// <param name="blockSize">Size of the underlying blocks.</param>
        /// <returns>The request as a block array.</returns>
        /// <remarks>
        /// <note>
        /// This always sets a Content-Length header.
        /// </note>
        /// </remarks>
        internal new BlockArray Serialize(int blockSize)
        {
            BlockArray blocks;

            this["Content-Length"] = content.Size.ToString();

            blocks = base.Serialize(blockSize);
            if (content != null)
                blocks.Append(content);

            return blocks;
        }

        /// <summary>
        /// Initiates a response parsing operation.
        /// </summary>
        public new void BeginParse()
        {
            if (parseState != ParseState.None || content != null || contentParser != null)
                throw new InvalidOperationException(BadParseMsg);

            parseState = ParseState.Headers;
            base.BeginParse();
        }

        /// <summary>
        /// Handles the parsing of received data.
        /// </summary>
        /// <param name="data">The received data.</param>
        /// <param name="cb">Number of bytes received.</param>
        /// <returns><c>true</c> if the data has been completely parsed.</returns>
        /// <remarks>
        /// <note>
        /// The data buffer passed MUST NOT be reused.  Ownership
        /// will be taken over by this instance.
        /// </note>
        /// </remarks>
        /// <exception cref="HttpBadProtocolException">Badly formatted HTTP message.</exception>
        public new bool Parse(byte[] data, int cb)
        {
            switch (parseState)
            {
                case ParseState.Headers:

                    BlockArray blocks;
                    int dataPos;

                    if (base.Parse(data, cb))
                    {
                        blocks        = base.EndParse(out dataPos);
                        parseState    = ParseState.Content;
                        contentParser = new HttpContentParser(this, cbContentMax);

                        if (contentParser.BeginParse(blocks, dataPos))
                        {
                            parseState = ParseState.GotAll;
                            return true;
                        }
                    }
                    else
                        return false;

                    break;

                case ParseState.Content:

                    if (contentParser.Parse(data, cb))
                    {
                        parseState = ParseState.GotAll;
                        return true;
                    }
                    else
                        return false;

                default:

                    throw new InvalidOperationException(BadParseMsg);
            }

            return false;
        }

        /// <summary>
        /// Completes the parsing of the response.
        /// </summary>
        public void EndParse()
        {
            if (parseState != ParseState.GotAll)
                throw new InvalidOperationException(BadParseMsg);

            content       = contentParser.EndParse();
            contentParser = null;
            parseState    = ParseState.Done;
        }
    }
}
