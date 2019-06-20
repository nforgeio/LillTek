//-----------------------------------------------------------------------------
// FILE:        ConfigFormatException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown by Config when a configuration file formatting
//              error is encountered.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown by <see cref="Config" /> when an error is encountered while
    /// parsing the configuration text.
    /// </summary>
    public sealed class ConfigFormatException : Exception, ICustomExceptionLogger
    {

        private int     lineNum;
        private string  text;

        /// <summary>
        /// Constructs the exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="lineNum">Number of the parsed line with the error.</param>
        /// <param name="text">Text of the error line.</param>
        public ConfigFormatException(string message, int lineNum, string text)
            : base(message)
        {
            this.lineNum = lineNum;
            this.text    = text;
        }

        /// <summary>
        /// Writes custom information about the exception to the string builder
        /// instance passed which will eventually be writtent to the event log.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        /// <remarks>
        /// Implementations of this method will typically write the exception's
        /// stack trace out to the string builder before writing out any custom
        /// information.
        /// </remarks>
        public void Log(StringBuilder sb)
        {
            sb.Append(this.StackTrace);
            sb.AppendFormat("[line:{0}] ", lineNum);
            sb.Append(text);
        }
    }
}
