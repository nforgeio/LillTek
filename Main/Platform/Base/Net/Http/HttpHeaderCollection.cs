//-----------------------------------------------------------------------------
// FILE:        HttpHeaderCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a collection of HTTP headers.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// Look into limiting the number of bytes of headers to be
// parsed while looking for the terminating CRLF CRLF.

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a collection of HTTP headers combined with the
    /// HTTP request and response line fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class can be initialized in one of two ways.  The first
    /// involves using the <see cref="Add(LillTek.Net.Http.HttpHeader)" /> 
    /// method or the indexer to add headers to the collection and then 
    /// explicitly setting the request/response line fields.  This is 
    /// suitable for creating a header set in preparation for network 
    /// transmission.
    /// </para>
    /// <para>
    /// The second method is used for efficiently parsing the header
    /// set from data received on the network.  To do this call
    /// <see cref="BeginParse" /> and then <see cref="Parse" /> for 
    /// each block of data received.  <see cref="Parse" /> will return 
    /// true when the last byte of header data has been read.  Then 
    /// call <see cref="EndParse" /> to complete the operation.
    /// </para>para>
    /// </remarks>
    public class HttpHeaderCollection : IEnumerable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Maximum number of characters allowed in a HTTP header, footer, or
        /// chunked transfer line.
        /// </summary>
        internal const int MaxHeaderChars = 1024;

        private const string NotParsedMsg = "Headers not parsed yet.";

        //---------------------------------------------------------------------
        // Instance members

        private bool            isRequest;  // True for a request, false for a response
        private string          method;     // HTTP request method (GET, PUT,...)
        private Version         version;    // HTTP version
        private string          rawUri;     // Raw request URI
        private HttpStatus      status;     // HTTP response status code
        private string          reason;     // Reason phrase
        private Hashtable       headers;    // Header values table keyed by uppercase name
        private BlockArray      blocks;     // Received network blocks
        private int             dataPos;    // Logical index in blocks of the first
                                            // byte of request data
        /// <summary>
        /// Constructs a header collection to be initialized by
        /// calls to BeginParse(), Parse(), and Parse().
        /// </summary>
        /// <param name="isRequest"><c>true</c> to parse a HTTP request, false for a response.</param>
        public HttpHeaderCollection(bool isRequest)
        {
            this.headers   = null;
            this.isRequest = isRequest;
        }

        /// <summary>
        /// Constructs a HTTP request header collection.
        /// </summary>
        /// <param name="version">HTTP protocol version.</param>
        /// <param name="method">HTTP method (GET, PUT,...)</param>
        /// <param name="rawUri">Raw request URI.</param>
        public HttpHeaderCollection(Version version, string method, string rawUri)
        {
            this.headers   = new Hashtable();
            this.isRequest = true;
            this.version   = version;
            this.method    = method.ToUpper();
            this.rawUri    = rawUri;
        }

        /// <summary>
        /// Constructs a HTTP 1.1 request header collection.
        /// </summary>
        /// <param name="method">HTTP method (GET, PUT,...)</param>
        /// <param name="rawUri">Raw request URI.</param>
        public HttpHeaderCollection(string method, string rawUri)
        {
            this.headers   = new Hashtable();
            this.isRequest = true;
            this.version   = HttpStack.Http11;
            this.method    = method.ToUpper();
            this.rawUri    = rawUri;
        }

        /// <summary>
        /// Constructs a HTTP response header collection.
        /// </summary>
        /// <param name="version">HTTP protocol version.</param>
        /// <param name="status">HTTP response status code.</param>
        /// <param name="reason">Response reason phrase.</param>
        public HttpHeaderCollection(Version version, HttpStatus status, string reason)
        {
            this.headers   = new Hashtable();
            this.isRequest = true;
            this.version   = version;
            this.status    = status;
            this.reason    = reason;
        }

        /// <summary>
        /// Constructs a HTTP 1.1 response header collection.
        /// </summary>
        /// <param name="status">HTTP response status code.</param>
        /// <param name="reason">Response reason phrase.</param>
        public HttpHeaderCollection(HttpStatus status, string reason)
        {
            this.headers   = new Hashtable();
            this.isRequest = false;
            this.version   = HttpStack.Http11;
            this.status    = status;
            this.reason    = reason;
        }

        /// <summary>
        /// Returns <c>true</c> if these are request headers.
        /// </summary>
        internal bool IsRequest
        {
            get { return isRequest; }
        }

        /// <summary>
        /// Returns <c>true</c> if these are response headers.
        /// </summary>
        internal bool IsResponse
        {
            get { return !isRequest; }
        }

        /// <summary>
        /// The HTTP version.
        /// </summary>
        public Version HttpVersion
        {
            get { return version; }
            set { version = value; }
        }

        /// <summary>
        /// The HTTP request method (always in upper case).
        /// </summary>
        internal string Method
        {
            get { return method; }
            set { method = value; }
        }

        /// <summary>
        /// The raw request URI.
        /// </summary>
        internal string RawUri
        {
            get { return rawUri; }
            set { rawUri = value; }
        }

        /// <summary>
        /// The response status code.
        /// </summary>
        internal HttpStatus Status
        {
            get { return status; }
            set { status = value; }
        }

        /// <summary>
        /// The response reason phrase.
        /// </summary>
        internal string Reason
        {
            get { return reason; }
            set { reason = value; }
        }

        /// <summary>
        /// Returns a collection enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator()
        {
            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            return headers.Values.GetEnumerator();
        }

        /// <summary>
        /// Adds the header passed to the collection.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <remarks>
        /// <note>
        /// Multiple headers with the same name will be
        /// consolidated into a single header with a list of values.
        /// </note>
        /// </remarks>
        public void Add(HttpHeader header)
        {
            HttpHeader  found;
            string      key;

            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            key = header.Name.ToUpper();
            found = (HttpHeader)headers[key];
            if (found != null)
                found.Append(header.Value);
            else
                headers.Add(key, header);
        }

        /// <summary>
        /// Adds the header passed to the collection.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <remarks>
        /// <note>
        /// Multiple headers with the same name will be
        /// consolidated into a single header with a list of values.
        /// </note>
        /// </remarks>
        public void Add(string name, string value)
        {
            HttpHeader  found;
            string      key;

            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            key = name.ToUpper();
            found = (HttpHeader)headers[key];
            if (found != null)
                found.Append(value);
            else
                headers.Add(key, new HttpHeader(name, value));
        }

        /// <summary>
        /// Accesses the named header value.
        /// </summary>
        public string this[string key]
        {
            get
            {
                HttpHeader header;

                if (headers == null)
                    throw new InvalidOperationException(NotParsedMsg);

                header = (HttpHeader)headers[key.ToUpper()];
                if (header == null)
                    return null;

                return header.Value;
            }

            set
            {
                headers[key.ToUpper()] = new HttpHeader(key, value);
            }
        }

        /// <summary>
        /// Looks up request header and returns its value as a string
        /// if present.  If the header is not present then a default value
        /// will be returned.
        /// </summary>
        /// <param name="key">Header name.</param>
        /// <param name="def">Default value.</param>
        /// <returns>The header value if present or the default value.</returns>
        public string Get(string key, string def)
        {
            HttpHeader header;

            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            header = (HttpHeader)headers[key.ToUpper()];
            if (header != null)
                return header.Value;

            return def;
        }

        /// <summary>
        /// Looks up request header and returns its value as an integer
        /// if present.  If the header is not present or cannot be parsed
        /// then a default value will be returned.
        /// </summary>
        /// <param name="key">Header name.</param>
        /// <param name="def">Default value.</param>
        /// <returns>The header value if present or the default value.</returns>
        public int Get(string key, int def)
        {
            HttpHeader header;

            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            try
            {
                header = (HttpHeader)headers[key.ToUpper()];
                if (header != null)
                    return header.AsInt;
            }
            catch
            {
            }

            return def;
        }

        /// <summary>
        /// Looks up request header and returns its value as a date
        /// if present.  If the header is not present or cannot be parsed
        /// then a default value will be returned.
        /// </summary>
        /// <param name="key">Header name.</param>
        /// <param name="def">Default value.</param>
        /// <returns>The header value if present or the default value.</returns>
        public DateTime Get(string key, DateTime def)
        {
            HttpHeader header;

            if (headers == null)
                throw new InvalidOperationException(NotParsedMsg);

            try
            {
                header = (HttpHeader)headers[key.ToUpper()];
                if (header != null)
                    return header.AsDate;
            }
            catch
            {
            }

            return def;
        }

        /// <summary>
        /// Serializes the request/response line and headers into a binary 
        /// form suitiable for network transmission.
        /// </summary>
        /// <param name="blockSize">Size of the underlying blocks.</param>
        /// <returns>The request as a block array.</returns>
        internal BlockArray Serialize(int blockSize)
        {
            var bs     = new BlockStream(0, blockSize);
            var writer = new StreamWriter(bs);

            if (isRequest)
                writer.Write("{0} {1} HTTP/{2}.{3}\r\n", method, rawUri, version.Major, version.Major);
            else
                writer.Write("HTTP/{0}.{1} {2} {3}\r\n", version.Major, version.Major, (int)status, reason);

            foreach (HttpHeader header in headers.Values)
                writer.Write("{0}: {1}\r\n", header.Name, header.Value);

            writer.Write("\r\n");

            writer.Flush();

            return bs.ToBlocks(true);
        }

        /// <summary>
        /// Performs the necessary initialization before 
        /// data recieved on the network can be parsed.
        /// </summary>
        public void BeginParse()
        {
            if (headers != null)
                throw new InvalidOperationException("Headers already initialized.");

            if (blocks != null)
                throw new InvalidOperationException("Parsing already begun.");

            blocks = new BlockArray();
            dataPos = -1;
        }

        /// <summary>
        /// Adds the data passed to the information to be parsed as
        /// headers.
        /// </summary>
        /// <param name="data">The received data.</param>
        /// <param name="cb">Number of bytes received.</param>
        /// <returns><c>true</c> if a complete set of headers has been received.</returns>
        /// <remarks>
        /// <note>
        /// Ownership of the data buffer is passed to
        /// this instance.  The code receiving data from the network
        /// MUST allocate a new buffer and not reuse this one.
        /// </note>
        /// </remarks>
        public bool Parse(byte[] data, int cb)
        {
            if (blocks == null)
                throw new InvalidOperationException("Parsing not begun.");

            if (cb <= 0)
                return false;

            // Here's the deal: I'm looking for a 4 byte CR-LF-CR-LF sequence
            // to indicate the end of the HTTP headers.  I'm going to append
            // data received to the block array and then begin scanning for
            // this sequence.  If the new block begins with a CR or LF character
            // then I'll begin the scan 3 logical bytes before the new data
            // to catch CR-LF-CR-LF sequences that span blocks.

            int pos = blocks.Size;

            blocks.Append(new Block(data, 0, cb));

            if (data[0] == Helper.CR || data[0] == Helper.LF)
            {
                // I have to worry about the possibility that the CR-LF-CR-LF
                // sequence spans blocks.

                pos -= 3;
                if (pos < 0)
                    pos = 0;

                while (pos < blocks.Size - 3)
                {
                    // $todo(jeff.lill): 
                    //
                    // This lookup won't perform that well due to
                    // how BlockArray caches the last referenced 
                    // index.  Since it won't be that common for
                    // the header termination to cross data blocks
                    // I'm not going to worry about this too much
                    // right now.

                    if (blocks[pos + 0] == Helper.CR &&
                        blocks[pos + 1] == Helper.LF &&
                        blocks[pos + 2] == Helper.CR &&
                        blocks[pos + 3] == Helper.LF)
                    {
                        dataPos = pos + 4;
                        return true;
                    }

                    pos++;
                }
            }
            else
            {
                // For better performance, I can simply scan the data block
                // for the header termination.

                for (pos = 0; pos < cb - 3; pos++)
                {
                    if (data[pos + 0] == Helper.CR &&
                        data[pos + 1] == Helper.LF &&
                        data[pos + 2] == Helper.CR &&
                        data[pos + 3] == Helper.LF)
                    {
                        dataPos = blocks.Size - cb + pos + 4;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Completes parsing of the headers.
        /// </summary>
        /// <param name="dataPos">Returns as the logical index of the first byte of request/response data.</param>
        /// <returns>A block array with the network data received so far.</returns>
        /// <remarks>
        /// The block array return contains the raw network data received so
        /// far.  Much of this will be the request/response line and header data
        /// but it may also include some of the request/response data.  The logical
        /// index of the first byte of any data will be returned in dataPos.
        /// </remarks>
        /// <exception cref="HttpBadProtocolException">Badly formatted HTTP message.</exception>
        /// <exception cref="InvalidOperationException">The class methods are not being used properly.</exception>
        public BlockArray EndParse(out int dataPos)
        {
            if (blocks == null)
                throw new InvalidOperationException("Parsing not begun.");

            if (this.dataPos == -1)
                throw new InvalidOperationException("Parsing not completed.");

            try
            {
                // Parse the request/response line and headers.

                BlockStream     stream;
                StreamReader    reader;
                string          line;
                string          name, value, key;
                int             pos, posEnd;
                HttpHeader      header, lastHeader;

                stream = new BlockStream(blocks);
                stream.SetLength(this.dataPos, false);
                reader = new StreamReader(stream, Encoding.ASCII);

                line = reader.ReadLine();
                if (line == null)
                    throw new HttpBadProtocolException();

                if (isRequest)
                {
                    // Request-Line = Method SP Request-URI SP HTTP-Version CRLF

                    posEnd = line.IndexOf(' ');
                    if (posEnd == -1)
                        throw new HttpBadProtocolException();

                    method = line.Substring(0, posEnd).ToUpper();
                    if (method.Length == 0)
                        throw new HttpBadProtocolException();

                    pos    = posEnd + 1;
                    posEnd = line.IndexOf(' ', pos);
                    if (posEnd == -1)
                        throw new HttpBadProtocolException();

                    rawUri = line.Substring(pos, posEnd - pos);
                    if (rawUri.Length == 0)
                        throw new HttpBadProtocolException();

                    pos    = posEnd + 1;
                    posEnd = line.IndexOf('/', pos);
                    if (posEnd == -1 || line.Substring(pos, posEnd - pos).ToUpper() != "HTTP")
                        throw new HttpBadProtocolException();

                    version = new Version(line.Substring(posEnd + 1));
                }
                else
                {
                    // Status-Line = HTTP-Version SP Status-Code SP Reason-Phrase CRLF

                    posEnd = line.IndexOf('/');
                    if (posEnd == -1 || line.Substring(0, posEnd).ToUpper() != "HTTP")
                        throw new HttpBadProtocolException();

                    pos     = posEnd + 1;
                    posEnd  = line.IndexOf(' ', pos);
                    version = new Version(line.Substring(pos, posEnd - pos));

                    pos     = posEnd + 1;
                    posEnd  = line.IndexOf(' ', pos);
                    status  = (HttpStatus)int.Parse(line.Substring(pos, posEnd - pos));

                    reason  = line.Substring(posEnd + 1);
                }

                // Parse the headers.  Note that I'm going to convert
                // multiple headers with the same name into the equivalent
                // list form.

                headers    = new Hashtable();
                lastHeader = null;

                line = reader.ReadLine();
                while (line != null && line.Length > 0)
                {
                    if (Char.IsWhiteSpace(line[0]))
                    {
                        // Header continuation

                        if (lastHeader == null)
                            throw new HttpBadProtocolException();

                        lastHeader.AppendContinuation(line);
                    }
                    else
                    {
                        posEnd = line.IndexOf(':');
                        if (posEnd == -1)
                            throw new HttpBadProtocolException();

                        name  = line.Substring(0, posEnd).Trim();
                        value = line.Substring(posEnd + 1).Trim();
                        key   = name.ToUpper();

                        header = (HttpHeader)headers[key];
                        if (header != null)
                            header.Append(value);
                        else
                        {

                            header = new HttpHeader(name, value);
                            headers.Add(key, header);
                            lastHeader = header;
                        }
                    }

                    line = reader.ReadLine();
                }

                // We're done, so return the data position and blocks.

                dataPos = this.dataPos;
                return blocks;
            }
            finally
            {
                blocks = null;  // Release this reference to help the GC
            }
        }
    }
}
