//-----------------------------------------------------------------------------
// FILE:        HttpContentSizeException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception used to signal that received HTTP content is too large.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Exception used to signal that received HTTP content is too large.
    /// </summary>
    public sealed class HttpContentSizeException : HttpException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public HttpContentSizeException()
            : base("Content size exceeds maximum allowed.")
        {
        }
    }
}
