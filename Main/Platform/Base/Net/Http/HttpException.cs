//-----------------------------------------------------------------------------
// FILE:        HttpException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception used to signal a HTTP error.

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
    public class HttpException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public HttpException()
            : base("HTTP Error.")
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="status">The <see cref="HttpStatus" /> code.</param>
        public HttpException(HttpStatus status)
            : base(string.Format("HTTP Error [{0}={1}].", HttpStack.GetReasonPhrase(status), (int)status))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        public HttpException(string message)
            : base(message)
        {
        }
    }
}
