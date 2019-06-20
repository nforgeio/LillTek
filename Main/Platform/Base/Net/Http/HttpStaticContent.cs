//-----------------------------------------------------------------------------
// FILE:        HttpStaticContent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes static content that can be served by an application
//              using a HttpListener.

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Text;

using LillTek.Common;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Describes static content that can be served by an application using a <see cref="HttpListener" />.
    /// </summary>
    /// <remarks>
    /// This class is included in the <b>LillTek.Net.Http</b> assembly for the use by
    /// applications that needs to serve static content from a <see cref="HttpListener" />.
    /// </remarks>
    public class HttpStaticContent
    {
        /// <summary>
        /// The virtual relative path to this for this content.
        /// </summary>
        public string VirtualPath { get; private set; }

        /// <summary>
        /// The MIME type for the content.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// The content data.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Constucts an instance from binary data.
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <param name="contentType"></param>
        /// <param name="data"></param>
        public HttpStaticContent(string virtualPath, string contentType, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(virtualPath))
                throw new ArgumentException("Cannot be empty.", "virtualPath");

            if (string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("Cannot be empty.", "contentType");

            if (data == null)
                throw new ArgumentException("data");

            this.VirtualPath = virtualPath;
            this.ContentType = contentType;
            this.Data        = data;
        }

        /// <summary>
        /// Constructs an instance from string data.k
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <param name="contentType"></param>
        /// <param name="data"></param>
        public HttpStaticContent(string virtualPath, string contentType, string data)
        {
            if (string.IsNullOrWhiteSpace(virtualPath))
                throw new ArgumentException("Cannot be empty.", "virtualPath");

            if (string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("Cannot be empty.", "contentType");

            if (data == null)
                throw new ArgumentException("data");

            this.VirtualPath = virtualPath;
            this.ContentType = contentType;
            this.Data        = Helper.ToUTF8(data);
        }
    }
}
