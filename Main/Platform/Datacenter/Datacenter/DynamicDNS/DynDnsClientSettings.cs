//-----------------------------------------------------------------------------
// FILE:        DynDnsClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the configuration settings for DynDnsClient.

using System;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Cryptography;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Defines the configuration settings for <see cref="DynDnsClient" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default constructor initializes an instance with reaasonable default values for all settings
    /// so application need only modify those settings that are important.  The <see cref="DynDnsClientSettings(string)" />
    /// constructor can also be used to create an instance by reading from the application configuration.
    /// </para>
    /// </remarks>
    public sealed class DynDnsClientSettings
    {
        /// <summary>
        /// Indicates whether the dynamic DNS client should be enabled.
        /// This defaults to <c>true</c>.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// Specifies the <see cref="NetworkBinding" /> the DNS client 
        /// should use for sending UDP host registration messages to the 
        /// DNS servers.
        /// </summary>
        public NetworkBinding NetworkBinding = NetworkBinding.Any;

        /// <summary>
        /// Controls how the client registers hosts with the DNS server.  The possible values
        /// are <see cref="DynDnsMode.Udp" /> or <see cref="DynDnsMode.Cluster"/>.
        /// <see cref="DynDnsMode.Both" /> is not allowed for DNS clients.
        /// </summary>
        public DynDnsMode Mode = DynDnsMode.Cluster;

        /// <summary>
        /// Shared symmetric encryption key used to decrypt UDP registration messages
        /// sent by DNS clients while in <see cref="DynDnsMode.Udp" /> mode.  This
        /// key must match the shared key configured for the client.  This defaults
        /// to the same reasonable default used by the DNS server class.
        /// </summary>
        public SymmetricKey SharedKey = new SymmetricKey("aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A==");

        /// <summary>
        /// Specifies the name server domain for the name server.  If this is
        /// specified, the DNS client will periodically query DNS for the NS
        /// records for the domain and then use the IP addresses to send UDP
        /// host registration messages to the servers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This setting must be formatted as a network binding with a host name
        /// and port, such as <b>LILLTEK.NET:DYNAMIC-DNS</b>.
        /// </para>
        /// <note>
        /// One of <b>Domain</b> or <b>NameServer</b> must be specified when when <b>Mode=UDP</b>.
        /// If both settings are present, then <b>Domain</b> will be used.
        /// </note>
        /// </remarks>
        public NetworkBinding Domain = NetworkBinding.Any;

        /// <summary>
        /// Specifies the network bindings for the DNS servers for the delivery
        /// of UDP host registration messages.  These entries may include
        /// IP addresses or host names, but note that host name lookups are
        /// performed only once by the server, when it starts.
        /// </summary>
        /// <remarks>
        /// <note>
        /// One of <b>Domain</b> or <b>NameServer</b> must be specified when when <b>Mode=UDP</b>.
        /// If both settings are present, then <b>Domain</b> will be used.
        /// </note>
        /// </remarks>
        public NetworkBinding[] NameServers = new NetworkBinding[0];

        /// <summary>
        /// The host registrations.  This defaults to an empty array.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The set of static host entries to be returned by the DNS server.  These
        /// entries are formatted as:
        /// </para>
        /// <code language="none">
        /// &lt;host name&gt; "," &lt;ip/cname&gt; [ "," &lt;TTL&gt; [ "," &lt;host-mode&gt; ] ]
        /// </code>
        /// <para>
        /// where <b>host name</b> is the DNS name being registered, <b>ip</b>/<b>cname</b>
        /// specifies the IP address or CNAME reference to the host, <b>TTL</b> is the optional
        /// time-to-live (TTL) to use for the entry in seconds, and <b>host-mode</b> is the optional 
        /// host entry mode, one of <b>ADDRESS</b>, <b>ADDRESSLIST</b>, <b>CNAME</b>, or <b>MX</b>.
        /// </para>
        /// <para>
        /// The <b>TTL</b> value defaults to 300 seconds (5 minutes) and the <b>host-mode</b>
        /// defaults to <b>ADDRESS</b> for IP addresses or <b>CNAME</b> for CNAME references.
        /// </para>
        /// <note>
        /// A host mode of <b>ADDRESS</b> or <b>ADDRESSLIST</b> can only be specified for IP
        /// addresses and <b>CNAME</b> can only be specified for CNAME entries.  IP addresses
        /// or host names can be specified for <b>MX</b> records.
        /// </note>
        /// </remarks>
        public DynDnsHostEntry[] Hosts = new DynDnsHostEntry[0];

        /// <summary>
        /// Minimum interval for which background activities will be scheduled.
        /// </summary>
        public TimeSpan BkInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Interval at which DNS NS queries will be performed to refresh the list of 
        /// name servers for a <b>Domain</b>.
        /// </summary>
        public TimeSpan DomainRefreshInterval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Interval at which UDP host registration messages will be sent to the
        /// DNS servers when operating in <b>Mode=UDP</b>.
        /// </summary>
        public TimeSpan UdpRegisterInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The <see cref="ClusterMemberSettings" />.  This defaults to <c>null</c>
        /// and must be initialized manually.
        /// </summary>
        public ClusterMemberSettings Cluster = null;

        /// <summary>
        /// Constructs an instance with reasonable defaults.
        /// </summary>
        public DynDnsClientSettings()
        {
        }

        /// <summary>
        /// Loads DNS client settings from a configuration section.
        /// </summary>
        /// <param name="keyPrefix">The configuration section key prefix (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The settings will loaded are:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Enabled</td>
        ///     <td>true</td>
        ///     <td>
        ///     Indicates whether the dynamic DNS client should be enabled.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>NetworkBinding</td>
        ///     <td>ANY</td>
        ///     <td>
        ///     Specifies the <see cref="NetworkBinding" /> the DNS client 
        ///     should use for sending UDP host registration messages to the 
        ///     DNS servers.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Mode</td>
        ///     <td>Cluster</td>
        ///     <td>
        ///     Controls how the client registers hosts with the DNS server.  The possible values
        ///     are <see cref="DynDnsMode.Udp" /> or <see cref="DynDnsMode.Cluster"/>.
        ///     <see cref="DynDnsMode.Both" /> is not allowed for DNS clients.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SharedKey</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     Shared symmetric encryption key used to decrypt UDP registration messages
        ///     sent by DNS clients while in <see cref="DynDnsMode.Udp" /> mode.  This
        ///     key must match the shared key configured for the client.  This defaults
        ///     to a reasonable value.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Domain</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     <para>
        ///     Specifies the name server domain for the name server.  If this is
        ///     specified, the DNS client will periodically query DNS for the NS
        ///     records for the domain and then use the IP addresses to send UDP
        ///     host registration messages to the servers.
        ///     </para>
        ///     <para>
        ///     This setting must be formatted as a network binding with a host name
        ///     and port, such as <b>LILLTEK.NET:DYNAMIC-DNS</b>.
        ///     </para>
        ///     <note>
        ///     One of <b>Domain</b> or <b>NameServer</b> must be specified when when <b>Mode=UDP</b>.
        ///     If both settings are present, then <b>Domain</b> will be used.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>NameServer[#]</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     <para>
        ///     Specifies the network bindings for the DNS servers for the delivery
        ///     of UDP host registration messages.  These entries may include
        ///     IP addresses or host names, but note that host name lookups are
        ///     performed only once by the server, when it starts.
        ///     </para>
        ///     <note>
        ///     One of <b>Domain</b> or <b>NameServer</b> must be specified when when <b>Mode=UDP</b>.
        ///     If both settings are present, then <b>Domain</b> will be used.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkInterval</td>
        ///     <td>1s</td>
        ///     <td>
        ///     Minimum interval for which background activities will be scheduled.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DomainRefreshInterval</td>
        ///     <td>15m</td>
        ///     <td>
        ///     Interval at which DNS NS queries will be performed to refresh the list of 
        ///     name servers for the specified <b>Domain</b>.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>UdpRegisterInterval</td>
        ///     <td>60s</td>
        ///     <td>
        ///     Interval at which UDP host registration messages will be sent to the
        ///     DNS servers when operating in <b>Mode=UDP</b>.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Host[#]</td>
        ///     <td>(optional)</td>
        ///     <td>
        ///     <para>
        ///     The set of static host entries to be returned by the DNS server.  These
        ///     entries are formatted as:
        ///     </para>
        ///     <code lang="none">
        ///     &lt;host name&gt; "," &lt;ip or cname&gt; [ "," &lt;TTL&gt; [ "," &lt;host-mode&gt; [ ";" "NAT" ] ] ]
        ///     </code>
        ///     <para>
        ///     where <b>host name</b> is the DNS name being registered, <b>ip</b> or <b>cname</b>
        ///     specifies the IP address or CNAME reference to the host, <b>TTL</b> is the optional
        ///     time-to-live (TTL) to use for the entry in seconds, and <b>host-mode</b> is the optional 
        ///     host entry mode, one of <b>ADDRESS</b>, <b>ADDRESSLIST</b>, or <b>CNAME</b>.
        ///     </para>
        ///     <para>
        ///     The <b>TTL</b> value defaults to -1 seconds (indicating that the server's default TTL
        ///     will be used) and the <b>host-mode</b> defaults to <b>ADDRESS</b> for IP addresses or 
        ///     <b>CNAME</b> for CNAME references. You can also set <b>TTL=-1</b> to use the DNS server
        ///     default for this.
        ///     </para>
        ///     <note>
        ///     A host mode of <b>ADDRESS</b> or <b>ADDRESSLIST</b> can only be specified for IP
        ///     addresses and <b>CNAME</b> can only be specified for CNAME entries.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Cluster</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     <b>Cluster</b> is a subsection in the configuration that
        ///     that specifies the settings required to establish a cooperative
        ///     cluster with the Dynamic DNS instances on the network while operating
        ///     in CLUSTER mode.  The client uses the <see cref="ClusterMember" /> class
        ///     to perform the work necessary to join the cluster.  The <b>ClusterBaseEP</b>
        ///     setting is required.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public DynDnsClientSettings(string keyPrefix)
        {
            Config                  config = new Config(keyPrefix);
            string[]                list;
            List<DynDnsHostEntry>   registrations;
            List<NetworkBinding>    bindings;

            list          = config.GetArray("Host");
            registrations = new List<DynDnsHostEntry>(list.Length);

            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    registrations.Add(DynDnsHostEntry.Parse(list[i]));
                }
                catch
                {
                    SysLog.LogWarning("DynamicDnsClient: Error parsing host registration [{0}Host[{1}]={2}].", config.KeyPrefix, i, list[i]);
                }
            }

            this.Hosts = registrations.ToArray();

            list       = config.GetArray("NameServer");
            bindings   = new List<NetworkBinding>(list.Length);

            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    bindings.Add(new NetworkBinding((list[i])));
                }
                catch
                {
                    SysLog.LogWarning("DynamicDnsClient: Error parsing name server binding [{0}NameServer[{1}]={2}].", config.KeyPrefix, i, list[i]);
                }
            }

            this.NameServers = bindings.ToArray();

            this.Enabled = config.Get("Enabled", this.Enabled);

            if (this.Enabled)
            {
                this.NetworkBinding        = config.Get("NetworkBinding", this.NetworkBinding);
                this.Mode                  = config.Get<DynDnsMode>("Mode", this.Mode);
                this.SharedKey             = new SymmetricKey(config.Get("SharedKey", "aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A=="));
                this.Domain                = config.Get("Domain", this.Domain);
                this.BkInterval            = config.Get("BkInterval", this.BkInterval);
                this.DomainRefreshInterval = config.Get("DomainRefreshInterval", this.DomainRefreshInterval);
                this.UdpRegisterInterval   = config.Get("UdpRegisterInterval", this.UdpRegisterInterval);

                if (this.Mode == DynDnsMode.Cluster)
                    this.Cluster = ClusterMemberSettings.LoadConfig(config.KeyPrefix + "Cluster");

                if (this.Mode == DynDnsMode.Both)
                    throw new FormatException("DynamicDnsClient: [Mode=BOTH] is not supported.");

                if (this.Mode == DynDnsMode.Udp && this.Domain.IsAny && this.NameServers.Length == 0)
                    throw new FormatException("DynDnsClient: One of DOMAIN or NAMESERVER[#] must be specified when [Mode=UDP].");
            }
        }
    }
}
