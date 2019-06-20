//-----------------------------------------------------------------------------
// FILE:        NetTraceSink.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Network trace information sink.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Diagnostics;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the callback use for trace packet receive notification.
    /// </summary>
    /// <param name="packets">The packets received.</param>
    public delegate void NetTraceSinkDelegate(NetTracePacket[] packets);

    /// <summary>
    /// Used for receive trace information from a network wide trace
    /// gathering infrastructure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The NetTrace and NetTraceSink classes work together to implement a
    /// general purpose network wide debug tracing mechanism.  Instances
    /// of these classes use UDP multicast to deliver NetTracePacket instance
    /// from trace sources to trace sinks.
    /// </para>
    /// <note>
    /// Tracing is available only if TRACE is defined.
    /// </note>
    /// </remarks>
    public sealed class NetTraceSink
    {
        //---------------------------------------------------------------------
        // Static members

        private static object           syncLock  = new object();   // Critical section
        private static NetTraceSink     traceSink = null;           // The global trace sink

        /// <summary>
        /// Starts a global trace sink.
        /// </summary>
        /// <param name="onReceive">The trace packet receive handler.</param>
        /// <remarks>
        /// This method configures the sink to receive broadcast information from the
        /// multicast group and port retrieved from the <b>Diagnostics.TraceEP</b> and
        /// <b>Diagnostics.TraceAdapter</b> settings setting, or to hardcoded defaults if these
        /// settings are not present.
        /// </remarks>
        [Conditional("TRACE")]
        public static void Start(NetTraceSinkDelegate onReceive)
        {
            lock (syncLock)
            {
                IPEndPoint      traceEP;
                IPAddress       traceAdapter;
                Config          config;

                config       = new Config("Diagnostics");
                traceEP      = config.Get("TraceEP", new IPEndPoint(Helper.ParseIPAddress(NetTrace.DefTraceGroup), NetTrace.DefTracePort));
                traceAdapter = config.Get("TraceAdapter", IPAddress.Any);

                if (traceSink != null)
                {
                    traceSink.Stop(null);
                    traceSink = null;
                }

                traceSink = new NetTraceSink();
                traceSink.Start(traceEP, traceAdapter, onReceive);
            }
        }

        /// <summary>
        /// Stops the global trace source (if one is running).
        /// </summary>
        [Conditional("TRACE")]
        public static void Stop()
        {
            lock (syncLock)
            {
                if (traceSink != null)
                    traceSink.Stop(null);

                traceSink = null;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Socket                  sock;           // Multicast socket
        private NetTraceSinkDelegate    onReceive;      // Packet receive handler (or null)
        private byte[]                  recvBuf;        // Packet receive buffer
        private EndPoint                recvEP;         // Receive endpoint
        private AsyncCallback           onSockRecv;     // Socket receive handler
        private Queue                   recvQueue;      // Queue of received trace packets
        private GatedTimer              timer;          // Packet notification timer
        private Hashtable               sources;        // Tracks trace sources by packet TraceOriginID.

        /// <summary>
        /// Used for tracking trace source information.
        /// </summary>
        private sealed class SourceInfo
        {
            public string   TraceOriginID;          // The globally unique trace source ID
            public int      NextPacketNum;          // Number of the next packet expected from this source

            public SourceInfo(string traceSourceID, int nextPacketNum)
            {
                this.TraceOriginID = traceSourceID;
                this.NextPacketNum = nextPacketNum;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public NetTraceSink()
        {
            this.sock      = null;
            this.recvQueue = null;
            this.sources   = new Hashtable();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~NetTraceSink()
        {
            Stop();
        }

        /// <summary>
        /// Starts the trace source by opening a multicast socket.
        /// </summary>
        /// <param name="traceEP">The multicast group and port for the trace sources and sinks.</param>
        /// <param name="traceAdapter">
        /// The IP address of the network adapter to be used for transmitting trace 
        /// packets or <see cref="IPAddress.Any" />.
        /// </param>
        /// <param name="onReceive">The trace packet receive handler.</param>
        [Conditional("TRACE")]
        public void Start(IPEndPoint traceEP, IPAddress traceAdapter, NetTraceSinkDelegate onReceive)
        {
            lock (syncLock)
            {
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                sock.Bind(new IPEndPoint(traceAdapter, traceEP.Port));

                if (traceAdapter.Equals(IPAddress.Any))
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(traceEP.Address));
                else
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                         new MulticastOption(traceEP.Address, traceAdapter));

                sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 5);
                sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 1);

                this.onReceive  = onReceive;
                this.recvBuf    = new byte[NetTracePacket.MaxPacket];
                this.recvEP     = new IPEndPoint(IPAddress.Any, 0);
                this.onSockRecv = new AsyncCallback(OnSockReceive);
                this.recvQueue  = new Queue(10000);
                this.timer      = new GatedTimer(new TimerCallback(OnTimer), null, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5));

                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onSockRecv, null);
            }
        }

        /// <summary>
        /// Handles the reception of data on the socket.
        /// </summary>
        /// <param name="ar">Tne async result.</param>
        private void OnSockReceive(IAsyncResult ar)
        {
            lock (syncLock)
            {
                if (sock == null)
                    return;

                sock.EndReceiveFrom(ar, ref recvEP);

                try
                {
                    var packet = new NetTracePacket();

                    packet.SourceEP = (IPEndPoint)recvEP;
                    packet.Read(recvBuf);

                    recvQueue.Enqueue(packet);
                }
                catch
                {
                    // Ignore packets that can't be parsed
                }

                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref recvEP, onSockRecv, null);
            }
        }

        /// <summary>
        /// Stops the trace source.
        /// </summary>
        /// <param name="o">Not used (pass as null).</param>
        [Conditional("TRACE")]
        public void Stop(object o)
        {
            lock (syncLock)
            {
                if (sock != null)
                {
                    timer.Dispose();
                    timer = null;

                    recvQueue.Clear();
                    recvQueue = null;

                    sock.Close();
                    sock = null;
                }
            }
        }

        /// <summary>
        /// Called periodically to extract queued packets and forward them
        /// onto the packet receive handler.
        /// </summary>
        /// <param name="o">Not used.</param>
        public void OnTimer(object o)
        {
            NetTracePacket[]    packets;
            NetTracePacket      packet;
            SourceInfo          sourceInfo;
            string              sourceID;
            string              lastSourceID;
            ArrayList           updated;

            // Extract the packets from the queue as quickly as possible while
            // under the lock to avoid losing packets due to TCP buffer overflow.

            lock (syncLock)
            {
                if (recvQueue.Count == 0)
                    return;

                packets = new NetTracePacket[recvQueue.Count];
                for (int i = 0; i < packets.Length; i++)
                    packets[i] = (NetTracePacket)recvQueue.Dequeue();
            }

            // Check all the packet sequence numbers against what is expected
            // for each source being tracked.  If we find anything out of order,
            // we're going to insert warning packets into the packet array at the
            // position where the problem was detected.  This code is optimized
            // for the situation where there is no problem (which should be the
            // common case).

            updated      = null;
            lastSourceID = null;
            sourceInfo   = null;

            for (int i = 0; i < packets.Length; i++)
            {
                packet   = packets[i];
                sourceID = packet.TraceOriginID;

                if (sourceID != lastSourceID)
                {
                    sourceInfo = (SourceInfo)sources[sourceID];
                    if (sourceInfo == null)
                    {
                        // We haven't seen this source yet, so create a
                        // record for it

                        sourceInfo = new SourceInfo(sourceID, packet.PacketNum);
                        sources.Add(sourceID, sourceInfo);
                    }
                }

                if (sourceInfo.NextPacketNum > packet.PacketNum)
                {
                    // Looks like an out-of-order or missing packet.  Create the "updated"
                    // array list, add all of the good packets up to this point, insert
                    // an notification packet, and then continue searching for problems.

                    updated = new ArrayList(packets.Length + 10);
                    for (int j = 0; j < i; j++)
                        updated.Add(packets[j]);

                    updated.Add(new NetTracePacket(0, 0, "LillTek.NetTrace", 0, "***** Trace Error *****", "Dropped or out-of-order packet(s).", null));
                    updated.Add(packets[i]);

                    lastSourceID = null;
                    for (int j = i + 1; j < packets.Length; j++)
                    {
                        packet   = packets[j];
                        sourceID = packet.TraceOriginID;

                        if (sourceID != lastSourceID)
                        {
                            sourceInfo = (SourceInfo)sources[sourceID];
                            if (sourceInfo == null)
                            {
                                // We haven't seen this source yet, so create a
                                // record for it

                                sourceInfo = new SourceInfo(sourceID, packet.PacketNum);
                                sources.Add(sourceID, sourceInfo);
                            }
                        }

                        if (sourceInfo.NextPacketNum > packet.PacketNum)
                            updated.Add(new NetTracePacket(0, 0, "LillTek.NetTrace", 0, "***** Trace Error *****", "Dropped or out-of-order packet(s).", null));

                        updated.Add(packets[j]);

                        sourceInfo.NextPacketNum++;
                        lastSourceID = sourceID;
                    }
                    break;
                }

                sourceInfo.NextPacketNum++;
                lastSourceID = sourceID;
            }

            if (updated != null)
            {
                packets = new NetTracePacket[updated.Count];
                for (int i = 0; i < updated.Count; i++)
                    packets[i] = (NetTracePacket)updated[i];
            }

            // Report the new packets to the receive delegate.

            onReceive(packets);
        }
    }
}
