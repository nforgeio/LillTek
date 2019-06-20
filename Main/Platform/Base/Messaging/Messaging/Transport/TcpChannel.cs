//-----------------------------------------------------------------------------
// FILE:        TcpChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a TCP based message channel.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging.Internal;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a TCP based message channel.
    /// </summary>
    /// <remarks>
    /// This class is thread safe.
    /// </remarks>
    public class TcpChannel : IMsgChannel
    {
        private const int DefMsgSize          = 512;    // Initial capacity of message serialization memory stream
        private const int MaxUnconnectedQueue = 100;    // Maximum number of messages to queue for an unconnected channel

        /// <summary>
        /// Used below for tracking message transmission errors.
        /// </summary>
        private sealed class TransmitNotify
        {
            public Msg          Msg;
            public Exception    Error;

            public TransmitNotify(Msg msg, Exception e)
            {
                this.Msg  = msg;
                this.Error = e;
            }
        }

        private MsgRouter           router;         // The associated router.
        private EnhancedSocket      sock;           // The socket connecting this channel to
                                                    // another router.
        private MsgEP               routerEP;       // The remote router's physical endpoint
        private ChannelEP           remoteEP;       // The remote endpoint
        private ChannelEP           localEP;        // This channel's endpoint (always normalized)
        private bool                connected;      // True if the socket is connected
        private bool                initProcessed;  // True if channel initialization message has been processed
        private DateTime            lastAccess;     // Time the channel last saw any activity
        private bool                isUplink;       // True if this is an uplink connection
        private bool                isDownlink;     // True if this is a downlink connection
        private bool                isP2P;          // True if the remote router is P2P enabled

        // Transmission related members

        private bool                sending;        // True if in the process of transmitting a message
        private Msg                 sendMsg;        // The message being transmitted
        private PriorityQueue<Msg>  sendQueue;      // Queue of pending outbound messages
        private AsyncCallback       onSend;         // Send completion callback
        private byte[]              sendBuf;        // Send buffer
        private int                 sendPos;        // Send position
        private int                 cbSend;         // Bytes to send

        // Reception related members

        private bool                recvHeader;     // True if receiving the header, false for the body
        private AsyncCallback       onReceive;      // Receive completion callback
        private byte[]              recvBuf;        // Receive buffer
        private int                 recvPos;        // Receive position
        private int                 cbRecv;         // Bytes to receive

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        public TcpChannel(MsgRouter router)
        {

            this.router        = router;
            this.sock          = null;
            this.routerEP      = null;
            this.remoteEP      = null;
            this.localEP       = null;
            this.connected     = false;
            this.initProcessed = false;
            this.lastAccess    = SysTime.Now;
            this.isUplink      = false;
            this.isDownlink    = false;
            this.isP2P         = false;

            this.sending       = false;
            this.sendMsg       = null;
            this.sendQueue     = new PriorityQueue<Msg>();
            this.onSend        = new AsyncCallback(OnSend);
            this.sendBuf       = null;
            this.sendPos       = 0;
            this.cbSend        = 0;

            this.onReceive     = new AsyncCallback(OnReceive);
            this.recvHeader    = false;
            this.recvBuf       = null;
            this.recvPos       = 0;
        }

        /// <summary>
        /// Sets the last access time.  Should be called from within a TimedLock.Lock().
        /// </summary>
        private void SetLastAccess()
        {
            if (sock != null && sock.IsOpen)
                lastAccess = SysTime.Now;
        }

        /// <summary>
        /// Associates the channel with the open socket passed and begins
        /// listening for messages received on the socket.
        /// </summary>
        /// <param name="sock">The open socket.</param>
        public void Open(EnhancedSocket sock)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                Assertion.Test(this.sock == null);

                this.sock      = sock;
                this.connected = true;
                this.localEP   = router.NormalizeEP(new ChannelEP(Transport.Tcp, router.TcpEP));
                this.remoteEP  = new ChannelEP(Transport.Tcp, new IPEndPoint(((IPEndPoint)sock.RemoteEndPoint).Address, 0));
                this.sendQueue.Clear();

                sock.SendBufferSize = router.TcpSockConfig.SendBufferSize;
                sock.ReceiveBufferSize = router.TcpSockConfig.ReceiveBufferSize;

                // Send the channel initialization message to the other endpoint.

                sock.NoDelay = !router.TcpDelay;

                if (router.FragmentTcp) 
                {
                    sock.SendMax    = 1;
                    sock.ReceiveMax = 1;
                }

                router.Trace(2, "TCP: Inbound", "LocalEP=" + localEP.NetEP.ToString() + " remoteEP=" + remoteEP.NetEP.ToString(), null);

                SetLastAccess();
                BeginReceive();

                TcpInitMsg msg;

                msg      = new TcpInitMsg(router.RouterEP, new MsgRouterInfo(router), isUplink, localEP.NetEP.Port);
                msg._TTL = 1;
                Transmit(router.NormalizeEP(remoteEP), msg, false);
            }
        }

        /// <summary>
        /// Used to hold the connect information state during the async'
        /// DNS resolve.
        /// </summary>
        private sealed class ConnectInfo
        {
            public string   Host;
            public int      Port;
            public Msg      Msg;

            public ConnectInfo(string host, int port, Msg msg)
            {
                this.Host = host;
                this.Port = port;
                this.Msg  = msg;
            }
        }

        /// <summary>
        /// Initiates a network connection to the message router at the
        /// specified network host and port and then initiates the transmission
        /// of the message once the connection is established.
        /// </summary>
        /// <param name="host">The host name or IP address in dotted-quad notation.</param>
        /// <param name="port">The port number.</param>
        /// <param name="msg">The message to be sent (or <c>null</c>).</param>
        public void Connect(string host, int port, Msg msg)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                // If the host name is a valid IP address then simply
                // drop through and call Connect() with an IPEndPoint.

                IPAddress addr;

                if (NetHelper.IsIPAddress(host))
                {
                    addr = Helper.ParseIPAddress(host);
                    Connect(new IPEndPoint(addr, port), msg);
                }

                // Initiate an async DNS lookup

                try
                {
                    router.Trace(2, "TCP: Outbound", " host=" + host + " port=" + port.ToString(), null);
                    Dns.BeginGetHostEntry(host, new AsyncCallback(OnDNSResolve), new ConnectInfo(host, port, msg));
                }
                catch (Exception e)
                {
                    router.Trace("TCP: DNS Resolve Failed [" + host + "]", e);
                    router.OnTcpClose(this);
                    Close();
                }
            }
        }

        /// <summary>
        /// Handles completion of the connect.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnDNSResolve(IAsyncResult ar)
        {
            ConnectInfo     info = (ConnectInfo)ar.AsyncState;
            IPAddress       addr = null;

            using (TimedLock.Lock(router.SyncRoot))
            {
                try
                {
                    // $hack(jeff.lill): 
                    //
                    // It's possible but very unlikely that only IPv6 addresses will be returned
                    // by the DNS and the array indexing operation will throw an exception.  The
                    // end result will be correct, since we don't handle IPv6 yet.

                    addr = Dns.EndGetHostEntry(ar).AddressList.IPv4Only()[0];
                    router.Trace(2, "TCP: DNS Resolve", "Host=" + info.Host + " Address=" + addr.ToString(), null);
                }
                catch (Exception e)
                {
                    router.Trace("TCP: DNS Resolve Failed [" + info.Host + "]", e);
                    router.OnTcpClose(this);
                    Close();
                    return;
                }

                try
                {
                    Connect(new IPEndPoint(addr, info.Port), info.Msg);
                }
                catch (Exception e)
                {

                    router.Trace(string.Format(null, "TCP: Connect Failed [{0},{1}:{2}]", info.Host, addr, info.Port), e);
                }
            }
        }

        /// <summary>
        /// Initiates a network connection to the message router at the
        /// specified network endpoint and then initiates the transmission
        /// of the message once the connection is established.
        /// </summary>
        /// <param name="ep">The remote router's endpoint.</param>
        /// <param name="msg">The message to be sent (or <c>null</c>).</param>
        public void Connect(IPEndPoint ep, Msg msg)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                Assertion.Test(sock == null);
                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                localEP = new ChannelEP(Transport.Tcp, router.NormalizeEP(router.TcpEP));

                sock.NoDelay = !router.TcpDelay;
                sock.SendBufferSize = router.TcpSockConfig.SendBufferSize;
                sock.ReceiveBufferSize = router.TcpSockConfig.ReceiveBufferSize;

                if (router.FragmentTcp) {

                    sock.SendMax    = 1;
                    sock.ReceiveMax = 1;
                }

                // Queue the channel initialization message and the message passed

                Msg     initMsg;

                initMsg     = new TcpInitMsg(router.RouterEP, new MsgRouterInfo(router), isUplink, router.TcpEP.Port);
                initMsg._TTL = 1;

                Serialize(initMsg);
                Enqueue(initMsg);

                try
                {
                    SetLastAccess();
                    remoteEP = new ChannelEP(Transport.Tcp, router.NormalizeEP(ep));

                    if (msg != null)
                    {
                        msg._SetToChannel(remoteEP);
                        msg._SetFromChannel(localEP);
                        msg._Trace(router, 2, "TCP: Queue", null);

                        Serialize(msg);
                        Enqueue(msg);
                    }

                    router.Trace(2, "TCP: Outbound", "LocalEP=" + localEP.NetEP.ToString() + " remoteEP=" + remoteEP.NetEP.ToString(), null);
                    sock.BeginConnect(remoteEP.NetEP, new AsyncCallback(OnConnect), null);
                }
                catch (Exception e)
                {
                    router.Trace(string.Format(null, "TCP: Connect Failed [{0}]", ep), e);
                    router.OnTcpClose(this);
                    Close();
                }
            }
        }

        /// <summary>
        /// Handles connection completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnConnect(IAsyncResult ar)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                try
                {
                    if (sock == null)
                        return;

                    sock.EndConnect(ar);

                    connected = true;
                    remoteEP = new ChannelEP(Transport.Tcp, new IPEndPoint(((IPEndPoint)sock.RemoteEndPoint).Address, 0));

                    router.Trace(2, "TCP: Connected", "LocalEP=" + localEP.NetEP.ToString() + " remoteEP=" + remoteEP.NetEP.ToString(), null);
                    SetLastAccess();

                    // Initiation reception of the first message

                    BeginReceive();

                    // Start sending any queued messages

                    Assertion.Test(!sending);
                    if (sendQueue.Count > 0)
                    {

                        Msg msg;

                        msg = Dequeue();
                        Transmit(msg._ToEP.ChannelEP, msg, false);
                    }
                }
                catch (Exception e)
                {
                    router.Trace(string.Format(null, "TCP: Connect Failed"), e);
                    router.OnTcpClose(this);
                    Close();
                }
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TcpChannel()
        {
            Close();
        }

        /// <summary>
        /// <c>true</c> if this is an uplink channel.
        /// </summary>
        /// <remarks>
        /// Uplink channels are those channels between a hub router and its
        /// parent.  These channels are designed to pierce firewalls and NATs
        /// and then provide two way connectivity between the two routers.
        /// </remarks>
        public bool IsUplink
        {
            get { return isUplink; }
            set { isUplink = true; }
        }

        /// <summary>
        /// <c>true</c> if this is a downlink channel.
        /// </summary>
        /// <remarks>
        /// Downlink channels represent the parent end of an uplink channel.
        /// </remarks>
        public bool IsDownlink
        {
            get { return isDownlink; }
            set { isDownlink = true; }
        }

        /// <summary>
        /// <c>true</c> if the remote router is peer-to-peer enabled.
        /// </summary>
        public bool IsP2P
        {
            get { return isP2P; }
            set { isP2P = true; }
        }

        /// <summary>
        /// Returns the physical endpoint of the remote router on this
        /// channel once the channel is fully connected and initialized.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return routerEP; }
        }

        /// <summary>
        /// Called occasionally by the associated router when the 
        /// channel's local endpoint is changed.
        /// </summary>
        /// <param name="localEP">The new (normalized) endpoint.</param>
        /// <remarks>
        /// <para>
        /// The endpoint can change if the channel isn't bound to a
        /// specific IP address (aka IPAddress.Any), and the router
        /// detects an IP address change (due perhaps to a new network
        /// connection or a new IP address during a DHCP lease renewal).
        /// </para>
        /// <note>
        /// The endpoint passed will be normalized: the
        /// IP address will be valid.  If no adapter IP address association
        /// can be found, then the IP address will be set to the loopback
        /// address: 127.0.0.1.
        /// </note>
        /// </remarks>
        public void OnNewEP(ChannelEP localEP)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                router.Trace(2, "TCP: NewEP", localEP.ToString(), null);
                this.localEP = localEP;
            }
        }

        /// <summary>
        /// Closes the channel if it's currently open.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                try
                {
                    if (sock != null)
                    {
                        try
                        {
                            if (sock.Connected)
                                router.Trace(2, "TCP: Close", "LocalEP=" + localEP.NetEP.ToString() + " remoteEP=" + remoteEP.NetEP.ToString(), null);

                            sock.ShutdownAndClose();
                        }
                        catch
                        {
                        }

                        connected = false;
                        sendQueue.Clear();

                        sending    = false;
                        sendMsg    = null;
                        sendBuf    = null;
                        sendPos    = 0;
                        cbSend     = 0;

                        recvHeader = false;
                        recvBuf    = null;
                        recvPos    = 0;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Closes the channel if there's been no network activity within
        /// the specified timespan.
        /// </summary>
        /// <param name="maxIdle">Maximum idle time.</param>
        /// <returns><c>true</c> if the channel was closed.</returns>
        public bool CloseIfIdle(TimeSpan maxIdle)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                if (isUplink)
                    return false;

                if (SysTime.Now - lastAccess >= maxIdle)
                {
                    router.Trace(2, "TCP: Idle", "LocalEP=" + localEP.NetEP.ToString() + " remoteEP=" + remoteEP.NetEP.ToString(), null);

                    Close();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns the remote endpoint associated with this channel.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The Port field of the endpoint will be set to 0
        /// until the channel receives the channel initialization message
        /// from the remote endpoint.
        /// </note>
        /// </remarks>
        public IPEndPoint RemoteEP
        {
            get
            {
                using (TimedLock.Lock(router.SyncRoot))
                {
                    Assertion.Test(sock != null);
                    return remoteEP.NetEP;
                }
            }
        }

        /// <summary>
        /// Serializes the message and save the result to the message's
        /// <see cref="Msg._MsgFrame" /> property.
        /// </summary>
        /// <param name="msg">The message to be serialized.</param>
        private void Serialize(Msg msg)
        {
            var ms = new EnhancedMemoryStream(DefMsgSize);
            int cbMsg;

            cbMsg = Msg.Save(ms, msg);
            msg._MsgFrame = router.EncryptFrame(ms.GetBuffer(), cbMsg);
        }

        /// <summary>
        /// Private implementation of the Transmit() method that implements an
        /// option that disables queuing for the message passed.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <param name="queue">Indicates whether queuing should be enabled for this message.</param>
        private void Transmit(ChannelEP toEP, Msg msg, bool queue)
        {
            msg._SetToChannel(toEP);
            msg._SetFromChannel(localEP);
            msg._Trace(router, 2, "TCP: Send", null);

            // Serialize the message here rather than within the lock
            // below for better multiprocessor performance.

            Serialize(msg);

            // Initiate transmission of the message or queue it if other
            // messages are awaiting transmission (if queuing is enabled).

            try
            {
                using (TimedLock.Lock(router.SyncRoot))
                {
                    if (!connected || sending)
                    {
                        Enqueue(msg);
                        return;
                    }

                    // If there are already messages in the queue then add this
                    // message to the end of the queue and then dequeue a message
                    // from the front of the queue and send it.

                    if (queue && sendQueue.Count > 0)
                    {
                        Enqueue(msg);
                        msg = Dequeue();
                    }

                    // Initiate message transmission

                    sendBuf = msg._MsgFrame;
                    sending = true;
                    sendPos = 0;
                    cbSend  = sendBuf.Length;

                    sock.BeginSend(sendBuf, sendPos, cbSend, SocketFlags.None, onSend, null);
                }
            }
            catch (Exception e)
            {
                TraceException(e);
                router.OnTcpClose(this);
                Close();
            }
        }

        /// <summary>
        /// Transmits the message to the specified channel endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        public void Transmit(ChannelEP toEP, Msg msg)
        {
            Transmit(toEP, msg, true);
        }

        /// <summary>
        /// Queues the message passed rather than initiating an
        /// immediate transmission.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message to queue.</param>
        internal void QueueTo(ChannelEP toEP, Msg msg)
        {
            msg._SetToChannel(toEP);
            msg._SetFromChannel(localEP);
            msg._Trace(router, 2, "TCP: Queue", null);

            Serialize(msg);

            using (TimedLock.Lock(router.SyncRoot))
            {
                msg._SetToChannel(toEP);
                Enqueue(msg);
            }
        }

        /// <summary>
        /// Updates the send queue parameters.
        /// </summary>
        /// <param name="countLimit">The maximum number of normal priority messages to queue.</param>
        /// <param name="sizeLimit">The maximum bytes of serialized normal priority messages to queue.</param>
        public void SetQueueLimits(int countLimit, int sizeLimit)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                sendQueue.CountLimit = countLimit;
                sendQueue.SizeLimit  = sizeLimit;
            }
        }

        /// <summary>
        /// Adds a message to the send queue.
        /// </summary>
        /// <param name="msg">The mmessage.</param>
        private void Enqueue(Msg msg)
        {
            if ((msg._Flags & MsgFlag.Priority) != 0)
                sendQueue.EnqueuePriority(msg);
            else
                sendQueue.Enqueue(msg);
        }

        /// <summary>
        /// Dequeues the next message from the internal message queue
        /// and returns it (or <c>null</c>).
        /// </summary>
        /// <returns>The next message from the queue.</returns>
        private Msg Dequeue()
        {
            Msg msg;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (sendQueue.Count == 0)
                    return null;

                msg = sendQueue.Dequeue();
            }

            // Make sure that the channel endpoints of the message's
            // to/from are set properly.

            msg._SetToChannel(remoteEP);
            msg._SetFromChannel(localEP);

            return msg;
        }

        /// <summary>
        /// Handles socket send completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnSend(IAsyncResult ar)
        {
            try
            {
                using (TimedLock.Lock(router.SyncRoot))
                {
                    if (sock == null)
                        return;

                    sendPos += sock.EndSend(ar);
                    SetLastAccess();

                    if (sendPos < cbSend)
                    {
                        // Continue sending the current message

                        sock.BeginSend(sendBuf, sendPos, cbSend - sendPos, SocketFlags.None, onSend, null);
                        return;
                    }

                    // Begin sending any queued messages

                    if (sendQueue.Count == 0)
                    {
                        sending = false;
                        sendBuf = null;
                        sendMsg = null;
                        return;
                    }

                    sendMsg = Dequeue();
                    sendBuf = sendMsg._MsgFrame;
                    sending = true;
                    sendPos = 0;
                    cbSend  = sendBuf.Length;

                    sock.BeginSend(sendBuf, sendPos, cbSend, SocketFlags.None, onSend, null);
                }
            }
            catch (Exception e)
            {

                TraceException(e);
                router.OnTcpClose(this);
                Close();
            }
        }

        /// <summary>
        /// Initiates the reception of a message.
        /// </summary>
        private void BeginReceive()
        {
            try
            {
                using (TimedLock.Lock(router.SyncRoot))
                {
                    SetLastAccess();

                    recvHeader = true;
                    recvBuf = new byte[MsgRouter.FrameHeaderSize];
                    recvPos = 0;
                    cbRecv = MsgRouter.FrameHeaderSize;

                    sock.BeginReceive(recvBuf, recvPos, cbRecv, SocketFlags.None, onReceive, null);
                }
            }
            catch (Exception e)
            {
                TraceException(e);
                router.OnTcpClose(this);
                Close();
                throw;
            }
        }

        /// <summary>
        /// Handles receive completions on the socket.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceive(IAsyncResult ar)
        {
            int cbRecv;

            try
            {
                using (TimedLock.Lock(router.SyncRoot))
                {
                    if (sock == null || recvBuf == null)
                        return;

                    try
                    {
                        cbRecv = sock.EndReceive(ar);
                    }
                    catch (SocketException e2)
                    {
                        if (e2.SocketErrorCode == SocketError.ConnectionReset)
                            cbRecv = 0;     // Treat connection resets as connection closure
                        else
                            throw;
                    }

                    if (cbRecv == 0)
                    {
                        sock.ShutdownAndClose();
                        return;
                    }

                    SetLastAccess();

                    recvPos += cbRecv;
                    if (recvPos < recvBuf.Length)
                    {
                        // Continue the reception

                        sock.BeginReceive(recvBuf, recvPos, recvBuf.Length - recvPos, SocketFlags.None, onReceive, null);
                        return;
                    }

                    if (recvHeader)
                    {
                        // Initiate reception of the frame payload

                        int         pos = 0;
                        int         cbFrame;
                        byte[]      buf;

                        cbFrame = Helper.ReadInt32(recvBuf, ref pos);
                        buf = new byte[MsgRouter.FrameHeaderSize + cbFrame];
                        Array.Copy(recvBuf, 0, buf, 0, MsgRouter.FrameHeaderSize);

                        recvHeader = false;
                        recvBuf    = buf;
                        recvPos    = MsgRouter.FrameHeaderSize;
                        cbRecv     = cbFrame;

                        sock.BeginReceive(recvBuf, recvPos, cbFrame, SocketFlags.None, onReceive, null);
                    }
                    else
                    {
                        // We've completed the reception of a message

                        Msg         msg;
                        byte[]      msgBuf;
                        int         cbMsg;

                        msgBuf = router.DecryptFrame(recvBuf, recvBuf.Length, out cbMsg);
                        msg    = Msg.Load(new EnhancedMemoryStream(msgBuf));
                        msgBuf = null;

                        // Handle initialization messages locally and queue a 
                        // notification for all other messages so that router.OnReceive() 
                        // will be called on a worker thread.

                        if (!initProcessed)
                        {
                            TcpInitMsg  initMsg;
                            Exception   e;

                            initMsg = msg as TcpInitMsg;
                            if (initMsg == null)
                            {
                                e = new MsgException("Invalid TCP channel protocol: TcpInitMsg expected.");
                                SysLog.LogException(e);
                                Helper.Rethrow(e);
                            }

                            initMsg._Trace(router, 2, "Receive", null);

                            // If the sender indicates that it is an uplink then we're going
                            // to use the actual remote port for the remoteEP rather than 
                            // ListenPort (which should be 0).  The reason for this is that
                            // intervening routers and NATs may translate the port number 
                            // reported by the child router.

                            routerEP            = initMsg.RouterEP;
                            remoteEP.NetEP.Port = initMsg.IsUplink ? ((IPEndPoint)sock.RemoteEndPoint).Port : initMsg.ListenPort;
                            isDownlink          = initMsg.IsUplink;
                            isP2P               = initMsg.RouterInfo.IsP2P;
                            initProcessed       = true;

                            router.OnTcpInit(this);
                        }
                        else
                        {
                            msg._SetFromChannel(remoteEP);
                            msg._Trace(router, 2, "TCP: Recv", null);
                            msg._Trace(router, 0, "Receive", string.Empty);
                            router.OnReceive(this, msg);
                        }

                        // Initiate reception of the next message

                        BeginReceive();
                    }
                }
            }
            catch (SocketException e)
            {
                // Don't log connection resets because we see these all the
                // time when a router stops.  We're not going to consider
                // this to be an error.

                if (e.SocketErrorCode != SocketError.ConnectionReset)
                {
                    TraceException(e);
                    SysLog.LogException(e);
                }

                router.OnTcpClose(this);
                Close();
            }
            catch (Exception e)
            {
                TraceException(e);
                SysLog.LogException(e);
                router.OnTcpClose(this);
                Close();
            }
        }

        /// <summary>
        /// Writes the exception along with channel information to the trace log.
        /// </summary>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        private void TraceException(Exception e)
        {
            router.Trace(string.Format(null, "TCP: Exception [remoteEP={0}]", remoteEP), e);
        }
    }
}
