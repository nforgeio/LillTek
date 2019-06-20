//-----------------------------------------------------------------------------
// FILE:        SipTransportException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The exception thrown to indicate SIP transport related errors.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Telephony.Sip {

    /// <summary>
    /// The exception thrown to indicate SIP transport related errors.
    /// </summary>
    public class SipTransportException : SipException {

        /// <summary>
        /// Enumerates the possible transport errors thrown.
        /// </summary>
        public enum ErrorType {

            /// <summary>
            /// The message transmission timed out.
            /// </summary>
            Timeout,

            /// <summary>
            /// The remote endpoint did not accept the message.
            /// </summary>
            Rejected
        }

        /// <summary>
        /// Indicates the type of transport error encountered.
        /// </summary>
        public readonly ErrorType Error;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="error">The type of transport error.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public SipTransportException(ErrorType error,string message,Exception innerException)
            : base(message,innerException) {

            this.Error = error;
        }
    }
}
