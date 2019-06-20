//-----------------------------------------------------------------------------
// FILE:        CancelException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown when an operation has been cancelled.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown when an operation has been cancelled.
    /// </summary>
    public sealed class CancelException : ApplicationException
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CancelException()
            : base("Operation cancelled.")
        {
        }

        /// <summary>
        /// Constructs the exception with a message string.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public CancelException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs the exception with a formatted message string.
        /// </summary>
        /// <param name="format">The exception message format string.</param>
        /// <param name="args">The message arguments</param>
        public CancelException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
