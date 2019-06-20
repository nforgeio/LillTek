//-----------------------------------------------------------------------------
// FILE:        HttpServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a light weight network server capable of processing
//              client HTTP requests.

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

using LillTek.Common;
using LillTek.Advanced;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// I haven't implemented any sort of timeout on the
// socket connections.

// $todo(jeff.lill): 
//
// This is only a partial implementation.  There is
// no support for transfer encodings other than
// "Identity" and there's currently no support for
// authentication, or SSL.  I'm sure that there's 
// a lot of other stuff missing as well since I only 
// spent two days on these classes.

// $todo(jeff.lill): Implement a limit on the number of inbound connections.

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a light weight network server capable of processing
    /// client HTTP requests.
    /// </summary>
    /// <remarks>
    /// A HttpServer monitors a specified set of network endpoints for incoming
    /// HTTP requests.  A set of <see cref="IHttpModule" /> instances are also passed to the
    /// server during construction.  Received HTTP requests will be passed to 
    /// each of the module's <see cref="IHttpModule.OnRequest" /> method in turn.  
    /// The first module that returns a non-<c>null</c> <see cref="HttpResponse" /> will be 
    /// considered to have handled the request and the response returned will be returned to 
    /// the client application.
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class HttpServer
    {
        /// <summary>
        /// The default HTTP Server header returned in responses.
        /// </summary>
        public const string DefaultServerHeader = "LillTek HTTP Server/1.0";

        // Implementation Note:
        //
        // I'm using the socket's AppState property to indicate when the
        // server is processing a request associated with the socket.  This 
        // will be set to a non-null value if this is the case, otherwise.

        private const int RecvBlockSize = 4096;
        private const int SendBlockSize = 4096;

        private TimedSyncRoot   syncLock;           // Thread synchronization target
        private IPEndPoint[]    endPoints;          // Server network endpoints
        private int             cBacklog;           // Number of queued pre-accept sockets
        private IHttpModule[]   modules;            // Module plug-ins
        private SocketListener  listener;           // Socket listener
        private AsyncCallback   onSend;             // Socket send handler
        private AsyncCallback   onRecv;             // Socket receive handler
        private Hashtable       connections;        // Table of socket connections keyed by socket
        private int             maxConnections;     // Maximum number of connections
        private int             cbQueryMax;         // Maximum query size
        private PerfCounter     perfBytesRecv;      // Performance counters (or null)
        private PerfCounter     perfBytesSent;

        // Default response headers

        private string          hdrServer;          // Server: LillTek HTTP Server/1.0
        private string          hdrCacheControl;    // Cache-Control: private

        /// <summary>
        /// Constructs the server.
        /// </summary>
        /// <param name="endPoints">The network interfaces to monitor.</param>
        /// <param name="modules">The HTTP modules that handle the inbound requests.</param>
        /// <param name="cBacklog">Maximum number of inbound sockets to queue before accepting.</param>
        /// <param name="maxConnections">The maximum number of inbound connections.</param>
        /// <param name="cbQueryMax">Maximum size of a request.</param>
        public HttpServer(IPEndPoint[] endPoints, IHttpModule[] modules, int cBacklog, int maxConnections, int cbQueryMax)
        {
            this.syncLock        = new TimedSyncRoot();
            this.endPoints       = endPoints;
            this.cBacklog        = cBacklog;
            this.modules         = modules;
            this.maxConnections  = maxConnections;
            this.cbQueryMax      = cbQueryMax;
            this.onSend          = new AsyncCallback(OnSend);
            this.onRecv          = new AsyncCallback(OnReceive);

            this.hdrServer       = DefaultServerHeader;
            this.hdrCacheControl = "private";
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public void Start()
        {
            Start(null, null);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="perfBytesRecv">Performance counter to receive updates about bytes received (or <c>null</c>).</param>
        /// <param name="perfBytesSent">Performance counter to receive updates about bytes sent (or <c>null</c>).</param>
        public void Start(PerfCounter perfBytesRecv, PerfCounter perfBytesSent)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (listener != null)
                    throw new InvalidOperationException("Server already started.");

                this.perfBytesRecv = perfBytesRecv;
                this.perfBytesSent = perfBytesSent;

                listener = new SocketListener();
                listener.SocketAcceptEvent += new SocketAcceptDelegate(OnAccept);
                for (int i = 0; i < endPoints.Length; i++)
                    listener.Start(endPoints[i], cBacklog);

                connections = new Hashtable();
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void Stop()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (listener == null)
                    return;

                listener.StopAll();

                listener = null;
                connections = null;
            }
        }

        /// <summary>
        /// The default Server header value that <see cref="AddDefaultHeaders" /> adds to responses.
        /// </summary>
        public string ServerHeader
        {
            get { return hdrServer; }
            set { hdrServer = value; }
        }

        /// <summary>
        /// The default Cache-Control header value that <see cref="AddDefaultHeaders" /> adds to responses.
        /// </summary>
        public string CacheControlHeader
        {
            get { return hdrCacheControl; }
            set { hdrCacheControl = value; }
        }

        /// <summary>
        /// Adds the default headers to the HTTP response if they're not already present.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <remarks>
        /// The default headers include: Date, Server, Content-Length, and Cache-Control.
        /// </remarks>
        public void AddDefaultHeaders(HttpResponse response)
        {
            // Note that Content-Length is actually set by the
            // HttpResponse.Serialize() method.

            if (response["Date"] == null)
                response.Add(new HttpHeader("Date", DateTime.UtcNow));

            if (response["Server"] == null)
                response.Add(new HttpHeader("Server", hdrServer));

            if (response["Cache-Control"] == null)
                response.Add("Cache-Control", hdrCacheControl);
        }

        /// <summary>
        /// Called by the socket listener when an inbound socket is accepted.
        /// </summary>
        /// <param name="sock">The accepted socket.</param>
        /// <param name="endPoint">The endpoint the socket was accepted from.</param>
        private void OnAccept(EnhancedSocket sock, IPEndPoint endPoint)
        {
            var httpState = new HttpAsyncState();
            var recvBuf   = new byte[RecvBlockSize];

            using (TimedLock.Lock(syncLock))
            {
                if (connections.Count >= maxConnections)
                {
                    sock.AsyncSendClose(new HttpResponse(HttpStatus.ServiceUnavailable, "Server is too busy").Serialize(SendBlockSize));
                    return;
                }

                connections.Add(sock, sock);
            }

            httpState.FirstRequest = true;
            httpState.Request      = new HttpRequest();
            httpState.Socket       = sock;
            httpState.Buffer       = recvBuf;
            httpState.RecvSize     = 0;

            httpState.Request.BeginParse(((IPEndPoint)sock.LocalEndPoint).Port);
            sock.BeginReceive(recvBuf, 0, recvBuf.Length, SocketFlags.None, onRecv, httpState);
        }

        /// <summary>
        /// Handles socket receive completions.
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceive(IAsyncResult ar)
        {
            HttpAsyncState  httpState = (HttpAsyncState)ar.AsyncState;
            HttpRequest     request   = httpState.Request;
            EnhancedSocket  sock      = httpState.Socket;
            byte[]          recvBuf   = httpState.Buffer;
            int             cbRecv;
            HttpResponse    response;
            bool            closeCon;
            bool            close;
            bool            firstRequest;

            try
            {
                cbRecv = sock.EndReceive(ar);
            }
            catch
            {
                using (TimedLock.Lock(syncLock))
                    connections.Remove(sock);

                sock.Close();
                return;
            }

            if (cbRecv == 0)
            {
                using (TimedLock.Lock(syncLock))
                    connections.Remove(sock);

                sock.ShutdownAndClose();
                return;
            }

            if (perfBytesRecv != null)
                perfBytesRecv.IncrementBy(cbRecv);

            httpState.RecvSize += cbRecv;
            if (httpState.RecvSize > cbQueryMax)
            {
                // The request is too large so respond with a HttpStatus.RequestEntityTooLarge
                // and close the socket.

                response = new HttpResponse(HttpStatus.RequestEntityTooLarge);
                sock.AsyncSendClose(response.Serialize(SendBlockSize));

                using (TimedLock.Lock(syncLock))
                    connections.Remove(sock);

                return;
            }

            if (!request.Parse(recvBuf, cbRecv))
            {
                recvBuf          = new byte[RecvBlockSize];
                httpState.Buffer = recvBuf;
                sock.BeginReceive(recvBuf, 0, recvBuf.Length, SocketFlags.None, onRecv, httpState);
                return;
            }

            // We have a complete request so process it.

            request.EndParse();

            firstRequest = httpState.FirstRequest;
            httpState.FirstRequest = false;

            try
            {
                sock.AppState = this;   // Indicate that we're processing a request

                closeCon = false;
                for (int i = 0; i < modules.Length; i++)
                {
                    response = modules[i].OnRequest(this, request, firstRequest, out close);
                    closeCon = closeCon || close;

                    if (response != null)
                    {
                        BlockArray blocks;

                        // Make sure the response version is reasonable

                        if (request.HttpVersion < response.HttpVersion)
                            response.HttpVersion = request.HttpVersion;

                        // Truncate any content data for HEAD requests

                        if (request.Method == "HEAD")
                        {
                            response.Content = null;
                            if (response["Content-Length"] != null)
                                response["Content-Length"] = "0";
                        }

                        blocks = response.Serialize(SendBlockSize);
                        if (perfBytesSent != null)
                            perfBytesSent.IncrementBy(blocks.Size);

                        if (closeCon)
                        {
                            sock.AsyncSendClose(blocks);

                            using (TimedLock.Lock(syncLock))
                                connections.Remove(sock);
                        }
                        else
                            sock.BeginSendAll(blocks, SocketFlags.None, onSend, httpState);

                        break;
                    }
                }
            }
            finally
            {
                sock.AppState = null;   // Indicate that we're done processing the request
            }
        }

        /// <summary>
        /// Handles socket send completions.
        /// </summary>
        /// <param name="ar"></param>
        private void OnSend(IAsyncResult ar)
        {
            var httpState = (HttpAsyncState)ar.AsyncState;
            var sock      = httpState.Socket;

            try
            {
                sock.EndSendAll(ar);
            }
            catch
            {
                using (TimedLock.Lock(syncLock))
                    connections.Remove(sock);

                sock.Close();
                return;
            }

            // We've successfully transmitted the response so turn around
            // and begin another receive.

            var recvBuf = new byte[RecvBlockSize];

            httpState.Request = new HttpRequest();
            httpState.Buffer  = recvBuf;

            httpState.Request.BeginParse(((IPEndPoint)sock.LocalEndPoint).Port);
            sock.BeginReceive(recvBuf, 0, recvBuf.Length, SocketFlags.None, onRecv, httpState);
        }

        /// <summary>
        /// Returns the current number of client connections.
        /// </summary>
        public int ConnectionCount
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (connections == null)
                        return 0;
                    else
                        return connections.Count;
                }
            }
        }

        /// <summary>
        /// This method should be called periodically on a backgound thread to sweep
        /// the server for inactive client connections.
        /// </summary>
        /// <param name="maxIdle">Maximum connection idle time.</param>
        public void SweepIdle(TimeSpan maxIdle)
        {
            var TTD     = SysTime.Now - maxIdle;
            var delList = new ArrayList();

            using (TimedLock.Lock(syncLock))
            {
                foreach (EnhancedSocket sock in connections.Values)
                    if (sock.AppState == null && sock.TouchTime <= TTD)
                        delList.Add(sock);

                for (int i = 0; i < delList.Count; i++)
                {
                    ((EnhancedSocket)delList[i]).ShutdownAndClose();
                    connections.Remove(delList[i]);
                }
            }
        }
    }
}
