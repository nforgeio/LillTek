//-----------------------------------------------------------------------------
// FILE:        SipTcpTransport.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the TCP based SIP transport.

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
    /// Implements the TCP based SIP transport.  See <see cref="ISipTransport" /> for more information.
    /// </summary>
    /// <threadsafety instance="true" />
    public class SipTcpTransport : ISipTransport, ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        private static byte[] CRLFCRLF = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A };

        //---------------------------------------------------------------------
        // Local types

        private class Connection
        {
            private const int   MaxHeaderSize = 16 * 1024;       // SIP headers must be 16K or less
            private const int   MaxContentSize = 128 * 1024;    // SIP contents must be 128K or less

            private SipTcpTransport         transport;          // The parent transport
            private EnhancedSocket          sock;               // The socket connected to the remote SIP element
            private IPEndPoint              remoteEP;           // The remote endpoint
            private Queue<SipMessage>       sendQueue;          // Messages queued for sending
            private AsyncCallback           onSend;             // Handles packet receive notifications
            private AsyncCallback           onRecv;             // Handles packet send notifications
            private bool                    sendPending;        // True if a message is being transmitted
            private byte[]                  headerBuf;          // Buffer used to read message envelopes
            private byte[]                  contentBuf;         // Buffer used to read message contents (or null)
            private int                     cbRecv;             // Number of bytes received so far (in the current buffer)
            private SipMessage              message;            // The message we're receiving contents for

            public Connection(SipTcpTransport transport, EnhancedSocket sock, IPEndPoint remoteEP)
            {
                this.transport   = transport;
                this.sock        = sock;
                this.remoteEP    = remoteEP;
                this.sendQueue   = new Queue<SipMessage>();
                this.onSend      = new AsyncCallback(OnSend);
                this.onRecv      = new AsyncCallback(OnReceive);
                this.sendPending = false;

                this.headerBuf   = new byte[MaxHeaderSize];
                this.contentBuf  = null;
                this.cbRecv      = 0;

                sock.BeginReceive(headerBuf, 0, headerBuf.Length, SocketFlags.None, onRecv, null);
            }

            public void Close()
            {
                if (sock == null)
                    return;

                using (TimedLock.Lock(transport))
                {
                    sock.ShutdownAndClose();
                    sock = null;
                }
            }

            private void CloseAndRemove()
            {
                TimedLock.AssertLocked(transport);

                Close();
                if (transport.connections != null)
                    transport.connections.Remove(remoteEP.ToString());
            }

            public DateTime TouchTime
            {
                get { return sock.TouchTime; }
            }

            public void Send(SipMessage message)
            {
                using (TimedLock.Lock(transport))
                {
                    if (sendPending)
                    {
                        sendQueue.Enqueue(message);
                        return;
                    }

                    try
                    {
                        var packet = message.ToArray();

                        sock.BeginSendAll(packet, 0, packet.Length, SocketFlags.None, onSend, null);
                        sendPending = true;
                    }
                    catch (SocketException)
                    {
                        CloseAndRemove();
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                }
            }

            private void OnSend(IAsyncResult ar)
            {
                using (TimedLock.Lock(transport))
                {
                    if (sock == null)
                        return;

                    if (sendQueue.Count == 0)
                    {
                        sendPending = false;
                        return;
                    }

                    // There's another message queued for sending
                    // so dequeue it and start transmitting it.

                    var message = sendQueue.Dequeue();
                    var packet  = message.ToArray();

                    try
                    {
                        sock.BeginSendAll(packet, 0, packet.Length, SocketFlags.None, onSend, null);
                    }
                    catch (SocketException)
                    {
                        CloseAndRemove();
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                }
            }

            private void OnReceive(IAsyncResult ar)
            {
                List<SipMessage> received = null;

                try
                {
                    using (TimedLock.Lock(transport))
                    {
                        int         cb;
                        int         pos;
                        int         cbContents;
                        SipHeader   contentLength;
                        byte[]      packet;

                        try
                        {
                            if (sock == null)
                                return;

                            if (contentBuf == null)
                            {
                                // We're reading a message envelope.

                                // Read packets into headerBuf until we can find the CRLFCRLF
                                // sequence marking the end of the message headers.

                                cb = sock.EndReceive(ar);
                                if (cb == 0)
                                {
                                    // The socket has been closed on by the remote element.

                                    CloseAndRemove();
                                    return;
                                }

                                cbRecv += cb;

                            tryAgain:

                                // Remove any leading CR or LF characters by shifting the
                                // buffer contents.  I know this isn't super efficient but
                                // we'll probably never actually see packets with this
                                // in the wild.

                                for (pos = 0; pos < cbRecv; pos++)
                                    if (headerBuf[pos] != 0x0D && headerBuf[pos] != 0x0A)
                                        break;

                                if (pos != 0)
                                {
                                    if (pos == cbRecv)
                                    {
                                        // No data remaining in the buffer

                                        cbRecv = 0;
                                        sock.BeginReceive(headerBuf, 0, headerBuf.Length, SocketFlags.None, onRecv, null);
                                        return;
                                    }

                                    Array.Copy(headerBuf, pos, headerBuf, 0, headerBuf.Length - pos);
                                    cbRecv -= pos;
                                }

                                // Scan the message for the CRLFCRLF sequence terminating the
                                // message envelope.

                                pos = Helper.IndexOf(headerBuf, CRLFCRLF, 0, cbRecv);
                                if (pos != -1)
                                {
                                    // We've got the message envelope

                                    pos += 4;   // Advance past the CRLFCRLF

                                    // Parse the message headers and then get the Content-Length header

                                    packet = Helper.Extract(headerBuf, 0, pos);

                                    try
                                    {
                                        message = SipMessage.Parse(packet, false);
                                    }
                                    catch (Exception e)
                                    {
                                        SipHelper.Trace(string.Format("TCP: UNPARSABLE message received  from {0}: [{1}]", remoteEP, e.Message), Helper.FromUTF8(packet));
                                        throw;
                                    }

                                    contentLength = message[SipHeader.ContentLength];

                                    if (contentLength == null || !int.TryParse(contentLength.Text, out cbContents) || cbContents < 0)
                                    {
                                        var e = new SipException("Malformed SIP message: Invalid or missing [Content-Length] header from streaming transport.");

                                        e.Transport = "TCP";
                                        e.BadPacket = packet;
                                        e.SourceEndpoint = remoteEP;
                                        throw e;
                                    }

                                    if (cbContents > MaxContentSize)
                                    {
                                        var e = new SipException("Invalid SIP message: [Content-Length={0}] exceeds [{1}].", cbContents, MaxContentSize);

                                        e.Transport = "TCP";
                                        e.BadPacket = packet;
                                        e.SourceEndpoint = remoteEP;
                                        throw e;
                                    }

                                    if (pos + cbContents <= cbRecv)
                                    {
                                        // We already have the message contents, so extract the contents,
                                        // add them to the message, and then queue the message for delivery
                                        // once we leave the lock.

                                        message.Contents = Helper.Extract(headerBuf, pos, cbContents);

                                        if (received == null)
                                            received = new List<SipMessage>();

                                        received.Add(message);
                                        message = null;

                                        // Shift any remaining data to the left in headerBuf,
                                        // adjust cbRecv, and the loop to look for another
                                        // message.

                                        pos += cbContents;
                                        cb = cbRecv - pos;   // Bytes remaining in the buffer

                                        if (cb == 0)
                                        {
                                            // No more data left in the buffer

                                            cbRecv = 0;
                                            sock.BeginReceive(headerBuf, 0, headerBuf.Length, SocketFlags.None, onRecv, null);
                                            return;
                                        }

                                        Array.Copy(headerBuf, pos, headerBuf, 0, cb);
                                        cbRecv = cb;
                                        goto tryAgain;
                                    }

                                    // We don't have all of the message contents, so allocate a buffer for
                                    // the contents, copy what we have already into this buffer, and then
                                    // initiate a receive operation to read the remaining data.

                                    contentBuf = new byte[cbContents];
                                    cbRecv = cbRecv - pos;   // Content bytes remaining in the buffer

                                    Array.Copy(headerBuf, pos, contentBuf, 0, cbRecv);
                                    sock.BeginReceiveAll(contentBuf, cbRecv, cbContents - cbRecv, SocketFlags.None, onRecv, null);
                                    return;
                                }

                                // Throw an error if the header buffer is full and we still haven't
                                // found the end of the envelope.

                                if (cbRecv >= headerBuf.Length)
                                {
                                    var e = new SipException("Malformed SIP message: Read [{0}] bytes and have not yet encountered end of headers.", headerBuf.Length);

                                    e.Transport = "TCP";
                                    e.SourceEndpoint = remoteEP;
                                    throw e;
                                }

                                // Continue receiving header data.

                                sock.BeginReceive(headerBuf, cbRecv, headerBuf.Length - cbRecv, SocketFlags.None, onRecv, null);
                            }
                            else
                            {
                                // We're in the process of reading the message contents.
                                // Complete the contents receive operation and queue the
                                // message for delivery after we leave the lock.

                                sock.EndReceiveAll(ar);
                                message.Contents = contentBuf;

                                if (received == null)
                                    received = new List<SipMessage>();

                                received.Add(message);

                                // Reset and start reading the next message envelope.

                                message = null;
                                contentBuf = null;
                                cbRecv = 0;

                                sock.BeginReceive(headerBuf, 0, headerBuf.Length, SocketFlags.None, onRecv, null);
                            }
                        }
                        catch (SocketException)
                        {
                            CloseAndRemove();
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                            CloseAndRemove();
                        }
                    }
                }
                finally
                {
                    // Deliver any queued messages (outside of the lock)

                    if (received != null)
                    {
                        foreach (var message in received)
                        {
                            message.SourceTransport = transport;
                            message.RemoteEndpoint  = remoteEP;

                            if ((transport.traceMode & SipTraceMode.Receive) != 0)
                                SipHelper.Trace(string.Format("TCP: received from {0}", remoteEP), message);

                            transport.router.Route(transport, message);
                        }
                    }
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private SocketListener                  listener = null;
        private SipTransportSettings            settings = null;
        private NetworkBinding                  localEP  = null;
        private bool                            disabled = false;   // True if the transport is disabled
        private int                             cbSocketBuffer;     // Socket buffer size
        private Dictionary<string, Connection>  connections;        // Connected sockets keyed by IP endpoint rendered as a string
        private DateTime                        nextSweepTime;      // Next scheduled time to sweep for inactive connections (SYS)
        private TimeSpan                        sweepInterval;      // Sweep interval
        private TimeSpan                        maxInactive;        // Max time a connection can remain inactive
        private ISipMessageRouter               router;             // Routes received requests
        private SipTraceMode                    traceMode;          // The diagnostic tracing mode

        /// <summary>
        /// Constructor.
        /// </summary>
        public SipTcpTransport()
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
            this.cbSocketBuffer = cbSocketBuffer;
            this.listener       = new SocketListener();
            this.connections    = new Dictionary<string, Connection>();
            this.sweepInterval  = TimeSpan.FromSeconds(30);
            this.maxInactive    = TimeSpan.FromMinutes(5);     // Minimum time is 3 minutes according to RFC 3261
            this.router         = router;
            this.localEP        = binding;
            this.traceMode      = SipTraceMode.None;

            try
            {
                listener.SocketAcceptEvent += new SocketAcceptDelegate(OnAccept);
                listener.Start(binding, 100);
            }
            catch
            {
                listener.StopAll();
                listener = null;
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
            if (listener == null)
                return;

            using (TimedLock.Lock(this))
            {
                if (!disabled)
                    listener.StopAll();

                foreach (Connection con in connections.Values)
                    con.Close();

                listener    = null;
                connections = null;
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
            using (TimedLock.Lock(this))
            {
                if (disabled)
                    return;

                listener.StopAll();

                foreach (Connection con in connections.Values)
                    con.Close();

                connections.Clear();
                disabled = true;
            }
        }

        /// <summary>
        /// Called by the socket listener whenever a connection is accepted.
        /// </summary>
        /// <param name="sock">The new connection.</param>
        /// <param name="acceptEP">The remote endpoint.</param>
        private void OnAccept(EnhancedSocket sock, IPEndPoint acceptEP)
        {
            // $todo(jeff.lill): This is where I need to add source filtering.

            var remoteEP = (IPEndPoint)sock.RemoteEndPoint;

            using (TimedLock.Lock(this))
            {
                sock.SendBufferSize = cbSocketBuffer;
                sock.ReceiveBufferSize = cbSocketBuffer;

                connections.Add(remoteEP.ToString(), new Connection(this, sock, remoteEP));
            }
        }

        /// <summary>
        /// Asynchronously transmits the message passed to the destination
        /// indicated by the <see paramref="remoteEP" /> parameter.
        /// </summary>
        /// <param name="remoteEP">The destination SIP endpoint's <see cref="NetworkBinding" />.</param>
        /// <param name="message">The <see cref="SipMessage" /> to be transmitted.</param>
        /// <remarks>
        /// Note that this method will go to some lengths to send the message
        /// down an existing connection to this endpoint.
        /// </remarks>
        /// <exception cref="SipTransportException">Thrown if the remote endpoint rejected the message or timed out.</exception>
        public void Send(NetworkBinding remoteEP, SipMessage message)
        {
            string          key = remoteEP.ToString();
            EnhancedSocket  sock;
            Connection      con;

            using (TimedLock.Lock(this))
            {
                if (listener == null)
                    throw new ObjectDisposedException("Transport is closed.");

                if (disabled)
                    return;

                if ((traceMode & SipTraceMode.Send) != 0)
                    SipHelper.Trace(string.Format("TCP: sending to {0}", remoteEP), message);

                // Send the message down an existing connection to this
                // endpoint (if there is one).

                if (connections.TryGetValue(key, out con))
                {
                    con.Send(message);
                    return;
                }
            }

            // Otherwise establish a connection to the endpoint and transmit
            // the message.  Note that I'm establishing the connection outside
            // of the lock so processing on other connections can continue
            // while the connection is established.

            try
            {
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(remoteEP);
            }
            catch (SocketException e)
            {
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

            using (TimedLock.Lock(this))
            {
                if (listener == null)
                {
                    // Transport must have been closed while we were outside of the lock.

                    sock.ShutdownAndClose();
                    throw new ObjectDisposedException("Transport is closed.");
                }

                if (connections.TryGetValue(key, out con))
                {
                    // Another connection to this endpoint must have been established 
                    // while we were outside of the lock.  Close the new socket and
                    // use the existing connection.

                    sock.ShutdownAndClose();

                    con.Send(message);
                    return;
                }

                // Add the new connection to the collection and then
                // send the message.

                con = new Connection(this, sock, remoteEP);
                connections.Add(key, con);
                con.Send(message);
            }
        }

        /// <summary>
        /// Returns <c>true</c> for streaming transports (TCP or TLS), <c>false</c> for packet transports (UDP).
        /// </summary>
        public bool IsStreaming
        {
            get { return true; }
        }

        /// <summary>
        /// Returns the transport's <see cref="SipTransportType" />.
        /// </summary>
        public SipTransportType TransportType
        {
            get { return SipTransportType.TCP; }
        }

        /// <summary>
        /// Returns the transport's name, one of <b>UDP</b>, <b>TCP</b>, <b>TLS</b>.  This
        /// value is suitable for including in a SIP message's <b>Via</b> header.
        /// </summary>
        public string Name
        {
            get { return "TCP"; }
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
        /// Returns the transport's local network binding.
        /// </summary>
        public NetworkBinding LocalEndpoint
        {
            get
            {
                if (listener == null)
                    throw new ObjectDisposedException("Transport is not running.");

                return localEP;
            }
        }

        /// <summary>
        /// This method must be called periodically on a background thread
        /// by the application so that the transport can implement any necessary
        /// background activities.
        /// </summary>
        public void OnBkTask()
        {
            if (SysTime.Now < nextSweepTime || disabled)
                return;

            // Close any connections that have been inactive too log.

            var delList = new List<string>();
            var cutoff  = SysTime.Now - maxInactive;

            using (TimedLock.Lock(this))
            {
                if (listener == null)
                    return;

                foreach (string key in connections.Keys)
                {
                    Connection con = connections[key];

                    if (con.TouchTime <= cutoff)
                    {
                        delList.Add(key);
                        con.Close();
                    }
                }

                foreach (string key in delList)
                    connections.Remove(key);
            }

            // Schedule the next background task

            nextSweepTime = SysTime.Now + sweepInterval;
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better locking diagnostics.
        /// </summary>
        /// <returns>A lock key to be used to identify this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
