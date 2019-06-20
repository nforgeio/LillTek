//-----------------------------------------------------------------------------
// FILE:        EnhancedSocket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements enhanced Socket functionality

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;

#if MOBILE_DEVICE
using AddressFamily = System.Net.Sockets.AddressFamily;
#else
using LillTek.Windows;
#endif

// $todo(jeff.lill): 
//
// Implement real scatter/gather behavior with calls to the underlying
// OS.  The code now just simulates this functionality for the 
// BeginSend() APIs.

// Implementation Note:
// --------------------
// I like to code asynchronous I/O classes assuming that all I/O operations are
// actually completed asynchronously, that is, on a different thread from that
// which initiated the operation.  This simplifies things greatly by not having
// to worried about whether the operation completed synchronously or not.
//
// It appears that the behavior of the Socket class changed for .NET 2.0 in that
// it now does complete some operations synchronously.  I've added some code
// to detect this and then queue the completion notification to another thread.

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Implements an enhanced wrapper on the <see cref="Socket" /> class that provides some additional
    /// functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The main additions are that this socket implements the <see cref="IsOpen" />
    /// property which is true when a stream socket is connected or listening or when a datagram
    /// socket is bound to an IP address and port.  The behavior of <see cref="Close" />
    /// and <see cref="Shutdown" /> have been modified so that they do not throw 
    /// <see cref="ObjectDisposedException" />s when the underlying socket has already 
    /// been closed.  This simplifies the implementation of handling socket closing scenarios.
    /// </para>
    /// <para>
    /// One subtle, but major difference between <see cref="EnhancedSocket" /> and the
    /// .NET Framework's <see cref="Socket" /> class is that <see cref="EnhancedSocket" />
    /// enables <see cref="LingerOption" /> with a reasonable default value for streaming
    /// sockets.  <see cref="LingerOption" /> is disabled by default by <see cref="Socket" />.
    /// This is useful, since the vast majority of applications will want this enabled.
    /// </para>
    /// <para>
    /// Another difference with <see cref="Socket" /> is the addition of the <see cref="ShutdownAndClose" />
    /// method.  This method calls the underlying <see cref="Socket.Shutdown" /> method passing
    /// <see cref="SocketShutdown.Both" />.  Then if no asynchronous <see cref="BeginReceive" /> or
    /// <see cref="BeginReceiveFrom" /> operations are pending, the socket will be closed immediately.  
    /// If either of these asynchronous receive methods are pending on the socket
    /// then socket will not be closed immediately.  Instead, the class will wait for the
    /// receive operation to complete returning a receive size of zero close the 
    /// underlying socket.  This change avoids seeing <see cref="ObjectDisposedException" />s
    /// thrown by the asynchronous receive completion method.
    /// </para>
    /// <para>
    /// The <see cref="EnhancedSocket" /> class also ensures that asynchronous operations will never
    /// complete synchronously.  This pattern helps to avoid re-entrancy and deadlock problems.
    /// </para>
    /// <para>
    /// The socket also implements the <see cref="SendMax" /> and <see cref="ReceiveMax" /> properties.
    /// For DEBUG builds, these properties control how many bytes will be send/received on stream
    /// sockets in a single operation.  This is useful for implementing test suites that
    /// exercise the application's ability to deal with data transmission being chunked 
    /// on different boundries.
    /// </para>
    /// <para>
    /// There's a version of the <b>Connect()</b> methods that handles DNS host name lookup.
    /// </para>
    /// <para>
    /// The <see cref="BeginSendAll(byte[], int, int, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> 
    /// and <see cref="EndSendAll" /> asynchronous methods and <see cref="SendAll(byte[])" /> synchronous method have been 
    /// added to provide an internal implementation of a buffer send state machine.
    /// </para>
    /// <para>
    /// <see cref="BeginReceiveAll(LillTek.Common.BlockArray, int, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> 
    /// and <see cref="EndReceiveAll" /> methods have 
    /// been added to provide an internal implementation of a buffer receive state machine.
    /// </para>
    /// <para>
    /// Some of the <b>Send()</b> methods now accept a <see cref="BlockArray" /> parameter that can be
    /// used to send data from multiple buffers in a single call.
    /// </para>
    /// <para>
    /// The <see cref="TouchTime" /> property can be used to identify inactive sockets for
    /// server applications.
    /// </para>
    /// <para>
    /// The <see cref="IgnoreUdpConnectionReset"/> property controls whether UDP sockets will ignore ICMP error responses 
    /// sent by hosts that were sent packets to an endpoint that does not exist.  Set this property to <c>true</c> to
    /// enable this mode.  This is useful for situations where applications may send UDP packets to remote
    /// endpoints that don't exist and the application does not want to be bothered
    /// with <see cref="SocketException" />s thrown for <see cref="SocketError.ConnectionReset" />
    /// errors.  When this property is <c>true</c>, the <b>Receive()</b> and
    /// <b>ReceiveFrom()</b>, <b>BeginReceive()</b>, <b>BeginReceiveFrom()</b>, <b>EndReceive()</b>,
    /// and <b>EndReceiveFrom()</b> methods will ignore these errors automatically for UDP sockets.
    /// </para>
    /// <para>
    /// The property <see cref="IgnoreUdpConnectionReset"/> is ignored for TCP sockets.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class EnhancedSocket : IDisposable, IAsyncResultOwner, INetIOProvider, ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        private const bool      LingerEnable  = true;   // Default linger enable state
        private const int       LingerTimeout = 10;     // Default maximum time (seconds) the TCP stack should attempt
                                                        // to transmit buffered data after a socket is closed.

        private static object   syncLock      = new object();
        private static uint     nextHashID    = 0;

        /// <summary>
        /// This class is used to hook the underlying Socket completion
        /// result and hold the operation's resulting values until the
        /// appropriate EndXXX() method is called.
        /// </summary>
        private sealed class SockResult : AsyncResult
        {
            // Misc state information

            public int              count;
            public EnhancedSocket   socket;
            public EndPoint         EP;
            public int              port;
            public SocketFlags      socketFlags;

            // SendAll related state

            public byte[]           sendBuf;
            public int              sendPos;
            public int              cbSend;
            public int              sendBlockIndex;
            public BlockArray       sendBlocks;

            // ReceiveAll related state

            public byte[]           recvBuf;
            public int              recvPos;
            public int              cbRecv;
            public int              cbRecvTotal;
            public BlockArray       recvBlocks;

            public SockResult(object owner, AsyncCallback callback, object state)
                : base(owner, callback, state)
            {
            }
        }

        /// <summary>
        /// Allocates a unique hash instance ID.
        /// </summary>
        private static uint AllocID()
        {
            lock (syncLock)
                return nextHashID++;
        }

        /// <summary>
        /// Creates a pair of datagram sockets bound to the IP address passed with
        /// consecutive port numbers.
        /// </summary>
        /// <param name="address">The address to bind the socket.</param>
        /// <param name="sockEven">Returns as the socket with the even port number in the pair.</param>
        /// <param name="sockOdd">Returns as the socket with the odd port number in the pair.</param>
        /// <remarks>
        /// This is useful in stupid protocols like SIP/RDP that require consecutive
        /// port numbers for some operations.
        /// </remarks>
        public static void CreateDatagramPair(IPAddress address, out EnhancedSocket sockEven, out EnhancedSocket sockOdd)
        {
            List<Socket>    socks = null;
            Socket          sock0 = null;
            Socket          sock1 = null;
            int             port0, port1;

            try
            {
                // Try allocating consecutive sockets until we are successful
                // or the system can't allocate any more sockets.

                while (true)
                {
                    sock0 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    sock0.Bind(new IPEndPoint(address, 0));
                    port0 = ((IPEndPoint)sock0.LocalEndPoint).Port;

                    try
                    {
                        sock1 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                        if ((port0 & 1) == 0)
                            port1 = port0 + 1;
                        else
                            port1 = port0 - 1;

                        sock1.Bind(new IPEndPoint(address, port1));
                        break;
                    }
                    catch (SocketException)
                    {
                        // $todo(jeff.lill): 
                        //
                        // For now, I'm going to assume that this means that
                        // the port is already in use.  I should really check
                        // the socket error code to be sure of this.

                        if (socks == null)
                            socks = new List<Socket>();

                        socks.Add(sock0);
                    }
                    catch
                    {
                        if (socks == null)
                            socks = new List<Socket>();

                        socks.Add(sock0);
                        throw;
                    }
                }

                // We found two adjacent ports so close any failed socket bindings and
                // then return the results.

                if ((port0 & 1) == 0)
                {
                    sockEven = new EnhancedSocket(sock0);
                    sockOdd = new EnhancedSocket(sock1);
                }
                else
                {
                    sockEven = new EnhancedSocket(sock1);
                    sockOdd = new EnhancedSocket(sock0);
                }
            }
            finally
            {
                // Close any failed socket bindings and then report the error

                if (socks != null)
                    foreach (Socket sock in socks)
                    {
                        try
                        {
                            sock.Close();
                        }
                        catch
                        {
                        }
                    }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current host supports IPv4 addressing.
        /// </summary>
        public static bool SupportsIPv4
        {
#if WINFULL
            get { return Socket.OSSupportsIPv4; }
#else
            get { return true; }
#endif
        }

        /// <summary>
        /// Returns <c>true</c> if the current host supports IPv6 addressing.
        /// </summary>
        public static bool SupportsIPv6
        {
#if WINFULL
            get { return Socket.OSSupportsIPv6; }
#else
            get { return false; }
#endif
        }

        //---------------------------------------------------------------------
        // Instance members

        private Socket      sock;               // The underlying socket
        private uint        hashID;             // Unique hashID for this instance
        private DateTime    touchTime;          // Last time the socket was touched (SYS)
        private bool        isOpen;             // True if the socket is open
        private bool        closePending;       // True if socket closure is pending async receive completion
        private int         asyncRecvPending;   // Number of asynchronous receive operations pending
        private bool        isShutdown;         // True if the Shutdown() has been called
        private string      ownerName;          // Async owner name
        private bool        disableHangTest;    // True to disable AsyncTracker hand test
        private object      appState;           // Application defined state
        private IPAddress   multicastGroup;     // The current multicast group (or null)
        private short       multicastTTL;       // The current multicast time-to-live
        private bool        reuseAddress;       // True if the socket can bind to an
                                                // endpoint that's already in use.

        // Enhanced state machine related fields.

        private AsyncCallback onSendAllBuffer;    // Callbacks (or null)
        private AsyncCallback onSendAllBlocks;
        private AsyncCallback onRecvAllBuffer;
        private AsyncCallback onRecvAllBlocks;
        private AsyncCallback onSendAllTo;
        private AsyncCallback onAccept;
        private AsyncCallback onReceive;
        private AsyncCallback onReceiveFrom;
        private AsyncCallback onSend;
        private AsyncCallback onSendTo;

#if DEBUG
        // Debug fields

        private int recvMax = -1;       // Maximum # of bytes to read in one operation
        private int sendMax = -1;       // Maximum # of bytes to send in one operation
#endif

        /// <summary>
        /// Initializes the enhanced state fields.
        /// </summary>
        private void InitEnhancedState()
        {

            this.touchTime                = SysTime.Now;
            this.multicastGroup           = null;
            this.multicastTTL             = 0;
            this.reuseAddress             = false;
            this.appState                 = null;
            this.onSendAllBuffer          = null;
            this.onSendAllBlocks          = null;
            this.onRecvAllBuffer          = null;
            this.onRecvAllBlocks          = null;
            this.onSendAllTo              = null;
            this.onAccept                 = new AsyncCallback(OnAccept);
            this.onReceive                = new AsyncCallback(OnReceive);
            this.onReceiveFrom            = new AsyncCallback(OnReceiveFrom);
            this.onSend                   = new AsyncCallback(OnSend);
            this.onSendTo                 = new AsyncCallback(OnSendTo);
            this.IgnoreUdpConnectionReset = false;
        }

        /// <summary>
        /// Used internally to construct a EnhancedSocket from an open Socket instance.
        /// </summary>
        /// <param name="sock">The open socket.</param>
        internal EnhancedSocket(Socket sock)
        {
            this.sock             = sock;
            this.hashID           = AllocID();
            this.isOpen           = true;
            this.closePending     = false;
            this.asyncRecvPending = 0;
            this.isShutdown       = false;
            this.ownerName        = null;
            this.disableHangTest  = false;

            InitEnhancedState();
        }

        /// <summary>
        /// Constructs a socket from the parameters passed.
        /// </summary>
        /// <param name="addressFamily">The network address family.</param>
        /// <param name="socketType">The socket type.</param>
        /// <param name="protocolType">The protocol type.</param>
        public EnhancedSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            this.sock             = new Socket(addressFamily, socketType, protocolType);
            this.hashID           = AllocID();
            this.isOpen           = false;
            this.closePending     = false;
            this.asyncRecvPending = 0;
            this.isShutdown       = false;
            this.ownerName        = null;
            this.disableHangTest  = false;

            InitEnhancedState();
        }

        /// <summary>
        /// A debug method that verifies that the socket is still open.
        /// </summary>
        [Conditional("DEBUG")]
        private void Verify()
        {
            if (!isOpen)
                throw new SocketClosedException(SocketCloseReason.LocalClose);
        }

        /// <summary>
        /// The name of the owning object (used for debugging purposes).
        /// </summary>
        public string OwnerName
        {
            get { return ownerName; }
            set { ownerName = value; }
        }

        /// <summary>
        /// Disables the socket hang test in debug builds.
        /// </summary>
        /// <remarks>
        /// Setting this to true indicates to the debug build of the AsyncTracker 
        /// that this instance should not be tested for hung async operations.
        /// </remarks>
        public bool DisableHangTest
        {
            get { return disableHangTest; }
            set { disableHangTest = value; }
        }

        /// <summary>
        /// Returns the socket's network address family.
        /// </summary>
        public AddressFamily AddressFamily
        {
            get { return sock.AddressFamily; }
        }

        /// <summary>
        /// Returns the number of bytes of data available to be
        /// received from the socket.
        /// </summary>
        public int Available
        {
            get
            {
                Verify();
                return sock.Available;
            }
        }

        /// <summary>
        /// Indicates whether the socket is in blocking mode or not.
        /// </summary>
        public bool Blocking
        {
            get { return sock.Blocking; }
            set { sock.Blocking = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the socket is connected.
        /// </summary>
        public bool Connected
        {
            get { return sock != null && isOpen && sock.Connected; }
        }

        /// <summary>
        /// Returns <c>true</c> if the socket is currently open.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                using (TimedLock.Lock(this))
                    return isOpen && !closePending;
            }
        }

        /// <summary>
        /// Specifies whether or not the socket allows Internet Protocol (IP)
        /// datagrams to be fragmented.
        /// </summary>
        public bool DontFragment
        {
            get { return sock.DontFragment; }
            set { sock.DontFragment = value; }
        }

        /// <summary>
        /// Enables/disables the Nagle algorithm on this socket.  Pass true
        /// to disable it.
        /// </summary>
        public bool NoDelay
        {
            get { return sock.NoDelay; }
            set { sock.NoDelay = value; }
        }

        /// <summary>
        /// The time-to-live value to be used for Internet Protocol (IP) packets
        /// transmitted by this socket.
        /// </summary>
        public short TTL
        {
            get { return sock.Ttl; }
            set { sock.Ttl = value; }
        }

        /// <summary>
        /// Enables UDP broadcasting and multicasting for the socket.
        /// </summary>
        public bool EnableBroadcast
        {
            get { return sock.EnableBroadcast; }
            set { sock.EnableBroadcast = value; }
        }

        /// <summary>
        /// Specifies whether or not the socket is allowed to bind to an endpoint
        /// already bound to a socket in another process.
        /// </summary>
        public bool ExclusiveAddressUse
        {
            get { return sock.ExclusiveAddressUse; }
            set { sock.ExclusiveAddressUse = value; }
        }

        /// <summary>
        /// Specifies whether this socket will be allowed to bind to an endpoint
        /// that is already in use.
        /// </summary>
        public bool ReuseAddress
        {
            get { return reuseAddress; }

            set
            {
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value ? 1 : 0);
                reuseAddress = value;
            }
        }

        /// <summary>
        /// Specifies an option that indicates whether the socket will delay closing of a socket
        /// in an attempt to send all pending data.
        /// </summary>
        public LingerOption LingerState
        {

            get { return sock.LingerState; }
            set { sock.LingerState = value; }
        }

        /// <summary>
        /// Returns the underlying Windows socket handle.
        /// </summary>
        public IntPtr Handle
        {
            get
            {
                Verify();
                return sock.Handle;
            }
        }

        /// <summary>
        /// Application defined state.  The can be used for any purpose.
        /// </summary>
        public object AppState
        {
            get { return appState; }
            set { appState = value; }
        }

        /// <summary>
        /// Returns the network endpoint for the local side of the
        /// connection.
        /// </summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                Verify();
                return sock.LocalEndPoint;
            }
        }

        /// <summary>
        /// Return's the socket's network protocol type.
        /// </summary>
        public ProtocolType ProtocolType
        {
            get { return sock.ProtocolType; }
        }

        /// <summary>
        /// Returns the network endpoint of the remote side
        /// of the connection.
        /// </summary>
        public EndPoint RemoteEndPoint
        {
            get { return sock.RemoteEndPoint; }
        }

        /// <summary>
        /// Specifies whether outgoing multicast packets should be delivered
        /// to this socket as well.
        /// </summary>
        public bool MulticastLoopback
        {
            get { return sock.MulticastLoopback; }
            set { sock.MulticastLoopback = value; }
        }

        /// <summary>
        /// Specifies the multicast group membership for the socket.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property provides for associating the socket with only one
        /// multicast group at a time (which is by far the most common case).
        /// Use SetSocketOption(SocketOptionName.AddMembership) if you need
        /// to associated multiple groups.
        /// </para>
        /// <para>
        /// Set this to <see cref="IPAddress.Any" /> disassociate the socket from
        /// from a multicast group.
        /// </para>
        /// </remarks>
        public IPAddress MulticastGroup
        {
            get { return isOpen ? multicastGroup : IPAddress.Any; }

            set
            {
                IPAddress address;

                if (!isOpen || isShutdown)
                    return;

                address = ((IPEndPoint)sock.LocalEndPoint).Address;
                if (address.Equals(IPAddress.Any))
                {
                    if (multicastGroup != null)
                    {
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership,
                                             new MulticastOption(multicastGroup));
                        multicastGroup = null;
                    }

                    if (value != null)
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                             new MulticastOption(value));
                }
                else
                {
                    if (multicastGroup != null)
                    {
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership,
                                             new MulticastOption(multicastGroup, address));
                        multicastGroup = null;
                    }

                    if (value != null)
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                             new MulticastOption(value, address));
                }

                multicastGroup = value;
            }
        }

        /// <summary>
        /// Specifies the Internet Protocol (IP) time-to-live (TTL) value for multicast
        /// sockets transmitted by this socket.
        /// </summary>
        /// <remarks>
        /// Returns 0 if the TTL has never been explicitly set and remains at the
        /// default value.
        /// </remarks>
        public short MulticastTTL
        {
            get { return multicastTTL; }

            set
            {
                sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, value);
                multicastTTL = value;
            }
        }

        /// <summary>
        /// Returns the socket type.
        /// </summary>
        public SocketType SocketType
        {
            get { return sock.SocketType; }
        }

        /// <summary>
        /// Specifies the size of the socket's receive buffer.
        /// </summary>
        public int ReceiveBufferSize
        {
            get { return sock.ReceiveBufferSize; }
            set { sock.ReceiveBufferSize = value; }
        }

        /// <summary>
        /// Specifies the timeout in milliseconds for synchronous Receive calls.
        /// <b>0</b> or <b>-1</b> specifies an infinite value.
        /// </summary>
        public int ReceiveTimeout
        {
            get { return sock.ReceiveTimeout; }
            set { sock.ReceiveTimeout = value; }
        }

        /// <summary>
        /// Specifies the size of the socket's send buffer.
        /// </summary>
        public int SendBufferSize
        {
            get { return sock.SendBufferSize; }
            set { sock.SendBufferSize = value; }
        }

        /// <summary>
        /// Specifies the timeout in milliseconds for synchronous <b>Send</b> calls.
        /// <b>0</b> or <b>-1</b> specifies an infinite value.
        /// </summary>
        public int SendTimeout
        {
            get { return sock.SendTimeout; }
            set { sock.SendTimeout = value; }
        }

        /// <summary>
        /// Controls whether UDP sockets will ignore ICMP responses from packets
        /// sent to remote endpoints that do not exist.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set this property to <c>true</c> to enable this mode.  This is useful
        /// for situations where applications may send UDP packets to remote
        /// endpoints that don't exist and the application does not want to be bothered
        /// with <see cref="SocketException" />s thrown for <see cref="SocketError.ConnectionReset" />
        /// errors.  When this property is <c>true</c>, the <b>Receive()</b> and
        /// <b>ReceiveFrom()</b>, <b>BeginReceive()</b>, and <b>BeginReceiveFrom()</b>
        /// methods will ignore these errors automatically
        /// for UDP sockets.
        /// </para>
        /// <para>
        /// This property is ignored for TCP sockets.
        /// </para>
        /// </remarks>
        public bool IgnoreUdpConnectionReset { get; set; }

        /// <summary>
        /// Returns the last time the socket was touched (SYS).
        /// </summary>
        /// <remarks>
        /// This is updated to the current <b>SysTime.Now</b> value whenever there's some
        /// sort of application or network activity that indicates that the socket
        /// is still active.  This information is typically used by server applications
        /// to identify and close inactive client sockets.
        /// </remarks>
        public DateTime TouchTime
        {
            get { return touchTime; }
        }

        /// <summary>
        /// Updates the <see cref="TouchTime" /> property to the current time (SYS).
        /// </summary>
        public void Touch()
        {
            touchTime = SysTime.Now;
        }

        /// <summary>
        /// Used to simulate full TCP transmission buffers by forcing the code to limit
        /// the number of bytes transmitted per low-level socket operation to the value
        /// set.  Pass -1 to remove the limit.  This functionality is used for test suites 
        /// and is enabled only for DEBUG builds.
        /// </summary>
        public int SendMax
        {
            get
            {
#if DEBUG
                return sendMax;
#else
                return -1;
#endif
            }

            set
            {
#if DEBUG
                sendMax = value;
#endif
            }
        }

        /// <summary>
        /// Used to simulate empty TCP receive buffers by forcing the code to limit
        /// the number of bytes read per low-level socket operation to the value
        /// set.  Pass -1 to remove the limit.  This functionality is used for test suites 
        /// and is enabled only for DEBUG builds.
        /// </summary>
        public int ReceiveMax
        {
            get
            {
#if DEBUG
                return recvMax;
#else
                return -1;
#endif
            }

            set
            {
#if DEBUG
                recvMax = value;
#endif
            }
        }

        /// <summary>
        /// Returns the number of bytes to actually transmit for a low-level socket send
        /// operation, taking the current SendMax value into account.
        /// </summary>
        private int SendSize(int cb)
        {
#if DEBUG
            if (sendMax <= 0 || sock.SocketType == SocketType.Dgram)
                return cb;
            else if (sendMax < cb)
                return sendMax;
            else
                return cb;
#else
            return cb;
#endif
        }

        /// <summary>
        /// Returns the number of bytes to actually receive for a low-level socket receive
        /// operation, taking the current ReceiveMax value into account.
        /// </summary>
        private int RecvSize(int cb)
        {
#if DEBUG

            if (recvMax <= 0 || sock.SocketType == SocketType.Dgram)
                return cb;
            else if (recvMax < cb)
                return recvMax;
            else
                return cb;
#else
            return cb;
#endif
        }

        private void Pack(byte[] dest, int offset, int value)
        {
            byte[] arr = BitConverter.GetBytes(value);

            for (int i = 0; i < 4; i++)
                dest[offset + i] = arr[i];
        }

        /// <summary>
        /// Sets the low-level TCP keep-alive interval.
        /// </summary>
        /// <param name="seconds">The keep-alive interval in seconds or 0 to disable.</param>
        public void SetKeepAlive(int seconds)
        {
            return;     // $todo: delete this after confirming that keep-alive really works

#if TODO
            // The input array corresponds to the structure:
            //
            // struct tcp_keepalive {
            //     u_long  onoff;
            //     u_long  keepalivetime;
            //     u_long  keepaliveinterval;
            // };

            byte[]      input  = new byte[12];
            byte[]      output = new byte[12];

            if (seconds != 0) {

                Pack(input,0,1);
                Pack(input,4,seconds);
                Pack(input,8,seconds);
            }

            sock.IOControl(Win32.SIO_KEEPALIVE_VALS,input,output);
#endif
        }

        /// <summary>
        /// Binds the socket the specified network endpoint.
        /// </summary>
        /// <param name="localEP">The local endpoint.</param>
        public void Bind(EndPoint localEP)
        {
            using (TimedLock.Lock(this))
            {
                sock.Bind(localEP);

                if (sock.SocketType == SocketType.Dgram)
                    isOpen = true;
            }
        }

        /// <summary>
        /// Binds the socket to an operating selected IP address and port.
        /// This is equivalent to calling Bind(new IPEndPoint(IPAddress.Any,0)).
        /// </summary>
        public void Bind()
        {
            Bind(new IPEndPoint(IPAddress.Any, 0));
        }

        /// <summary>
        /// Closes the socket.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already closed.
        /// </note>
        /// </remarks>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    return;

                this.MulticastGroup = null;

                sock.Close();
                isOpen = false;
            }
        }

        /// <summary>
        /// Gracefully stops communication on the socket and then closes it.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already closed.
        /// </note>
        /// </remarks>
        public void ShutdownAndClose()
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    return;

                this.MulticastGroup = null;

                sock.Shutdown(SocketShutdown.Both);
                isShutdown = true;

                if (asyncRecvPending == 0)
                    sock.Close();
                else
                    closePending = true;

                isOpen = false;
            }
        }

        /// <summary>
        /// Returns the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <returns>The option value.</returns>
        public object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
        {
            Verify();
            return sock.GetSocketOption(optionLevel, optionName);
        }

        /// <summary>
        /// Returns the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <param name="optionValue">Byte array to receive the value.</param>
        public void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            Verify();
            sock.GetSocketOption(optionLevel, optionName, optionValue);
        }

        /// <summary>
        /// Returns the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <param name="optionLength">Expected length of the option value.</param>
        /// <returns>The option value in a byte array.</returns>
        public object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength)
        {
            Verify();
            return sock.GetSocketOption(optionLevel, optionName, optionLength);
        }

        /// <summary>
        /// Releases all unmanaged resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            this.Close();
        }

        /// <summary>
        /// Transforms a <see cref="ObjectDisposedException" /> or a <see cref="SocketException" /> with
        /// for error <b>WSAECONNRESET</b> (10054) into a <see cref="SocketClosedException" />.
        /// </summary>
        /// <param name="e">The input exception.</param>
        /// <returns>
        /// The <see cref="SocketClosedException" /> if the conditions above were 
        /// met or else the exception passed to the method.
        /// </returns>
        private Exception TransformException(Exception e)
        {
            if (!isOpen && e is ObjectDisposedException)
                return new SocketClosedException(SocketCloseReason.LocalClose);

            var socketException = e as SocketException;

            if (socketException != null && socketException.ErrorCode == 10054)
                return new SocketClosedException(SocketCloseReason.RemoteReset);

            return e;
        }

        /// <summary>
        /// Implements low-level I/O control functions.
        /// </summary>
        /// <param name="ioControlCode">The control code.</param>
        /// <param name="optionInValue">Input values.</param>
        /// <param name="optionOutValue">Output values.</param>
        /// <returns>The number of bytes returned in optionOutValue.</returns>
        public int IOControl(int ioControlCode, byte[] optionInValue, byte[] optionOutValue)
        {
            Verify();
            return sock.IOControl(ioControlCode, optionInValue, optionOutValue);
        }

        /// <summary>
        /// Initializes a server socket by have it start listening for inbound
        /// connection attempts.
        /// </summary>
        /// <param name="backLog">The maximum number of inbound connections to queue.</param>
        public void Listen(int backLog)
        {
            using (TimedLock.Lock(this))
            {
                Touch();
                sock.Listen(backLog);
                isOpen = true;
            }
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public bool Poll(int microSeconds, SelectMode mode)
        {
            // No self-respecting .NET application is ever going to call this API.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <param name="optionValue">The option data.</param>
        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            sock.SetSocketOption(optionLevel, optionName, optionValue);
        }

        /// <summary>
        /// Sets the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <param name="optionValue">The option value.</param>
        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            // $todo(jeff.lill): 
            //
            // Hopefully this all got fixed in CF 2.0 so I should
            // be able to delete this stuff.

            sock.SetSocketOption(optionLevel, optionName, optionValue);
        }

        /// <summary>
        /// Sets the specified socket option.
        /// </summary>
        /// <param name="optionLevel">The option level.</param>
        /// <param name="optionName">The option name.</param>
        /// <param name="optionValue">The option value.</param>
        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
            sock.SetSocketOption(optionLevel, optionName, optionValue);
        }

        /// <summary>
        /// Gracefully stops communication on the socket in one or both directions.
        /// </summary>
        /// <param name="how">Describes which directions of communication are to be stopped.</param>
        public void Shutdown(SocketShutdown how)
        {
            using (TimedLock.Lock(this))
            {
                if (!isOpen || isShutdown)
                    return;

                try
                {
                    sock.Shutdown(how);
                    isShutdown = true;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Returns a unique hash code for this instance.
        /// </summary>
        /// <returns>A unique hash code.</returns>
        public override int GetHashCode()
        {
            return (int)hashID;
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed matches this instance.
        /// </summary>
        /// <param name="o">The object to test.</param>
        /// <returns><c>true</c> if there's a match.</returns>
        public override bool Equals(object o)
        {
            var test = o as EnhancedSocket;

            if (test == null)
                return false;

            return test.hashID == this.hashID;
        }

        //---------------------------------------------------------------------
        // Connect methods

        /// <summary>
        /// Directs the socket to connect to the specified host and port.
        /// </summary>
        /// <param name="host">A DNS host name or a serialized IP address.</param>
        /// <param name="port">The port number.</param>
        public void Connect(string host, int port)
        {
            IAsyncResult ar;

            try
            {

                ar = BeginConnect(host, port, null, null);
                EndConnect(ar);
            }
            catch (Exception e)
            {

                throw TransformException(e);
            }
        }

        /// <summary>
        /// Directs the socket to connect to the specified network endpoint.
        /// </summary>
        /// <param name="remoteEP">The target endpoint.</param>
        public void Connect(EndPoint remoteEP)
        {
            try
            {
                using (TimedLock.Lock(this))
                {
                    Touch();
                    sock.LingerState = new LingerOption(LingerEnable, LingerTimeout);
                    sock.Connect(remoteEP);
                    isOpen = true;
                }
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Initiates an asynchronous connection attempt to the specified host and port.
        /// </summary>
        /// <param name="host">A DNS host name or a serialized IP address.</param>
        /// <param name="port">The port number.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginConnect(string host, int port, AsyncCallback callback, object state)
        {
            Touch();

            // If the host name is actually a valid IP address then initiate the connection.

            if (NetHelper.IsIPAddress(host))
            {
                var ar = (AsyncResult)BeginConnect(new IPEndPoint(Helper.ParseIPAddress(host), port), callback, state);

                ar.Started();
                return ar;
            }

            // Initiate an asynchronous DNS lookup

            var sockAR = new SockResult(this, callback, state);

            sockAR.port = port;

#if !MOBILE_DEVICE
            EnhancedDns.BeginGetHostByName(host, new AsyncCallback(OnDNSLookup), sockAR);
#else
            Dns.BeginGetHostEntry(host,new AsyncCallback(OnDNSLookup),sockAR);
#endif
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles completion of the DNS lookup.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnDNSLookup(IAsyncResult ar)
        {

            SockResult      sockAR = (SockResult)ar.AsyncState;
            IPHostEntry     ipEntry;
            IPAddress       address;

            Touch();
            try
            {
                // Get the IP address and then initiate the async
                // connection request

#if !MOBILE_DEVICE
                ipEntry = EnhancedDns.EndGetHostByName(ar);
#else
                ipEntry = Dns.EndGetHostEntry(ar);
#endif

                address = null;
                foreach (var entry in ipEntry.AddressList)
                    if (entry.AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = entry;
                        break;
                    }

                if (address == null)
                    throw new NotImplementedException(string.Format("Host name [{0}] did not resolve to an IPv4 address.", ipEntry.HostName));

                sock.BeginConnect(new IPEndPoint(address, sockAR.port), new AsyncCallback(OnConnect), sockAR);
            }
            catch (Exception e)
            {
                sockAR.Notify(e);
            }
        }

        /// <summary>
        /// Initiates an asynchronous connection attempt to the specified network endpoint.
        /// </summary>
        /// <param name="remoteEP">The target endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginConnect(IPEndPoint remoteEP, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Touch();
            sock.LingerState = new LingerOption(LingerEnable, LingerTimeout);
            sock.BeginConnect(remoteEP, new AsyncCallback(OnConnect), sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles async connection completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnConnect(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, new AsyncCallback(OnConnect));
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            try
            {
                Touch();
                sock.EndConnect(ar);
                isOpen = true;
                sockAR.Notify();
            }
            catch (Exception e)
            {

                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes a pending async connection attempt.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginConnect(System.Net.IPEndPoint, System.AsyncCallback, object)" />.</param>
        public void EndConnect(IAsyncResult ar)
        {
            ((SockResult)ar).Finish();
        }

        //---------------------------------------------------------------------
        // Accept methods

        /// <summary>
        /// Blocks the current thread until an inbound connection is accepted on this listening socket.
        /// </summary>
        /// <returns>The accepted socketed.</returns>
        public EnhancedSocket Accept()
        {
            Socket sockAccepted;

            try
            {
                Verify();

                sockAccepted             = sock.Accept();
                sockAccepted.LingerState = new LingerOption(LingerEnable, LingerTimeout);

                return new EnhancedSocket(sockAccepted);
            }
            catch (Exception e)
            {

                throw TransformException(e);
            }
            finally
            {

                Touch();
            }
        }

        /// <summary>
        /// Begins an async operation to accept an inbound socket connection.
        /// </summary>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginAccept(AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Touch();
            sock.BeginAccept(onAccept, sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles async accept completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnAccept(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onAccept);
            if (ar == null)
                return;

            // Handle the completion

            SockResult  sockAR = (SockResult)ar.AsyncState;
            Socket      sockAccept;

            try
            {
                Touch();
                sockAccept = sock.EndAccept(ar);
                sockAccept.LingerState = new LingerOption(LingerEnable, LingerTimeout);
                sockAR.socket = new EnhancedSocket(sockAccept);
                sockAR.Notify();
            }
            catch (Exception e)
            {

                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes a pending accept request.
        /// </summary>
        /// <param name="ar">The async result.</param>
        /// <returns>The accepted socket.</returns>
        public EnhancedSocket EndAccept(IAsyncResult ar)
        {
            var sockAR = (SockResult)ar;

            sockAR.Wait();
            try
            {
                if (sockAR.Exception != null)
                    throw sockAR.Exception;

                return sockAR.socket;
            }
            finally
            {

                sockAR.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // Receive methods

        /// <summary>
        /// Initiates an asynchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Offset where received data is to be placed.</param>
        /// <param name="size">Number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            using (TimedLock.Lock(this))
            {

            retry:

                try
                {
                    sock.BeginReceive(buffer, offset, RecvSize(size), socketFlags, onReceive, sockAR);
                }
                catch (SocketException e)
                {
                    if (UdpReceiveRetry(e))
                        goto retry;

                    throw TransformException(e);
                }

                asyncRecvPending++;

                sockAR.Started();
                return sockAR;
            }
        }

        /// <summary>
        /// Handles async receive completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceive(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onReceive);
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            using (TimedLock.Lock(this))
            {
                asyncRecvPending--;
                Assertion.Test(asyncRecvPending >= 0);

                try
                {
                    Verify();
                    Touch();
                    sockAR.count = sock.EndReceive(ar);
                    sockAR.Notify();
                }
                catch (Exception e)
                {
                    sockAR.Notify(TransformException(e));
                }

                if (closePending && asyncRecvPending == 0)
                {
                    closePending = false;
                    sock.Close();
                }
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginReceive" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginReceive" />.</param>
        /// <returns>The number of bytes actually received.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// Under certain circumstances for UDP sockets with <see cref="IgnoreUdpConnectionReset"/>=<c>true</c>
        /// this method may return a received packet length of zero.  In particular, this can happen
        /// when the target host of a previuous <b>SentTo()</b> transmission actively rejected the
        /// packet by responding with an ICMP connection reset error.
        /// </para>
        /// <para>
        /// Most applications that set <see cref="IgnoreUdpConnectionReset"/>=<c>true</c> should
        /// just ignore this result and initiate another receive operation.
        /// </para>
        /// </note>
        /// </remarks>
        public int EndReceive(IAsyncResult ar)
        {
            var sockAR = (SockResult)ar;

            sockAR.Wait();
            try
            {
                if (sockAR.Exception != null)
                {
                    if (this.IgnoreUdpConnectionReset)
                    {
                        var sockException = sockAR.Exception as SocketException;

                        if (sockException != null && sockException.SocketErrorCode == SocketError.ConnectionReset)
                            return 0;
                    }

                    throw sockAR.Exception;
                }

                return sockAR.count;
            }
            finally
            {
                sockAR.Dispose();
            }
        }

        /// <summary>
        /// Synchronously receives the specified number of bytes, blocking until all
        /// of the bytes have been received.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Offset where received data is to be placed.</param>
        /// <param name="size">Number of bytes to receive.</param>
        /// <exception cref="SocketClosedException">Thrown if the remote side of the socket was closed.</exception>
        public void ReceiveAll(byte[] buffer, int offset, int size)
        {
            var ar = BeginReceiveAll(buffer, offset, size, SocketFlags.None, null, null);

            EndReceiveAll(ar);
        }

        /// <summary>
        /// Initiates an asynchronous receive operation that will not complete until
        /// all of the requested bytes have been received.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Offset where received data is to be placed.</param>
        /// <param name="size">Number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginReceiveAll(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            sockAR.socketFlags = socketFlags;
            if ((socketFlags & SocketFlags.Peek) != 0)
                throw new InvalidOperationException("SocketFlags.Peek cannot be used in BeginReceiveAll().");

            if (onRecvAllBuffer == null)
                onRecvAllBuffer = new AsyncCallback(OnReceiveAllBuffer);

            sockAR.recvBuf = buffer;
            sockAR.recvPos = offset;
            sockAR.cbRecv = size;

        retry:

            try
            {
                sock.BeginReceive(sockAR.recvBuf, sockAR.recvPos, RecvSize(sockAR.cbRecv), socketFlags, onRecvAllBuffer, sockAR);
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }

            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles async single buffer receive all completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceiveAllBuffer(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onRecvAllBuffer);
            if (ar == null)
                return;

            // Handle the completion

            SockResult  sockAR = (SockResult)ar.AsyncState;
            int         cb;

            try
            {
                Touch();

                cb = sock.EndReceive(ar);
                if (cb == 0)
                {
                    // The socket was closed by the remote endpoint without sending all of
                    // the expected data.  Handle this by throwing a [SocketClosedException].

                    sockAR.Notify(new SocketClosedException(SocketCloseReason.RemoteClose));
                    return;
                }

                sockAR.recvPos += cb;
                sockAR.cbRecv  -= cb;

                if (sockAR.cbRecv == 0)
                {
                    // We've got all of the data.

                    sockAR.Notify();
                }
                else
                {
                    // Still waiting for more data.

                    retry:

                    try
                    {
                        sock.BeginReceive(sockAR.recvBuf, sockAR.recvPos, RecvSize(sockAR.cbRecv), sockAR.socketFlags, onRecvAllBuffer, sockAR);
                    }
                    catch (SocketException e)
                    {
                        if (UdpReceiveRetry(e))
                            goto retry;

                        throw TransformException(e);
                    }
                }
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Initiates an asynchronous receive operation that will not complete until
        /// all of the requested bytes have been received.
        /// </summary>
        /// <param name="blocks">The receive block array.</param>
        /// <param name="size">Number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        /// <exception cref="SocketClosedException">Thrown if the remote side of the socket was closed.</exception>
        /// <remarks>
        /// <para>
        /// The data received on the stream will be appended to the 
        /// block array passed.  Data will be received in chunks whose
        /// size is determined by blocks.BlockSize.
        /// </para>
        /// <note>
        /// The block array will be truncated to the actual
        /// number of bytes received.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceiveAll(BlockArray blocks, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            sockAR.socketFlags = socketFlags;
            if ((socketFlags & SocketFlags.Peek) != 0)
                throw new InvalidOperationException("SocketFlags.Peek cannot be used in BeginReceiveAll().");

            if (sockAR.recvBuf != null)
                throw new InvalidOperationException("Another receive is already pending");

            if (onRecvAllBlocks == null)
                onRecvAllBlocks = new AsyncCallback(OnReceiveAllBlocks);

            sockAR.recvBlocks  = blocks;
            sockAR.cbRecv      = size < blocks.BlockSize ? size : blocks.BlockSize;
            sockAR.cbRecvTotal = size;
            sockAR.recvBuf     = new byte[sockAR.cbRecv];
            sockAR.recvPos     = 0;

            using (TimedLock.Lock(this))
            {
            retry:

                try
                {
                    sock.BeginReceive(sockAR.recvBuf, sockAR.recvPos, RecvSize(sockAR.cbRecv), socketFlags, onRecvAllBlocks, sockAR);
                }
                catch (SocketException e)
                {
                    if (UdpReceiveRetry(e))
                        goto retry;

                    throw TransformException(e);
                }

                asyncRecvPending++;

                sockAR.Started();
                return sockAR;
            }
        }

        /// <summary>
        /// Handles async block array receive all completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceiveAllBlocks(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onRecvAllBlocks);
            if (ar == null)
                return;

            // Handle the completion

            SockResult  sockAR = (SockResult)ar.AsyncState;
            int         cb;

            try
            {
                Touch();

                cb = sock.EndReceive(ar);
                if (cb == 0 && !sockAR.NotifyCalled)
                    sockAR.Notify(new SocketClosedException(SocketCloseReason.LocalClose));

                sockAR.recvPos     += cb;
                sockAR.cbRecv      -= cb;
                sockAR.cbRecvTotal -= cb;

                if (sockAR.cbRecv == 0)
                {
                    // We've completed reading the current block so add it to
                    // the block array.

                    sockAR.recvBlocks.Append(new Block(sockAR.recvBuf, 0, sockAR.recvBuf.Length));
                }
                else
                {
                    // Continue receiving the current block.

                    retry:

                    try
                    {
                        sock.BeginReceive(sockAR.recvBuf, sockAR.recvPos, RecvSize(sockAR.cbRecv), sockAR.socketFlags, onRecvAllBlocks, sockAR);
                    }
                    catch (SocketException e)
                    {
                        if (UdpReceiveRetry(e))
                            goto retry;

                        throw TransformException(e);
                    }

                    return;
                }

                if (cb == 0 || sockAR.cbRecvTotal == 0)
                {
                    // We're done

                    if (!sockAR.NotifyCalled)
                        sockAR.Notify();
                }
                else
                {
                    // Setup to receive another block

                    sockAR.cbRecv = sockAR.cbRecvTotal < sockAR.recvBlocks.BlockSize ? sockAR.cbRecvTotal : sockAR.recvBlocks.BlockSize;
                    sockAR.recvBuf = new byte[sockAR.cbRecv];
                    sockAR.recvPos = 0;

                retry:

                    try
                    {
                        sock.BeginReceive(sockAR.recvBuf, sockAR.recvPos, RecvSize(sockAR.cbRecv), sockAR.socketFlags, onRecvAllBlocks, sockAR);
                    }
                    catch (SocketException e)
                    {
                        if (UdpReceiveRetry(e))
                            goto retry;

                        throw TransformException(e);
                    }
                }
            }
            catch (Exception e)
            {

                if (!sockAR.NotifyCalled)
                    sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginReceiveAll(byte[], int, int, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginReceiveAll(byte[], int, int, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" />.</param>
        public void EndReceiveAll(IAsyncResult ar)
        {
            ((SockResult)ar).Finish();
        }

        /// <summary>
        /// Initiates an asynchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Offset where received data is to be placed.</param>
        /// <param name="size">Number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">Receives the remote network endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            using (TimedLock.Lock(this))
            {
                sockAR.EP = new IPEndPoint(IPAddress.Any, 0);

            retry:

                try
                {
                    sock.BeginReceiveFrom(buffer, offset, size, socketFlags, ref sockAR.EP, onReceiveFrom, sockAR);
                }
                catch (SocketException e)
                {
                    if (UdpReceiveRetry(e))
                        goto retry;

                    throw TransformException(e);
                }

                asyncRecvPending++;

                sockAR.Started();
                return sockAR;
            }
        }

        /// <summary>
        /// Completes asynchronous receive from operations.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceiveFrom(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onReceiveFrom);
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            using (TimedLock.Lock(this))
            {
                asyncRecvPending--;
                Assertion.Test(asyncRecvPending >= 0);

                try
                {
                    Touch();

                    sockAR.count = sock.EndReceiveFrom(ar, ref sockAR.EP);
                    sockAR.Notify();
                }
                catch (Exception e)
                {
                    sockAR.Notify(TransformException(e));
                }

                if (closePending && asyncRecvPending == 0)
                {
                    closePending = false;
                    sock.Close();
                }
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginReceiveFrom" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginReceiveFrom" />.</param>
        /// <param name="fromEP">Returns as the remote network endpoint.</param>
        /// <returns>The number of bytes actually received.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// Under certain circumstances for UDP sockets with <see cref="IgnoreUdpConnectionReset"/>=<c>true</c>
        /// this method may return a received packet length of zero.  In particular, this can happen
        /// when the target host of a previuous <b>SentTo()</b> transmission actively rejected the
        /// packet by responding with an ICMP connection reset error.
        /// </para>
        /// <para>
        /// Most applications that set <see cref="IgnoreUdpConnectionReset"/>=<c>true</c> should
        /// just ignore this result and initiate another receive operation.
        /// </para>
        /// </note>
        /// </remarks>
        public int EndReceiveFrom(IAsyncResult ar, ref EndPoint fromEP)
        {
            var sockAR = (SockResult)ar;

            sockAR.Wait();
            try
            {
                if (sockAR.Exception != null)
                {
                    if (this.IgnoreUdpConnectionReset)
                    {
                        var sockException = sockAR.Exception as SocketException;

                        if (sockException != null && sockException.SocketErrorCode == SocketError.ConnectionReset)
                            return 0;
                    }

                    throw sockAR.Exception;
                }

                fromEP = sockAR.EP;
                return sockAR.count;
            }
            finally
            {
                sockAR.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // Send methods

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The bytes to be sent.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int Send(byte[] buffer)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Send(buffer, SendSize(buffer.Length), SocketFlags.None);
#else
                return sock.Send(buffer);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int Send(byte[] buffer, SocketFlags socketFlags)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Send(buffer, SendSize(buffer.Length), socketFlags);
#else
                return sock.Send(buffer,socketFlags);
#endif
            }
            catch (Exception e)
            {

                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int Send(byte[] buffer, int size, SocketFlags socketFlags)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Send(buffer, SendSize(size), socketFlags);
#else
                return sock.Send(buffer,size,socketFlags);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">Index into the buffer where data transmission is to begin.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Send(buffer, offset, SendSize(size), socketFlags);
#else
                return sock.Send(buffer,offset,size,socketFlags);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="remoteEP">The remote network endpoint.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int SendTo(byte[] buffer, EndPoint remoteEP)
        {
            try
            {

                Verify();
                Touch();
#if DEBUG
                return sock.SendTo(buffer, SendSize(buffer.Length), SocketFlags.None, remoteEP);
#else
                return sock.SendTo(buffer,remoteEP);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The remote network endpoint.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int SendTo(byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.SendTo(buffer, SendSize(buffer.Length), socketFlags, remoteEP);
#else
                return sock.SendTo(buffer,socketFlags,remoteEP);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The remote network endpoint.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int SendTo(byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.SendTo(buffer, SendSize(size), socketFlags, remoteEP);
#else
                return sock.SendTo(buffer,size,socketFlags,remoteEP);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">Index into the buffer where data transmission is to begin.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The remote network endpoint.</param>
        /// <returns>The number of bytes actually sent.</returns>
        public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
        {
            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.SendTo(buffer, offset, SendSize(size), socketFlags, remoteEP);
#else
                return sock.SendTo(buffer,offset,size,socketFlags,remoteEP);
#endif
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Asynchronously transmits the data on the socket and then closes the socket when
        /// the transmission is complete.
        /// </summary>
        /// <param name="blocks">The block array holding the data.</param>
        /// <remarks>
        /// This method is useful in situations where an error message needs to
        /// be transmitted and then the socket closed without worrying whether
        /// the transmission was successful.
        /// </remarks>
        public void AsyncSendClose(BlockArray blocks)
        {
            // $todo(jeff.lill): 
            //
            // For now I'm just going to assemble the blocks
            // into one buffer and send it.

            Verify();
            Touch();

            AsyncSendClose(blocks.ToByteArray());
        }

        /// <summary>
        /// Asynchronously transmits the data on the socket and then closes the socket when
        /// the transmission is complete.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <remarks>
        /// This method is useful in situations where an error message needs to
        /// be transmitted and then the socket closed without worrying whether
        /// the transmission was successful.
        /// </remarks>
        public void AsyncSendClose(byte[] buffer)
        {
            try
            {
                Verify();
                Touch();

                AsyncSendClose(buffer, 0, buffer.Length, SocketFlags.None);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Asynchronously transmits the data on the socket and then closes the socket when
        /// the transmission is complete.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">Index into the buffer where data transmission is to begin.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <remarks>
        /// This method is useful in situations where an error message needs to
        /// be transmitted and then the socket closed without worrying whether
        /// the transmission was successful.
        /// </remarks>
        public void AsyncSendClose(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            try
            {
                Verify();
                Touch();

                BeginSendAll(buffer, offset, size, socketFlags, new AsyncCallback(OnAsyncSendClose), null);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Handles the closing of the socket after the data from an <see cref="AsyncSendClose(LillTek.Common.BlockArray)" />
        /// has been transmitted.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnAsyncSendClose(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, new AsyncCallback(OnAsyncSendClose));
            if (ar == null)
                return;

            // Handle the completion

            try
            {
                Touch();

                EndSendAll(ar);
                Shutdown(SocketShutdown.Send);
            }
            catch
            {

                Close();
            }
        }

        /// <summary>
        /// Initiates an asynchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">The offset of the first byte to send.</param>
        /// <param name="size">The number of bytes to transmit.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();
            sock.BeginSend(buffer, offset, SendSize(size), socketFlags, onSend, sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles asynchronous <see cref="BeginSend" /> operations.
        /// </summary>
        /// <param name="ar">The asynchronous result.</param>
        private void OnSend(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onSend);
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            try
            {
                Touch();

                sockAR.count = sock.EndSend(ar);
                sockAR.Notify();
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginSend" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginSend" />.</param>
        /// <returns>The number of bytes actually transmitted.</returns>
        public int EndSend(IAsyncResult ar)
        {
            var sockAR = (SockResult)ar;

            sockAR.Wait();
            try
            {
                if (sockAR.Exception != null)
                    throw sockAR.Exception;

                return sockAR.count;
            }
            finally
            {
                sockAR.Dispose();
            }
        }

        /// <summary>
        /// Initiates an asynchronous send operation that will not complete
        /// until all of the requested bytes have been transmitted.
        /// </summary>
        /// <param name="blocks">The data blocks to be transmitted.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        /// <remarks>
        /// This method sends the data from the blocks passed, defining what is essentially
        /// the gather behavior of scatter/gather APIs.  Although the current socket implementation
        /// doesn't yet call the underlying operating system's scatter/gather APIs, these APIs
        /// are provided so that applications can be written to take advantage of this when
        /// this functionality is implemented.
        /// </remarks>
        public IAsyncResult BeginSendAll(BlockArray blocks, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            // $todo(jeff.lill): 
            //
            // I need to come back and optimize this by either actually calling 
            // the underlying gather.

            // If there's not very much data then assemble it into
            // a single buffer to avoid making a bunch of unmanaged
            // socket calls.

            if (blocks.Size <= 512)
            {
                var buffer = blocks.ToByteArray();

                return BeginSendAll(buffer, 0, buffer.Length, socketFlags, callback, state);
            }

            // Initiate transmission of the first block

            SockResult  sockAR = new SockResult(this, callback, state);
            Block       block;

            Verify();
            Touch();

            if (sockAR.sendBuf != null)
                throw new InvalidOperationException("Another send is already pending.");

            if (onSendAllBlocks == null)
                onSendAllBlocks = new AsyncCallback(OnSendAllBlock);

            sockAR.socketFlags = socketFlags;

            sockAR.sendBlocks     = blocks;
            sockAR.sendBlockIndex = 0;
            block                 = sockAR.sendBlocks.GetBlock(0);
            sockAR.sendBuf        = block.Buffer;
            sockAR.sendPos        = block.Offset;
            sockAR.cbSend         = block.Length;

            sock.BeginSend(sockAR.sendBuf, sockAR.sendPos, SendSize(sockAR.cbSend), socketFlags, onSendAllBlocks, sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles asynchronous block <see cref="BeginSendAll(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> 
        /// operations.
        /// </summary>
        /// <param name="ar">The asynchronous result.</param>
        private void OnSendAllBlock(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onSendAllBlocks);
            if (ar == null)
                return;

            // Handle the completion

            var     sockAR = (SockResult)ar.AsyncState;
            int     cb;

            try
            {
                Touch();

                cb = sock.EndSend(ar);
                if (cb == 0)
                    sockAR.Notify(new SocketClosedException(SocketCloseReason.LocalClose));

                sockAR.sendPos += cb;
                sockAR.cbSend -= cb;

                if (sockAR.cbSend == 0)
                {
                    // We've completed the transmission of the current block.  Continue transmitting
                    // the next block if there are more.

                    sockAR.sendBlockIndex++;
                    if (sockAR.sendBlockIndex < sockAR.sendBlocks.Count)
                    {
                        var block = sockAR.sendBlocks.GetBlock(sockAR.sendBlockIndex);

                        sockAR.sendBuf = block.Buffer;
                        sockAR.sendPos = block.Offset;
                        sockAR.cbSend = block.Length;

                        sock.BeginSend(sockAR.sendBuf, sockAR.sendPos, SendSize(sockAR.cbSend), sockAR.socketFlags, onSendAllBlocks, sockAR);
                        return;
                    }

                    // We have transmitted all of the blocks.

                    sockAR.Notify();
                }
                else
                {
                    // Continue transmitting the current buffer

                    sock.BeginSend(sockAR.sendBuf, sockAR.sendPos, SendSize(sockAR.cbSend), sockAR.socketFlags, onSendAllBuffer, sockAR);
                }
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Transmits an entire byte array synchronously, blocking until all of the bytes
        /// have been transmitted. 
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        public void SendAll(byte[] buffer)
        {
            SendAll(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Transmits a section of a byte array synchronously.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">The offset of the first byte to send.</param>
        /// <param name="size">The number of bytes to transmit.</param>
        public void SendAll(byte[] buffer, int offset, int size)
        {
            var ar = BeginSendAll(buffer, offset, size, SocketFlags.None, null, null);

            EndSendAll(ar);
        }

        /// <summary>
        /// Initiates an asynchronous send operation that will not complete
        /// until all of the requested bytes have been transmitted.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">The offset of the first byte to send.</param>
        /// <param name="size">The number of bytes to transmit.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginSendAll(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            if (sockAR.sendBuf != null)
                throw new InvalidOperationException("Another send is already pending.");

            if (onSendAllBuffer == null)
                onSendAllBuffer = new AsyncCallback(OnSendAllBuffer);

            sockAR.socketFlags = socketFlags;

            sockAR.sendBuf = buffer;
            sockAR.sendPos = offset;
            sockAR.cbSend = size;

            sock.BeginSend(sockAR.sendBuf, sockAR.sendPos, SendSize(sockAR.cbSend), socketFlags, onSendAllBuffer, sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles asynchronous buffer <see cref="BeginSendAll(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> operations.
        /// </summary>
        /// <param name="ar">The asynchronous result.</param>
        private void OnSendAllBuffer(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onSendAllBuffer);
            if (ar == null)
                return;

            // Handle the completion

            var     sockAR = (SockResult)ar.AsyncState;
            int     cb;

            try
            {
                Touch();

                cb = sock.EndSend(ar);
                if (cb == 0)
                    sockAR.Notify(new SocketClosedException(SocketCloseReason.LocalClose));

                sockAR.sendPos += cb;
                sockAR.cbSend -= cb;

                if (sockAR.cbSend == 0)
                {
                    // We're done

                    sockAR.Notify();
                }
                else
                    sock.BeginSend(sockAR.sendBuf, sockAR.sendPos, SendSize(sockAR.cbSend), sockAR.socketFlags, onSendAllBuffer, sockAR);
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginSendAll(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginSendAll(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.AsyncCallback, object)" />.</param>
        public void EndSendAll(IAsyncResult ar)
        {
            ((SockResult)ar).Finish();
        }

        /// <summary>
        /// Initiates an asynchronous send operation.
        /// </summary>
        /// <param name="blocks">The data blocks to be transmitted.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The target network endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        /// <remarks>
        /// This method sends the data from the buffers passed, defining what is essentially
        /// the gather behavior of scatter/gather APIs.  Although the current socket implementation
        /// doesn't yet call the underlying operating system's scatter/gather APIs, these APIs
        /// are provided so that applications can be written to take advantage of this when
        /// this functionality is implemented.
        /// </remarks>
        public IAsyncResult BeginSendTo(BlockArray blocks, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state)
        {
            // $todo(jeff.lill): 
            //
            // For now I'm implementing this by assembling all of the buffers
            // into a large array and sending that.  I need to come back and
            // optimize this by either actually calling the underlying gather
            // operating APIs or by at least implementing a different callback
            // that sends data directly from the array buffers.
            //
            // One other thing.  Any optimized implementation should be smart
            // about avoiding too many calls to the underlying OS.  It might
            // be more efficient to actually assemble small messages and making
            // one OS call rather than doing multiple calls.

           var buffer = blocks.ToByteArray();

            return BeginSendTo(buffer, 0, buffer.Length, socketFlags, remoteEP, callback, state);
        }

        /// <summary>
        /// Initiates an asynchronous send operation.
        /// </summary>
        /// <param name="buffer">The send buffer.</param>
        /// <param name="offset">The offset of the first byte to send.</param>
        /// <param name="size">The number of bytes to transmit.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The target network endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        public IAsyncResult BeginSendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state)
        {
            var sockAR = new SockResult(this, callback, state);

            Verify();
            Touch();

            sock.BeginSendTo(buffer, offset, SendSize(size), socketFlags, remoteEP, onSendTo, sockAR);
            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles completion of a <see cref="BeginSendTo(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.Net.EndPoint, System.AsyncCallback, object)" /> operation.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnSendTo(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onSendTo);
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            try
            {
                Touch();

                sockAR.count = sock.EndSendTo(ar);
                sockAR.Notify();
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginSendTo(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.Net.EndPoint, System.AsyncCallback, object)" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginSendTo(LillTek.Common.BlockArray, System.Net.Sockets.SocketFlags, System.Net.EndPoint, System.AsyncCallback, object)" />.</param>
        /// <returns>The number of bytes actually transmitted.</returns>
        public int EndSendTo(IAsyncResult ar)
        {
            var sockAR = (SockResult)ar;

            sockAR.Wait();
            try
            {
                if (sockAR.Exception != null)
                    throw sockAR.Exception;

                return sockAR.count;
            }
            finally
            {
                sockAR.Dispose();
            }
        }

        /// <summary>
        /// Initiates an operation to send one or more packets of data 
        /// to a remote endpoint.
        /// </summary>
        /// <param name="blocks">The packets encoded into blocks.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">The target network endpoint.</param>
        /// <param name="callback">The delegate to call when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state.</param>
        /// <returns>The async result.</returns>
        /// <remarks>
        /// <para>
        /// Use this method to asynchronously send multiple packets on a
        /// connection-less socket.  Each block in the array passed will be
        /// transmitted as a separate packet.  The operation doesn't complete
        /// until all packets are transmitted.
        /// </para>
        /// <note>
        /// There must be at least one block in the array.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSendAllTo(BlockArray blocks, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state)
        {
            SockResult  sockAR = new SockResult(this, callback, state);
            Block       block;

            if (blocks.Count == 0)
                throw new SocketException();

            Touch();

            sockAR.sendBlocks     = blocks;
            sockAR.sendBlockIndex = 0;
            block                 = blocks.GetBlock(0);
            sockAR.socketFlags    = socketFlags;
            sockAR.EP             = remoteEP;

            if (onSendAllTo == null)
                onSendAllTo = new AsyncCallback(OnSendAllTo);

            sock.BeginSendTo(block.Buffer, block.Offset, block.Length, socketFlags, remoteEP, onSendAllTo, sockAR);

            sockAR.Started();
            return sockAR;
        }

        /// <summary>
        /// Handles completions from <see cref="BeginSendAllTo" />.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnSendAllTo(IAsyncResult ar)
        {
            // Queue operations that completed synchronously so that they'll
            // be dispatched on a different thread.

            ar = QueuedAsyncResult.QueueSynchronous(ar, onSendAllTo);
            if (ar == null)
                return;

            // Handle the completion

            var sockAR = (SockResult)ar.AsyncState;

            try
            {
                Touch();

                sock.EndSendTo(ar);

                // Initiate the transmission of the next block if there is one.

                sockAR.sendBlockIndex++;
                if (sockAR.sendBlockIndex < sockAR.sendBlocks.Count)
                {
                    var block = sockAR.sendBlocks.GetBlock(sockAR.sendBlockIndex);

                    sock.BeginSendTo(block.Buffer, block.Offset, block.Length, sockAR.socketFlags, sockAR.EP, onSendAllTo, sockAR);
                    return;
                }

                // Looks like we're done

                sockAR.Notify();
            }
            catch (Exception e)
            {
                sockAR.Notify(TransformException(e));
            }
        }

        /// <summary>
        /// Completes the <see cref="BeginSendAllTo" /> operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginSendAllTo" />.</param>
        public void EndSendAllTo(IAsyncResult ar)
        {
            ((SockResult)ar).Finish();
        }

        //---------------------------------------------------------------------
        // Receive methods

        /// <summary>
        /// Determines whether the exception passed should be ignored and the 
        /// receive operation retried.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <returns><c>true</c> if the operation should be retried.</returns>
        /// <remarks>
        /// This method returns <c>true</c> if this is a UDP socket, <see cref="IgnoreUdpConnectionReset" />
        /// is set to <c>true</c>, and the socket error is <see cref="SocketError.ConnectionReset" />.
        /// </remarks>
        private bool UdpReceiveRetry(SocketException e)
        {
            return sock.ProtocolType == ProtocolType.Udp &&
                   this.IgnoreUdpConnectionReset &&
                   e.ErrorCode == (int)SocketError.ConnectionReset;
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int Receive(byte[] buffer)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Receive(buffer, RecvSize(buffer.Length), SocketFlags.None);
#else
                return sock.Receive(buffer);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int Receive(byte[] buffer, SocketFlags socketFlags)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Receive(buffer, RecvSize(buffer.Length), socketFlags);
#else
                return sock.Receive(buffer,socketFlags);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int Receive(byte[] buffer, int size, SocketFlags socketFlags)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Receive(buffer, RecvSize(size), socketFlags);
#else
                return sock.Receive(buffer,size,socketFlags);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Index into the buffer where received data is to be placed.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.Receive(buffer, offset, RecvSize(size), socketFlags);
#else
                return sock.Receive(buffer,offset,size,socketFlags);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="remoteEP">Returns as the remote network endpoint.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.ReceiveFrom(buffer, RecvSize(buffer.Length), SocketFlags.None, ref remoteEP);
#else
                return sock.ReceiveFrom(buffer,ref remoteEP);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">Returns as the remote network endpoint.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.ReceiveFrom(buffer, RecvSize(buffer.Length), socketFlags, ref remoteEP);
#else
                return sock.ReceiveFrom(buffer,socketFlags,ref remoteEP);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">Returns as the remote network endpoint.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.ReceiveFrom(buffer, RecvSize(size), socketFlags, ref remoteEP);
#else
                return sock.ReceiveFrom(buffer,size,socketFlags,ref remoteEP);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
        }

        /// <summary>
        /// Implements a synchronous receive operation.
        /// </summary>
        /// <param name="buffer">The receive buffer.</param>
        /// <param name="offset">Index into the buffer where received data is to be placed.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <param name="remoteEP">Returns as the remote network endpoint.</param>
        /// <returns>The number of bytes actually received.</returns>
        public int ReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP)
        {
        retry:

            try
            {
                Verify();
                Touch();
#if DEBUG
                return sock.ReceiveFrom(buffer, offset, RecvSize(size), socketFlags, ref remoteEP);
#else
                return sock.ReceiveFrom(buffer,offset,size,socketFlags,ref remoteEP);
#endif
            }
            catch (SocketException e)
            {
                if (UdpReceiveRetry(e))
                    goto retry;

                throw TransformException(e);
            }
            catch (Exception e)
            {
                throw TransformException(e);
            }
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
