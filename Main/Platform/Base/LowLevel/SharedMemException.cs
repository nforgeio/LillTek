//-----------------------------------------------------------------------------
// FILE:        SharedMemException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown for shared memory related exceptions.

using System;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Thrown for shared memory related exceptions.
    /// </summary>
    public class SharedMemException : Exception
    {
        /// <summary>
        /// Constucts an exception with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public SharedMemException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constucts an exception with a formatted message.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The format arguments.</param>
        public SharedMemException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
