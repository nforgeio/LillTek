//-----------------------------------------------------------------------------
// FILE:        SipStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the known SIP status codes.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes the known SIP status codes.
    /// </summary>
    /// <remarks>
    /// Note that this type defines LillTek SIP stack error codes with values less than zero
    /// and whose names begin with <b>Stack</b>.  These codes are used to communicate errors
    /// detected within the stack and should never be submitted within SIP responses delivered
    /// to a remote endpoint.
    /// </remarks>
    public enum SipStatus : int
    {
        //---------------------------------------------------------------------
        // Stack error codes

        /// <summary>
        /// The status has not been set.
        /// </summary>
        Stack_Unknown = -1,

        /// <summary>
        /// The SIP stack detected a protocol error with a remote endpoint.
        /// </summary>
        Stack_ProtocolError = -2,

        /// <summary>
        /// The SIP stack cannot find a transport suitable for delivering a message.
        /// </summary>
        Stack_NoAvailableTransport = -3,

        /// <summary>
        /// The SIP stack timed-out.
        /// </summary>
        Stack_Timeout = -4,

        //---------------------------------------------------------------------
        // Standard error codes

        /// <summary>
        /// Trying
        /// </summary>
        Trying = 100,

        /// <summary>
        /// Ringing
        /// </summary>
        Ringing = 180,

        /// <summary>
        /// Call Is Being Forwarded
        /// </summary>
        Forwarding = 181,

        /// <summary>
        /// Queued
        /// </summary>
        Queued = 182,

        /// <summary>
        /// Session Progress
        /// </summary>
        SessionProgress = 183,

        /// <summary>
        /// OK
        /// </summary>
        OK = 200,

        /// <summary>
        /// Multiple Choices
        /// </summary>
        MultipleChoices = 300,

        /// <summary>
        /// Moved Permanently
        /// </summary>
        MovedPermanently = 301,

        /// <summary>
        /// Moved Temporarily
        /// </summary>
        MovedTemporarily = 302,

        /// <summary>
        /// Use Proxy
        /// </summary>
        UseProxy = 305,

        /// <summary>
        /// Alternative Service
        /// </summary>
        AlternateService = 380,

        /// <summary>
        /// Bad Request
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Unauthorized
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// Payment Required
        /// </summary>
        PaymentRequired = 402,

        /// <summary>
        /// Forbidden
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// Not Found
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Method Not Allowed
        /// </summary>
        MethodNotAllowed = 405,

        /// <summary>
        /// Not Acceptable
        /// </summary>
        NotAcceptable = 406,

        /// <summary>
        /// Proxy Authentication Required
        /// </summary>
        ProxyAuthenticationRequired = 407,

        /// <summary>
        /// Request Timeout
        /// </summary>
        RequestTimeout = 408,

        /// <summary>
        /// Gone
        /// </summary>
        Gone = 410,

        /// <summary>
        /// Request Entity Too Large
        /// </summary>
        RequestEntityTooLarge = 413,

        /// <summary>
        /// Request-URI Too Long
        /// </summary>
        RequestUriTooLong = 414,

        /// <summary>
        /// Unsupported Media Type
        /// </summary>
        UnsupportedMediaType = 415,

        /// <summary>
        /// Unsupported URI Scheme
        /// </summary>
        UnsupportedUriScheme = 416,

        /// <summary>
        /// Bad Extension
        /// </summary>
        BadExtension = 420,

        /// <summary>
        /// Extension Required
        /// </summary>
        ExtensionRequired = 421,

        /// <summary>
        /// Interval Too Brief
        /// </summary>
        IntervalTooBrief = 423,

        /// <summary>
        /// Temporarily Unavailable
        /// </summary>
        TemporarilyUnavailble = 480,

        /// <summary>
        /// Call/Transaction Does Not Exist
        /// </summary>
        TransactionDoesNotExist = 481,

        /// <summary>
        /// Loop Detected
        /// </summary>
        LoopDetected = 482,

        /// <summary>
        /// Too Many Hops
        /// </summary>
        TooManyHops = 483,

        /// <summary>
        /// Address Incomplete
        /// </summary>
        AddressIncomplete = 484,

        /// <summary>
        /// Ambiguous
        /// </summary>
        Ambiguous = 485,

        /// <summary>
        /// Busy Here
        /// </summary>
        BusyHere = 486,

        /// <summary>
        /// Request Terminated
        /// </summary>
        RequestTerminated = 487,

        /// <summary>
        /// Not Acceptable Here
        /// </summary>
        NotAcceptableHere = 488,

        /// <summary>
        /// Request Pending
        /// </summary>
        RequestPending = 491,

        /// <summary>
        /// Undecipherable
        /// </summary>
        Undecipherable = 493,

        /// <summary>
        /// Server Internal Error
        /// </summary>
        ServerError = 500,

        /// <summary>
        /// Not Implemented
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// Bad Gateway
        /// </summary>
        BadGateway = 502,

        /// <summary>
        /// Service Unavailable
        /// </summary>
        ServiceUnavailable = 503,

        /// <summary>
        /// Server Timeout
        /// </summary>
        ServerTimeout = 504,

        /// <summary>
        /// Version Not Supported
        /// </summary>
        VersionNotSupported = 505,

        /// <summary>
        /// Message Too Large
        /// </summary>
        MessageTooLarge = 513,

        /// <summary>
        /// Busy Everywhere
        /// </summary>
        BusyEverwhere = 600,

        /// <summary>
        /// Decline
        /// </summary>
        Decline = 603,

        /// <summary>
        /// Does Not Exist Anywhere
        /// </summary>
        DoesNotExistAnywhere = 604,

        /// <summary>
        /// Not Acceptable
        /// </summary>
        NotAcceptableAnywhere = 606,
    }
}
