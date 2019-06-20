//-----------------------------------------------------------------------------
// FILE:        HttpContentParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the parsing of an HTTP request or response's
//              content stream.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

// $hack(jeff.lill): 
//
// I'm not sure if this is actually a hack or not but I'm assuming
// that the chunked transfer encoding size line and footers are
// encoded as single byte ANSI characters.

namespace LillTek.Net.Http
{
    /// <summary>
    /// Handles the parsing of an HTTP request or response's content stream.
    /// </summary>
    internal sealed class HttpContentParser
    {
        private HttpHeaderCollection    headers;        // Request/response headers
        private BlockArray              content;        // Gathered content
        private int                     cbContent;      // -1 indicates that we're waiting for a socket close
        private int                     cbContentMax;   // Maximum content size allowed (or -1)
        private bool                    isRequest;      // True if this is a request
        private bool                    isChunked;      // True for chunked transfer encoding

        // Chunked transfer encoding state

        private enum ChunkState
        {
            Start,      // Initiating parsing of a chunk
            Size,       // Parsing the chunk size line
            Data,       // Parsing the chunk data
            DataCR,     // Parsing the CR after chunk data
            DataLF,     // Parsing the LF after chunk data
            Footers     // Parsing the optional footers
        }

        private ChunkState  chunkState;     // Current chunk parsing state
        private string      sizeLine;       // The chunk size line
        private int         cbChunk;        // Current chunk size in bytes
        private int         cbChunkRead;    // Number of bytes of chunk data read so far

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="headers">The request/response headers.</param>
        /// <param name="cbContentMax">Maximum allowed content size or <b>-1</b> to disable checking.</param>
        public HttpContentParser(HttpHeaderCollection headers, int cbContentMax)
        {
            this.headers      = headers;
            this.content      = null;
            this.cbContentMax = cbContentMax;
        }

        /// <summary>
        /// Parses the data block passed as chunked data.
        /// </summary>
        /// <param name="received">The request/response data received so far.</param>
        /// <param name="pos">Position of the first byte of content data after the headers.</param>
        /// <returns><c>true</c> if the data has been completely parsed.</returns>
        /// <exception cref="HttpBadProtocolException">Badly formatted HTTP message.</exception>
        /// <exception cref="HttpContentSizeException">The content size exceeds the maximum allowed.</exception>
        private bool ChunkParser(BlockArray received, int pos)
        {
            int cbRecv = received.Size;

            // $hack(jeff.lill):
            //
            // The string appending is probably not the most efficient way to
            // implement this but it's a lot easier and probably less bug prone.
            // Most chunked transfer text lines will be very small anyway in
            // real life so this isn't likely to be a big performance problem.

            while (pos < cbRecv)
            {
                switch (chunkState)
                {
                    case ChunkState.Start:

                        content = new BlockArray();
                        sizeLine = string.Empty;
                        chunkState = ChunkState.Size;
                        break;

                    case ChunkState.Size:

                        // Append characters until the string is terminated with CRLF.

                        sizeLine += (char)received[pos++];
                        if (sizeLine.EndsWith(Helper.CRLF))
                        {
                            cbChunk = -1;
                            for (int i = 0; i < sizeLine.Length; i++)
                            {
                                char ch = sizeLine[i];
                                int digit;

                                if ('0' <= ch && ch <= '9')
                                    digit = ch - '0';
                                else if ('a' <= ch && ch <= 'f')
                                    digit = ch - 'a' + 10;
                                else if ('A' <= ch && ch <= 'F')
                                    digit = ch - 'A' + 10;
                                else
                                    break;

                                if (cbChunk == -1)
                                    cbChunk = 0;
                                else
                                    cbChunk <<= 4;

                                cbChunk += digit;
                            }

                            if (cbChunk == -1)
                                throw new HttpBadProtocolException("Invalid chunked transfer size line.");

                            if (cbChunk == 0)
                                chunkState = ChunkState.Footers;
                            else
                            {
                                cbChunkRead = 0;
                                chunkState = ChunkState.Data;
                            }
                        }
                        else if (sizeLine.Length > HttpHeaderCollection.MaxHeaderChars)
                            throw new HttpBadProtocolException("Chunked transfer size line is too long.");

                        break;

                    case ChunkState.Data:

                        int cbRemain = received.Size - pos;
                        int cb;

                        cb = cbChunk - cbChunkRead;
                        if (cb > cbRemain)
                            cb = cbRemain;

                        content.Append(received.Extract(pos, cb));

                        pos         += cb;
                        cbChunkRead += cb;

                        if (cbChunk == cbChunkRead)
                            chunkState = ChunkState.DataCR;

                        break;

                    case ChunkState.DataCR:

                        if (received[pos++] != Helper.CR)
                            throw new HttpBadProtocolException("CRLF expected after chunk data.");

                        chunkState = ChunkState.DataLF;
                        break;

                    case ChunkState.DataLF:

                        if (received[pos++] != Helper.LF)
                            throw new HttpBadProtocolException("CRLF expected after chunk data.");

                        chunkState = ChunkState.Size;
                        sizeLine = string.Empty;
                        break;

                    case ChunkState.Footers:

                        // $todo(jeff.lill): 
                        //
                        // I'm not going to worry about parsing chunked transfer
                        // footers right now.  All I'm going to do is continue
                        // accumulating characters into sizeLine until I
                        // see a CRLF CRLF sequence terminating the footers.

                        sizeLine += (char)received[pos++];
                        if (sizeLine.EndsWith(Helper.CRLF))
                            return true;

                        break;
                }
            }

            if (cbContentMax != -1 && content != null && content.Size >= cbContentMax)
                throw new HttpContentSizeException();

            return false;
        }

        /// <summary>
        /// Initiates content parsing.
        /// </summary>
        /// <param name="received">The request/response data received so far.</param>
        /// <param name="dataPos">Position of the first byte of content data after the headers.</param>
        /// <returns><c>true</c> if the data has been completely parsed.</returns>
        /// <remarks>
        /// <para>
        /// This method will return true indicating that all of the content has been parsed
        /// if the content was included in the blocks received while parsing the headers.
        /// </para>
        /// <note>
        /// The block array passed should not be used again
        /// after making this call.  Ownership should be considered to
        /// have been passed to this instance.
        /// </note>
        /// </remarks>
        /// <exception cref="HttpBadProtocolException">Badly formatted HTTP message.</exception>
        /// <exception cref="HttpContentSizeException">The content size exceeds the maximum allowed.</exception>
        public bool BeginParse(BlockArray received, int dataPos)
        {
            isChunked = String.Compare(headers.Get("Transfer-Encoding", "identity"), "chunked", true) == 0;
            if (isChunked)
            {
                cbContent = 0;
                chunkState = ChunkState.Start;
                return ChunkParser(received, dataPos);
            }

            content   = received.Extract(dataPos);
            isRequest = headers.IsRequest;

            // Get the content length.  If this is not present and this is an
            // HTTP request then assume that there is no content.  For responses,
            // we're going to assume that the server will close its side of
            // the socket to signal the end of the content.

            cbContent = headers.Get("Content-Length", -1);
            if (cbContent < 0)
            {
                if (isRequest)
                {
                    cbContent = 0;
                    return true;
                }

                cbContent = -1;
            }

            // Return true if we already have all of the content.

            return cbContent != -1 && content.Size >= cbContent;
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
        public bool Parse(byte[] data, int cb)
        {
            if (isChunked)
            {
                // We'll get a zero length block when the other side of the
                // socket is closed.  This should never happen for chunked
                // transfers.

                if (cb == 0)
                    throw new HttpBadProtocolException("Client closed socket.");

                return ChunkParser(new BlockArray(new Block(data, 0, cb)), 0);
            }

            if (cb == 0)
            {
                // We'll get a zero length block when the other side of the
                // socket is closed.  This is a protocol error for requests.
                // For responses this indicates the end of the content data.

                if (isRequest)
                    throw new HttpBadProtocolException("Client closed socket.");

                if (cbContent != -1 && cbContent != content.Size)
                {

                    // We have a protocol error if there was a Content-Length 
                    // header and its value doesn't match the content actually
                    // gathered.

                    throw new HttpBadProtocolException("Content-Length mismatch.");
                }

                return true;
            }

            content.Append(new Block(data, 0, cb));
            if (cbContent != -1 && content.Size > cbContent)
                throw new HttpBadProtocolException();    // We got more than Content-Length data

            if (cbContentMax != -1 && content != null && content.Size >= cbContentMax)
                throw new HttpContentSizeException();

            return content.Size == cbContent;
        }

        /// <summary>
        /// Completes the parsing of the content by returning the parsed
        /// content as a BlockArray.
        /// </summary>
        /// <returns>The content data.</returns>
        public BlockArray EndParse()
        {
            try
            {
                return content;
            }
            finally
            {
                content = null;     // Release this reference to help to GC.
            }
        }
    }
}
