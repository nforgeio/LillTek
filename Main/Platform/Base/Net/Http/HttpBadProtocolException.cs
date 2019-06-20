//-----------------------------------------------------------------------------
// FILE:        HttpBadProtocolException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception used to signal a bad request or response.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Exception used to signal a bad request or response.
    /// </summary>
    public sealed class HttpBadProtocolException : HttpException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public HttpBadProtocolException()
            : base("Badly formatted HTTP message.")
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        public HttpBadProtocolException(string message)
            : base(message)
        {
        }
    }
}
