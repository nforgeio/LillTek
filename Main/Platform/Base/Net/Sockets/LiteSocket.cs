//-----------------------------------------------------------------------------
// FILE:        LiteSocket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simplified implementation of EnhancedSocket that is suitable
//              for client applications on all target platforms and devices 
//              including Desktop Windows, Silverlight, Windows Phone, iOS, 
//              and Android devices.
//
//              This file has the source code for the Windows Desktop and Mono 
//              based platforms.  The Silverlight/Windows Phone implementation
//              has the same interface but is implemented as separate source.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// A simplified implementation of EnhancedSocket that is suitable for client
    /// applications on all target platforms and devices including Desktop Windows, 
    /// Silverlight, Windows Phone, iOS, and Android devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class was added to the platform due to the fact that the Windows Phone
    /// <b>Socket</b> class has an entirely different programming model than the
    /// standard .NET and Mono socket implementations.  Rather than try to abstract
    /// these differences in the <b>EnhancedSocket</b> class, I decided to create
    /// <see cref="LiteSocket" /> instead, simplifying some of the functionality 
    /// as well.  This avoids the risk of breaking something in <b>EnhancedSocket</b>
    /// and also prunes a lot of functionality that won't map to anything Windows Phone
    /// supports.  This will make it easier to write cross platform code.
    /// </para>
    /// <para>
    /// <see cref="LiteSocket" /> supports both <b>TCP</b> and <b>unicast-UDP</b> communication
    /// mechanisms.  UDP broadcast and multicast are not currently supported.
    /// </para>
    /// <para><b><c>TCP Socket Communication</c></b></para>
    /// <para>
    /// Use the static <see cref="CreateTcp()" /> method to instantiate a TCP socket with 
    /// default buffer sizes or <see cref="CreateTcp(int,int)" /> to create a socket with
    /// customized buffer sizes.  To establish a connection to a remote endpoint call
    /// the synchronous <see cref="Connect" /> method or use the asynchronous <see cref="BeginConnect" />
    /// and <see cref="EndConnect" /> pattern.  The <see cref="NetworkBinding" /> passed to <see cref="Connect" />
    /// or <see cref="BeginConnect" /> will specify the IP address or host name as well as
    /// the port number of the remote endpoint.
    /// </para>
    /// <note>
    /// The <see cref="Connect" /> and <see cref="BeginConnect" /> methods' <b>packet</b> parameter is
    /// ignored for TCP sockets.  This parameter is relevant only for UDP sockets.
    /// </note>
    /// <note>
    /// By default, Nagle delay is enabled on TCP sockets.  set <b>NoDelay</b>=<c>true</c> to 
    /// disable this if desired, before establising a connection.
    /// </note>
    /// <para>
    /// Once a connection has been established, you can use <see cref="Send" /> or 
    /// <see cref="BeginSend" />/<see cref="EndSend" /> to transmit data to the remote endpoint 
    /// and <see cref="ReceiveAll" /> or <see cref="BeginReceiveAll"/> and <see cref="EndReceiveAll" />
    /// to receive a block of data of a given size or use the <see cref="Receive" /> or
    /// <see cref="BeginReceive" />/<see cref="EndReceive" /> variants that return the next
    /// received chunk of data.
    /// </para>
    /// <note>
    /// Only a single send and/or receive request may be pending at any time for a TCP socket.
    /// </note>
    /// <para>
    /// You'll want to call <see cref="Shutdown" /> and <see cref="Close" /> or <see cref="ShutdownAndClose()" />
    /// to gracefully close an established connection once you're finished communicating.  <see cref="Close" />
    /// should be called though, to ensure that unmanaged resources are properly released.
    /// </para>
    /// <para><b><c>UDP Socket Communication</c></b></para>
    /// <para>
    /// Use the static <see cref="CreateUdp()" /> method to instantiate a UDP socket with 
    /// default buffer sizes or <see cref="CreateUdp(int,int)" /> to create a socket with
    /// customized buffer sizes.  To establish a <i>virtual connection</i> to a remote endpoint call
    /// the synchronous <see cref="Connect" /> method or use the asynchronous <see cref="BeginConnect" />
    /// and <see cref="EndConnect" /> pattern.  The <see cref="NetworkBinding" /> passed to <see cref="Connect" />
    /// or <see cref="BeginConnect" /> will specify the IP address or host name as well as
    /// the port number of the remote endpoint.
    /// </para>
    /// <para>
    /// Although UDP is by nature connectionless, the <see cref="LiteSocket" /> programming model
    /// currently supports communication with a single remote endpoint.  This is called a
    /// <i>virtual UDP connection</i>.  The <see cref="Connect" /> and <see cref="BeginConnect" />/<see cref="EndConnect" />
    /// methods establish the remote endpoint and this is an async pattern due to the possibility of
    /// needing to perform a DNS resolution.
    /// </para>
    /// <note>
    /// Due to quirks in how sockets work in Silverlight and Windows Phone, a packet of data must be
    /// transmitted when establishing a UDP connection.  This passed as a byte array in the
    /// <b>packet</b> parameter.
    /// </note>
    /// <para>
    /// Data transmitted between the local and remote endpoint are essentially packets of bytes.
    /// You'll use synchronous <see cref="Send" /> or the asynchronous <see cref="BeginSend" />/<see cref="EndSend" />
    /// pattern to transmit a packet to the remote endpoint and <see cref="Receive" /> or 
    /// <see cref="BeginReceive" />/<see cref="EndReceive" /> methods to receive a packet.
    /// <see cref="ReceiveAll" />, <see cref="BeginReceiveAll" /> and <see cref="EndReceiveAll" /> 
    /// are not supported for UDP sockets.
    /// </para>
    /// <note>
    /// The socket will not actually receive any packets until the first packet has been transmitted
    /// to the remote endpoint.  This is due to the nature of Desktop Silverlight and Windows Phones
    /// where I believe the security policy process is not actually initiated until the first transmission
    /// happens.  It is safe though to call <see cref="Receive" /> or <see cref="BeginReceive" /> before
    /// the first transmission for a <see cref="LiteSocket" /> even though this would cause an exception 
    /// from the base Silverlight <see cref="Socket" />.  <see cref="LiteSocket" /> will defer calling
    /// the underlying receive method until the first packet is transmitted.
    /// </note>
    /// <note>
    /// UDP sockets support multiple pending send requests but only a single receive may be pending at
    /// an one time.
    /// </note>
    /// <para>
    /// Applications should call <see cref="Close" /> when they're finished communication with a UDP
    /// socket to ensure that any resources are released.  <see cref="Shutdown" /> is a NOP and 
    /// may be called but will be ignored.
    /// </para>
    /// <note>
    /// The <see cref="IAsyncResult" /> instances returned by the various <b>BeginXXX()</b> asynchronous
    /// methods should not be referenced again after being passed to the matching <b>EndXXX()</b> method.
    /// The async result should be considered to have been disposed or released at this point.  The reason
    /// for this is that some <see cref="LiteSocket" /> implementations maintain a pool of <see cref="IAsyncResult" />
    /// instances that may be reused after operations complete.  This shouldn't be a big issue, since it
    /// really doesn't make much sense to hold a reference to an async result after operations complete.
    /// </note>
    /// <b><u>Silverlight Implementation Notes</u></b>
    /// <para>
    /// The Silverlight platform (not including Windows Phone) has a few quirks.  First, <b>Silverlight does
    /// not support UDP sockets</b>.  Technically, Silverlight does support multicast UDP on the local
    /// network for trusted applications, but general support over the Internet is disabled.
    /// </para>
    /// <para>
    /// Silverlight TCP connections are limited ports within the range of <b>4502-4534</b> for non-trusted
    /// applications and Silverlight will also require that the server deliver a <b>clientaccesspolicy.xml</b>
    /// file that grants access for the application to establish a TCP connection.  Silverlight will obtain
    /// this file using HTTP port 80 or TCP port 943 depending on the global <see cref="ClientAccessPolicyProtocol" />
    /// setting (which defaults to HTTP).
    /// </para>
    /// <example>
    /// <![CDATA[
    /// <?xml version="1.0" encoding="utf-8"?>
    /// <access-policy>
    ///     <cross-domain-access>
    ///         <allow-from http-request-headers="*">
    ///             <domain uri="*" />
    ///             <domain uri="http://*" />
    ///             <domain uri="file:///" />
    ///         </allow-from>
    ///         <grant-to>
    ///             <resource path="/" include-subpaths="true" />
    ///             <socket-resource port="4502-4534" protocol="tcp"/>
    ///         </grant-to>
    ///     </cross-domain-access>
    /// </access-policy>
    /// ]]>
    /// </example>
    /// <para>
    /// For more information on Silverlight client access policies see the
    /// <a href="http://msdn.microsoft.com/en-us/library/cc645032(v=vs.95).aspx">MSDN Documentation</a>.
    /// </para>
    /// </remarks>
    public class LiteSocket : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a TCP socket with default buffer sizes.
        /// </summary>
        /// <returns>The created socket.</returns>
        public static LiteSocket CreateTcp()
        {
            return CreateTcp(0, 0);
        }

        /// <summary>
        /// Creates a TCP socket with specified buffer size.
        /// </summary>
        /// <param name="sendBufferSize">The send buffer size in bytes or zero to use the platform default size.</param>
        /// <param name="receiveBufferSize">The receive buffer size in bytes or zero to use the platform default size.</param>
        /// <returns>The created socket.</returns>
        public static LiteSocket CreateTcp(int sendBufferSize, int receiveBufferSize)
        {
            return new LiteSocket(true);
        }

        /// <summary>
        /// Creates a UDP socket with default buffer sizes.
        /// </summary>
        /// <returns>The created socket.</returns>
        /// <exception cref="NotSupportedException">Thrown for Silverlight applications.</exception>
        /// <remarks>
        /// <note>
        /// UDP is not supported on the Silverlight platform.
        /// </note>
        /// </remarks>
        public static LiteSocket CreateUdp()
        {
            return CreateUdp(0, 0);
        }

        /// <summary>
        /// Creates a UDP socket with specified buffer size.
        /// </summary>
        /// <param name="sendBufferSize">The send buffer size in bytes or zero to use the platform default size.</param>
        /// <param name="receiveBufferSize">The receive buffer size in bytes or zero to use the platform default size.</param>
        /// <returns>The created socket.</returns>
        /// <exception cref="NotSupportedException">Thrown for Silverlight applications.</exception>
        /// <remarks>
        /// <note>
        /// UDP is not supported on the Silverlight platform.
        /// </note>
        /// </remarks>
        public static LiteSocket CreateUdp(int sendBufferSize, int receiveBufferSize)
        {
            return new LiteSocket(false);
        }

        /// <summary>
        /// Used by Silverlight applications to specify the protocol (HTTP or TCP) that the platform will use for verifying
        /// security access before allowing <see cref="LiteSocket" /> connections.  This defaults to
        /// <see cref="LillTek.Net.Sockets.ClientAccessPolicyProtocol.Http" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the application requires the TCP protocol, then this global must be set before attempting
        /// to connect a socket.
        /// </para>
        /// <note>
        /// This setting is used only for applications running on the Silverlight platform.  This is
        /// ignored for all other platforms including Windows Phone.
        /// </note>
        /// </remarks>
        static ClientAccessPolicyProtocol ClientAccessPolicyProtocol { get; set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static LiteSocket()
        {
            ClientAccessPolicyProtocol = ClientAccessPolicyProtocol.Http;
        }

        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock = new object();
        private EnhancedSocket  sock;                   // Base socket
        private bool            isTcp;                  // True if TCP, false for UDP
        private bool            tcpSendPending;         // True if an async (TCP) send is in-progress
        private bool            receivePending;         // True if an async receive is in-progress
        private NetworkBinding  remoteBinding;          // Remote endpoint binding
        private bool            isUdpConnected;         // True if virtual UDP connection established
        private IPEndPoint      udpRemoteEndPoint;      // Resolved UDP remote endpoint
        private byte[]          udpConnectPacket;       // Initial UDP connection packet or null

        /// <summary>
        /// Constructs an TCP or UDP socket based on the parameter passed.
        /// </summary>
        /// <param name="isTcp">Pass <c>true</c> for a TCP socket, <c>false</c> for a UDP socket.</param>
        private LiteSocket(bool isTcp)
        {
            this.isTcp = isTcp;

            if (isTcp)
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            else
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        /// <summary>
        /// Gracefully stops communication on the socket in one or both directions.
        /// </summary>
        /// <param name="how">Describes which directions of communication are to be stopped.</param>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already shut down or closed.
        /// </note>
        /// </remarks>
        public void Shutdown(SocketShutdown how)
        {
            lock (syncLock)
            {
                if (isTcp)
                    sock.Shutdown(how);
            }
        }

        /// <summary>
        /// Gracefully stops communication on the socket and then closes it.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already shut down or closed.
        /// </note>
        /// </remarks>
        public void ShutdownAndClose()
        {
            lock (syncLock)
            {
                if (isTcp)
                    sock.ShutdownAndClose();
                else
                    sock.Close();
            }
        }

        /// <summary>
        /// Gracefully stops communication on the socket and then closes it after waiting 
        /// up to a specified time limit to transmit any buffered data.
        /// </summary>
        /// <param name="lingerTime">The maximum time to delay closing of the socket will transmitting any buffered data.</param>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already closed.
        /// </note>
        /// </remarks>
        public void ShutdownAndClose(TimeSpan lingerTime)
        {
            lock (syncLock)
            {
                if (isTcp)
                {
                    if (lingerTime != TimeSpan.Zero)
                        sock.LingerState = new LingerOption(true, (int)lingerTime.TotalSeconds);

                    sock.ShutdownAndClose();
                }
                else
                    Close();
            }
        }

        /// <summary>
        /// Closes the socket if it is open.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is safe to call when the socket is already closed.
        /// </note>
        /// </remarks>
        public void Close()
        {
            sock.Close();
        }

        /// <summary>
        /// Releases all resources associated with the socket.
        /// </summary>
        /// <remarks>
        /// This is equivalent to calling <see cref="Close" />.
        /// </remarks>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Determines whether the socket uses the Nagle algorthm to combine data packets
        /// to reduce network overhead for TCP sockets in sume circumstances (ignored for
        /// UDP sockets).
        /// </summary>
        public bool NoDelay
        {
            get
            {
                if (isTcp)
                    return sock.NoDelay;
                else
                    return true;
            }

            set
            {
                if (isTcp)
                    sock.NoDelay = value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the socket is connected.
        /// </summary>
        public bool Connected
        {
            get
            {
                if (isTcp)
                    return sock.Connected;
                else
                    return isUdpConnected;
            }
        }

        /// <summary>
        /// The time-to-live value to be used for Internet Protocol (IP) packets
        /// transmitted by this socket.
        /// </summary>
        public short TTL
        {
            get { return sock.TTL; }
            set { sock.TTL = value; }
        }

        /// <summary>
        /// Return's the socket's network protocol type.
        /// </summary>
        public ProtocolType ProtocolType
        {
            get { return sock.ProtocolType; }
        }

        /// <summary>
        /// Returns the network endpoint of the remote side of the connection or <c>null</c>
        /// if the socket is closed or not connected.
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                if (Connected)
                    return (IPEndPoint)sock.RemoteEndPoint;
                else
                    return null;
            }
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
        public bool IgnoreUdpConnectionReset
        {
            get { return sock.IgnoreUdpConnectionReset; }
            set { sock.IgnoreUdpConnectionReset = value; }
        }

        /// <summary>
        /// Returns a unique hash code for this instance.
        /// </summary>
        /// <returns>A unique hash code.</returns>
        public override int GetHashCode()
        {
            return sock.GetHashCode();
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed matches this instance.
        /// </summary>
        /// <param name="o">The object to test.</param>
        /// <returns><c>true</c> if there's a match.</returns>
        public override bool Equals(object o)
        {
            var test = o as LiteSocket;

            if (test == null)
                return false;

            return test.GetHashCode() == this.GetHashCode();
        }

        /// <summary>
        /// Initiates an asynchronous operation to establish a connection with a remote endpoint.
        /// </summary>
        /// <param name="remoteBinding">Specifies the remote endpoint.</param>
        /// <param name="packet">The packet to be transmitted for UDP sockets, ignored for TCP.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>
        /// An <see cref="IAsyncResult" /> instance to be used to track the progress of the 
        /// operation and to eventually to be passed to the <see cref="EndConnect" /> method.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is already been connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="remoteBinding" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown for UDP sockets when <paramref name="packet" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packet" /> is empty.</exception>
        /// <remarks>
        /// <para>
        /// Due to quirks in how UDP sockets work in Silverlight and Windows Phone, a packet must be
        /// transmitted to remote endpoint as part of establishing a connection.  Use the <paramref name="packet" />
        /// parameter to pass the non-empty array of bytes to be transmitted.  This parameter is ignored
        /// for TCP connections.
        /// </para>
        /// <note>
        /// All successful calls to <see cref="BeginConnect" /> should be eventually matched with a call to <see cref="EndConnect" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginConnect(NetworkBinding remoteBinding, byte[] packet, AsyncCallback callback, object state)
        {
            if (remoteBinding == null)
                throw new ArgumentNullException("remoteBinding");

            lock (syncLock)
            {
                this.remoteBinding = remoteBinding;

                if (isTcp)
                    return sock.BeginConnect(remoteBinding, callback, state);
                else
                {

                    if (isUdpConnected)
                        throw new InvalidOperationException("Socket is already connected.");

                    const string packetError = "[LiteSocket] requires a valid [packet] when connecting a UDP socket.";

                    if (packet == null)
                        throw new ArgumentNullException("packet", packetError);

                    if (packet.Length == 0)
                        throw new ArgumentNullException("packet", packetError);

                    udpConnectPacket = packet;

                    return Dns.BeginGetHostAddresses(remoteBinding.HostOrAddress, callback, state);
                }
            }
        }

        /// <summary>
        /// Completes a pending asynchronous socket connection attempt.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating call to <see cref="BeginConnect" />.</param>
        /// <exception cref="SocketException">Thrown if the connection could not be establised.</exception>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginConnect" /> should be eventually matched with a call to <see cref="EndConnect" />.
        /// </note>
        /// </remarks>
        public void EndConnect(IAsyncResult ar)
        {
            lock (syncLock)
            {
                if (isTcp)
                    sock.EndConnect(ar);
                else
                {
                    var addresses = Dns.EndGetHostAddresses(ar);

                    sock.Bind();

                    udpRemoteEndPoint = new IPEndPoint(addresses[0], remoteBinding.Port);
                    isUdpConnected = true;

                    // $hack(jeff.lill)
                    //
                    // This is a minor hack.  Instead of adding the additional complexity of transmitting the
                    // connection packet asynchronously, I'm just going to make a synchronous call here.  This
                    // shouldn't ever block in real life since the socket send buffer starts out empty.

                    sock.SendTo(udpConnectPacket, udpRemoteEndPoint);

                    udpConnectPacket = null;   // Don't need this any longer
                }
            }
        }

        /// <summary>
        /// Synchronously establishes a connection to a remote network endpoint.
        /// </summary>
        /// <param name="remoteBinding">Specifies the remote endpoint.</param>
        /// <param name="packet">The packet to be transmitted for UDP sockets, ignored for TCP.</param>
        /// <exception cref="InvalidOperationException">Thrown if the socket is already been connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="remoteBinding" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown for UDP sockets when <paramref name="packet" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packet" /> is empty.</exception>
        /// <exception cref="SocketException">Thrown if the connection could not be establised.</exception>
        /// <remarks>
        /// <para>
        /// Due to quirks in how UDP sockets work in Silverlight and Windows Phone, a packet must be
        /// transmitted to remote endpoint as part of establishing a connection.  Use the <paramref name="packet" />
        /// parameter to pass the non-empty array of bytes to be transmitted.  This parameter is ignored
        /// for TCP connections.
        /// </para>
        /// </remarks>
        public void Connect(NetworkBinding remoteBinding, byte[] packet)
        {
            EndConnect(BeginConnect(remoteBinding, packet, null, null));
        }

        /// <summary>
        /// Initiates the transmission of bytes from a buffer to the remote side of the connection.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Index of the first byte to be transmitted.</param>
        /// <param name="count">Number of bytes to be transmitted.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>
        /// An <see cref="IAsyncResult" /> instance to be used to track the progress of the 
        /// operation and to eventually be passed to the <see cref="EndSend" /> method.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected or if another send operation is already pending for TCP connections.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <remarks>
        /// <note>
        /// Only one send operation may be outstanding at a time for any TCP connections.  Multiple sends
        /// may be in progress for UDP connections.
        /// </note>
        /// <note>
        /// For TCP connections, the send operation will continue until all bytes have been transmitted to the
        /// remote endpoint.  For UDP connections, only a single packet with as many bytes as may be delivered
        /// by the underlying infrastructure will be send.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginSend" /> must eventually be followed by a call to <see cref="EndSend" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0 || offset + count > buffer.Length + 1)
                throw new IndexOutOfRangeException(string.Format("[LiteSocket.Send: offset={0}] and [count={1}] is not valid for buffer of length [{2}].", offset, count, buffer.Length));

            lock (syncLock)
            {
                if (isTcp)
                {
                    if (!Connected)
                        throw new InvalidOperationException("Socket is not connected.");

                    if (tcpSendPending)
                        throw new InvalidOperationException("LiteSocket.Send: Another send operation is already pending on this TCP socket.");

                    try
                    {
                        tcpSendPending = true;

                        return sock.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
                    }
                    catch
                    {
                        tcpSendPending = false;
                        throw;
                    }
                }
                else
                    return sock.BeginSendTo(buffer, offset, count, SocketFlags.None, udpRemoteEndPoint, callback, state);
            }
        }

        /// <summary>
        /// Completes the asynchronous transmission of bytes to the remote side of the connection.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the call to <see cref="BeginSend" /> that initiated the transmission.</param>
        /// <exception cref="SocketException">Thrown if the transmission failed.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginSend" /> must eventually be followed by a call to <see cref="EndSend" />.
        /// </note>
        /// </remarks>
        public void EndSend(IAsyncResult ar)
        {
            lock (syncLock)
            {
                if (isTcp)
                {
                    try
                    {
                        sock.EndSend(ar);
                    }
                    finally
                    {
                        tcpSendPending = false;
                    }
                }
                else
                    sock.EndSendTo(ar);
            }
        }

        /// <summary>
        /// Initiates the transmission of bytes from a buffer to the remote side of the connection.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Index of the first byte to be transmitted.</param>
        /// <param name="count">Number of bytes to be transmitted.</param>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected or if another send operation is already pending for TCP connections.</exception>
        /// <exception cref="SocketException">Thrown if the transmission failed.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <remarks>
        /// <note>
        /// Only one send operation may be outstanding at a time for any TCP connections.  Multiple sends
        /// may be in progress for UDP connections.
        /// </note>
        /// <note>
        /// For TCP connections, the send operation will continue until all bytes have been transmitted to the
        /// remote endpoint.  For UDP connections, only a single packet with as many bytes as may be delivered
        /// by the underlying infrastructure will be send.
        /// </note>
        /// </remarks>
        public void Send(byte[] buffer, int offset, int count)
        {
            EndSend(BeginSend(buffer, offset, count, null, null));
        }

        /// <summary>
        /// Initiates reception the next chunk of received data up to a specified maxinum number of bytes.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Index where the first received byte is to be written.</param>
        /// <param name="count">Number of bytes to be received.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>
        /// An <see cref="IAsyncResult" /> instance to be used to track the progress of the 
        /// operation and to eventually be passed to the <see cref="EndReceive" /> method.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected or if another receive operation is already pending.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <remarks>
        /// <note>
        /// Only one receive operation may be pending per socket for both TCP and UDP connections.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginReceive" /> must eventually be followed by a call to <see cref="EndReceive" />.
        /// </note>
        /// <note>
        /// For TCP connections, the operation will be considered to have completed when some number of bytes
        /// between one and the specified <paramref name="count" /> have been received from the remote endpoint
        /// or the remote endpoint has gracefully closed the connection.  For UDP connections, the operation will
        /// be considered to be complete when the next packet is received, regardless of size.  Note that the
        /// buffer size specified by <paramref name="count" /> must be large enough to hold the next received
        /// UDP packet.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0 || offset + count > buffer.Length + 1)
                throw new IndexOutOfRangeException(string.Format("[LiteSocket.Send: offset={0}] and [count={1}] is not valid for buffer of length [{2}].", offset, count, buffer.Length));

            lock (syncLock)
            {
                if (!Connected)
                    throw new InvalidOperationException("Socket is not connected.");

                if (receivePending)
                    throw new InvalidOperationException("LiteSocket.Receive: Another receive operation is already pending on this socket.");

                try
                {
                    receivePending = true;

                    return sock.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
                }
                catch
                {
                    receivePending = false;
                    throw;
                }
            }
        }

        /// <summary>
        /// Completes the reception of bytes from the remote endpoint.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginReceive" /> call.</param>
        /// <returns>The number of bytes received or zero if the remote endpoint has gracefully closed the connection (for TCP connections).</returns>
        /// <exception cref="SocketException">Thrown if the was an network error.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <remarks>
        /// <note>
        /// For TCP connections, the operation will be considered to have completed when some number of bytes
        /// between one and the specified <b>count</b> have been received from the remote endpoint
        /// or the remote endpoint has gracefully closed the connection.  For UDP connections, the operation will
        /// be considered to be complete when the next packet is received, regardless of size.  Note that the
        /// buffer size specified by <b>count</b> must be large enough to hold the next received
        /// UDP packet.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginReceive" /> must eventually be followed by a call to <see cref="EndReceive" />.
        /// </note>
        /// </remarks>
        public int EndReceive(IAsyncResult ar)
        {
            lock (syncLock)
            {
                try
                {
                    return sock.EndReceive(ar);
                }
                finally
                {
                    receivePending = false;
                }
            }
        }

        /// <summary>
        /// Receives the next chunk of received data up to a specified maxinum number of bytes.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Index where the first received byte is to be written.</param>
        /// <param name="count">Number of bytes to be received.</param>
        /// <returns>The number of bytes received or zero if the remote endpoint has gracefully closed the connection (for TCP connections).</returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected or if another receive operation is already pending.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <exception cref="SocketException">Thrown if the was an network error.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <remarks>
        /// <note>
        /// Only one receive operation may be pending per socket for both TCP and UDP connections.
        /// </note>
        /// <note>
        /// For TCP connections, the operation will be considered to have completed when some number of bytes
        /// between one and the specified <paramref name="count" /> have been received from the remote endpoint
        /// or the remote endpoint has gracefully closed the connection.  For UDP connections, the operation will
        /// be considered to be complete when the next packet is received, regardless of size.  Note that the
        /// buffer size specified by <paramref name="count" /> must be large enough to hold the next received
        /// UDP packet.
        /// </note>
        /// </remarks>
        public int Receive(byte[] buffer, int offset, int count)
        {
            return EndReceive(BeginReceive(buffer, offset, count, null, null));
        }

        /// <summary>
        /// Initiates the complete reception of bytes from the remote endpoint over a TCP connection.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Index where the first received byte is to be written.</param>
        /// <param name="count">Number of bytes to be received.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>
        /// An <see cref="IAsyncResult" /> instance to be used to track the progress of the 
        /// operation and to eventually be passed to the <see cref="EndReceive" /> method.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected, if it is a UDP connection or if another receive operation is already pending.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <remarks>
        /// <note>
        /// This method is available only for TCP connections and only one receive operation may be pending per socket.
        /// </note>
        /// <note>
        /// The operation will be considered to have completed only when the specified number of bytes have been 
        /// received from the remote endpoint.  This method will never return partial results.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginReceiveAll" /> must eventually be followed by a call to <see cref="EndReceiveAll" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginReceiveAll(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0 || offset + count > buffer.Length + 1)
                throw new IndexOutOfRangeException(string.Format("[LiteSocket.Send: offset={0}] and [count={1}] is not valid for buffer of length [{2}].", offset, count, buffer.Length));

            lock (syncLock)
            {
                if (!Connected)
                    throw new InvalidOperationException("Socket is not connected.");

                if (!isTcp)
                    throw new InvalidOperationException("[LiteSocket.BeginReceiveAll] is not supported on UDP connections.");

                if (receivePending)
                    throw new InvalidOperationException("LiteSocket.ReceiveAll: Another receive operation is already pending on this socket.");

                receivePending = true;

                try
                {
                    return sock.BeginReceiveAll(buffer, offset, count, SocketFlags.None, callback, state);
                }
                catch
                {
                    receivePending = false;
                    throw;
                }
            }
        }

        /// <summary>
        /// Completes the reception of bytes on a TCP connection.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginReceiveAll" /> call.</param>
        /// <exception cref="SocketException">Thrown if the was an network error.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginReceiveAll" /> must eventually be followed by a call to <see cref="EndReceiveAll" />.
        /// </note>
        /// </remarks>
        public void EndReceiveAll(IAsyncResult ar)
        {
            lock (syncLock)
            {
                if (!isTcp)
                    throw new InvalidOperationException("[LiteSocket.EndReceiveAll] is not supported on UDP connections.");

                try
                {
                    sock.EndReceiveAll(ar);
                }
                finally
                {
                    receivePending = false;
                }
            }
        }

        /// <summary>
        /// Receives the specified number of bytes complete reception of bytes from the remote endpoint over a TCP connection.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">Index where the first received byte is to be written.</param>
        /// <param name="count">Number of bytes to be received.</param>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected, if it is a UDP connection or if another receive operation is already pending.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer" /> is <c>null</c>.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="offset" /> and <paramref name="count" /> specify bytes outside of the <paramref name="buffer" />.</exception>
        /// <exception cref="SocketException">Thrown if the was an network error.</exception>
        /// <exception cref="SocketClosedException">Thrown if the socket has been closed.</exception>
        /// <remarks>
        /// <note>
        /// This method is available only for TCP connections and only one receive operation may be pending per socket.
        /// </note>
        /// <note>
        /// The operation will be considered to have completed only when the specified number of bytes have been 
        /// received from the remote endpoint.  This method will never return partial results.
        /// </note>
        /// </remarks>
        public void ReceiveAll(byte[] buffer, int offset, int count)
        {
            EndReceiveAll(BeginReceiveAll(buffer, offset, count, null, null));
        }
    }
}
