//-----------------------------------------------------------------------------
// FILE:        DnsServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a simple DNS server component that can be used
//              for implementing DNS enabled services.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// The delegate to be used to define <see cref="DnsServer" /> event callbacks.
    /// </summary>
    /// <param name="server">The <see cref="DnsServer" /> that raised the event.</param>
    /// <param name="args">
    /// A <see cref="DnsServerEventArgs" /> instance holding the <see cref="DnsRequest" />
    /// message and as well as a field to pass a <see cref="DnsResponse" /> message
    /// back to the server.
    /// </param>
    public delegate void DnsServerDelegate(DnsServer server, DnsServerEventArgs args);

    /// <summary>
    /// The event arguments passed by <see cref="DnsServer" /> to its event handlers.
    /// </summary>
    public sealed class DnsServerEventArgs
    {
        /// <summary>
        /// The <see cref="IPEndPoint" /> of the computer that sent the request.
        /// </summary>
        public IPEndPoint RemoteEP;

        /// <summary>
        /// This will be initialized as the <see cref="DnsRequest" /> received by
        /// the server.
        /// </summary>
        public DnsRequest Request;

        /// <summary>
        /// This will be initialized to <c>null</c>.  Event handlers that wish to
        /// reply with a <see cref="DnsResponse" /> must set this to a valid 
        /// instance before returning to the <see cref="DnsServer" />.
        /// </summary>
        public DnsResponse Response;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="remoteEP">The <see cref="IPEndPoint" /> of the DNS resolver making the request.</param>
        /// <param name="request">The received <see cref="DnsRequest" />.</param>
        internal DnsServerEventArgs(IPEndPoint remoteEP, DnsRequest request)
        {
            this.RemoteEP = remoteEP;
            this.Request  = request;
            this.Response = null;
        }
    }

    /// <summary>
    /// Implements a simple DNS server component that can be used
    /// for implementing DNS enabled services using the protocol
    /// defined by <a href="http://www.ietf.org/rfc/rfc1035.txt?number=1035">RFC-1035</a>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To use this class, instantiate an instance, add an event handler
    /// to the <see cref="RequestEvent" /> and then call <see cref="Start" />,
    /// passing a <see cref="DnsServerSettings" /> instance with the
    /// configuration settings.  When you're done with the server,
    /// call <see cref="Stop" />.
    /// </para>
    /// <para>
    /// The <see cref="DnsServer" /> will raise the <see cref="RequestEvent" />
    /// whenever a <see cref="DnsRequest" /> is received, passing a 
    /// <see cref="DnsServerEventArgs" /> instance which includes the
    /// DNS request.  To reply to the request, the event handler needs
    /// to set the <c>DnsServerEventArgs.Reponse</c> property.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class DnsServer
    {
        private object          syncLock = new object();
        private EnhancedSocket  sock;       // The server socket
        private AsyncCallback   onReceive;  // Receive delegate
        private byte[]          recvBuf;    // The receive buffer
        private EndPoint        remoteEP;

        /// <summary>
        /// Raised when the server receives a <see cref="DnsRequest" />.
        /// </summary>
        public event DnsServerDelegate RequestEvent;

        /// <summary>
        /// Constuctor.
        /// </summary>
        public DnsServer()
        {
            this.sock      = null;
            this.onReceive = new AsyncCallback(OnReceive);
            this.recvBuf   = new byte[DnsMessage.PacketSize];
        }

        /// <summary>
        /// Starts the DNS server using the settings passed.
        /// </summary>
        /// <param name="settings">The <see cref="DnsServerSettings" /> to be used to initialize the server.</param>
        /// <exception cref="InvalidOperationException">Thrown if the server has already started.</exception>
        public void Start(DnsServerSettings settings)
        {
            lock (syncLock)
            {
                if (sock != null)
                    throw new InvalidOperationException("DNS server has already started.");

                sock                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.IgnoreUdpConnectionReset = true;
                sock.ReceiveBufferSize        =
                sock.ReceiveBufferSize        = 1024 * 1024;  // $todo(jeff.lill): Hardcoded
                sock.Bind(settings.NetworkBinding);

                remoteEP = new IPEndPoint(IPAddress.Any, 0);
                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref remoteEP, onReceive, sock);
            }
        }

        /// <summary>
        /// Handles received packets.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance.</param>
        private void OnReceive(IAsyncResult ar)
        {
            DnsRequest  request = null;
            int         cbRecv;
            IPEndPoint  ep;

            try
            {
                cbRecv = ((EnhancedSocket)ar.AsyncState).EndReceiveFrom(ar, ref remoteEP);
            }
            catch
            {
                cbRecv = 0;
            }

            if (sock == null)
                return; // The server has stopped

            if (cbRecv != 0)
            {
                // Parse the request packet

                try
                {
                    request = new DnsRequest();
                    request.ParsePacket(recvBuf, cbRecv);
                }
                catch (Exception e)
                {

                    SysLog.LogException(e);
                }
            }

            // Save the remote EP and then initiate another async
            // packet receive.

            ep       = (IPEndPoint)remoteEP;
            remoteEP = new IPEndPoint(IPAddress.Any, 0);

            sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref remoteEP, onReceive, sock);

            // Process the request and transmit the response (if there is one).

            if (request != null && RequestEvent != null)
            {
                var args = new DnsServerEventArgs(ep, request);

                RequestEvent(this, args);
                if (args.Response != null)
                {
                    byte[]  sendBuf;
                    int     cbSend;

                    // $todo(jeff.lill): 
                    //
                    // Remove this exception code after figuring out why the 
                    // response's QName field is sometimes NULL.

                    try
                    {
                        sendBuf = args.Response.FormatPacket(out cbSend);
                    }
                    catch
                    {
                        SysLog.LogError("DNS Formatting Error:\r\n\r\n" +
                                        args.Request.GetTraceDetails(ep.Address) +
                                        "\r\n" +
                                        args.Response.GetTraceDetails(ep.Address));
                        throw;
                    }

                    lock (syncLock)
                    {
                        if (sock != null)
                            sock.SendTo(sendBuf, cbSend, SocketFlags.None, args.RemoteEP);
                    }
                }
            }
        }

        /// <summary>
        /// Stops the DNS server if it is running.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (sock != null)
                {
                    sock.Close();
                    sock = null;
                }
            }
        }
    }
}
