//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The client side implementation of the the UdpBroadcastServer
//              that simulates UDP multicast and broadcast for use in network
//              environments (such as many cloud hosting services) that don't 
//              support this functionality.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Net.Broadcast
{
    /// <summary>
    /// Arguments passed when the <see cref="UdpBroadcastClient.PacketReceived" />
    /// event is raised.
    /// </summary>
    public class UdpBroadcastEventArgs : EventArgs
    {
        /// <summary>
        /// The IPv4 source address of the source of the broadcast packet.
        /// </summary>
        public IPAddress SourceAddress { get; internal set; }

        /// <summary>
        /// The received packet payload.
        /// </summary>
        public byte[] Payload { get; internal set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceAddress">The IPv4 source endpoint of the packet source.</param>
        /// <param name="payload">The received packet payload.</param>
        internal UdpBroadcastEventArgs(IPAddress sourceAddress, byte[] payload)
        {
            this.SourceAddress = sourceAddress;
            this.Payload       = payload;
        }
    }

    /// <summary>
    /// Delegate type for the <see cref="UdpBroadcastClient.PacketReceived" /> event.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="args">The event arguments.</param>
    public delegate void UdpBroadcastDelegate(object sender, UdpBroadcastEventArgs args);

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
    /// when the client automatically renews its registration.  The <b>broadcast group</b> is
    /// also passed with this message.  <b>ClientUnregister</b> messages are sent when
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
    /// domain name system which, by definition, has to located at fixed static IP addresses.
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
    public class UdpBroadcastClient : IDisposable
    {
        private object                      syncLock = new object();
        private UdpBroadcastClientSettings  settings;               // Client settings
        private List<IPEndPoint>            servers;                // List of broadcast server endpoints
        private EnhancedSocket              socket;                 // UDP socket
        private AsyncCallback               onReceive;              // Async socket receive callback
        private byte[]                      recvBuf;                // Socket receive buffer
        private EndPoint                    rawRecvEP;              // Accepts the source endpoint for received socket packets
        private GatedTimer                  bkTimer;                // Background task timer
        private PolledTimer                 keepAliveTimer;         // Fires when a keep alive must be sent to the serves
        private PolledTimer                 serverResolveTimer;     // Fires when it's time to re-resolve the server DNS names
        private IPAddress                   sourceAddress;          // IP address to use as this instance's source address

        /// <summary>
        /// Maximum UDP message envelope size in bytes.
        /// </summary>
        public const int MessageEnvelopeSize = UdpBroadcastMessage.EnvelopeSize;

        /// <summary>
        /// Raised when a broadcast packet is received.
        /// </summary>
        public event UdpBroadcastDelegate PacketReceived;

        /// <summary>
        /// Creates and starts a UDP broadcast client, using configuration settings loaded from
        /// the application configuration at the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <exception cref="ArgumentException">Thrown if the settings passed are not valid.</exception>
        public UdpBroadcastClient(string keyPrefix)
            : this(new UdpBroadcastClientSettings(keyPrefix))
        {
        }

        /// <summary>
        /// Creates and starts a UDP broadcast client using the settings passed.
        /// </summary>
        /// <param name="settings">The client settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings passed are not valid.</exception>
        public UdpBroadcastClient(UdpBroadcastClientSettings settings)
        {
            this.settings = settings;

            if (settings == null)
                throw new ArgumentNullException("settings");

            if (settings.Servers == null || settings.Servers.Length == 0)
                throw new ArgumentException("Invalid UDP broadcast client settings: At least one broadcast server endpoint is required.");

            this.servers = new List<IPEndPoint>();

            // $hack(jeff.lill)
            //
            // This is a bit of a hack to discover the source IP address to use for this instance.
            // If the configured NetworkBinding specifies a specific interface, then we'll use
            // this, otherwise we'll use the IPv4 address for the first active network adaptor we find.
            // In a perfect world, I'd send a packet to the broadcast servers and have it respond with
            // the UDP source address it sees that would discover the actual IP address.  This hack
            // should work 99% of the time though.

            if (!settings.NetworkBinding.Address.Equals(IPAddress.Any))
                this.sourceAddress = settings.NetworkBinding.Address;
            else
                this.sourceAddress = NetHelper.GetActiveAdapter();

            // Open the socket and start receiving packets.

            socket                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.IgnoreUdpConnectionReset = true;
            socket.ReceiveBufferSize        = settings.SocketBufferSize;
            socket.SendBufferSize           = settings.SocketBufferSize;

            socket.Bind(settings.NetworkBinding);

            onReceive = new AsyncCallback(OnReceive);
            recvBuf = new byte[8192];

            rawRecvEP = new IPEndPoint(IPAddress.Any, 0);
            socket.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref rawRecvEP, onReceive, null);

            // Crank up the timers.

            keepAliveTimer = new PolledTimer(settings.KeepAliveInterval, false);
            keepAliveTimer.FireNow();

            serverResolveTimer = new PolledTimer(settings.ServerResolveInterval, false);
            serverResolveTimer.FireNow();

            bkTimer = new GatedTimer(new TimerCallback(OnBkTask), null, TimeSpan.Zero, settings.BkTaskInterval);

            // Sleep for a couple seconds to allow the server DNS lookups to complete.

            Thread.Sleep(2000);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~UdpBroadcastClient()
        {
            Close();
        }

        /// <summary>
        /// Closes the instance, removing it from the broadcast group.
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
                if (socket != null)
                {
                    var packet = GetMessageBytes(UdpBroadcastMessageType.ClientUnregister, settings.BroadcastGroup);

                    foreach (var server in servers)
                        if (!PauseNetwork)
                        {
                            try
                            {
                                socket.SendTo(packet, server);
                            }
                            catch (Exception e)
                            {
                                SysLog.LogException(e);
                            }
                        }

                    socket.Close();
                    socket = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources associated with the instance (equivalent to calling <see cref="Close" />).
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
        /// Simulates a UDP broadcast client failure by closing the instance without 
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
        /// Used for unit testing.  Returns an array of the current server IP endpoints as
        /// determined periodically by the client by resolving any host name references
        /// in the configuration settings into IP addresses.
        /// </summary>
        /// <returns>The current server IP endpoints.</returns>
        internal IPEndPoint[] GetServerEndpoints() 
        {
            lock (syncLock)
            {
                if (socket == null || servers == null)
                    return new IPEndPoint[0];
                else
                    return servers.ToArray();
            }
        }

        /// <summary>
        /// Used for unit testing to gain access to the client settings.
        /// </summary>
        internal UdpBroadcastClientSettings Settings
        {
            get { return settings; }
        }

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
                        throw new InvalidOperationException("UDP Broadcast client is closed.");

                    return (IPEndPoint)socket.LocalEndPoint;
                }
            }
        }

        /// <summary>
        /// Broadcasts the packet data passed to all instances in the same broadcast group
        /// (including the current instance).
        /// </summary>
        /// <param name="payload">The packet data.</param>
        /// <exception cref="InvalidOperationException">Thrown if the broadcast client is closed.</exception>
        public void Broadcast(byte[] payload)
        {
            lock (syncLock)
            {
                if (socket == null)
                    throw new InvalidOperationException("UDP broadcast client is closed.");

                var packet = GetMessageBytes(UdpBroadcastMessageType.Broadcast, settings.BroadcastGroup, payload);

                foreach (var server in servers)
                    if (!PauseNetwork)
                        socket.SendTo(packet, server);
            }
        }

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
            return GetMessageBytes(messageType, broadcastGroup, null);
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
            var message = new UdpBroadcastMessage(messageType, sourceAddress, broadcastGroup, payload);

            if (FixedTimestampUtc > DateTime.MinValue)
                message.TimeStampUtc = FixedTimestampUtc;

            return message.ToArray(settings.SharedKey);
        }

        /// <summary>
        /// Handles the background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            // Perform the name server lookup outside of the lock
            // so we won't block packet reception while the lookups
            // proceed.

            List<IPEndPoint> newServers = null;

            if (socket != null && serverResolveTimer.HasFired)
            {
                try
                {
                    newServers = new List<IPEndPoint>();

                    foreach (var binding in settings.Servers)
                    {
                        if (!binding.IsHost)
                        {
                            newServers.Add(binding);
                            continue;
                        }

                        try
                        {
                            foreach (var address in Dns.GetHostAddresses(binding.Host).IPv4Only())
                                newServers.Add(new IPEndPoint(address, binding.Port));

                            if (newServers.Count == 0)
                                newServers = null;
                        }
                        catch
                        {
                            // Ignoring
                        }
                    }
                }
                finally
                {
                    serverResolveTimer.Reset();
                }
            }

            lock (syncLock)
            {
                if (socket == null)
                    return; // Client is closed

                if (newServers != null)
                    servers = newServers;

                if (keepAliveTimer.HasFired)
                {
                    try
                    {
                        if (servers != null)
                        {
                            // Transmit a broadcast message to all of the cluster servers.

                            var packet = GetMessageBytes(UdpBroadcastMessageType.ClientRegister, settings.BroadcastGroup);

                            foreach (var server in servers)
                                if (!PauseNetwork)
                                    socket.SendTo(packet, server);
                        }
                    }
                    finally
                    {
                        if (servers == null)
                            keepAliveTimer.ResetImmediate();
                        else
                            keepAliveTimer.Reset();
                    }
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
            IPEndPoint              recvEP;
            int                     cbRecv;

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
            }

            // Process the message (if any) outside of the lock.  Note that only
            // broadcast messages are processed by UDP broadcast clients.

            if (message == null || message.MessageType != UdpBroadcastMessageType.Broadcast || PauseNetwork)
                return;

            // Ignore messages that don't match the broadcast group.

            if (settings.BroadcastGroup != message.BroadcastGroup)
                return;

            if (PacketReceived != null)
                PacketReceived(this, new UdpBroadcastEventArgs(message.SourceAddress, message.Payload));
        }
    }
}
