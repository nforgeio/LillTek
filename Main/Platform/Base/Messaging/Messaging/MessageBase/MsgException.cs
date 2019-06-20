//-----------------------------------------------------------------------------
// FILE:        MsgException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the MsgException class

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Used for signaling problems with messaging related methods.
    /// </summary>
    public sealed class MsgException : ApplicationException
    {
        /// <summary>
        /// Constructs a messaging related exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public MsgException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructs a messaging related exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public MsgException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a messaging related exception.
        /// </summary>
        /// <param name="format">The exception text format string.</param>
        /// <param name="args">The formatting arguments.</param>
        public MsgException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
