//-----------------------------------------------------------------------------
// FILE:        DnsResolver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a specialized low-level DNS resolver that provides 
//              finer control over DNS queries.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// This implementation is pretty primitive.  A robust implementation
// would track response times and favor the "closer" DNS server and
// would also ignore unresponsive servers for some period of time.

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// This delegate is used in combination with the <see cref="DnsResolver.BadPacket" />
    /// event to communicate malformed DNS messages to debug logging code.
    /// </summary>
    /// <param name="packet">A buffer holding raw DNS message.</param>
    /// <param name="cbPacket">The size of the message in bytes.</param>
    public delegate void DnsBadPacketHandler(byte[] packet, int cbPacket);

    /// <summary>
    /// Implements a specialized low-level DNS resolver that provides 
    /// fine control over DNS queries and responses.  Note that only
    /// UDP DNS queries are supported at this time.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This DNS resolver does not implement any local caching of DNS
    /// responses.
    /// </note>
    /// <para><b><u>Implementation Note</u></b></para>
    /// <para>
    /// This class needs to crank up a global timer that scans the set of
    /// outstanding requests for any that have exceeded their timeout period.
    /// This timer will be created if one does not already exist when
    /// <see cref="Query" /> or <see cref="BeginQuery" /> is called.
    /// The time of the last call to these methods will also be recorded.
    /// </para>
    /// <para>
    /// This timer will trigger on one second intervals when any outstanding
    /// requests will be tested for timeout.  If there are no outstanding
    /// queries and 1 minute as elapsed since the last query, then the timer
    /// will be disposed.
    /// </para>
    /// <para>
    /// By default, the class uses a single UDP socket bound to an operating
    /// system selected network interface and port for transmitting and
    /// receiving DNS request and response packets.  The <see cref="Bind(IPEndPoint[],int,int)" />
    /// method can be used to specify one or more specific network bindings
    /// to be used.  If mutiple bindings are specified then DNS requests
    /// will be load balanced across them.
    /// </para>
    /// <note>
    /// This class does not implement any sort of throttling.  When
    /// the underlying network socket buffers fill up or the network is
    /// saturated, DNS request packets will start getting discarded causing
    /// queries to timeout.
    /// </note>
    /// </remarks>
    public static class DnsResolver
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used for tracking DNS requests both internally within this
        /// class as well as externally via the <b>BeginXXX()</b> and <b>EndXXX()</b> methods.
        /// </summary>
        private class DnsAsyncResult : AsyncResult
        {
            public DnsSocket    DnsSocket;              // The DNS socket used to transmit the request.
            public DnsRequest   Request;                // The DNS request
            public long         TimerStart;             // Hires timer count when the request packet was sent
            public DnsResponse  Response;               // The DNS response

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="dnsSocket">The socket used to transmit the request.</param>
            /// <param name="request">The DNS request.</param>
            /// <param name="timeout">The maximum time to wait.</param>
            /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
            /// <param name="state">The application state (or <c>null</c>).</param>
            public DnsAsyncResult(DnsSocket dnsSocket, DnsRequest request, TimeSpan timeout, AsyncCallback callback, object state)
                : base(null, callback, state)
            {
                this.DnsSocket  = dnsSocket;
                this.Request    = request;
                this.TTD        = SysTime.Now + timeout;
                this.TimerStart = 0;
                this.Response   = null;
            }
        }

        /// <summary>
        /// Used to relate DNS specific state with a UDP socket used for making requests.
        /// </summary>
        private class DnsSocket
        {
            public EnhancedSocket   Socket;         // The socket
            public ushort           SocketID;       // Index of the socket in the sockets array
            public byte[]           RecvPacket;     // 512 byte packet receive buffer
            public EndPoint         FromEP;         // Source endpoint for received packets
            public ushort           NextQID;        // The next DNS query ID

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="socket">The real socket.</param>
            /// <param name="socketID">Index of the socket in the sockets array.</param>
            public DnsSocket(EnhancedSocket socket, ushort socketID)
            {
                this.Socket     = socket;
                this.SocketID   = socketID;
                this.RecvPacket = new byte[512];
                this.FromEP     = new IPEndPoint(IPAddress.Any, 0);
                this.NextQID    = (ushort)Helper.Rand();    // Initialize with a random number as a
                                                            // weak step towards avoiding DNS poisoning.
            }
        }

        /// <summary>
        /// Used to track DNS query retries.
        /// </summary>
        private sealed class DnsRetryAsyncResult : AsyncResult
        {
            /// <summary>
            /// The DNS request message.
            /// </summary>
            public DnsRequest Request;

            /// <summary>
            /// The DNS response message.
            /// </summary>
            public DnsResponse Response;

            /// <summary>
            /// The number of times a request should be sent to a
            /// particular name server.
            /// </summary>
            public int MaxSendCount;

            /// <summary>
            /// The number of times the request has been transmitted to
            /// the current name server.
            /// </summary>
            public int SendCount;

            /// <summary>
            /// The timeout to use for each request retry.
            /// </summary>
            public TimeSpan Timeout;

            /// <summary>
            /// The list of name servers IP addresses remaining to be
            /// tried (or retried).  The first entry in the list is the
            /// address of the name server currently being queried.
            /// </summary>
            public List<IPAddress> NameServers;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="request">The DNS request.</param>
            /// <param name="nameServer">The single name server IP address.</param>
            /// <param name="maxSendCount">
            /// The number of times a request should be sent to a
            /// particular name server.
            /// </param>
            /// <param name="timeout">The timeout to use for each request retry.</param>
            /// <param name="callback">The deleate to be called when the operation completes (or <c>null</c>).</param>
            /// <param name="state">The application defined state (or <c>null</c>).</param>
            /// <remarks>
            /// This initializes <see cref="SendCount" /> to 1 under the assumption
            /// that the first request will be sent out immediately.
            /// </remarks>
            public DnsRetryAsyncResult(DnsRequest request, IPAddress nameServer, int maxSendCount, TimeSpan timeout,
                                       AsyncCallback callback, object state)
                : base(null, callback, state)
            {
                this.NameServers = new List<IPAddress>(1);
                this.NameServers.Add(nameServer);

                this.Request      = request;
                this.Response     = null;
                this.MaxSendCount = maxSendCount;
                this.SendCount    = 1;
                this.Timeout      = timeout;
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="request">The DNS request.</param>
            /// <param name="nameServers">The name server IP addresses.</param>
            /// <param name="maxSendCount">
            /// The number of times a request should be sent to a
            /// particular name server.
            /// </param>
            /// <param name="timeout">The timeout to use for each request retry.</param>
            /// <param name="callback">The deleate to be called when the operation completes (or <c>null</c>).</param>
            /// <param name="state">The application defined state (or <c>null</c>).</param>
            /// <remarks>
            /// <para>
            /// Note that a random name server endpoint from the list will be 
            /// selected and moved to the head of the list.
            /// </para>
            /// <para>
            /// This initializes <see cref="SendCount" /> to 1 under the assumption
            /// that the first request will be sent out immediately.
            /// </para>
            /// </remarks>
            public DnsRetryAsyncResult(DnsRequest request, IPAddress[] nameServers, int maxSendCount, TimeSpan timeout,
                                       AsyncCallback callback, object state)
                : base(null, callback, state)
            {

                this.NameServers = new List<IPAddress>(nameServers.Length);
                for (int i = 0; i < nameServers.Length; i++)
                    this.NameServers.Add(nameServers[i]);

                this.Request      = request;
                this.Response     = null;
                this.MaxSendCount = maxSendCount;
                this.SendCount    = 1;
                this.Timeout      = timeout;

                RandomizeNameServers();
            }

            /// <summary>
            /// Private constructor.
            /// </summary>
            /// <param name="callback">The deleate to be called when the operation completes (or <c>null</c>).</param>
            /// <param name="state">The application defined state (or <c>null</c>).</param>
            private DnsRetryAsyncResult(AsyncCallback callback, object state)
                : base(null, callback, state)
            {
            }

            /// <summary>
            /// Selects a random name server IP address and moves it to the
            /// head of the list.
            /// </summary>
            public void RandomizeNameServers()
            {
                int         index;
                IPAddress   addr;

                if (NameServers.Count <= 1)
                    return;

                index = Helper.RandIndex(NameServers.Count);
                addr  = NameServers[index];
                NameServers.RemoveAt(index);
                NameServers.Insert(0, addr);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// <see cref="NetTrace" /> subsystem name for the DNS resolver.
        /// </summary>
        public const string TraceSubSystem = "LillTek.Net.DnsResolver";

        private static object                           syncLock = new object();
        private static DnsSocket[]                      sockets;        // UDP query sockets with next QID
        private static TimeSpan                         pollTime;       // Background task polling interval
        private static TimeSpan                         maxIdleTime;    // Maximum idle time before background
                                                                        // timer should be stopped
        private static TimerCallback                    onBkTimer;      // Timer callback
        private static AsyncCallback                    onReceive;      // Packet async receive callback
        private static AsyncCallback                    onDnsQuery;     // DNS query completion async callback
        private static GatedTimer                       bkTimer;        // Background task timer
        private static Dictionary<int, DnsAsyncResult>  requests;       // See note below
        private static DateTime                         lastQueryTime;  // Time of the last query

        /// <summary>
        /// This event is raised whenever a malformed packet is received by the 
        /// resolver.  The idea is that applications can use this to log and
        /// analyze the packets received for debugging purposes.
        /// </summary>
        public static event DnsBadPacketHandler BadPacket;

        // Note: The requests hash table is used to track the outstanding DNS requests.
        //       The table is keyed by a 32-bit integer formed by putting the index of
        //       the socket used in the HIWORD and the DNS QID in the LOWORD.
        //
        //       I'm also using locks pretty heavily in this code but I'm not holding
        //       them for very long so I think that performance will still be pretty good.

        static DnsResolver()
        {
            sockets       = null;
            pollTime      = TimeSpan.FromSeconds(1.0);
            maxIdleTime   = TimeSpan.FromMinutes(1.0);
            onBkTimer     = new TimerCallback(OnBkTimer);
            onReceive     = new AsyncCallback(OnReceive);
            onDnsQuery    = new AsyncCallback(OnDnsQuery);
            bkTimer       = null;
            requests      = new Dictionary<int, DnsAsyncResult>();
            lastQueryTime = DateTime.MinValue;
        }

        /// <summary>
        /// Forms a 32-bit request key by combining the index of the socket
        /// where the request was transmitted with the 16-bit DNS QID.
        /// </summary>
        /// <param name="socketID">The socket index.</param>
        /// <param name="qid">The DNS QID.</param>
        /// <returns>The 32-bit key.</returns>
        private static int GenRequestKey(ushort socketID, ushort qid)
        {
            return (socketID << 16) | qid;
        }

        /// <summary>
        /// Cancels all outstanding DNS queries.  Each request will fail with a
        /// <see cref="CancelException" />.
        /// </summary>
        public static void CancelAll()
        {
            lock (syncLock)
            {
                foreach (DnsAsyncResult request in requests.Values)
                    request.Notify(new CancelException());

                requests.Clear();
                Thread.Sleep(1000);     // Wait a second to give the notifications
                                        // a chance to be dispatched on worker threads
                                        // before closing the sockets
            }
        }

        /// <summary>
        /// Sets the set of client endpoints to be used for performing DNS queries
        /// back to the default of one operating system assigned endpoint.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The existing sockets will be closed by this method any any
        /// outstanding requests will fail with a <see cref="CancelException" />.
        /// </note>
        /// </remarks>
        public static void Bind()
        {
            Bind(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, 0) }, 0, 0);
        }

        /// <summary>
        /// Specifies a new set of client network endpoints to be used for performing 
        /// DNS query operations.
        /// </summary>
        /// <param name="endpoints">The set of one or more valid endpoints.</param>
        /// <param name="cbSendBuf">Socket send buffer size in bytes (or 0 for a reasonable default).</param>
        /// <param name="cbRecvBuf">Socket receive buffer size in bytes(or 0 for a reasonable default).</param>
        /// <remarks>
        /// <para>
        /// By default, this class uses only a single socket bound to an operating
        /// selected network interface and port.  Some applications may find it necessary
        /// to bind to a specific network endpoint if the computer is multi-homed.
        /// Other high, performance applications may need to load balance across 
        /// several sockets to avoid issues with the 16-bit <see cref="DnsMessage.QID" />
        /// property wrapping around too quickly.
        /// </para>
        /// <para>
        /// The endpoints passed may specify the IP address as IPAddress.Any and/or
        /// the port=0, indicating that the a reasonable default value should 
        /// be selected.  If a socket cannot be opened for any of the
        /// endpoints, then all of the sockets opened so far will be closed and
        /// the current bindings will be retained.
        /// </para>
        /// <note>
        /// The existing sockets will be closed by this method any any
        /// outstanding requests will fail with a <see cref="CancelException" />.
        /// </note>
        /// </remarks>
        public static void Bind(IPEndPoint[] endpoints, int cbSendBuf, int cbRecvBuf)
        {
            EnhancedSocket[]    newSocks;
            int                 cbBuf;

            if (endpoints.Length == 0 || endpoints.Length > 128)
                throw new ArgumentException("Between 1 and 128 endpoints can be specified.");

            // Create and bind the new sockets, aborting the entire
            // operation if there's an error.

            newSocks = new EnhancedSocket[endpoints.Length];
            for (int i = 0; i < endpoints.Length; i++)
            {
                try
                {
                    newSocks[i] = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    newSocks[i].Bind(endpoints[i]);

                    cbBuf = 64 * 1024;
                    if (cbSendBuf == 0)
                        newSocks[i].SendBufferSize = cbBuf;
                    else
                        newSocks[i].SendBufferSize = cbSendBuf;

                    if (cbRecvBuf == 0)
                        newSocks[i].ReceiveBufferSize = cbBuf;
                    else
                        newSocks[i].ReceiveBufferSize = cbRecvBuf;
                }
                catch
                {
                    for (int j = 0; j < i; j++)
                        if (newSocks[i] != null)
                            newSocks[i].Close();

                    throw;
                }
            }

            // Cancel any outstanding requests, close the current sockets, 
            // and then setup the new ones.

            lock (syncLock)
            {
                CancelAll();

                if (sockets != null)
                    for (int i = 0; i < sockets.Length; i++)
                        sockets[i].Socket.Close();

                sockets = new DnsSocket[newSocks.Length];
                for (int i = 0; i < newSocks.Length; i++)
                {
                    sockets[i] = new DnsSocket(newSocks[i], (ushort)i);
                    sockets[i].Socket.BeginReceiveFrom(sockets[i].RecvPacket, 0, 512, SocketFlags.None, ref sockets[i].FromEP, onReceive, sockets[i]);
                }
            }
        }

        /// <summary>
        /// Makes sure that the class is prepared to transmit a query by
        /// ensuring that a socket exists and the background timer is
        /// running.  This method also updates lastQueryTime.
        /// </summary>
        /// <param name="request">The DNS request.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state to be passed to the callback (or <c>null</c>).</param>
        /// <returns>An initialized async result.</returns>
        private static DnsAsyncResult PrepareQuery(DnsRequest request, TimeSpan timeout,
                                                   AsyncCallback callback, object state)
        {
            DnsAsyncResult arDns;

            lock (syncLock)
            {
                lastQueryTime = SysTime.Now;

                if (sockets == null)
                    Bind();

                if (bkTimer == null)
                    bkTimer = new GatedTimer(onBkTimer, null, pollTime, pollTime);

                // Randomly select the socket to use

                arDns       = new DnsAsyncResult(sockets[Helper.RandIndex(sockets.Length)], request, timeout, callback, state);
                request.QID = arDns.DnsSocket.NextQID++;

                // Add the async result to the requests table

                requests.Add(GenRequestKey(arDns.DnsSocket.SocketID, request.QID), arDns);
            }

            return arDns;
        }

        /// <summary>
        /// Transmits a DNS request to a name server and then waits for and 
        /// then returns the response.
        /// </summary>
        /// <param name="nameServer">IP address of the name server.</param>
        /// <param name="request">The DNS request.</param>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <returns>The query response.</returns>
        /// <remarks>
        /// <note>
        /// This DNS resolver does not implement any local caching of DNS
        /// responses.
        /// </note>
        /// <para>
        /// This method does not perform any iterative processing on the request
        /// or implement any retry behavior.  It simply sends a DNS message to the
        /// name server and returns the response message or throws a 
        /// <see cref="TimeoutException" />.
        /// </para>
        /// <note>
        /// The method will initialize the request's <see cref="DnsMessage.QID" />
        /// property with a unique 16-bit query ID.
        /// </note>
        /// <note>
        /// This method performs no checks to verify that the
        /// response returned actually answers the question posed in the
        /// request.  The only validation performed is to verify that the
        /// message is a valid response and that its QID matches that
        /// of the request.
        /// </note>
        /// </remarks>
        public static DnsResponse Query(IPAddress nameServer, DnsRequest request, TimeSpan timeout)
        {
            var arDns = BeginQuery(nameServer, request, timeout, null, null);

            return EndQuery(arDns);
        }

        /// <summary>
        /// Initiates an asynchronous DNS request to a name server.
        /// </summary>
        /// <param name="nameServer">IP address of the name server.</param>
        /// <param name="request">The DNS request.</param>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state to be passed to the callback (or <c>null</c>).</param>
        /// <returns>The IAsyncResult object to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// This DNS resolver does not implement any local caching of DNS
        /// responses.
        /// </note>
        /// <para>
        /// This method in combination with <see cref="EndQuery" /> does not perform any 
        /// iterative processing on the request or implement any retry behavior.  It simply 
        /// sends a DNS message to the name server and returns the response message or throws a 
        /// <see cref="TimeoutException" />.
        /// </para>
        /// <note>
        /// The method will initialize the request's <see cref="DnsMessage.QID" />
        /// property with a unique 16-bit query ID.
        /// </note>
        /// </remarks>
        public static IAsyncResult BeginQuery(IPAddress nameServer, DnsRequest request, TimeSpan timeout,
                                              AsyncCallback callback, object state)
        {
            DnsAsyncResult  arDns;
            byte[]          packet;
            int             cbPacket;

            lock (syncLock)
            {
                arDns  = PrepareQuery(request, timeout, callback, state);
                packet = request.FormatPacket(out cbPacket);
                arDns.TimerStart = HiResTimer.Count;

                request.Trace(TraceSubSystem, 0, nameServer, null);
                arDns.DnsSocket.Socket.SendTo(packet, 0, cbPacket, SocketFlags.None, new IPEndPoint(nameServer, NetworkPort.DNS));
                arDns.Started();
            }

            return arDns;
        }

        /// <summary>
        /// Completes an asynchronous DNS request.
        /// </summary>
        /// <param name="ar">The IAsyncResult instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The query response.</returns>
        /// <remarks>
        /// <para>
        /// This method in combination with <see cref="EndQuery" /> does not perform any 
        /// iterative processing on the request or implement any retry behavior.  It simply 
        /// sends a DNS message to the name server and returns the response message or throws a 
        /// <see cref="TimeoutException" />.
        /// </para>
        /// <note>
        /// This method performs no checks to verify that the
        /// response returned actually answers the question posed in the
        /// request.  The only validation performed is to verify that the
        /// message is a valid response and that its QID matches that
        /// of the request.
        /// </note>
        /// </remarks>
        public static DnsResponse EndQuery(IAsyncResult ar)
        {
            var arDns = (DnsAsyncResult)ar;

            arDns.Wait();
            try
            {
                if (arDns.Exception != null)
                {
                    NetTrace.Write(TraceSubSystem, 0, "Exception", arDns.Exception);
                    throw arDns.Exception;
                }

                return arDns.Response;
            }
            finally
            {
                arDns.Dispose();
            }
        }

        /// <summary>
        /// Called when an async packet receive operation completes on one of the DNS sockets.
        /// </summary>
        /// <param name="ar">The operation's async result instance.</param>
        private static void OnReceive(IAsyncResult ar)
        {

            DnsSocket       dnsSock = (DnsSocket)ar.AsyncState;
            DnsResponse     response;
            DnsAsyncResult  arDns;
            int             cbPacket;
            int             requestKey;

            lock (syncLock)
            {
                try
                {
                    cbPacket = dnsSock.Socket.EndReceiveFrom(ar, ref dnsSock.FromEP);
                    response = new DnsResponse();
                    if (!response.ParsePacket(dnsSock.RecvPacket, cbPacket))
                    {
                        NetTrace.Write(TraceSubSystem, 0, "Bad DNS message", string.Empty,
                                       Helper.HexDump(dnsSock.RecvPacket, 0, cbPacket, 16, HexDumpOption.ShowAll));

                        BadPacket(dnsSock.RecvPacket, cbPacket);
                        return;
                    }

                    response.Trace(TraceSubSystem, 0, ((IPEndPoint)dnsSock.FromEP).Address, null);

                    // We've parsed a valid DNS response so attempt to match it
                    // up with the corresponding request and signal that the 
                    // query operation is complete.

                    requestKey = GenRequestKey(dnsSock.SocketID, response.QID);
                    if (requests.TryGetValue(requestKey, out arDns))
                    {
                        if (response.RCode != DnsFlag.RCODE_OK)
                        {
                            arDns.Notify(new DnsException(response.RCode));
                            return;
                        }

                        response.Latency = HiResTimer.CalcTimeSpan(arDns.TimerStart);
                        arDns.Response = response;
                        arDns.Notify();
                    }
                    else
                        response.Trace(TraceSubSystem, 0, ((IPEndPoint)dnsSock.FromEP).Address, "Orphan DNS Response");
                }
                catch (SocketException)
                {
                    // We're going to get SocketException(10054) "Connection Reset" errors if 
                    // we send a packet to a port that's not actually open on the remote 
                    // machine.  Ignore these exceptions and let the operation timeout.
                }
                finally
                {
                    if (dnsSock.Socket.IsOpen)
                        dnsSock.Socket.BeginReceiveFrom(dnsSock.RecvPacket, 0, 512, SocketFlags.None, ref dnsSock.FromEP, onReceive, dnsSock);
                }
            }
        }

        /// <summary>
        /// Performs a more advanced query that load balances against a set of 
        /// DNS server endpoints and also implements retry behaviors.
        /// </summary>
        /// <param name="nameServers">The set of name server IP addresses.</param>
        /// <param name="request">The DNS request.</param>
        /// <param name="timeout">The timeout to use for the initial request as well as the retries.</param>
        /// <param name="maxSendCount">The maximum number of requests to send to any single name server.</param>
        /// <returns>The received DNS response message.</returns>
        /// <remarks>
        /// <note>
        /// This DNS resolver does not implement any local caching of DNS
        /// responses.
        /// </note>
        /// </remarks>
        public static DnsResponse QueryWithRetry(IPAddress[] nameServers, DnsRequest request, TimeSpan timeout, int maxSendCount)
        {
            var ar = BeginQueryWithRetry(nameServers, request, timeout, maxSendCount, null, null);

            return EndQueryWithRetry(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation that performs a more advanced query that load balances 
        /// against a set of DNS server endpoints and also implements retry behaviors.
        /// </summary>
        /// <param name="nameServers">The set of name server IP addresses.</param>
        /// <param name="request">The DNS request.</param>
        /// <param name="timeout">The timeout to use for the initial request as well as the retries.</param>
        /// <param name="maxSendCount">The maximum number of requests to send to any single name server.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state to be associated with the operation.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// This DNS resolver does not implement any local caching of DNS
        /// responses.
        /// </note>
        /// </remarks>
        public static IAsyncResult BeginQueryWithRetry(IPAddress[] nameServers, DnsRequest request, TimeSpan timeout, int maxSendCount,
                                                       AsyncCallback callback, object state)
        {
            DnsRetryAsyncResult arRetry;

            if (maxSendCount < 1)
                throw new ArgumentException("[maxSendCount] must be >= 1.");

            arRetry = new DnsRetryAsyncResult(request, nameServers, maxSendCount, timeout, callback, state);
            BeginQuery(arRetry.NameServers[0], request, timeout, OnDnsQuery, arRetry);
            arRetry.Started();

            return arRetry;
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginQueryWithRetry" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQueryWithRetry" />.</param>
        /// <returns>The received DNS response message.</returns>
        public static DnsResponse EndQueryWithRetry(IAsyncResult ar)
        {
            var arRetry = (DnsRetryAsyncResult)ar;

            arRetry.Wait();
            try
            {
                if (arRetry.Exception != null)
                {
                    NetTrace.Write(TraceSubSystem, 0, "Exception", arRetry.Exception);
                    throw arRetry.Exception;
                }

                return arRetry.Response;
            }
            finally
            {
                arRetry.Dispose();
            }
        }

        /// <summary>
        /// Handles DNS query completions by implementing retry behavior.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private static void OnDnsQuery(IAsyncResult ar)
        {
            var         arRetry = (DnsRetryAsyncResult)ar.AsyncState;
            DnsResponse response;

            try
            {
                arRetry.Response = response = EndQuery(ar);
                if (response.RCode != DnsFlag.RCODE_OK)
                    arRetry.Notify(new DnsException(response.RCode));
                else
                    arRetry.Notify();
            }
            catch (TimeoutException e)
            {
                if (arRetry.SendCount < arRetry.MaxSendCount)
                {
                    // Resend to the same name server

                    arRetry.SendCount++;
                    BeginQuery(arRetry.NameServers[0], arRetry.Request.Clone(), arRetry.Timeout, onDnsQuery, arRetry);
                    return;
                }

                // We've exceeded the retry limit for the name server at
                // the front of list.  If there are more servers, then
                // pick another one to try.

                if (arRetry.NameServers.Count <= 1)
                {
                    arRetry.Notify(e);
                    return;
                }

                arRetry.NameServers.RemoveAt(0);
                arRetry.RandomizeNameServers();

                arRetry.SendCount = 1;
                BeginQuery(arRetry.NameServers[0], arRetry.Request.Clone(), arRetry.Timeout, onDnsQuery, arRetry);
                return;
            }
            catch (Exception e)
            {
                arRetry.Notify(e);
            }
        }

        /// <summary>
        /// Handles timeout processing.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnBkTimer(object state)
        {
            var delKeys = new List<int>();
            var now = SysTime.Now;

            lock (syncLock)
            {
                // Walk the list of requests, looking for those that have timed out
                // and adding them to the deleted list.

                foreach (int key in requests.Keys)
                {
                    var arDns = requests[key];

                    if (now >= arDns.TTD)
                        delKeys.Add(key);
                }

                // Signal a timeout exception for each deleted request and
                // remove them from the requests table.

                for (int i = 0; i < delKeys.Count; i++)
                {
                    var key = delKeys[i];
                    var arDns = requests[key];

                    requests.Remove(key);
                    arDns.Notify(new TimeoutException());
                }

                // Kill the timer if there are no outstanding requests and the 
                // class has been idle for a while.

                if (requests.Count == 0 && now - lastQueryTime >= maxIdleTime)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }
            }
        }
    }
}
