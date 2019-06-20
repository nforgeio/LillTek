//-----------------------------------------------------------------------------
// FILE:        ExpiredTicketException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an efficient array of boolean values that can also
//              perform bit oriented operations such as AND, OR, NOT, XOR.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;

namespace LillTek.Common
{
    /// <summary>
    /// Indicates that a service call failed due to the fact that the
    /// the service ticket has expired.
    /// </summary>
    /// <remarks>
    /// Client applications will typically try to reauthenticate against
    /// the service to obtain a new ticket when this happens.
    /// </remarks>
    public sealed class ExpiredTicketException : SecurityException
    {
        /// <summary>
        /// Constructs an exception with a generic message.
        /// </summary>
        public ExpiredTicketException()
            : base("Access denied due to service ticket expiration.")
        {
        }

        /// <summary>
        /// Constructs an exception with a specific message.
        /// </summary>
        /// <param name="message"></param>
        public ExpiredTicketException(string message)
            : base(message)
        {
        }
    }
}
