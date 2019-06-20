//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The server side implementation that simulates UDP multicast 
//              and broadcast for use in network environments (such as many 
//              cloud hosting services) that don't support this functionality.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

// $todo(jeff.lill):
//
// I could add a few more performance counters:
//
//      ICMP rejects/sec
//      ICMP reject count
//      Bad Messages/sec
//      Bad Message count

// $todo(jeff.lill):
//
// It would be nice if the server[#] table could accept a multi-valued
// DNS host name and do the right thing.  It's a bit of a pain to have
// to configure DNS hosts like BROADCAST01 and BROADCAST02 that refer to
// specific broadcast servers in addition to something like the multi-valued
// BROADCAST host that specifies all of the broadcast servers for
// the broadcast client applications.  Just a nice-to-have though.
// Not a priority.

namespace LillTek.Net.Broadcast
{
    /// <summary>
    /// The server side implementation that simulates UDP multicast 
    /// and broadcast for use in network environments (such as many 
    /// cloud hosting services) that don't support this functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Amazon Web Services and Microsoft Azure do not currently support 
    /// UDP multicast or broadcast between virtual machines running on
    /// their network.  This can be problematic for distributed applications
    /// that use multicast packets for discovery and other purposes.
    /// In particular, LillTek Messaging makes extensive use of UDP
    /// multicast for router discovery and routing table updates.
    /// </para>
    /// <para>
    /// The <see cref="UdpBroadcastServer" /> class in conjunction with
    /// <see cref="UdpBroadcastClient" /> can be used as an alternative 
    /// solution.  The basic idea is to deploy one or more <see cref="UdpBroadcastServer" />
    /// instances on the network within running service applications
    /// (<b>broadcast servers</b>).  <see cref="UdpBroadcastClient" />
    /// instances will be instantiated in client applications, specifying
    /// the network endpoints of the broadcast server instances, as well as a broadcast
    /// <b>broadcast group</b>, and a <b>shared encryption key</b>.  The broadcast client will 
    /// register their presence with the servers and periodically renew this 
    /// automatically.
    /// </para>
    /// <para>
    /// Client applications can then call the <see cref="UdpBroadcastClient.Broadcast" /> 
    /// method to broadcast a packet to the collection of broadcast clients
    /// currently registered with the broadcast servers.  Under the covers, the
    /// broadcast client sends the packet via UDP to one the broadcast servers
    /// and the server then resends the packet via UDP to each of the known 
    /// broadcast clients registered using the same <b>broadcast group</b>.
    /// </para>
    /// <para>
    /// <see cref="UdpBroadcastClient" />s instances will receive these packets
    /// and make these available via the <see cref="UdpBroadcastClient.PacketReceived" />
    /// event.
    /// </para>
    /// <para>
    /// Client applications should call the <see cref="UdpBroadcastClient.Close" />
    /// or <see cref="UdpBroadcastClient.Dispose" /> method when the application
    /// is shutting down or broadcasting will no longer be performed.  This will
    /// cause a packet to be sent to the broadcast services, indicating that the
    /// client is no longer participating in the broadcast group and will also
    /// terminate the automatic registration.
    /// </para>
    /// <para><b><u>Broadcast Servers</u></b></para>
    /// <para>
    /// Multiple broadcast servers should be deployed for redundancy with each 
    /// server being identified by a DNS host name or static (or Elastic IP for AWS).
    /// Each client instance will need to be configured with the network endpoint
    /// for all of the broadcast servers, so it is best not to use a dynamically
    /// assigned IP address or host name (such as the internal EC2 AWS instance IP 
    /// address), to avoid having to reconfigure and restart all of the dependant
    /// client applications after rebooting one or more broadcast servers.
    /// </para>
    /// <para>
    /// Broadcast servers accept three basic messages from broadcast clients:
    /// <b>ClientRegister</b>, <b>ClientUnregister</b>, and <b>Broadcast</b>.  The <b>ClientRegister</b>
    /// message is sent by a client when it is first instantiated and periodically
    /// when the client automatically renews its registration.  The <b>broadcast group</b>
    /// is also passed with this message.  <b>ClientUnregister</b> messages are sent when
    /// the broadcast client is closed.
    /// </para>
    /// <para>
    /// <b>Broadcast</b> messages contain the packet to be forwarded to all registered
    /// clients by the broadcast server.  Broadcast servers will verify that the source
    /// of a broadcast packet is currently registered and will then forward the message
    /// to each registered client (including the original sender).  Clients will
    /// raise their <see cref="UdpBroadcastClient.PacketReceived" /> event when this
    /// happens.
    /// </para>
    /// <note>
    /// <b>Broadcast</b> messages wrap the payload bytes within a small envelope.  This
    /// means that the the actual packet on the wire will be larger than the payload.
    /// Applications concerned with the possibility of UDP fragmentation should take this 
    /// into account.
    /// </note>
    /// <para>
    /// Broadcast servers run from the time they are instantiated until <see cref="UdpBroadcastServer.Close" />
    /// or <see cref="UdpBroadcastServer.Dispose" /> is called.  The network desired network binding as well
    /// the host names or bindings for all the broadcast server instances is passed to the server's
    /// constructor.  Note that broadcast clients send messages to all configured broadcast servers for
    /// redundancy purposes.  This allows any one broadcast server to handle the broadcasts even if 
    /// all of the others are offline.
    /// </para>
    /// <para>
    /// For network efficiency, we don't want all live broadcast servers to retransmit every
    /// message received to every client.  Instead, the broadcast servers will form a simple
    /// cluster where they'll negotiate which instance will retransmit received broadcast messages.
    /// Here's how this works:
    /// </para>
    /// <list type="bullet">
    ///     <item>Broadcast servers send a <b>ServerRegister</b> message to all of the other servers periodically.</item>
    ///     <item>Each broadcast server tracks the servers who have resently sent a <b>ServerRegister</b> message.</item>
    ///     <item>
    ///     When a <b>Broadcast</b> message is received, the server with the lexically lowest IP:port address 
    ///     will be considered to be the master and will handle the rebroadcasting.  The other servers
    ///     will ignore the broadcast.
    ///     </item>
    ///     <item>
    ///     Server instances send a <b>ServerUnregister</b> message to all of the other servers
    ///     when the service instance is closed.  The receiving servers will immediately remove the 
    ///     sending server from the set of know servers in the cluster.
    ///     </item>
    /// </list>
    /// <para>
    /// This simple protocol ensures that broadcasting will failover within a reasonable period of time
    /// and also handle situations where all of the broadcast servers go offline and then come back.
    /// This will also be relatively efficient from a network perspective.  The only significant overhead
    /// is the extra packets necessary when broadcast clients send <b>ClientRegister</b> and <b>Broadcast</b>
    /// messages to each broadcast server.  This should scale up pretty well to handle a few hundred 
    /// servers without too much overhead.
    /// </para>
    /// <para><b><u>Hosting Broadcast Servers in Dynamic Cloud Platforms</u></b></para>
    /// <para>
    /// One of the challenges of hosting services on a dynamic cloud platform such as
    /// Azure or AWS is that server instances will boot with a different IP address every
    /// time.  This means that it is not possible to statically register static IP as a DNS
    /// host name for a particular instance or reference the instance via a static network
    /// endpoint.  The UDP broadcast client/server combination is designed to provide the
    /// base infrastructure to allow for the dynamic discovery of services, but the the
    /// the endpoints for the UDP broadcast servers must still be specified somehow, to 
    /// bootstrap the entire system.
    /// </para>
    /// <para>
    /// Broadcast servers are specified by a set of <see cref="NetworkBinding" />s specified
    /// in the <see cref="UdpBroadcastClientSettings" />.  These bindings can be IP address/port
    /// pairs or host name/port pairs.  One approach to specifying broadcast servers for
    /// distributed application instances is to simply assign a static IP (or Elastic IP on AWS)
    /// to the broadcast servers and then use this IP address in the dependant application
    /// configurations.  Although this would work, there are a few problems with doing this.
    /// First, the availability of static IP addresses is limited and have costs associated
    /// with them.  Second, this approach requires that the UDP broadcast traffic be routed
    /// from the servers in the datacenter out to the front-end routers and then back down
    /// to the server.  This means that we'd be charged for additional network bandwidth and
    /// also that we'd have to open the UDP port at the public service firewall.
    /// </para>
    /// <para>
    /// The better approach is to deploy a LillTek Dynamic DNS server behind static or elastic
    /// IP addresses and then have the broadcast servers dynamically register their assigned
    /// local IP address against a specific host name, say <b>broadcast.lilltek.net</b>.
    /// Then you'd configure the application references to the broadcast server using the
    /// <b>broadcast.lilltek.net:UDP-BROADCAST</b> network binding.
    /// </para>
    /// <para>
    /// The broadcast client will periodically query the DNS for this host name and the
    /// current set of dynamically registered IP addresses will be returned and the
    /// client will begin communicating with these servers.
    /// </para>
    /// <para>
    /// The nice aspect of this approach, is that all bootstrapping occurs by querying the
    /// domain name system which, by definition, has to be located at fixed static IP addresses.
    /// </para>
    /// <para><b><u>Message Security</u></b></para>
    /// <para>
    /// Messages transmitted between clients and servers in the UDP broadcast cluster are encrypted with a 
    /// shared symmetric key.  This key is specified in the settings passed to the UDP broadcast client
    /// and server class constructors.  Messages also include the current time (UTC) and 32-bits of 
    /// cryptographic salt.
    /// </para>
    /// <para>
    /// This all means that messages are essentially signed to prevent tampering and are timestamped
    /// to deal with replay attacks.  See <see cref="UdpBroadcastMessage" /> for more details.
    /// </para>
    /// <para><b><u>Scalability</u></b></para>
    /// <para>
    /// The UDP broadcast packet rebroadcast technique implemented by this class can work reasonably well
    /// for installations with up to about 100 client instances.  In this situation, assuming each client broadcasts
    /// a 512 byte packet every 60 seconds, the overall network bandwidth consumed would be about 840Kbps,
    /// which is approaching 1% of an 100Mpbs network.  Bandwidth consumed increases by the square of the 
    /// number of clients, so the bandwidth consumed will explode as the number of clients increases.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class UdpBroadcastServer : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds the performance counters maintained by the server.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string IsMaster_Name                    = "IsMaster";
            const string AdminMessageRate_Name            = "Admin Messages/sec";
            const string AdminByteRate_Name               = "Admin Bytes/sec";
            const string BroadcastReceiveMessageRate_Name = "Broadcast Messages Received/sec";
            const string BroadcastReceiveByteRate_Name    = "Broadcast Receive Bytes/sec";
            const string BroadcastSendMessageRate_Name    = "Broadcast Messages Sent/sec";
            const string BroadcastSendByteRate_Name       = "Broadcast Send Bytes/sec";
            const string TotalMessageRate_Name            = "Total Messages/sec";
            const string TotalByteRate_Name               = "Total Bytes/sec";
            const string ServerCount_Name                 = "UDP Servers";
            const string ClientCount_Name                 = "UDP Clients";
            const string Runtime_Name                     = "Runtime (min)";

            /// <summary>
            /// Installs the service's performance counters by adding them to the
            /// performance counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public static void Install(PerfCounterSet perfCounters, string perfPrefix)
            {
                if (perfCounters == null)
                    return;

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                perfCounters.Add(new PerfCounter(perfPrefix + IsMaster_Name, "Indicates if this is the cluster master (0/1)", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + AdminMessageRate_Name, "Admin Messages/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + AdminByteRate_Name, "Admin Bytes/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + BroadcastReceiveMessageRate_Name, "Broadcast Messages Received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + BroadcastReceiveByteRate_Name, "Broadcast Receive Bytes/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + BroadcastSendMessageRate_Name, "Broadcast Messages Sent/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + BroadcastSendByteRate_Name, "Broadcast Send Bytes/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + TotalMessageRate_Name, "Total Messages (sent or received)/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + TotalByteRate_Name, "Total Bytes (sent or received)/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + ServerCount_Name, "Number of UDP broadcast servers in the cluster", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + ClientCount_Name, "Number of UDP broadcast clients in the cluster", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter IsMaster;                       // (0 or 1) indicating whether this is the cluster master
            public PerfCounter AdminMessageRate;               // # of admin messages received/sec
            public PerfCounter AdminByteRate;                  // # of admin bytes received/sec
            public PerfCounter BroadcastReceiveMessageRate;    // # of broadcast messages received/sec
            public PerfCounter BroadcastReceiveByteRate;       // # of broadcast bytes received/sec
            public PerfCounter BroadcastSendMessageRate;       // # of broadcast messages sent/sec
            public PerfCounter BroadcastSendByteRate;          // # of broadcast bytes sent/sec
            public PerfCounter TotalMessageRate;               // # of messages sent or received/sec
            public PerfCounter TotalByteRate;                  // # of bytes sent or received/sec
            public PerfCounter ServerCount;                    // # servers in the cluster
            public PerfCounter ClientCount;                    // # clients in the cluster
            public PerfCounter Runtime;                        // Service runtime in minutes

            /// <summary>
            /// Initializes the service's performance counters from the performance
            /// counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public Perf(PerfCounterSet perfCounters, string perfPrefix)
            {
                Install(perfCounters, perfPrefix);

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                if (perfCounters != null)
                {
                    IsMaster                    = perfCounters[perfPrefix + IsMaster_Name];
                    AdminMessageRate            = perfCounters[perfPrefix + AdminMessageRate_Name];
                    AdminByteRate               = perfCounters[perfPrefix + AdminByteRate_Name];
                    BroadcastReceiveMessageRate = perfCounters[perfPrefix + BroadcastReceiveMessageRate_Name];
                    BroadcastReceiveByteRate    = perfCounters[perfPrefix + BroadcastReceiveByteRate_Name];
                    BroadcastSendMessageRate    = perfCounters[perfPrefix + BroadcastSendMessageRate_Name];
                    BroadcastSendByteRate       = perfCounters[perfPrefix + BroadcastSendByteRate_Name];
                    TotalMessageRate            = perfCounters[perfPrefix + TotalMessageRate_Name];
                    TotalByteRate               = perfCounters[perfPrefix + TotalByteRate_Name];
                    ServerCount                 = perfCounters[perfPrefix + ServerCount_Name];
                    ClientCount                 = perfCounters[perfPrefix + ClientCount_Name];
                    Runtime                     = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {

                    IsMaster                    =
                    AdminMessageRate            =
                    AdminByteRate               =
                    BroadcastReceiveMessageRate =
                    BroadcastReceiveByteRate    =
                    BroadcastSendMessageRate    =
                    BroadcastSendByteRate       =
                    TotalMessageRate            =
                    TotalByteRate               =
                    ServerCount                 =
                    ClientCount                 =
                    Runtime                     = PerfCounter.Stub;
                }
            }
        }

        /// <summary>
        /// Used to track the status of known UDP broadcast clients.
        /// </summary>
        internal sealed class ClientState
        {

            public IPEndPoint   EndPoint;           // UDP endpoint for the client
            public int          BroadcastGroup;     // Client's broadcast group
            public DateTime     TTD;                // Client time-to-die if not renewed (SYS)

            public ClientState(IPEndPoint endPoint, int broadcastGroup, DateTime ttd)
            {
                this.EndPoint       = endPoint;
                this.BroadcastGroup = broadcastGroup;
                this.TTD            = ttd;
            }
        }

        /// <summary>
        /// Used to track the status of known UDP broadcast clients.
        /// </summary>
        internal sealed class ServerState
        {
            public IPEndPoint   EndPoint;           // UDP endpoint for the server
            public DateTime     TTD;                // Server time-to-die if not renewed (SYS)

            public ServerState(IPEndPoint endPoint, DateTime ttd)
            {
                this.EndPoint = endPoint;
                this.TTD      = ttd;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the performance counters managed by the class to the performance counter
        /// set passed (if not null).  This will be called during the application installation
        /// process when performance counters are being installed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        public static void InstallPerfCounters(PerfCounterSet perfCounters, string perfPrefix)
        {
            Perf.Install(perfCounters, perfPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                              syncLock = new object();
        private UdpBroadcastServerSettings          settings;       // Holds the server settings
        private EnhancedSocket                      socket;         // UDP socket
        private GatedTimer                          bkTimer;        // Background timer
        private AsyncCallback                       onReceive;      // Handles received UDP packats
        private byte[]                              recvBuf;        // Packet receive buffer
        private EndPoint                            rawRecvEP;      // Packet receive source endpoint
        private PolledTimer                         registerTimer;  // Fires when it's time to send a ServerRegister message
        private Dictionary<IPEndPoint, ServerState> servers;        // Holds state of known cluster servers
        private Dictionary<IPEndPoint, ClientState> clients;        // Holds state of known cluster clients
        private bool                                closePending;   // True if the server is in the process of closing gracefully
        private DateTime                            startTime;      // Time the server was started (UTC)
        private Perf                                perf;           // Performance counters

        /// <summary>
        /// Creates and starts a UDP broadcast server, using configuration settings loaded from
        /// the application configuration at the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <param name="perfCounters">The application's performance counters (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="perfCounters" /> parameter is type as <see cref="object" /> so that
        /// applications using this class will not be required to reference the <b>LillTel.Advanced</b>
        /// assembly.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the settings passed are not valid.</exception>
        public UdpBroadcastServer(string keyPrefix, object perfCounters, string perfPrefix)
            : this(new UdpBroadcastServerSettings(keyPrefix), perfCounters, perfPrefix)
        {
        }

        /// <summary>
        /// Creates and starts a UDP broadcast server, using configuration settings loaded from
        /// the application configuration at the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <exception cref="ArgumentException">Thrown if the settings passed are not valid.</exception>
        public UdpBroadcastServer(string keyPrefix)
            : this(new UdpBroadcastServerSettings(keyPrefix), null, null)
        {
        }

        /// <summary>
        /// Creates and starts a UDP broadcast server using the settings passed.
        /// </summary>
        /// <param name="settings">The server settings.</param>
        /// <param name="perfCounters">The application's performance counters (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="perfCounters" /> parameter is type as <see cref="object" /> so that
        /// applications using this class will not be required to reference the <b>LillTel.Advanced</b>
        /// assembly.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the settings passed are not valid.</exception>
        public UdpBroadcastServer(UdpBroadcastServerSettings settings, object perfCounters, string perfPrefix)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            if (settings.Servers == null || settings.Servers.Length == 0)
                throw new ArgumentException("Invalid UDP broadcast server settings: At least one broadcast server endpoint is required.");

            if (perfCounters != null && !(perfCounters is PerfCounterSet))
                throw new ArgumentException("Only instances of type [PerfCounterSet] may be passed in the [perfCounters] parameter.", "perfCounters");

            this.startTime    = DateTime.UtcNow;
            this.closePending = false;
            this.settings     = settings;
            this.perf         = new Perf(perfCounters as PerfCounterSet, perfPrefix);

            bool found = false;

            for (int i = 0; i < settings.Servers.Length; i++)
            {
                var binding = settings.Servers[i];

                if (binding == settings.NetworkBinding)
                {
                    found = true;

                    // I'm going to special case the situation where the network binding address is ANY.
                    // In this case, one of the server endpoints must also include an ANY entry and I'll
                    // fill out with the loop back addres (127.1.0.1).

                    if (binding.IsAnyAddress)
                        settings.Servers[i] = new NetworkBinding(IPAddress.Loopback, settings.Servers[i].Port);

                    break;
                }
            }

            if (!found)
            {
                if (!settings.NetworkBinding.IsAnyAddress)
                    throw new ArgumentException("Invalid UDP broadcast server settings: The current server's network binding must also be present in the Servers[] bindings.");
            }

            // Initialize the clients and servers state.

            clients = new Dictionary<IPEndPoint, ClientState>();
            servers = new Dictionary<IPEndPoint, ServerState>();

            // Open the socket and start receiving packets.

            socket                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.IgnoreUdpConnectionReset = true;
            socket.ReceiveBufferSize        = settings.SocketBufferSize;
            socket.SendBufferSize           = settings.SocketBufferSize;

            socket.Bind(settings.NetworkBinding);

            onReceive = new AsyncCallback(OnReceive);
            recvBuf   = new byte[TcpConst.MTU];

            rawRecvEP = new IPEndPoint(IPAddress.Any, 0);
            socket.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref rawRecvEP, onReceive, null);

            // Crank up a timer to send the ClientRegister messages to the server cluster.

            registerTimer = new PolledTimer(settings.ClusterKeepAliveInterval, true);
            registerTimer.FireNow();    // Make sure that we send registration messages immediately

            bkTimer = new GatedTimer(new TimerCallback(OnBkTask), null, TimeSpan.Zero, settings.BkTaskInterval);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~UdpBroadcastServer()
        {
            Close();
        }

        /// <summary>
        /// Stops the server, removing it from the cluster.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method then the instance has already been closed.
        /// </note>
        /// </remarks>
        public void Close()
        {
            lock (syncLock)
            {
                if (closePending || socket == null)
                    return;

                var packet = GetMessageBytes(UdpBroadcastMessageType.ServerUnregister);

                foreach (var server in settings.Servers)
                    if (!PauseNetwork)
                        socket.SendTo(packet, server);

                closePending = true;
            }

            // Sleep for a couple seconds so that any broadcast messages in transit
            // will still be retransmitted during the time it will take for the
            // other servers in the cluster to decide on a new master.

            Thread.Sleep(2000);

            socket.Close();
            socket = null;

            if (bkTimer != null)
            {

                bkTimer.Dispose();
                bkTimer = null;
            }

            servers.Clear();
            clients.Clear();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources associated with the instance (equivalant to calling <see cref="Close" />).
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Set to <c>true</c> to simulate a network failure for unit testing purposes by causing
        /// the instance to ignore all inbound messages and transmit no outbound messages.
        /// </summary>
        internal bool PauseNetwork { get; set; }

        /// <summary>
        /// Returns the local UDP <see cref="IPEndPoint "/> for the instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the instance is closed.</exception>
        public IPEndPoint EndPoint
        {
            get
            {
                lock (syncLock)
                {
                    if (socket == null)
                        throw new InvalidOperationException("UDP Broadcast server is closed.");

                    return (IPEndPoint)socket.LocalEndPoint;
                }
            }
        }

        /// <summary>
        /// Used for unit testing.  Set this to something other than <see cref="DateTime.MinValue" />
        /// and the client will make sure that all messages transmitted will set this as the
        /// timestamp.  This is useful for tests that verify that messages with timestamps
        /// that exceed the limits are properly rejected.
        /// </summary>
        internal DateTime FixedTimestampUtc { get; set; }

        /// <summary>
        /// Constructs a <see cref="UdpBroadcastMessage"/> from the parameters passed and
        /// serializes it into the wire format.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>The packet bytes.</returns>
        private byte[] GetMessageBytes(UdpBroadcastMessageType messageType)
        {
            return GetMessageBytes(messageType, 0, null);
        }

        /// <summary>
        /// Constructs a <see cref="UdpBroadcastMessage"/> from the parameters passed and
        /// serializes it into the wire format.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>The packet bytes.</returns>
        /// <param name="broadcastGroup">The broadcast group.</param>
        private byte[] GetMessageBytes(UdpBroadcastMessageType messageType, int broadcastGroup)
        {
            return GetMessageBytes(messageType, IPAddress.Any, broadcastGroup, null);
        }

        /// <summary>
        /// Constructs a <see cref="UdpBroadcastMessage"/> from the parameters passed and
        /// serializes it into the wire format.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="broadcastGroup">The broadcast group.</param>
        /// <param name="payload">The broadcast packet payload.</param>
        /// <returns>The packet bytes.</returns>
        private byte[] GetMessageBytes(UdpBroadcastMessageType messageType, int broadcastGroup, byte[] payload)
        {
            return GetMessageBytes(messageType, IPAddress.Any, broadcastGroup, payload);
        }

        /// <summary>
        /// Constructs a <see cref="UdpBroadcastMessage"/> from the parameters passed and
        /// serializes it into the wire format.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="sourceAddress">The IP address of the message source.</param>
        /// <param name="broadcastGroup">The broadcast group.</param>
        /// <param name="payload">The broadcast packet payload.</param>
        /// <returns>The packet bytes.</returns>
        private byte[] GetMessageBytes(UdpBroadcastMessageType messageType, IPAddress sourceAddress, int broadcastGroup, byte[] payload)
        {
            var message = new UdpBroadcastMessage(messageType, sourceAddress, broadcastGroup, payload);

            if (FixedTimestampUtc > DateTime.MinValue)
                message.TimeStampUtc = FixedTimestampUtc;

            return message.ToArray(settings.SharedKey);
        }

        /// <summary>
        /// Simulates a UDP broadcast server failure by closing the instance without 
        /// transmitting <b>Server.Unregister</b> messages.
        /// </summary>
        internal void CloseFail()
        {
            lock (syncLock) 
            {
                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                if (bkTimer != null) 
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                servers.Clear();
                clients.Clear();
            }
        }

        /// <summary>
        /// Returns information about the current set of clients known to the server
        /// for use fopr whitebox unit testing.
        /// </summary>
        /// <returns>The array of client information.</returns>
        internal ClientState[] GetClients()
        {
            lock (syncLock)
            {
                var     state = new ClientState[clients.Count];
                int     i;

                i = 0;
                foreach (var client in clients.Values)
                    state[i++] = client;

                return state;
            }
        }

        /// <summary>
        /// Returns information about the current set of clients known to the server
        /// for use fopr whitebox unit testing.
        /// </summary>
        /// <returns>The array of server information.</returns>
        internal ServerState[] GetServers()
        {
            lock (syncLock)
            {
                var     state = new ServerState[servers.Count];
                int     i;

                i = 0;
                foreach (var server in servers.Values)
                    state[i++] = server;

                return state;
            }
        }

        /// <summary>
        /// Handles background tasks when the timer is fired.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            lock (syncLock)
            {
                if (socket == null)
                    return; // Server is closed

                // Update performance counters

                perf.IsMaster.RawValue    = IsMaster ? 1 : 0;
                perf.Runtime.RawValue     = (int)(DateTime.UtcNow - startTime).TotalMinutes;
                perf.ServerCount.RawValue = servers.Count;
                perf.ClientCount.RawValue = clients.Count;

                // Prune any servers and clients that have exceeded their TTL.

                var delServers = new List<ServerState>();
                var delClients = new List<ClientState>();
                var sysNow     = SysTime.Now;

                foreach (var server in servers.Values)
                    if (server.TTD <= sysNow)
                        delServers.Add(server);

                foreach (var server in delServers)
                    servers.Remove(server.EndPoint);

                foreach (var client in clients.Values)
                    if (client.TTD <= sysNow)
                        delClients.Add(client);

                foreach (var client in delClients)
                    clients.Remove(client.EndPoint);

                // Transmit periodic ServerRegister messages to the servers in the cluster.

                if (registerTimer.HasFired && !closePending)
                {
                    var packet = GetMessageBytes(UdpBroadcastMessageType.ServerRegister);
                    int cDelivered = 0;

                    foreach (var server in settings.Servers)
                        if (!PauseNetwork)
                        {
                            socket.SendTo(packet, server);
                            cDelivered++;
                        }

                    perf.AdminByteRate.IncrementBy(cDelivered * packet.Length);
                    perf.AdminMessageRate.IncrementBy(cDelivered);
                }
            }
        }

        /// <summary>
        /// Determines whether the current UDP broadcast server is currently the cluster master.
        /// </summary>
        /// <returns><c>true</c> if this is the master.</returns>
        internal bool IsMaster
        {
            get
            {
                lock (syncLock)
                {
                    if (socket == null || PauseNetwork)
                        return false;

                    string thisEndpoint = socket.LocalEndPoint.ToString();

                    foreach (var server in servers.Values)
                        if (String.Compare(server.EndPoint.ToString(), thisEndpoint, true) < 0)
                            return false;

                    return true;
                }
            }
        }

        /// <summary>
        /// Called when a packet is received on the socket.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnReceive(IAsyncResult ar)
        {
            UdpBroadcastMessage     message = null;
            IPEndPoint              recvEP  = null;
            int                     cbRecv  = 0;

            lock (syncLock)
            {
                if (socket == null)
                    return; // Client is closed

                try
                {
                    // Parse received packet.

                    cbRecv = socket.EndReceiveFrom(ar, ref rawRecvEP);
                    recvEP = (IPEndPoint)rawRecvEP;

                    if (cbRecv == 0)
                        return;     // This happens when we receive an ICMP(connection-reset) from a
                    // remote host that's actively refusing an earlier packet transmission.
                    // We're just going to ignore this.

                    perf.TotalMessageRate.Increment();
                    perf.TotalByteRate.IncrementBy(cbRecv);

                    message = new UdpBroadcastMessage(Helper.Extract(recvBuf, 0, cbRecv), settings.SharedKey);

                    // Validate that the message timestamp is reasonable and discard
                    // messages with timestamps from too far in the past or too far
                    // in the future.

                    DateTime now = DateTime.UtcNow;

                    if (!Helper.Within(now, message.TimeStampUtc, settings.MessageTTL))
                    {
                        SysLog.LogWarning("UDP Broadcast message timestamp out of range. SystemTime={0}, Timestamp={1}, Source={2}, BroadcastGroup={3}",
                                          now.ToString("u"), message.TimeStampUtc.ToString("u"), recvEP, message.BroadcastGroup);
                        return;
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
                finally
                {
                    // Initiate the next receive.

                    try
                    {
                        rawRecvEP = new IPEndPoint(IPAddress.Any, 0);
                        socket.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref rawRecvEP, onReceive, null);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                }

                // Process the message (if any).

                if (message == null || PauseNetwork)
                    return;

                ServerState     server;
                ClientState     client;

                switch (message.MessageType)
                {
                    case UdpBroadcastMessageType.ServerRegister:

                        // Add the server to the tracking table if it's not already
                        // present and update its TTD.

                        if (!servers.TryGetValue(recvEP, out server))
                        {
                            server = new ServerState(recvEP, SysTime.Now + settings.ServerTTL);
                            servers.Add(recvEP, server);
                        }
                        else
                            server.TTD = SysTime.Now + settings.ServerTTL;

                        perf.AdminByteRate.IncrementBy(cbRecv);
                        perf.AdminMessageRate.Increment();
                        break;

                    case UdpBroadcastMessageType.ServerUnregister:

                        // Remove the server from the tracking table (if present).

                        if (servers.ContainsKey(recvEP))
                            servers.Remove(recvEP);

                        perf.AdminByteRate.IncrementBy(cbRecv);
                        perf.AdminMessageRate.Increment();
                        break;

                    case UdpBroadcastMessageType.ClientRegister:

                        // Add the client to the tracking table if it's not already
                        // present and update its TTD.

                        if (!clients.TryGetValue(recvEP, out client))
                        {
                            client = new ClientState(recvEP, message.BroadcastGroup, SysTime.Now + settings.ServerTTL);
                            clients.Add(recvEP, client);
                        }
                        else
                            client.TTD = SysTime.Now + settings.ServerTTL;

                        perf.AdminByteRate.IncrementBy(cbRecv);
                        perf.AdminMessageRate.Increment();
                        break;

                    case UdpBroadcastMessageType.ClientUnregister:

                        // Remove the client from the tracking table (if present).

                        if (clients.ContainsKey(recvEP))
                            clients.Remove(recvEP);

                        perf.AdminByteRate.IncrementBy(cbRecv);
                        perf.AdminMessageRate.Increment();
                        break;

                    case UdpBroadcastMessageType.Broadcast:

                        // Transmit the message to all clients the belong to the same broadcast group,
                        // if this is the master server.

                        if (!IsMaster)
                            return;

                        var packet = GetMessageBytes(UdpBroadcastMessageType.Broadcast, message.SourceAddress, message.BroadcastGroup, message.Payload);
                        int cDelivered = 0;

                        foreach (var c in clients.Values)
                            if (c.BroadcastGroup == message.BroadcastGroup)
                            {
                                socket.SendTo(packet, c.EndPoint);
                                cDelivered++;
                            }

                        perf.BroadcastReceiveByteRate.IncrementBy(cbRecv);
                        perf.BroadcastReceiveMessageRate.Increment();
                        perf.BroadcastSendByteRate.IncrementBy(cbRecv * packet.Length);
                        perf.BroadcastSendMessageRate.IncrementBy(cDelivered);
                        break;
                }
            }
        }
    }
}
