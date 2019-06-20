//-----------------------------------------------------------------------------
// FILE:        SipUdpTransport.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the UDP based SIP transport.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements the UDP based SIP transport.  See <see cref="ISipTransport" /> for more information.
    /// </summary>
    /// <threadsafety instance="true" />
    public class SipUdpTransport : ISipTransport
    {
        private object                  syncLock = new object();
        private EnhancedSocket          sock = null;
        private SipTransportSettings    settings = null;
        private NetworkBinding          localEP = null;
        private bool                    disabled = false;
#pragma warning disable 414
        private bool                    recvPending = false;
#pragma warning restore 414
        private AsyncCallback           onRecv;
        private byte[]                  recvBuf;
        private EndPoint                recvEP;
        private ISipMessageRouter       router;
        private SipTraceMode        traceMode;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SipUdpTransport()
        {
        }

        /// <summary>
        /// Starts the transport.
        /// </summary>
        /// <param name="binding">The network binding the transport will use.</param>
        /// <param name="cbSocketBuffer">Size of the socket's send and receive buffers in bytes.</param>
        /// <param name="router">The <see cref="ISipMessageRouter" /> instance that will handle the routing of received messages.</param>
        /// <exception cref="SocketException">Thrown if there's a conflict with the requested and existing socket bindings.</exception>
        public void Start(NetworkBinding binding, int cbSocketBuffer, ISipMessageRouter router)
        {
            try
            {
                sock                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SendBufferSize           = cbSocketBuffer;
                sock.ReceiveBufferSize        = cbSocketBuffer;
                sock.IgnoreUdpConnectionReset = true;
                sock.Bind(binding);

                this.localEP   = (IPEndPoint)sock.LocalEndPoint;
                this.onRecv    = new AsyncCallback(OnReceive);
                this.recvBuf   = new byte[64 * 1024];
                this.recvEP    = new IPEndPoint(IPAddress.Any, 0);
                this.router    = router;
                this.traceMode = SipTraceMode.None;

                recvPending = true;
                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onRecv, null);
            }
            catch
            {
                if (sock.IsOpen)
                    sock.Close();

                sock = null;
                throw;
            }
        }

        /// <summary>
        /// Starts the transport.
        /// </summary>
        /// <param name="settings">The <see cref="SipTransportSettings" />.</param>
        /// <param name="router">The <see cref="ISipMessageRouter" /> instance that will handle the routing of received messages.</param>
        /// <exception cref="SocketException">Thrown if there's a conflict with the requested and existing socket bindings.</exception>
        public void Start(SipTransportSettings settings, ISipMessageRouter router)
        {
            this.settings = settings;
            Start(settings.Binding, settings.BufferSize, router);
        }

        /// <summary>
        /// Stops the transport if it's running.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (sock == null)
                    return;

                sock.Close();
                onRecv  = null;
                recvBuf = null;
            }
        }

        /// <summary>
        /// Sets the diagnostic tracing mode.
        /// </summary>
        /// <param name="traceMode">The <see cref="SipTraceMode" /> flags.</param>
        public void SetTraceMode(SipTraceMode traceMode)
        {
            this.traceMode = traceMode;
        }

        /// <summary>
        /// Disables the transport such that it will no longer send or receive SIP messages.  Used for
        /// unit testing to simulate network and hardware failures.
        /// </summary>
        public void Disable()
        {
            disabled = true;
        }

        /// <summary>
        /// Used internally by UNIT tests to modify the message router.
        /// </summary>
        internal ISipMessageRouter Router
        {
            get { return router; }
            set { router = value; }
        }

        /// <summary>
        /// Asynchronously transmits the message passed to the destination
        /// indicated by the <see paramref="remoteEP" /> parameter.
        /// </summary>
        /// <param name="remoteEP">The destination SIP endpoint's <see cref="NetworkBinding" />.</param>
        /// <param name="message">The <see cref="SipMessage" /> to be transmitted.</param>
        /// <exception cref="SipTransportException">Thrown if the remote endpoint rejected the message or timed out.</exception>
        public void Send(NetworkBinding remoteEP, SipMessage message)
        {
            if (disabled)
                return;

            if ((traceMode & SipTraceMode.Send) != 0)
                SipHelper.Trace(string.Format("UDP: sending to {0}", remoteEP), message);

            try
            {
                sock.SendTo(message.ToArray(), remoteEP);
            }
            catch (SocketException e)
            {
                // $todo(jeff.lill): 
                //
                // This is just copied from the TCP transport.  It probably
                // doesn't apply here.

                switch ((SocketError)e.ErrorCode)
                {
                    case SocketError.ConnectionAborted:
                    case SocketError.ConnectionRefused:
                    case SocketError.ConnectionReset:
                    case SocketError.HostDown:
                    case SocketError.HostNotFound:
                    case SocketError.HostUnreachable:

                        throw new SipTransportException(SipTransportException.ErrorType.Rejected, e.Message, e);

                    case SocketError.TimedOut:

                        throw new SipTransportException(SipTransportException.ErrorType.Timeout, e.Message, e);

                    default:

                        throw;
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> for streaming transports (TCP or TLS), <c>false</c> for packet transports (UDP).
        /// </summary>
        public bool IsStreaming
        {
            get { return false; }
        }

        /// <summary>
        /// The <see cref="SipTransportSettings" /> associated with this transport instance (or <c>null</c>).
        /// </summary>
        public SipTransportSettings Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        /// <summary>
        /// Returns the transport's <see cref="SipTransportType" />.
        /// </summary>
        public SipTransportType TransportType
        {
            get { return SipTransportType.UDP; }
        }

        /// <summary>
        /// Returns the transport's name, one of <b>UDP</b>, <b>TCP</b>, <b>TLS</b>.  This
        /// value is suitable for including in a SIP message's <b>Via</b> header.
        /// </summary>
        public string Name
        {
            get { return "UDP"; }
        }

        /// <summary>
        /// Returns the transport's local network binding.
        /// </summary>
        public NetworkBinding LocalEndpoint
        {
            get
            {
                if (sock == null)
                    throw new ObjectDisposedException("Transport is not running.");

                return localEP;
            }
        }

        /// <summary>
        /// Called when a packet is received by the socket from a remote endpoint.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private void OnReceive(IAsyncResult ar)
        {
            byte[]      packet = null;
            IPEndPoint  remoteEP = new IPEndPoint(IPAddress.Any, 0);
            int         cb;
            SipMessage  message;

            recvPending = false;

            if (sock == null || recvBuf == null)
                return;

            try
            {
                cb = sock.EndReceiveFrom(ar, ref recvEP);
            }
            catch (Exception e)
            {
                // Log the exception if the socket appears to be open and then submit
                // another receive request.

                if (sock.IsOpen)
                {
                    SysLog.LogException(e);

                    try
                    {
                        recvEP = new IPEndPoint(IPAddress.Any, 0);
                        sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onRecv, null);
                    }
                    catch (Exception e2)
                    {
                        SysLog.LogException(e2, "SIP UDP transport is no longer able to receive packets.");
                    }
                }

                return;
            }

            // $todo(jeff.lill): This is where I need to add source filtering.

            // Make a copy of what we received before initiating the next packet receive.

            try
            {
                packet = Helper.Extract(recvBuf, 0, cb);
                remoteEP = (IPEndPoint)recvEP;
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            // Initiate the receive of the next message

            lock (syncLock)
            {
                if (sock == null || !sock.IsOpen)
                    return;

                try
                {
                    recvEP = new IPEndPoint(IPAddress.Any, 0);
                    recvPending = true;
                    sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onRecv, null);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            // Parse and dispatch the message

            if (packet == null)
                return;

            // It looks like SIP clients like X-Lite send 4 byte CRLF CRLF messages
            // periodically over UDP to keep NAT mappings alive.  I'm going to
            // ignore these messages.

            if (packet.Length == 4)
                return;

            // Looks like we have a real message.

            try
            {
                try
                {
                    message = SipMessage.Parse(packet, true);
                }
                catch (Exception e)
                {
                    SipHelper.Trace(string.Format("UDP: UNPARSABLE message received  from {0}: [{1}]", remoteEP, e.Message), Helper.FromUTF8(packet));
                    throw;
                }

                message.SourceTransport = this;
                message.RemoteEndpoint  = remoteEP;
            }
            catch (SipException e)
            {
                e.BadPacket = packet;
                e.SourceEndpoint = remoteEP;
                SysLog.LogException(e);
                return;
            }
            catch (Exception e)
            {
                SipException sipException;

                sipException                = new SipException("Error parsing SIP message.", e);
                sipException.Transport      = string.Format("UDP [{0}]", localEP);
                sipException.BadPacket      = packet;
                sipException.SourceEndpoint = remoteEP;

                SysLog.LogException(sipException);
                return;
            }

            if (disabled)
                return;

            if ((traceMode & SipTraceMode.Receive) != 0)
                SipHelper.Trace(string.Format("UDP: received from {0}", remoteEP), message);

            router.Route(this, message);
        }

        /// <summary>
        /// This method must be called periodically on a background thread
        /// by the application so that the transport can implement any necessary
        /// background activities.
        /// </summary>
        public void OnBkTask()
        {
            // This is a NOP for the UDP transport.
        }
    }
}
