//-----------------------------------------------------------------------------
// FILE:        IAck.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface for an acknowledgement message that
//              includes an optional Exception property.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// All acknowledgement messages that want the session manager
    /// to automatically deserialize and throw exceptions received from
    /// the server should implement this interface.
    /// </summary>
    public interface IAck
    {
        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        string Exception { get; set; }

        /// <summary>
        /// The fully qualified name of the exception type.
        /// </summary>
        string ExceptionTypeName { get; set; }
    }
}
