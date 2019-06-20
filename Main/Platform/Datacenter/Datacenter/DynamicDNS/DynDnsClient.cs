//-----------------------------------------------------------------------------
// FILE:        DynDnsClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side access to the Dynamic DNS Service.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Provides the client side access to the <b>Dynamic DNS Service</b>.
    /// </summary>
    /// <threadsafety instance="true" />
    /// <remarks>
    /// <para>
    /// You'll use the <see cref="DynDnsClient" /> class in applications that need
    /// to register host names with a dynamic DNS service cluster.  To do this,
    /// you need to instantiate an instance and then establish a relationship
    /// with the dynamic DNS cluster by calling <see cref="Open" />, passing
    /// <see cref="DynDnsClientSettings" /> with the client configuration settings.
    /// The desired host registrations can be specified in these settings.
    /// </para>
    /// <para>
    /// The application can also use the <see cref="Register(DynDnsHostEntry)" />, 
    /// <see cref="Unregister" />, and <see cref="UnregisterAll" /> methods to dynamically 
    /// manage the set of hosts registered with the dynamic cluster.  Note that these methods 
    /// can be called before calling <see cref="Open" />.
    /// </para>
    /// <para>
    /// Call <see cref="Close" /> to gracefully sever the relationship with the
    /// dynamic DNS cluster, so that all registrations made by this instance
    /// will be promply purged across the cluster.
    /// </para>
    /// </remarks>
    public sealed class DynDnsClient : ILockable
    {
        /// <summary>
        /// The default Dynamic DNS cluster base endpoint.
        /// </summary>
        public const string AbstractBaseEP = "abstract://LillTek/DataCenter/DynDNS";

        private object                  syncLock;
        private bool                    isOpen;
        private DynDnsClientSettings    settings;
        private MsgRouter               router;
        private ClusterMember           cluster;
        private bool                    enabled;
        private EnhancedSocket          socket;
        private GatedTimer              bkTimer;
        private PolledTimer             udpRegisterTimer;
        private PolledTimer             domainRefreshTimer;

        private Dictionary<DynDnsHostEntry, DynDnsHostEntry> hosts;

        /// <summary>
        /// Constructor.
        /// </summary>
        public DynDnsClient()
        {
            this.syncLock = this;
            this.isOpen   = false;
            this.router   = null;
            this.cluster  = null;
            this.enabled  = false;
            this.settings = null;
            this.bkTimer  = null;
            this.hosts    = new Dictionary<DynDnsHostEntry, DynDnsHostEntry>();
        }

        /// <summary>
        /// Opens the <see cref="DynDnsClient" /> instance enabling it to join
        /// the Dynamic DNS Service cluster and to start replicating host/IP address
        /// associations.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to be associated with the client (or <c>null</c>).</param>
        /// <param name="settings">The <see cref="DynDnsClientSettings" /> to be used.</param>
        /// <exception cref="InvalidOperationException">Thrown if the instance is already open.</exception>
        /// <remarks>
        /// <note>
        /// The <see cref="ClusterMemberSettings" />' <see cref="ClusterMemberSettings.Mode" /> 
        /// property will be ignored and the instance aill join the cluster as an
        /// <see cref="ClusterMemberMode.Observer" />.  The rest of the member settings
        /// should be identical to the settings across all cluster instances to avoid
        /// strange protocol problems.
        /// </note>
        /// </remarks>
        public void Open(MsgRouter router, DynDnsClientSettings settings)
        {
            var orgMode = ClusterMemberMode.Unknown;

            if (this.isOpen)
                throw new InvalidOperationException("DynDnsClient is already open.");

            if (settings.Mode == DynDnsMode.Both)
                throw new ArgumentException("DynDnsClient does not support [Mode=BOTH].");

            this.isOpen   = true;
            this.settings = settings;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Initialize

            enabled = settings.Enabled;
            if (!enabled)
                return;

            if (settings.Cluster != null)
            {
                orgMode               = settings.Cluster.Mode;  // $hack(jeff.lill): This is a bit of a hack
                settings.Cluster.Mode = ClusterMemberMode.Observer;
            }

            foreach (var entry in settings.Hosts)
                Register(entry);

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (settings.Mode == DynDnsMode.Cluster)
                    {
                        if (router == null)
                            throw new ArgumentException("DynDnsClient requires a valid router when operating with [Mode=CLUSTER].");

                        if (settings.Cluster == null)
                            throw new ArgumentException("Cluster settings must be initialized when [Mode=CLUSTER].");

                        this.router  = router;
                        this.cluster = new ClusterMember(router, settings.Cluster);

                        foreach (DynDnsHostEntry host in settings.Hosts)
                            hosts[host] = host;

                        this.cluster.Start();

                        Register();     // Add any pre-existing host registrations to
                                        // the cluster member state
                    }
                    else if (settings.Mode == DynDnsMode.Udp)
                    {
                        this.udpRegisterTimer = new PolledTimer(settings.UdpRegisterInterval, true);
                        this.udpRegisterTimer.FireNow();

                        this.domainRefreshTimer = new PolledTimer(settings.DomainRefreshInterval, true);
                        this.domainRefreshTimer.FireNow();

                        this.bkTimer = new GatedTimer(new TimerCallback(OnBkTask), null, settings.BkInterval);

                        this.socket = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        this.socket.IgnoreUdpConnectionReset = true;
                        this.socket.Bind(settings.NetworkBinding);
                    }
                }
            }
            catch
            {
                Close();
                throw;
            }
            finally
            {
                if (settings.Cluster != null)
                    settings.Cluster.Mode = orgMode;
            }
        }

        /// <summary>
        /// Returns the <see cref="ClusterMember" /> used to replicate host registrations
        /// to the Dynamic DNS cluster.
        /// </summary>
        public ClusterMember Cluster
        {
            get { return cluster; }
        }

        /// <summary>
        /// Returns a copy of the <see cref="DynDnsHostEntry" /> registrations currently
        /// exposed to the Dynamic DNS cluster.
        /// </summary>
        /// <returns>A <see cref="DynDnsHostEntry" /> array listing the current registrations.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the client has not been started.</exception>
        public DynDnsHostEntry[] GetHostRegistrations()
        {
            using (TimedLock.Lock(syncLock))
            {
                var list = new DynDnsHostEntry[hosts.Count];
                int i;

                i = 0;
                foreach (DynDnsHostEntry hostEntry in hosts.Values)
                    list[i++] = hostEntry;

                return list;
            }
        }

        /// <summary>
        /// Internal method that writes the host/entry registrations to the cluster member properties.
        /// </summary>
        private void Register()
        {
            TimedLock.AssertLocked(syncLock);

            if (!enabled || cluster == null)
                return;

            int index = 0;

            cluster.Clear();
            foreach (DynDnsHostEntry hostEntry in hosts.Values)
                cluster.Set(string.Format("host[{0}:{1}]", hostEntry.Host, index++), hostEntry.ToString());
        }

        /// <summary>
        /// Registers a <see cref="DynDnsHostEntry" /> with the Dynamic DNS cluster.
        /// </summary>
        /// <param name="hostEntry">The host/IP address <see cref="DynDnsHostEntry" /> to be registered.</param>
        /// <remarks>
        /// <note>
        /// This method does not throw an exception if the registration is already registered.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the client has not been started.</exception>
        public void Register(DynDnsHostEntry hostEntry)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!hosts.ContainsKey(hostEntry))
                {
                    hosts[hostEntry] = hostEntry;
                    Register();
                }
            }
        }

        /// <summary>
        /// Removes a <see cref="DynDnsHostEntry" /> registration from the Dynamic
        /// DNS cluster.
        /// </summary>
        /// <param name="hostEntry">The host/IP address <see cref="DynDnsHostEntry" /> to be unregistered.</param>
        /// <remarks>
        /// <note>
        /// This method does not throw an exception if the registration is not present.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the client has not been started.</exception>
        public void Unregister(DynDnsHostEntry hostEntry)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (hosts.ContainsKey(hostEntry))
                {
                    // For UDP mode, we need to send two immediate Unregister message to each DNS
                    // server for the host entry.

                    if (settings.Mode == DynDnsMode.Udp && isOpen)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            DynDnsMessage   message;
                            byte[]          packet;

                            message = new DynDnsMessage(DynDnsMessageFlag.OpUnregister, hostEntry);
                            packet  = message.ToArray(settings.SharedKey);

                            foreach (var nameServer in settings.NameServers)
                                socket.SendTo(packet, nameServer);
                        }
                    }

                    // Remove the host entry from the local table.

                    hosts.Remove(hostEntry);
                    Register();
                }
            }
        }

        /// <summary>
        /// Removes all <see cref="DynDnsHostEntry" />s registered with the Dynamic DNS cluster.
        /// <exception cref="InvalidOperationException">Thrown if the client has not been started.</exception>
        /// </summary>
        public void UnregisterAll()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (hosts.Count > 0)
                {
                    // For UDP mode, we need to send two immediate Unregister messages to each DNS
                    // server for the host entry.

                    if (settings.Mode == DynDnsMode.Udp && isOpen)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            DynDnsMessage   message;
                            byte[]          packet;

                            foreach (var entry in hosts.Values)
                            {
                                message = new DynDnsMessage(DynDnsMessageFlag.OpUnregister, entry);
                                packet  = message.ToArray(settings.SharedKey);

                                foreach (var nameServer in settings.NameServers)
                                    socket.SendTo(packet, nameServer);
                            }
                        }
                    }

                    // Remove all host entries from the local table.

                    hosts.Clear();
                    Register();
                }
            }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    return;

                // If we're running in UDP mode, then send a couple deregister messages
                // for all host entries to each known name server.  This will ensure that
                // the DNS updated quickly when servers are shut down gracefully.  Note
                // that I'm going to send duplicate messages to each server just to be
                // on the safe side.

                if (settings.Mode == DynDnsMode.Udp)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        DynDnsMessage   message;
                        byte[]          packet;

                        foreach (var entry in hosts.Values)
                        {
                            message = new DynDnsMessage(DynDnsMessageFlag.OpUnregister, entry);
                            packet  = message.ToArray(settings.SharedKey);

                            foreach (var nameServer in settings.NameServers)
                                socket.SendTo(packet, nameServer);

                            Thread.Sleep(500);
                        }
                    }
                }

                // Shut down the client.

                if (cluster != null)
                {
                    cluster.Stop();
                    cluster = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                router = null;
                isOpen = false;
            }
        }

        /// <summary>
        /// Handles background tasks when operating in UDP mode.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!isOpen || settings.Mode != DynDnsMode.Udp)
                        return;

                    if (settings.Domain.IsHost && domainRefreshTimer.HasFired)
                    {
                        // Perform a NS lookup for the domain and populate 
                        // settings.NameServer with the results.

                        // $todo(jeff.lill):
                        //
                        // Note that I'm going to ignore any CNAME records returned
                        // for now.  It might be important to make sure these are
                        // added to the staticHosts table, but I need to think about
                        // this some more.

                        try
                        {
                            var         nameServers = new Dictionary<IPAddress, bool>();
                            string      qname;
                            DnsRequest  request;
                            DnsResponse response;

                            qname = settings.Domain.Host;
                            if (!qname.EndsWith("."))
                                qname += ".";

                            request  = new DnsRequest(DnsFlag.RD, qname, DnsQType.NS);
                            response = DnsResolver.QueryWithRetry(NetHelper.GetDnsServers(), request, TimeSpan.FromMilliseconds(500), 4);

                            if (response.RCode != DnsFlag.RCODE_OK)
                                SysLog.LogWarning("DynDnsClient: Domain NS query failed with error [{0}].", response.RCode);
                            else
                            {
                                // Build the set of name server endpoints by resolving the
                                // name server names returned in the NS response into IP addresses.

                                foreach (var answer in response.Answers)
                                    if (answer.RRType == DnsRRType.NS)
                                    {
                                        NS_RR nsAnswer = (NS_RR)answer;

                                        try
                                        {
                                            foreach (var address in Dns.GetHostAddresses(nsAnswer.NameServer).IPv4Only())
                                                if (!nameServers.ContainsKey(address))
                                                    nameServers.Add(address, true);
                                        }
                                        catch
                                        {
                                            // Ignorning
                                        }
                                    }

                                if (nameServers.Count == 0)
                                {
                                    // Note that I'm going to keep any name server addresses I already have
                                    // in the case where the name server queries above return nothing.  This
                                    // will give us some resiliency in the face of transient network problems, etc.
                                    // I'm going to set the timer to retry in 1 minute in this case.

                                    domainRefreshTimer.ResetTemporary(TimeSpan.FromMinutes(1));
                                }
                                else
                                {
                                    var newServers = new List<NetworkBinding>(nameServers.Count);

                                    foreach (var address in nameServers.Keys)
                                        newServers.Add(new IPEndPoint(address, settings.Domain.Port));

                                    settings.NameServers = newServers.ToArray();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            domainRefreshTimer.Reset(TimeSpan.FromSeconds(60));
                            SysLog.LogException(e);
                        }
                    }

                    if (udpRegisterTimer.HasFired)
                    {
                        // It's time to transmit host registrations to the dynamic DNS servers.

                        DynDnsMessage    message;
                        byte[]          packet;

                        foreach (var entry in hosts.Values)
                        {
                            message = new DynDnsMessage(DynDnsMessageFlag.OpRegister, entry);
                            packet  = message.ToArray(settings.SharedKey);

                            foreach (var nameServer in settings.NameServers)
                                socket.SendTo(packet, nameServer);
                        }
                    }
                }
            }
            catch (Exception e)
            {

                SysLog.LogException(e);
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
