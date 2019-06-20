//-----------------------------------------------------------------------------
// FILE:        AuthenticationException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Used to signal errors encountered during authentication related
//              activities.

using System;
using System.Collections.Generic;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Used to signal errors encountered during authentication related
    /// activities.
    /// </summary>
    public sealed class AuthenticationException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public AuthenticationException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerException">The inner exception.</param>
        public AuthenticationException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public AuthenticationException(Exception innerException, string format, params object[] args)
            : base(string.Format(format, args), innerException)
        {
        }
    }
}
