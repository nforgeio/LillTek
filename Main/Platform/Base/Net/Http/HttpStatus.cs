//-----------------------------------------------------------------------------
// FILE:        HttpStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The HTTP status codes.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// The HTTP status codes.
    /// </summary>
    public enum HttpStatus
    {
        /// <summary>
        /// The client SHOULD continue with its request. 
        /// </summary>
        Continue = 100,

        /// <summary>
        /// The server understands and is willing to comply with the client's
        /// request, via the Upgrade message header field (section 14.42), for a
        /// change in the application protocol being used on this connection.
        /// </summary>
        SwitchingProtocols = 101,

        /// <summary>
        /// This class of status code indicates that the client's request was
        /// successfully received, understood, and accepted.
        /// </summary>
        OK = 200,

        /// <summary>
        /// The request has been fulfilled and resulted in a new resource being
        /// created. 
        /// </summary>
        Created = 201,

        /// <summary>
        /// The request has been accepted for processing, but the processing has
        /// not been completed.  
        /// </summary>
        Accepted = 202,

        /// <summary>
        /// The returned metainformation in the entity-header is not the
        /// definitive set as available from the origin server, but is gathered
        /// from a local or a third-party copy. 
        /// </summary>
        NonAuthoritativeInformation = 203,

        /// <summary>
        /// The server has fulfilled the request but does not need to return an
        /// entity-body, and might want to return updated metainformation. 
        /// </summary>
        NoContent = 204,

        /// <summary>
        /// The server has fulfilled the request and the user agent SHOULD reset
        /// the document view which caused the request to be sent. 
        /// </summary>
        ResetContent = 205,

        /// <summary>
        /// The server has fulfilled the partial GET request for the resource.
        /// </summary>
        PartialContent = 206,

        /// <summary>
        /// The requested resource corresponds to any one of a set of
        /// representations, each with its own specific location, and agent-
        /// driven negotiation information (section 12) is being provided so that
        /// the user (or user agent) can select a preferred representation and
        /// redirect its request to that location.
        /// </summary>
        MultipleChoices = 300,

        /// <summary>
        /// The requested resource has been assigned a new permanent URI and any
        /// future references to this resource SHOULD use one of the returned
        /// URIs.  
        /// </summary>
        MovedPermanently = 301,

        /// <summary>
        /// The requested resource resides temporarily under a different URI.
        /// </summary>
        Found = 302,

        /// <summary>
        /// The response to the request can be found under a different URI and
        /// SHOULD be retrieved using a GET method on that resource. 
        /// </summary>
        SeeOther = 303,

        /// <summary>
        /// If the client has performed a conditional GET request and access is
        /// allowed, but the document has not been modified, the server SHOULD
        /// respond with this status code. 
        /// </summary>
        NotModified = 304,

        /// <summary>
        /// The requested resource MUST be accessed through the proxy given by
        /// the Location field. 
        /// </summary>
        UseProxy = 305,

        /// <summary>
        /// The 306 status code was used in a previous version of the
        /// specification, is no longer used, and the code is reserved.
        /// </summary>
        Unused = 306,

        /// <summary>
        /// The requested resource resides temporarily under a different URI.
        /// Since the redirection MAY be altered on occasion, the client SHOULD
        /// continue to use the Request-URI for future requests.  
        /// </summary>
        TemporaryRedirect = 307,

        /// <summary>
        /// The request could not be understood by the server due to malformed
        /// syntax. 
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// The request requires user authentication. 
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// This code is reserved for future use.
        /// </summary>
        PaymentRequired = 402,

        /// <summary>
        /// The server understood the request, but is refusing to fulfill it.
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// The server has not found anything matching the Request-URI. 
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// The method specified in the Request-Line is not allowed for the
        /// resource identified by the Request-URI. 
        /// </summary>
        MethodNotAllowed = 405,

        /// <summary>
        /// The resource identified by the request is only capable of generating
        /// response entities which have content characteristics not acceptable
        /// according to the accept headers sent in the request.
        /// </summary>
        NotAcceptable = 406,

        /// <summary>
        /// Proxy Authentication Required.
        /// </summary>
        ProxyAuthenticationRequired = 407,

        /// <summary>
        /// The client did not produce a request within the time that the server
        /// was prepared to wait. 
        /// </summary>
        RequestTimeout = 408,

        /// <summary>
        /// The request could not be completed due to a conflict with the current
        /// state of the resource. 
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// The requested resource is no longer available at the server and no
        /// forwarding address is known. 
        /// </summary>
        Gone = 410,

        /// <summary>
        /// The server refuses to accept the request without a defined Content-
        /// Length. 
        /// </summary>
        LengthRequired = 411,

        /// <summary>
        /// The precondition given in one or more of the request-header fields
        /// evaluated to false when it was tested on the server. 
        /// </summary>
        PreconditionFailed = 412,

        /// <summary>
        /// The server is refusing to process a request because the request
        /// entity is larger than the server is willing or able to process. 
        /// </summary>
        RequestEntityTooLarge = 413,

        /// <summary>
        /// The server is refusing to service the request because the Request-URI
        /// is longer than the server is willing to interpret. 
        /// </summary>
        RequestURITooLong = 414,
        /// <summary>
        /// The server is refusing to service the request because the entity of
        /// the request is in a format not supported by the requested resource
        /// for the requested method.
        /// </summary>
        UnsupportedMediaType = 415,

        /// <summary>
        /// Requested range not satisfiable.
        /// </summary>
        RequestedRangeNotSatisfiable = 416,

        /// <summary>
        /// The expectation given in an Expect request-header field  could not be met by this server, or, if the server is a proxy,
        /// the server has unambiguous evidence that the request could not be met
        /// by the next-hop server.
        /// </summary>
        ExpectationFailed = 417,

        /// <summary>
        /// The server encountered an unexpected condition which prevented it
        /// from fulfilling the request.
        /// </summary>
        InternalServerError = 500,

        /// <summary>
        /// The server does not support the functionality required to fulfill the
        /// request. 
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// The server, while acting as a gateway or proxy, received an invalid
        /// response from the upstream server it accessed in attempting to
        /// fulfill the request.
        /// </summary>
        BadGateway = 502,

        /// <summary>
        /// The server is currently unable to handle the request due to a
        /// temporary overloading or maintenance of the server. 
        /// </summary>
        ServiceUnavailable = 503,

        /// <summary>
        /// The server, while acting as a gateway or proxy, did not receive a
        /// timely response from the upstream server specified by the URI.
        /// </summary>
        GatewayTimeout = 504,

        /// <summary>
        /// The server does not support, or refuses to support, the HTTP protocol
        /// version that was used in the request message. 
        /// </summary>
        HTTPVersionNotSupported = 505
    }
}



