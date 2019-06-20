//-----------------------------------------------------------------------------
// FILE:        ServiceException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes service related errors.

using System;

namespace LillTek.Service
{
    /// <summary>
    /// Describes service related errors.
    /// </summary>
    public sealed class ServiceException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ServiceException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The error message format string.</param>
        /// <param name="args">The error message arguments.</param>
        public ServiceException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServiceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
