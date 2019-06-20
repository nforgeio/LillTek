//-----------------------------------------------------------------------------
// FILE:        JsonException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a JSON error.

using System;
using System.IO;
using System.Text;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Json
{
    /// <summary>
    /// Describes a JSON error.
    /// </summary>
    public class JsonException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        public JsonException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Comstructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public JsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
