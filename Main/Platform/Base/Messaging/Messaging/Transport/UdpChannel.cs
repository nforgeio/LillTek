//-----------------------------------------------------------------------------
// FILE:        UdpChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a UDP based message channel.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Net.Broadcast;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// I'm not currently serializing message frames to the Msg._MsgFrame
// property before queuing an outbound message like I do for TCP Channels.
// This means that the MsgRouter.UdpMsgQueueSizeMax property will
// not actually limit the byte size of the UDP queue.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a UDP based message channel.
    /// </summary>
    /// <remarks>
    /// This class is threadsafe.
    /// </remarks>
    public class UdpChannel : IMsgChannel
    {
        private const int MulticastTTL = 10;

        private MsgRouter           router;             // The associated message router
        private bool                isOpen;             // True if the channel is open
        private EnhancedSocket      sock;               // The UDP socket
        private int                 port;               // The assigned port
        private ChannelEP           localEP;            // This channel's local endpoint (always normalized)
        private Transport           transport;          // Type of channel (Transport.Multicast or Transport.Udp)
        private UdpBroadcastClient  broadcastClient;    // Non-null if operating in UDP-BROADCAST mode.

        // Message transmission related members

        private PriorityQueue<Msg>  sendQueue;          // Queue of pending outbound messages
        private AsyncCallback       onSend;             // Async packet send handler
        private Msg                 sendMsg;            // Message being transmitted (or null)
        private byte[]              msgBuf;             // Serialized message
        private byte[]              sendBuf;            // Outbound frame
        private int                 cbSend;             // Size of frame
        private IPEndPoint          cloudEP;            // Endpoint for the multicast group (or null)
        private bool                multicastInit;      // True if the socket has been successfully
                                                        // added to the multicast group

        // Message receive related members

        private AsyncCallback       onSocketReceive;    // Async packet receive from socket handler
        private byte[]              recvBuf;            // The receive buffer
        private System.Net.EndPoint recvEP;             // Receives the transmitting socket's endpoint

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        public UdpChannel(MsgRouter router)
        {
            this.router          = router;
            this.isOpen          = false;
            this.sock            = null;
            this.port            = 0;
            this.recvEP          = new IPEndPoint(IPAddress.Any, 0);
            this.multicastInit   = false;
            this.broadcastClient = null;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~UdpChannel()
        {
            Close();
        }

        /// <summary>
        /// Opens a UDP unicast socket.
        /// </summary>
        /// <param name="ep">The UDP endpoint to open.</param>
        /// <remarks>
        /// <para>
        /// Pass <b>ep.Address=IPAddress.Any</b> if the channel should be opened on all 
        /// network adapters.
        /// </para>
        /// <para>
        /// Pass <b>ep.Port=0</b> if Windows should assign the socket's port.  The
        /// port assigned can be determined via the <see cref="Port" /> property.
        /// </para>
        /// </remarks>
        public void OpenUnicast(IPEndPoint ep)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                this.sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                this.sock.Bind(ep);
                
                this.transport       = Transport.Udp;
                this.localEP         = router.NormalizeEP(new ChannelEP(Transport.Udp, (IPEndPoint)sock.LocalEndPoint));
                this.port            = localEP.NetEP.Port;
                this.sendQueue       = new PriorityQueue<Msg>();
                this.onSend          = new AsyncCallback(OnSend);
                this.sendMsg         = null;
                this.cbSend          = 0;
                this.sendBuf         = null;
                this.msgBuf          = new byte[TcpConst.MTU];
                this.cloudEP         = null;
                this.multicastInit   = false;
                this.broadcastClient = null;

                sendQueue.CountLimit = router.UdpMsgQueueCountMax;
                sendQueue.SizeLimit  = router.UdpMsgQueueSizeMax;

                this.onSocketReceive = new AsyncCallback(OnSocketReceive);
                this.recvBuf         = new byte[TcpConst.MTU];

                router.Trace(1, "UDP: OpenUnicast", "localEP=" + localEP.NetEP.ToString(), null);

                sock.IgnoreUdpConnectionReset = true;
                sock.SendBufferSize           = router.UdpUnicastSockConfig.SendBufferSize;
                sock.ReceiveBufferSize         = router.UdpUnicastSockConfig.ReceiveBufferSize;

                // Initiate the first async receive operation on this socket

                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onSocketReceive, null);

                // Mark the channel as open.

                this.isOpen = true;
            }
        }

        /// <summary>
        /// Opens a UDP multicast socket.
        /// </summary>
        /// <param name="adapter">The network adapter to bind this socket.</param>
        /// <param name="cloudEP">Specifies the multicast group and port.</param>
        /// <remarks>
        /// <para>
        /// Pass <b>adapter=IPAddress.Any</b> to bind the socket to all available network
        /// adapters.
        /// </para>
        /// <note>
        /// A valid port and address must be specified in <b>cloudEP</b>.
        /// </note>
        /// </remarks>
        public void OpenMulticast(IPAddress adapter, IPEndPoint cloudEP)
        {
            if (cloudEP.Address.Equals(IPAddress.Any))
                throw new MsgException("Invalid multicast address.");

            if (cloudEP.Port == 0)
                throw new MsgException("Invalid multicast port.");

            using (TimedLock.Lock(router.SyncRoot))
            {
                this.sock                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                this.sock.ReuseAddress             = true;
                this.sock.EnableBroadcast          = true;
                this.sock.MulticastTTL             = MulticastTTL;
                this.sock.MulticastLoopback        = true;
                this.sock.IgnoreUdpConnectionReset = true;

                this.sock.Bind(new IPEndPoint(adapter, cloudEP.Port));

                // The framework throws an exception if there is no connected network connection
                // when we attempt to add the socket to the multicast group.  I'm going to catch
                // this exception and track that this didn't work and then periodically retry
                // the operation.

                try
                {
                    this.sock.MulticastGroup = cloudEP.Address;
                    this.multicastInit       = true;
                }
                catch
                {

                    this.multicastInit = false;
                }

                this.transport       = Transport.Multicast;
                this.localEP         = router.NormalizeEP(new ChannelEP(Transport.Udp, router.UdpEP));
                this.port            = cloudEP.Port;
                this.sendQueue       = new PriorityQueue<Msg>();
                this.onSend          = new AsyncCallback(OnSend);
                this.sendMsg         = null;
                this.cbSend          = 0;
                this.sendBuf         = null;
                this.msgBuf          = new byte[TcpConst.MTU];
                this.cloudEP         = cloudEP;

                this.onSocketReceive = new AsyncCallback(OnSocketReceive);
                this.recvBuf         = new byte[TcpConst.MTU];

                sendQueue.CountLimit = router.UdpMsgQueueCountMax;
                sendQueue.SizeLimit  = router.UdpMsgQueueSizeMax;

                router.Trace(1, "UDP: OpenMulticast",
                               string.Format("cloudEP={0} localEP={1} adaptor={2} NIC={3}",
                                             cloudEP, localEP.NetEP, adapter, NetHelper.GetNetworkAdapterIndex(adapter)), null);

                sock.SendBufferSize = router.UdpMulticastSockConfig.SendBufferSize;
                sock.ReceiveBufferSize = router.UdpMulticastSockConfig.ReceiveBufferSize;

                // Initiate the first async receive operation on this socket

                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onSocketReceive, null);

                // Mark the channel as open.

                this.isOpen = true;
            }
        }

        /// <summary>
        /// Opens a broadcast channel that uses a  instance to
        /// broadcast messages across a collection of servers.  This is typically used on networks
        /// that do not support multicast.
        /// </summary>
        /// <param name="settings">The settings to use for the <see cref="UdpBroadcastClient" />.</param>
        public void OpenUdpBroadcast(UdpBroadcastClientSettings settings)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                this.broadcastClient                 = new UdpBroadcastClient(settings);
                this.broadcastClient.PacketReceived += new UdpBroadcastDelegate(OnBroadcastReceive);

                this.transport       = Transport.Multicast;
                this.localEP         = router.NormalizeEP(new ChannelEP(Transport.Udp, router.UdpEP));
                this.port            = 0;
                this.sendQueue       = null;
                this.onSend          = null;
                this.sendMsg         = null;
                this.cbSend          = 0;
                this.sendBuf         = null;
                this.msgBuf          = new byte[TcpConst.MTU];
                this.cloudEP         = null;
                this.onSocketReceive = null;
                this.recvBuf         = null;

                router.Trace(1, "UDP: OpenUdpBroadcast", null, null);

                // Mark the channel as open.

                this.isOpen = true;
            }
        }

        /// <summary>
        /// Returns the TCP port assigned to the channel.
        /// </summary>
        public int Port
        {
            get
            {
                using (TimedLock.Lock(router.SyncRoot))
                    return port;
            }
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
        /// The endpoint passed will be normalized: the IP address will 
        /// be valid.  If no adapter IP address association can be found, 
        /// then the IP address will be set to the loopback address: 127.0.0.1.
        /// </note>
        /// </remarks>
        public void OnNewEP(ChannelEP localEP)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                router.Trace(1, "UDP: NewEP", localEP.ToString(), null);
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
                if (!isOpen)
                    return;

                if (sock != null)
                {
                    if (localEP != null)
                        router.Trace(1, "UDP: Close", "LocalEP=" + localEP.NetEP.ToString(), null);

                    sock.Close();
                    sock = null;
                }
                else if (broadcastClient != null)
                {
                    router.Trace(1, "UDP: Close(UDP-BROADCAST)", null, null);

                    broadcastClient.Close();
                    broadcastClient = null;
                }

                isOpen          = false;
                port            = 0;
                sendQueue       = null;
                onSend          = null;
                sendBuf         = null;
                sendMsg         = null;
                onSocketReceive = null;
                recvBuf         = null;
            }
        }

        /// <summary>
        /// Closes the channel if there's been no message activity within
        /// the specified timespan.
        /// </summary>
        /// <param name="maxIdle">Maximum idle time.</param>
        /// <returns><c>true</c> if the channel was closed.</returns>
        /// <remarks>
        /// Channels are not required to honor this call.  Specifically,
        /// the UDP channel implementation will always return false.
        /// </remarks>
        public bool CloseIfIdle(TimeSpan maxIdle)
        {
            return false;
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
                if (sendQueue != null)
                {
                    sendQueue.CountLimit = countLimit;
                    sendQueue.SizeLimit  = sizeLimit;
                }
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
        /// Handles messages received from the UdpBroadcast client.
        /// </summary>
        /// <param name="sender">The UDP broadcast client.</param>
        /// <param name="args">Event arguments.</param>
        private void OnBroadcastReceive(object sender, UdpBroadcastEventArgs args)
        {
            Msg         msg = null;
            byte[]      msgBuf;
            int         cbMsg;
            int         cb;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (!isOpen)
                    return;

                try
                {
                    cb = args.Payload.Length;
                    if (cb > 0)
                    {
                        msgBuf = router.DecryptFrame(args.Payload, cb, out cbMsg);
                        msg = Msg.Load(new EnhancedMemoryStream(msgBuf));

                        msg._SetFromChannel(new ChannelEP(transport, new IPEndPoint(args.SourceAddress, 0)));
                    }
                }
                catch (MsgException)
                {
                    // Ignore messages that can't be parsed
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            if (msg != null)
            {
                msg._Trace(router, 2, "UDP: Broadcast Recv", string.Format("From: {0}", args.SourceAddress));
                msg._Trace(router, 0, "Receive", string.Empty);
                router.OnReceive(this, msg);
            }
        }

        /// <summary>
        /// Handles message packets received on the socket.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnSocketReceive(IAsyncResult ar)
        {
            int         cb;
            Msg         msg = null;
            byte[]      msgBuf;
            int         cbMsg;
            IPEndPoint  fromEP = NetworkBinding.Any;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (!isOpen)
                    return;

                try
                {
                    cb = sock.EndReceiveFrom(ar, ref recvEP);
                    if (cb > 0)
                    {
                        msgBuf = router.DecryptFrame(recvBuf, cb, out cbMsg);
                        msg = Msg.Load(new EnhancedMemoryStream(msgBuf));

                        fromEP = (IPEndPoint)recvEP;
                        msg._SetFromChannel(new ChannelEP(transport, fromEP));
                    }
                }
                catch (MsgException)
                {
                    // Ignore messages that can't be parsed
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                // Initiate the receive of the next message

                if (sock.IsOpen)
                {
                    try
                    {
                        sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onSocketReceive, null);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e, "LillTek UDP message channel is no longer able to receive messages.");
                    }
                }
            }

            if (msg != null)
            {
                msg._Trace(router, 2, "UDP: Recv", string.Format("From: {0}", fromEP));
                msg._Trace(router, 0, "Receive", string.Empty);
                router.OnReceive(this, msg);
            }
        }

        /// <summary>
        /// Transmits the message via the socket.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        private void TransmitViaSocket(ChannelEP toEP, Msg msg)
        {
            int cbMsg;

            if (cloudEP != null && !multicastInit)
            {
                // Retry adding the socket to the multicast group if this didn't
                // work earlier.

                try
                {
                    this.sock.MulticastGroup = cloudEP.Address;
                    this.multicastInit       = true;
                }
                catch
                {
                    this.multicastInit = false;
                }
            }

            msg._SetToChannel(toEP);
            msg._SetFromChannel(localEP);
            msg._Trace(router, 2, "UDP: Send", null);

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (sendMsg != null)
                {
                    // We're already in the process of transmitting
                    // a message so queue this one.

                    Enqueue(msg);
                    return;
                }

                // If there are already messages in the queue then queue
                // this one and then setup to transmit the first message
                // waiting in the queue.

                if (sendQueue.Count > 0)
                {
                    Enqueue(msg);
                    msg = sendQueue.Dequeue();
                }

                // Initiate transmission of the message

                sendMsg = msg;
                cbMsg   = Msg.Save(new EnhancedMemoryStream(msgBuf), sendMsg);
                sendBuf = router.EncryptFrame(msgBuf, cbMsg);
                cbSend  = sendBuf.Length;

                Assertion.Validate(cbSend <= TcpConst.MTU, "Message larger than UDP MTU.");

                try
                {
                    sock.BeginSendTo(sendBuf, 0, cbSend, SocketFlags.None, router.NormalizeEP(toEP.NetEP), onSend, null);
                }
                catch
                {
                    // Ignoring
                }
            }
        }

        /// <summary>
        /// Transmits the message via the UDP broadcast client.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        private void TransmitViaUdpBroadcast(ChannelEP toEP, Msg msg)
        {
            byte[]  sendBuf;
            int     cbSend;
            int     cbMsg;

            msg._SetToChannel(toEP);
            msg._SetFromChannel(localEP);
            msg._Trace(router, 2, "UDP: Send", null);

            using (TimedLock.Lock(router.SyncRoot))
            {
                // Initiate transmission of the message

                cbMsg   = Msg.Save(new EnhancedMemoryStream(msgBuf), msg);
                sendBuf = router.EncryptFrame(msgBuf, cbMsg);
                cbSend  = sendBuf.Length;

                Assertion.Validate(cbSend <= TcpConst.MTU - UdpBroadcastClient.MessageEnvelopeSize, "Message larger than UDP MTU.");

                try
                {
                    broadcastClient.Broadcast(sendBuf);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Transmits the message to the specified channel endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        public void Transmit(ChannelEP toEP, Msg msg)
        {
            if (broadcastClient != null)
                TransmitViaUdpBroadcast(toEP, msg);
            else
                TransmitViaSocket(toEP, msg);
        }

        /// <summary>
        /// Used by unit tests to queue the message passed for sending
        /// rather than sending it immediately.  A subsequent Send() call
        /// should send the message passed and then send the queued message.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message to queue.</param>
        internal void QueueTo(ChannelEP toEP,Msg msg)
        {
            using (TimedLock.Lock(router.SyncRoot)) 
            {
                msg._SetToChannel(toEP);
                Enqueue(msg);
            }
        }

        /// <summary>
        /// Handles message packet send completions on the socket.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnSend(IAsyncResult ar)
        {
            ChannelEP   toEP;
            int         cb;
            int         cbMsg;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (!isOpen || sendMsg == null)
                    return;

                Assertion.Test(sendMsg != null);
                sendMsg = null;

                try
                {
                    cb = sock.EndSendTo(ar);
                    Assertion.Test(cb == cbSend);

                    if (sendQueue.Count > 0)
                    {
                        sendMsg = sendQueue.Dequeue();
                        toEP    = sendMsg._ToEP.ChannelEP;

                        sendMsg._SetFromChannel(localEP);

                        cbMsg   = Msg.Save(new EnhancedMemoryStream(msgBuf), sendMsg);
                        sendBuf = router.EncryptFrame(msgBuf, cbMsg);
                        cbSend  = sendBuf.Length;

                        Assertion.Validate(cbSend <= TcpConst.MTU, "Message larger than UDP MTU.");
                        sock.BeginSendTo(sendBuf, 0, cbSend, SocketFlags.None, router.NormalizeEP(toEP.NetEP), onSend, null);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
