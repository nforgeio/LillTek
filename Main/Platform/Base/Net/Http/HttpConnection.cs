//-----------------------------------------------------------------------------
// FILE:        HttpConnection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a light-weight HTTP connection.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// Convert the existing methods or add new methods that accept
// actual Uri class parameters rather than strings.

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a light-weight HTTP connection.
    /// </summary>
    public class HttpConnection : IDisposable, ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        private const string AlreadyConnectedMsg = "Already connected.";
        private const string NotConnectedMsg     = "Not connected.";
        private const string QueryPendingMsg     = "Query already in progress.";

        private static object   syncLock = new object();
        private static int      nextID   = 0;

        /// <summary>
        /// Allocates a unique hash instance ID.
        /// </summary>
        private static int AllocID()
        {
            lock (syncLock)
                return nextID++;
        }

        //---------------------------------------------------------------------
        // Instance members

        private int             id;             // Unique connection ID
        private EnhancedSocket  sock;           // Client socket
        private IPAddress       ipAddress;      // IP address of the server
        private HttpOption      options;        // Connection options
        private bool            timedOut;       // True if the current operation has timed out
        private bool            queryPending;   // True if a query is in progress
        private DateTime        TTD;            // Time-to-die for the current query
        private int             cbContentMax;   // Maximum allowed content size (or -1)
        private AsyncCallback   onRequestSent;  // Async callbacks
        private AsyncCallback   onResponseRecv;
        private PerfCounter     perfBytesRecv;  // Performance counters (or null)
        private PerfCounter     perfBytesSent;

        /// <summary>
        /// Constructs an HTTP connection.
        /// </summary>
        /// <param name="options">The connection options.</param>
        public HttpConnection(HttpOption options)
            : this(options, -1, null, null)
        {
        }

        /// <summary>
        /// Constructs an HTTP connection.
        /// </summary>
        /// <param name="options">The connection options.</param>
        /// <param name="cbContentMax">Maximum allowed content size or <b>-1</b> to disable checking.</param>
        public HttpConnection(HttpOption options, int cbContentMax)
            : this(options, cbContentMax, null, null)
        {
        }

        /// <summary>
        /// Constructs an HTTP connection.
        /// </summary>
        /// <param name="options">The connection options.</param>
        /// <param name="cbContentMax">Maximum allowed content size or <b>-1</b> to disable checking.</param>
        /// <param name="perfBytesRecv">Performance counter to receive updates about bytes received (or <c>null</c>).</param>
        /// <param name="perfBytesSent">Performance counter to receive updates about bytes sent (or <c>null</c>).</param>
        public HttpConnection(HttpOption options, int cbContentMax, PerfCounter perfBytesRecv, PerfCounter perfBytesSent)
        {

            this.id             = AllocID();
            this.cbContentMax   = cbContentMax;
            this.sock           = null;
            this.ipAddress      = IPAddress.Any;
            this.options        = options;
            this.timedOut       = false;
            this.onRequestSent  = new AsyncCallback(OnRequestSent);
            this.onResponseRecv = new AsyncCallback(OnResponseReceived);
            this.perfBytesRecv  = perfBytesRecv;
            this.perfBytesSent  = perfBytesSent;
        }

        /// <summary>
        /// Returns the IP address of the remote endpoint after a connection has been
        /// established.  Returns IPAddress.Any if no connection has been made.
        /// </summary>
        public IPAddress IPAddress
        {
            get { return ipAddress; }
        }

        /// <summary>
        /// Connects to the host and port specified in A URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        public void Connect(string uri)
        {
            // $todo(jeff.lill): 
            //
            // This should probably set HttpOption.SSL if the
            // uri scheme is HTTPS.

            Uri u = new Uri(uri);

            Connect(u.Host, u.Port);
        }

        /// <summary>
        /// Connects to a host and port.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="port">The port number.</param>
        public void Connect(string host, int port)
        {
            var ar = BeginConnect(host, port, null, null);

            EndConnect(ar);
        }

        /// <summary>
        /// Connects to a network endpoint.
        /// </summary>
        /// <param name="endPoint"></param>
        public void Connect(IPEndPoint endPoint)
        {
            var ar = BeginConnect(endPoint, null, null);

            EndConnect(ar);
        }

        /// <summary>
        /// Initates an asynchronous connection to the server whose host and
        /// port are specified by a URI.
        /// </summary>
        /// <param name="uri">The uri.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result used to track the operation.</returns>
        public IAsyncResult BeginConnect(string uri, AsyncCallback callback, object state)
        {
            var u = new Uri(uri);

            using (TimedLock.Lock(this))
            {
                if (sock != null)
                    throw new InvalidOperationException(AlreadyConnectedMsg);

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                return sock.BeginConnect(u.Host, u.Port, callback, state);
            }
        }

        /// <summary>
        /// Initates an asynchronous connection to the server whose host and
        /// port are specified.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="port">The port.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result used to track the operation.</returns>
        public IAsyncResult BeginConnect(string host, int port, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                return sock.BeginConnect(host, port, callback, state);
            }
        }

        /// <summary>
        /// Initates an asynchronous connection to the server whose network
        /// endpoint is specified.
        /// </summary>
        /// <param name="endPoint">The network endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result used to track the operation.</returns>
        public IAsyncResult BeginConnect(IPEndPoint endPoint, AsyncCallback callback, object state)
        {
            using (TimedLock.Lock(this))
            {
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                return sock.BeginConnect(endPoint, callback, state);
            }
        }

        /// <summary>
        /// Completes an asynchronous connection attempt.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginConnect(string, System.AsyncCallback, object)" />.</param>
        public void EndConnect(IAsyncResult ar)
        {
            using (TimedLock.Lock(this))
            {
                sock.EndConnect(ar);

                this.ipAddress    = ((IPEndPoint)sock.RemoteEndPoint).Address;
                this.timedOut     = false;
                this.queryPending = false;

                HttpStack.AddConnection(this);
            }
        }

        /// <summary>
        /// Submits a HTTP query to the connected server.
        /// </summary>
        /// <param name="query">The query request.</param>
        /// <param name="ttd">The expiration time (time-to-die) for the query (SYS).</param>
        /// <returns>The query response.</returns>
        /// <remarks>
        /// <para>
        /// Pass ttd as the time (SYS) when the query should be considered to
        /// have timed-out.  Connections will be periodically polled for timed out 
        /// connections by the HttpStack class.  Connections with active queries that
        /// have exceeded this time will be closed and the query operation will
        /// throw a TimeoutException.
        /// </para>
        /// <para>
        /// Pass ttd=DateTime.MaxValue to disable timeout checking.
        /// </para>
        /// </remarks>
        public HttpResponse Query(HttpRequest query, DateTime ttd)
        {
            var ar = BeginQuery(query, ttd, null, null);
            return EndQuery(ar);
        }

        /// <summary>
        /// Initiates an asynchronous HTTP query to the connected server.
        /// </summary>
        /// <param name="query">The query request.</param>
        /// <param name="ttd">The expiration time (time-to-die) for the query (SYS).</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// Pass ttd as the time (SYS) when the query should be considered to
        /// have timed-out.  Connections will be perodically polled for timed out 
        /// connections by the HttpStack class.  Connections with active queries that
        /// have exceeded this time will be closed and the query operation will
        /// throw a TimeoutException.
        /// </para>
        /// <para>
        /// Pass ttd=DateTime.MaxValue to disable timeout checking.
        /// </para>
        /// </remarks>
        public IAsyncResult BeginQuery(HttpRequest query, DateTime ttd, AsyncCallback callback, object state)
        {
            HttpAsyncResult     httpAR;
            BlockArray          blocks;

            using (TimedLock.Lock(this))
            {
                if (sock == null || !sock.IsOpen)
                    throw new InvalidOperationException(NotConnectedMsg);

                if (queryPending)
                    throw new InvalidOperationException(QueryPendingMsg);

                blocks = query.Serialize(HttpStack.BlockSize);

                if (perfBytesSent != null)
                    perfBytesSent.IncrementBy(blocks.Size);

                httpAR = new HttpAsyncResult(this, callback, state);
                httpAR.Started();

                sock.BeginSendAll(blocks, SocketFlags.None, onRequestSent, httpAR);

                this.queryPending = true;
                this.TTD          = ttd;

                return httpAR;
            }
        }

        /// <summary>
        /// Handles completion of the request transmission.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnRequestSent(IAsyncResult ar)
        {
            var httpAR = (HttpAsyncResult)ar.AsyncState;

            using (TimedLock.Lock(this))
            {
                try
                {
                    sock.EndSendAll(ar);

                    // The request was sent successfully so start receiving
                    // the response.

                    httpAR.Response = new HttpResponse(cbContentMax);
                    httpAR.Buffer   = new byte[HttpStack.BlockSize];

                    httpAR.Response.BeginParse();
                    sock.BeginReceive(httpAR.Buffer, 0, HttpStack.BlockSize, SocketFlags.None, onResponseRecv, httpAR);
                }
                catch (Exception e)
                {

                    httpAR.Notify(e);
                }
            }
        }

        /// <summary>
        /// Handles completion of the response reception.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnResponseReceived(IAsyncResult ar)
        {
            var httpAR = (HttpAsyncResult)ar.AsyncState;
            int cbRecv;

            using (TimedLock.Lock(this))
            {
                try
                {
                    // Pump the received data into the response parser,
                    // signalling completion when we have the entire message.

                    cbRecv = sock.EndReceive(ar);

                    if (perfBytesRecv != null)
                        perfBytesRecv.IncrementBy(cbRecv);

                    if (httpAR.Response.Parse(httpAR.Buffer, cbRecv))
                    {
                        httpAR.Response.EndParse();
                        httpAR.Notify();
                        return;
                    }

                    if (cbRecv == 0)
                    {
                        sock.ShutdownAndClose();
                        return;
                    }

                    // Continue receiving response data

                    httpAR.Buffer = new byte[HttpStack.BlockSize];
                    sock.BeginReceive(httpAR.Buffer, 0, HttpStack.BlockSize, SocketFlags.None, onResponseRecv, httpAR);
                }
                catch (Exception e)
                {
                    httpAR.Notify(e);
                }
            }
        }

        /// <summary>
        /// Completes an asynchronous query request.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginQuery" />.</param>
        /// <returns>The query response.</returns>
        public HttpResponse EndQuery(IAsyncResult ar)
        {
            var httpAR = (HttpAsyncResult)ar;

            httpAR.Wait();
            using (TimedLock.Lock(this))
            {
                try
                {
                    if (httpAR.Exception != null)
                    {
                        Close();
                        throw timedOut ? new TimeoutException() : httpAR.Exception;
                    }

                    return httpAR.Response;
                }
                finally
                {
                    queryPending = false;
                    httpAR.Dispose();
                }
            }
        }

        /// <summary>
        /// Closes the connection if open.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (sock == null || !sock.IsOpen)
                    return;

                sock.Close();
            }

            HttpStack.RemoveConnection(this);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes the connection if open, a query is in progress and the query 
        /// has exceeded its timeout period.
        /// </summary>
        /// <param name="now">The current time (SYS).</param>
        /// <returns><c>true</c> if the connection was closed due to a timeout.</returns>
        public bool CloseIfTimeout(DateTime now)
        {
            using (TimedLock.Lock(this))
            {
                if (queryPending && this.TTD <= now)
                {
                    Close();

                    queryPending = false;
                    timedOut     = true;

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the connection is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                using (TimedLock.Lock(this))
                    return sock == null || !sock.IsOpen;
            }
        }

        /// <summary>
        /// Returns the connection's hash code.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return id;
        }

        /// <summary>
        /// Compares this connection to a parameter.
        /// </summary>
        /// <param name="obj">The connection to test.</param>
        /// <returns><c>true</c> if the connections are the same.</returns>
        public override bool Equals(object obj)
        {
            var con = obj as HttpConnection;

            if (con == null)
                return false;

            return this.id == con.id;
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
