//-----------------------------------------------------------------------------
// FILE:        InstallException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an installation related error.

using System;

namespace LillTek.Install
{
    /// <summary>
    /// Describes an installation related error.
    /// </summary>
    public sealed class InstallException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public InstallException()
            : base("Install error")
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public InstallException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
