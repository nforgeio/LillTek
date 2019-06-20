//-----------------------------------------------------------------------------
// FILE:        HttpRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a light-weight HTTP request.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a light-weight HTTP request.
    /// </summary>
    public sealed class HttpRequest : HttpHeaderCollection
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
        private Uri                 uri;                // The request URI
        private int                 parsePort;          // Holds the network port number while
                                                        // parsing a request.
        private int                 cbContentMax;       // Maximum content size (or -1)

        /// <summary>
        /// Constructs a request to be parsed from a network stream.
        /// </summary>
        public HttpRequest()
            : base(true)
        {
            this.parseState    = ParseState.None;
            this.contentParser = null;
            this.content       = null;
            this.cbContentMax  = -1;
        }

        /// <summary>
        /// Constructs a request to be parsed from a network stream.
        /// </summary>
        /// <param name="cbContentMax">Maximum allowed content size or <b>-1</b> to disable checking.</param>
        public HttpRequest(int cbContentMax)
            : base(true)
        {
            this.parseState    = ParseState.None;
            this.contentParser = null;
            this.content       = null;
            this.cbContentMax  = cbContentMax;
        }

        /// <summary>
        /// Constructs a HTTP request.
        /// </summary>
        /// <param name="version">HTTP protocol version.</param>
        /// <param name="method">HTTP method (GET, POST, PUT,...)</param>
        /// <param name="rawUri">The raw (escaped) request URI.</param>
        /// <param name="content">The request content (or <c>null</c>).</param>
        public HttpRequest(Version version, string method, string rawUri, BlockArray content)
            : base(version, method, rawUri)
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();

            this.content = content;
        }

        /// <summary>
        /// Constructs a HTTP 1.1 request.
        /// </summary>
        /// <param name="method">HTTP method (GET, POST, PUT,...)</param>
        /// <param name="rawUri">The raw (escaped) request URI.</param>
        /// <param name="content">The request content (or <c>null</c>).</param>
        public HttpRequest(string method, string rawUri, BlockArray content)
            : base(method, rawUri)
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();

            this.content = content;
        }

        /// <summary>
        /// Constructs a HTTP request.
        /// </summary>
        /// <param name="version">HTTP protocol version.</param>
        /// <param name="method">HTTP method (GET, POST, PUT,...)</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="content">The request content (or <c>null</c>).</param>
        public HttpRequest(Version version, string method, Uri uri, BlockArray content)
            : base(version, method, Helper.EscapeUri(uri.PathAndQuery))
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();

            this.content = content;
            base["Host"] = uri.Host;
        }

        /// <summary>
        /// Constructs a HTTP 1.1 request.
        /// </summary>
        /// <param name="method">HTTP method (GET, POST, PUT,...)</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="content">The request content (or <c>null</c>).</param>
        public HttpRequest(string method, Uri uri, BlockArray content)
            : base(method, Helper.EscapeUri(uri.PathAndQuery))
        {
            this.parseState = ParseState.Disabled;

            if (content == null)
                content = new BlockArray();

            this.content = content;
            base["Host"] = uri.Host;
        }

        /// <summary>
        /// Returns the HTTP request method (always in upper case).
        /// </summary>
        public new string Method
        {
            get { return base.Method; }
        }

        /// <summary>
        /// Returns the raw (escaped) request URI.
        /// </summary>
        public new string RawUri
        {
            get { return base.RawUri; }
        }

        /// <summary>
        /// Returns the request URI.
        /// </summary>
        public Uri Uri
        {
            get { return uri; }
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
        /// Serializes the request into a binary form suitiable for network transmission.
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
        /// Initiates a request parsing operation.
        /// </summary>
        /// <param name="port">The network port the request was received on.</param>
        public void BeginParse(int port)
        {
            if (parseState != ParseState.None || content != null || contentParser != null)
                throw new InvalidOperationException(BadParseMsg);

            parseState = ParseState.Headers;
            parsePort = port;

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

                    BlockArray  blocks;
                    int         dataPos;

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
        /// Completes the parsing of the request.
        /// </summary>
        public void EndParse()
        {
            string      host;
            int         pos;

            if (parseState != ParseState.GotAll)
                throw new InvalidOperationException(BadParseMsg);

            content       = contentParser.EndParse();
            contentParser = null;
            parseState    = ParseState.Done;

            // Build up the request URI.

            host = base["Host"];
            if (host == null)
                host = "localhost";

            pos = host.IndexOf(':');    // Strip off the port if present
            if (pos != -1)
                host = host.Substring(0, pos);

            uri = new Uri(new Uri(string.Format("http://{0}:{1}", host, parsePort)), base.RawUri);
        }
    }
}
