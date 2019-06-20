//-----------------------------------------------------------------------------
// FILE:        NotAvailableException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown when a service is not currently available.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown when a service is not currently available.
    /// </summary>
    public sealed class NotAvailableException : ApplicationException
    {
        /// <summary>
        /// Constructs the exception.
        /// </summary>
        public NotAvailableException()
            : base("Service is not available.")
        {
        }

        /// <summary>
        /// Constructs the exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public NotAvailableException(string message)
            : base(message)
        {
        }
    }
}
