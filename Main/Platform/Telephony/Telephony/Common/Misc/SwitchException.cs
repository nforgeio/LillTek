//-----------------------------------------------------------------------------
// FILE:        SwitchException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Describes a NeonSwitch related error condition.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes a telephony related error condition.
    /// </summary>
    public class SwitchException : Exception
    {
        /// <summary>
        /// Constructs an exception with message text.
        /// </summary>
        /// <param name="message">The message text.</param>
        public SwitchException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs an exception with formatted message text.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public SwitchException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Constructs an exception with a message and wrapping another exception.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="innerException">The inner exception.</param>
        public SwitchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructs an exception by wrapping another exception and generating a message
        /// from the wrapped exceptions type and message.
        /// </summary>
        /// <param name="innerException">The inner exception.</param>
        public SwitchException(Exception innerException)
            : base(string.Format("[{0}]: {1}", innerException.GetType().Name, innerException.Message), innerException)
        {
        }
    }
}
