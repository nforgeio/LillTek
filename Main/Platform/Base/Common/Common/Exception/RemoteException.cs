//-----------------------------------------------------------------------------
// FILE:        RemoteException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to signal an error returned by a remote web service.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Used to signal an error returned by a web service.
    /// </summary>
    public class RemoteException : Exception
    {

        /// <summary>
        /// Constructs an exception with the message: <b>Internal Error</b>.
        /// </summary>
        public RemoteException()
            : base("Internal Error")
        {
        }

        /// <summary>
        /// Constructs and exception with the message passed.
        /// </summary>
        /// <param name="message">The error message.</param>
        public RemoteException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs an exception with a formatted message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public RemoteException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}
