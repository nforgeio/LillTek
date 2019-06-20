//-----------------------------------------------------------------------------
// FILE:        PackageException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exceptions thrown from the Package related classes.

using System;
using System.IO;

namespace LillTek.Install
{
    /// <summary>
    /// Exceptions thrown from the Package related classes.
    /// </summary>
    public sealed class PackageException : ApplicationException
    {
        /// <summary>
        /// Constructs an exception from a message string.
        /// </summary>
        /// <param name="message">The message string.</param>
        internal PackageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs an exception by formatting a message string.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        internal PackageException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Constructs an exception from a message string and a reference
        /// to an inner exception.
        /// </summary>
        /// <param name="inner">The inner exception.</param>
        /// <param name="message">The message string.</param>
        internal PackageException(Exception inner, string message)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Constructs an exception by formatting a message string and
        /// associating an inner exception.
        /// </summary>
        /// <param name="inner">The inner exception.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        internal PackageException(Exception inner, string format, params object[] args)
            : base(string.Format(format, args), inner)
        {
        }
    }
}
