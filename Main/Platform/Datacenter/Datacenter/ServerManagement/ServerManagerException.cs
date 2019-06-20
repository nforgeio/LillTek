//-----------------------------------------------------------------------------
// FILE:        ServerManagerException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a server manager exception.

using System;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Used to signal errors from the server manager classes.
    /// </summary>
    public sealed class ServerManagerException : ApplicationException
    {
        /// <summary>
        /// Constructs a remote service control related exception with
        /// a detailed message srting.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public ServerManagerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a remote service control rtelated exception with
        /// a formatted detailed message.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message formatting arguments.</param>
        public ServerManagerException(string format, params object[] args)
            : base(string.Format(null, format, args))
        {
        }
    }
}
