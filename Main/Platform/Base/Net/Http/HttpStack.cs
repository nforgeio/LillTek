//-----------------------------------------------------------------------------
// FILE:        HttpStack.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides some global Http capabilities.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Provides some global Http capabilities.
    /// </summary>
    public static class HttpStack
    {
        /// <summary>
        /// Version constant for HTTP/1.0.
        /// </summary>
        public static readonly Version Http10 = new Version(1, 0);

        /// <summary>
        /// Version constant for HTTP/2.0.
        /// </summary>
        public static readonly Version Http11 = new Version(1, 1);

        private static object       syncLock = new object();
        private static Hashtable    clientCons;         // Client connections
        private static GatedTimer   sweepTimer;         // Timeout sweep timer
        private static TimeSpan     sweepInterval;      // The timeout sweep interval
        private static int          blockSize;          // Client connection block size
        private static bool         sweeping = false;   // True if sweeping for timeouts

        /// <summary>
        /// Static constructor
        /// </summary>
        static HttpStack()
        {
            clientCons    = new Hashtable();
            sweepInterval = TimeSpan.FromSeconds(5.0);
            sweepTimer    = null;
            blockSize     = 4096;
        }

        /// <summary>
        /// Adds a connection to the internal collection used to 
        /// detect timeout.
        /// </summary>
        /// <param name="con">The connection.</param>
        internal static void AddConnection(HttpConnection con)
        {
            lock (syncLock)
            {
                clientCons.Add(con, con);

                // Crank up the sweep timer if it's not already running.

                if (sweepTimer == null)
                    sweepTimer = new GatedTimer(new TimerCallback(OnSweepTimer), null, sweepInterval, sweepInterval);
            }
        }

        /// <summary>
        /// Removes a connection from the internal collection to
        /// detect timeouts.
        /// </summary>
        /// <param name="con">The connections.</param>
        internal static void RemoveConnection(HttpConnection con)
        {
            lock (syncLock)
            {
                if (sweeping)
                    return;     // This is handled below by the sweep handler

                clientCons.Remove(con);

                // If that was the last connection then stop the sweep timer.

                if (clientCons.Count == 0 && sweepTimer != null)
                {
                    sweepTimer.Dispose();
                    sweepTimer = null;
                }
            }
        }

        /// <summary>
        /// Returns the current number of client HTTP connections.
        /// </summary>
        public static int ClientConnectionCount
        {
            get
            {
                lock (syncLock)
                    return clientCons.Count;
            }
        }

        /// <summary>
        /// The transmission size of blocks send and received by client connections
        /// when communicating with the server.
        /// </summary>
        /// <remarks>
        /// This defaults to 4096.
        /// </remarks>
        public static int BlockSize
        {
            get { return blockSize; }
            set { blockSize = value; }
        }

        /// <summary>
        /// The interval at which client connections are swept for timeout.
        /// </summary>
        public static TimeSpan TimeoutSweepInterval
        {
            get { return sweepInterval; }

            set
            {
                lock (syncLock)
                {
                    sweepInterval = value;

                    // Restart the timer if it's running.

                    if (sweepTimer != null)
                    {
                        sweepTimer.Dispose();
                        sweepTimer = new GatedTimer(new TimerCallback(OnSweepTimer), null, TimeSpan.Zero, sweepInterval);
                    }
                }
            }
        }

        /// <summary>
        /// Implements the background client connection sweep.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnSweepTimer(object state)
        {
            lock (syncLock)
            {
                try
                {
                    sweeping = true;

                    if (clientCons == null)
                        return;

                    var delList = new ArrayList();
                    var now     = SysTime.Now;

                    foreach (HttpConnection con in clientCons.Values)
                        if (con.CloseIfTimeout(now))
                            delList.Add(con);

                    for (int i = 0; i < delList.Count; i++)
                        clientCons.Remove(delList[i]);

                    // Stop the sweep timer if there are no more connections.

                    if (clientCons.Count == 0 && sweepTimer != null)
                    {
                        sweepTimer.Dispose();
                        sweepTimer = null;
                    }
                }
                finally
                {
                    sweeping = false;
                }
            }
        }

        /// <summary>
        /// Manually sweeps client connections for timeouts.
        /// </summary>
        public static void SweepIdle()
        {
            OnSweepTimer(null);
        }

        /// <summary>
        /// Returns the reason phrase for a status code.
        /// </summary>
        /// <param name="status">The status code.</param>
        /// <returns>The reason phrase.</returns>
        public static string GetReasonPhrase(HttpStatus status)
        {
            switch (status)
            {
                case HttpStatus.Continue: return "Contiunue";
                case HttpStatus.SwitchingProtocols: return "Switching protocols";
                case HttpStatus.OK: return "OK";
                case HttpStatus.Created: return "Created";
                case HttpStatus.Accepted: return "Accepted";
                case HttpStatus.NonAuthoritativeInformation: return "Nonauthoritative information";
                case HttpStatus.NoContent: return "No content";
                case HttpStatus.ResetContent: return "Reset content";
                case HttpStatus.PartialContent: return "Partial content";
                case HttpStatus.MultipleChoices: return "Multiple choices";
                case HttpStatus.MovedPermanently: return "Moved permanently";
                case HttpStatus.Found: return "Found";
                case HttpStatus.SeeOther: return "See other";
                case HttpStatus.NotModified: return "Not modified";
                case HttpStatus.UseProxy: return "Use proxy";
                case HttpStatus.Unused: return "Unused";
                case HttpStatus.TemporaryRedirect: return "Temporary redirect";
                case HttpStatus.BadRequest: return "Bad request";
                case HttpStatus.Unauthorized: return "Unauthorized";
                case HttpStatus.PaymentRequired: return "Payemnt required";
                case HttpStatus.Forbidden: return "Forbidden";
                case HttpStatus.NotFound: return "Not found";
                case HttpStatus.MethodNotAllowed: return "Method not allowed";
                case HttpStatus.NotAcceptable: return "Not acceptable";
                case HttpStatus.ProxyAuthenticationRequired: return "Proxy authentication required";
                case HttpStatus.RequestTimeout: return "Request timeout";
                case HttpStatus.Conflict: return "Conflict";
                case HttpStatus.Gone: return "Gone";
                case HttpStatus.LengthRequired: return "Length required";
                case HttpStatus.PreconditionFailed: return "Precondition failed";
                case HttpStatus.RequestEntityTooLarge: return "Request entity too large";
                case HttpStatus.RequestURITooLong: return "Request URI too long";
                case HttpStatus.UnsupportedMediaType: return "Unsupported media type;";
                case HttpStatus.RequestedRangeNotSatisfiable: return "Request range not satisfiable";
                case HttpStatus.ExpectationFailed: return "Exectation failed";
                case HttpStatus.InternalServerError: return "Internal server error";
                case HttpStatus.NotImplemented: return "Not implemented";
                case HttpStatus.BadGateway: return "Bad gateway";
                case HttpStatus.ServiceUnavailable: return "Service unavailable";
                case HttpStatus.GatewayTimeout: return "Gateway timeout";
                case HttpStatus.HTTPVersionNotSupported: return "HTTP version not supported";
                default: return "Unknown error";
            }
        }

        /// <summary>
        /// Performs a HTTP get request and returns the response contents
        /// as well as the content type.
        /// </summary>
        /// <param name="uri">The request URI.</param>
        /// <param name="ttl">The maximum time to wait for a response.</param>
        /// <param name="contentType">Returns as the response content type.</param>
        /// <returns>The response contents.</returns>
        /// <remarks>
        /// <note>
        /// This method isn't very robust in its current implementation as it
        /// doesn't handle redirections, etc.  It should be suitable for various unit testing
        /// application though.
        /// </note>
        /// </remarks>
        /// <exception cref="SocketException">Thrown if there's an error communicating with the server.</exception>
        /// <exception cref="HttpException">Thrown if an error is detected while processing the query.</exception>
        public static byte[] Get(Uri uri, TimeSpan ttl, out string contentType)
        {
            HttpConnection  con = new HttpConnection(HttpOption.None);
            HttpRequest     request;
            HttpResponse    response;

            contentType = null;

            try
            {
                con.Connect(uri.ToString());

                request               = new HttpRequest(Http11, "GET", uri.PathAndQuery, null);
                request["host"]       = uri.Host;
                request["accept"]     = "*/*";
                request["user-agent"] = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1; .NET CLR 2.0.50727)";

                response = con.Query(request, SysTime.Now + ttl);
                if ((int)response.Status < 200 || (int)response.Status >= 300)
                    throw new HttpException(response.Status);

                contentType = response["content-type"];
                return response.Content.ToByteArray();
            }
            finally
            {
                con.Close();
            }
        }
    }
}
