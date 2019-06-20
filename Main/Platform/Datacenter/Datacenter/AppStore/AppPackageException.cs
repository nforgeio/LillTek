//-----------------------------------------------------------------------------
// FILE:        AppPackageException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used for errors relating to managing application packages.

using System;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Used for errors relating to managing application packages.
    /// </summary>
    public sealed class AppPackageException : ApplicationException
    {
        /// <summary>
        /// Constructs a remote service control related exception with
        /// a detailed message srting.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public AppPackageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a remote service control rtelated exception with
        /// a formatted detailed message.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message formatting arguments.</param>
        public AppPackageException(string format, params object[] args)
            : base(string.Format(null, format, args))
        {
        }
    }
}
