//-----------------------------------------------------------------------------
// FILE:        VersionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown when an operation cannot be performed due 
//              to a version incompatibility.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown when an operation cannot be performed due to a version incompatibility.
    /// </summary>
    public sealed class VersionException : ApplicationException
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public VersionException()
            : base("Version incompatibility.")
        {
        }

        /// <summary>
        /// Constructs the exception with a message string. 
        /// </summary>
        /// <param name="message">The exception message.</param>
        public VersionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs the exception with a formatted message string.
        /// </summary>
        /// <param name="format">The exception message format string.</param>
        /// <param name="args">The message arguments</param>
        public VersionException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
