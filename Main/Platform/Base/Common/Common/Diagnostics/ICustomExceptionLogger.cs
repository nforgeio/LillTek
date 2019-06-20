//-----------------------------------------------------------------------------
// FILE:        ICustomExceptionLogger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the Log() method that provides a mechanism for
//              exception classes to customize the information written to
//              an event log.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Implements the Log() method that provides a mechanism for exception 
    /// classes to customize the information written to an event log.
    /// </summary>
    public interface ICustomExceptionLogger
    {
        /// <summary>
        /// Writes custom information about the exception to the string builder
        /// instance passed which will eventually be written to the event log.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        /// <remarks>
        /// Implementations of this method will typically write the exception's
        /// stack trace out to the string builder before writing out any custom
        /// information.
        /// </remarks>
        void Log(StringBuilder sb);
    }
}
