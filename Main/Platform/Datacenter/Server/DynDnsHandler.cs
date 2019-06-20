//-----------------------------------------------------------------------------
// FILE:        DynDnsHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the core functionality of the Dynamic DNS Service.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Messaging;
using LillTek.Net.Sockets;

// $todo(jeff.lill):
//
// I know how I'd like to implement location dependant lookups.
// This feature would provide for returning different IP addresses
// based on whether the DNS query came from within or outside of
// the datacenter.  The configuration would include a set of
// subnets that specify the internal network (like: 10.0.0.0/24)
// and the NAT entry in the host keys now become the network
// visibility setting with the possible values being:
//
//      NAT         - Same as implemented
//      LOCAL       - Address returned for internal requests
//      PUBLIC      - Address returned for public requests
//      ALL         - Address returned for all requests

// $todo(jeff.lill):
//
// The technique I'm using to construct the DNS host entries table
// will work reasonably well for perhaps a few hundred to a few
// thousand entries.  There will be a significant performance cost
// much beyond this.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements the core functionality of the Dynamic DNS Service which 
    /// provides load balancing, fail-over, and other specialized capabilities 
    /// to legacy applications calling LillTek based services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Dynamic DNS Service can be configured for four basic scenarios:
    /// </para>
    /// <list type="bullet">
    ///     <item>Load-Balancing and fail-over</item>
    ///     <item>Dynamic endpoint discovery</item>
    ///     <item>Static host registration</item>
    ///     <item>Registration of host behind a NAT</item>
    /// </list>
    /// <para>
    /// Applications that wish to register dynamic endpoints with the Dynamic
    /// DNS service will use the <see cref="DynDnsClient" /> class to do so.
    /// This class is responsible for registering and periodically refreshing
    /// host DNS entries managed by the DNS server.  Static host names can
    /// be configured via the DNS server's configuration file.
    /// </para>
    /// <para>
    /// Host name entries registered with the dynamic DNS can be configured
    /// to return a single A record, multiple A records, or a single CNAME record.
    /// This mode is specified by the <see cref="DynDnsHostMode" /> enumeration,
    /// whose possible values are <see cref="DynDnsHostMode.Address" />,
    /// <see cref="DynDnsHostMode.CName" />, or <see cref="DynDnsHostMode.AddressList" />.
    /// </para>
    /// <para>
    /// The DNS service can operate on one of three modes defined by the
    /// <see cref="DynDnsMode" /> enumeration.  While operating in the
    /// <see cref="DynDnsMode.Udp" /> mode, the DynDNS server will accept
    /// host registration messages sent via UDP by the DynDNS client.
    /// In the <see cref="DynDnsMode.Cluster" /> mode, the DynDNS clients
    /// and servers use LillTek clustering to share host entry state.
    /// In the <see cref="DynDnsMode.Both" /> mode, the DynDNS server 
    /// will accept UDP messages and also participate in a cluster.
    /// </para>
    /// <para><b>Dynamic Fail-over and Load Balancing</b></para>
    /// <para>
    /// The Dynamic DNS Service is used to dynamically expose the IP addresses
    /// of applications built on the LillTtek Platform to legacy applications
    /// based on older, more static protocols, such as HTTP.  Service applications
    /// will use the <see cref="DynDnsClient" /> class to register their
    /// presence with the Dynamic DNS Service instances which will in turn,
    /// expose the service IP endpoints via standard DNS queries.
    /// </para>
    /// <para>
    /// For example, assume that a Dynamic DNS Service instance is running
    /// and is configured into the global DNS system to be the authoritative
    /// name server for the <b>lilltek.net</b> zone.  We then decide to deploy 
    /// two Authorization Service instances which expose query entry points
    /// based on WCF and JSON over HTTP.  Rather than having to hardcode
    /// the authorization host names or IP addresses into legacy client
    /// applications or configuring some sort of hardware based load balancing
    /// solution, we will use the Dynamic DNS Service.
    /// </para>
    /// <para>
    /// The Authentication Service starts a  <see cref="DynDnsClient" /> instance
    /// and registers its IP address as <b>auth.lilltek.net</b>.  The <see cref="DynDnsClient" />
    /// transmits this information to the Dynamic DNS service cluster which keeps
    /// track of all the currently available authentication service instances and
    /// their IP address/host name pairs.
    /// </para>
    /// <para>
    /// Legacy applications will be configured to use the <b>auth.lilltek.net</b> host
    /// name to access the authentication service.  An example URI might be something
    /// like: <b>http://auth.lilltek.net/AuthService/Auth.json</b>.  Here's what happens
    /// when the legacy application submits this HTTP request:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     The application performs a DNS lookup for <b>auth.lilltek.net</b>.
    ///     </item>
    ///     <item>
    ///     The operating systems DNS resolver performs global DNS queries to
    ///     find the name server responsible for <b>lilltek.net</b>.  The
    ///     service returns the IP addresses for the Dynamic DNS Service instances.
    ///     </item>
    ///     <item>
    ///     The operating system selects one of the Dynamic DNS instances
    ///     and requests the IP address for <b>auth.lilltek.net</b>.
    ///     </item>
    ///     <item>
    ///     The Dynamic DNS service randomly selects one of the IP addresses
    ///     currently registered against the <b>auth</b> host name and
    ///     returns the result to the legacy application.
    ///     </item>
    ///     <item>
    ///     The legacy application uses this IP address to connect to the
    ///     authentication server and perform the query.
    ///     </item>
    ///     <item>
    ///     When an authentication service instance stops gracefully or
    ///     has not renewed its registration with the Dynamic DNS cluster
    ///     for some period of time, the cluster will remove the
    ///     offline authenticaton service's IP address from the association 
    ///     with the <b>auth.lilltek.net</b> host.
    ///     </item>
    /// </list>
    /// <para><b>Dynamic Endpoint Discovery</b></para>
    /// <para>
    /// In dynamic hosting environments such as the Windows Azure and Amazon Web Service
    /// cloud platforms, server instances will dynamically be assigned local
    /// and public IP addresses and host names when servers are started.  Although
    /// it is possible to assigned static IP addresses in some cases to these
    /// instances (known as Elastic IPs on AWS), doing so takes manual intervention
    /// or a custom tool and these addresses are typically Internet facing, not
    /// always what you want.
    /// </para>
    /// <para>
    /// The LillTek Dynamic DNS system provides a way for servers to dynamically
    /// register a host name to IP address or CNAME mapping with the domain name
    /// system.  This can be used to map publically available network endpoints
    /// as well as local endpoints that are reachable from only within the datacenter.
    /// </para>
    /// <para>
    /// An interesting example is making a SQL Server instance running on AWS
    /// available locally to other servers running on the cloud.  It would be
    /// possible to assign an Elastic IP to the instance manually and then
    /// have all of the other servers use this in their connection string but
    /// there are a couple disadvantages with doing so.
    /// </para>
    /// <para>
    /// First, the Elastic IP has to be manually associated with the SQL Server
    /// instance.  This adds a level of complexity and opportunity for error
    /// under stressful sutiations such as when the instance fails and needs
    /// to be restarted.  Second, using the Elastic IP means that internal network 
    /// traffic between the local instances and the SQL server will need to be
    /// routed back up to the DMZ and publicly facing routers and then back down
    /// to the internal networks.  This is inefficient and may even incure
    /// extra network transport costs.
    /// </para>
    /// <para>
    /// The solution is to have the SQL server instance register its local
    /// IP address dynamically with the DNS server, mapping to a host name
    /// that the other instances will use in their connection strings.
    /// Here's what a host mapping that accomplishes this might look like:
    /// </para>
    /// <code language="none">
    /// Host[0] = SQL.LILLTEK.NET,$(local-ip),60,ADDRESS
    /// </code>
    /// <para>
    /// This host entry maps the host <b>SQL.LILLTEK.NET</b> to the internal datacenter
    /// IP address of the current server, with the DNS response gaving a 60 second 
    /// TTL and an A record with the address.  The publicly facing IP address could
    /// be mapped by using the <b>$(public-ip)</b> macro or a a CNAME response could
    /// also be specified using <b>$(local-hostname)</b> or <b>$(public-hostname)</b>
    /// and specifying the <b>CNAME</b> entry type.
    /// </para>
    /// <para><b>Static Host Registration</b></para>
    /// <para>
    /// The LillTek Dynamic DNS insfrastructure also supports static host registration
    /// by simply hardcoding an IP address or CNAME in a host entry.  At this time, only
    /// A, CNAME and MX records can be returned by the DNS.  Here's an example of a
    /// couple static host entries:
    /// </para>
    /// <code language="none">
    /// Host[-] = SQL.LILLTEK.NET,10.0.0.5,60,ADDRESS
    /// Host[-] = MYSERVICE.LILLTEK.COM,INTERNAL.LILLTEK.COM
    /// Host[-] = MAIL.LILLTEK.COM,MAIL.BLACKMOON.COM,1800,MX
    /// </code>
    /// <para><b><u>Cached A Records</u></b></para>
    /// <para>
    /// The DNS server can be configured to maintain proactively maintain a
    /// cache of A record resolutions for names that are resolved by this or
    /// other servers.  Doing this may improve DNS lookup performance by
    /// avoiding a second DNS resolution to resolve a CNAME response to
    /// an A record with an actual IP address.
    /// </para>
    /// <para>
    /// The <b>AddressCache[#]</b> configuration setting is used to manage this.
    /// This will specify zero or more hosts for which the DNS server will
    /// proactively maintain the current address resolutions.  The format for
    /// each cache entry is:
    /// </para>
    /// <code language="none">
    /// "AddressCache[-]" "=" &lt;host&gt; [ "," &lt;min-TTL&gt; ]
    /// </code>
    /// <para>
    /// where <b>host</b> is the host name and <b>min-TTL</b> optionally specifies the
    /// minimum time-to-live (in seconds) to use for cached resolutions (overriding the TTL
    /// returned by the host name's origin DNS server).
    /// </para>
    /// <para><b><u>Registration of host behind a NAT</u></b></para>
    /// <para>
    /// Most home networks are located behind a network router which also
    /// acts as network address translator (NAT) which obscures the
    /// IP addresses for the home network behind the dynamically allocated
    /// public IP address of the router.
    /// </para>
    /// <para>
    /// To expose a service running within you home network, you'll need 
    /// to dynamically map the router's public IP address to a DNS host name
    /// and then configure the router to forward traffic to the specific server.
    /// </para>
    /// <para>
    /// The LillTek dynamic DNS infrastructure supports this sort of registration.
    /// Simply add <b>, NAT</b> to the end of a <b>Host[#]</b> entry in the
    /// dynamic DNS client's configuration.  This instructs the dynamic DNS server
    /// to register the source IP address for the UDP packet received rather
    /// than the IP address specified in the host entry.  The source IP address
    /// will be the NAT's public address.  Here's an example:
    /// </para>
    /// <code language="none">
    /// Host[0] = SQL.LILLTEK.NET,$(ip-address),60,ADDRESS,NAT
    /// </code>
    /// <para><b><u>Start of Authority (SOA) Records</u></b></para>
    /// <para>
    /// The server automatically generates SOA DNS records for the domains with
    /// hosts registered on the server with the SOA fields being set as described
    /// in the table below:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>mname</b></term>
    ///         <description>
    ///         <para>
    ///         This is the fully qualified host name of the primary nameserver for
    ///         the domain.  The first host specified in the <b>Host[#]</b> configuration
    ///         setting will be used as the primary server.  This value must be the same
    ///         for all DNS servers in the cluster.
    ///         </para>
    ///         <note>
    ///         The server will not respond to SOA queries if no <b>Host[#]</b> nameservers
    ///         are specified in the configuration file.
    ///         </note>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>rname</b></term>
    ///         <description>
    ///         This returns as the email address of the responsible person (with the "@"
    ///         character replaced with a ".".  The server will automatically set this to
    ///         <b>dnsadmin.<i>yourdomain.com</i></b>.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>serial</b></term>
    ///         <description>
    ///         This is the zone serial number.  This will be automatically generated and
    ///         will be the unsigned 32-bit number of seconds since 1/1/2010 (UTC).
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>refresh</b></term>
    ///         <description>
    ///         Number of seconds between the time a secondary nameserver gets a copy of the
    ///         zone and the next time it checks to see if it needs an update.  This value
    ///         is loaded from the <b>SOA-Refresh</b> configuration setting.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>retry</b></term>
    ///         <description>
    ///         Number of seconds a secondary nameserver waits before retrying to update the zone
    ///         from the primary nameserver after a failure.  This value
    ///         is loaded from the <b>SOA-Retry</b> configuration setting.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>expire</b></term>
    ///         <description>
    ///         Number of seconds between a secondary nameserver can retain zone information
    ///         and still have it considered to be authoritative.  This value
    ///         is loaded from the <b>SOA-Expire</b> configuration setting.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>minimum</b></term>
    ///         <description>
    ///         The minimum TTL in seconds for zone records.  This is ignored for responses returned
    ///         by the server itself.  This value is loaded from the <b>SOA-Minimum</b> configuration setting.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// The DNS Server uses reasonable defaults for the SOA timing parameters but these
    /// may be overridden if necessary using the SOA configuration settings described below.
    /// </para>
    /// <para><b><u>Clustered Implementation Details</u></b></para>
    /// <para>
    /// This functionality is implemented using the <see cref="ClusterMember" />
    /// class.  Dynamic DNS Service instances create a cluster on the
    /// base <b>abstract://LillTek/DataCenter/DynDNS</b> endpoint.  These
    /// instances join as normal members so that one will be elected as
    /// the <b>master</b> and the others will participate as <b>slaves</b>.
    /// </para>
    /// <para>
    /// Applications that need to expose dynamic DNS hosts will join this 
    /// cluster indirectly via <see cref="DynDnsClient" />.  This class
    /// joins the cluster as an <b>observer</b> with the host/IP address
    /// associations published to the cluster as cluster member properties.
    /// </para>
    /// <para>
    /// <see cref="ClusterMember" /> takes care of replicating this information
    /// across the cluster as well as detecting and handling the various
    /// failure scenarios.
    /// </para>
    /// <para><b><u>UDP Implementation Details</u></b></para>
    /// <para>
    /// In this mode, the service will bind a UDP socket to listen for
    /// packets from dynamic DNS clients.  By default, this socket will
    /// be bound to port <see cref="NetworkPort.DynamicDns" /> on the all
    /// network interfaces, but this can be customized in the client
    /// and server configuration settings.
    /// </para>
    /// <para>
    /// <see cref="DynDnsClient" /> instances will be configured with either
    /// the static IP addresses of all the DNS servers or to periodically
    /// perform a DNS NS lookup for a domain to obtain the name server
    /// addresses.  Then the client will periodically send host registration
    /// messages to <b>each</b> name server for each host name to be dynamically
    /// registered.  The name servers will record these registrations and
    /// mark them with a time-to-die (TTD).  The name server will purge
    /// host entries that are not renewed with another registration message
    /// from the client before the TTD has been reached.
    /// </para>
    /// <para>
    /// The UDP messages are encrypted with a shared key and marked with
    /// a transmission time for security purposes.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, Dynamic DNS service settings are prefixed by 
    /// <b>LillTek.Datacenter.DynDNS</b> (a custom prefix can be
    /// passed to <see cref="Start" /> if desired).  The available settings
    /// and their default values are described in the table below:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>NetworkBinding</td>
    ///     <td>ANY:DNS</td>
    ///     <td>
    ///     Specifies the <see cref="NetworkBinding" /> the DNS server 
    ///     should listen on for standard DNS requests.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>UdpBinding</td>
    ///     <td>ANY:DYNAMIC-DNS</td>
    ///     <td>
    ///     Specifies the <see cref="NetworkBinding" /> the DNS server 
    ///     should listen on to receive UDP host registration messages
    ///     from <see cref="DynDnsClient" />s.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Mode</td>
    ///     <td>Both</td>
    ///     <td>
    ///     Controls how the server is to be configured to obtain host
    ///     registrations from dynamic DNS clients.  The possible values
    ///     are <see cref="DynDnsMode.Udp" />, <see cref="DynDnsMode.Cluster"/>,
    ///     or <see cref="DynDnsMode.Both" />.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SharedKey</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     Shared symmetric encryption key used to decrypt UDP registration messages
    ///     sent by DNS clients while in <see cref="DynDnsMode.Udp" /> or
    ///     <see cref="DynDnsMode.Both" /> mode.  This key must match the shared 
    ///     key configured for the client.  This defaults to the same reasonable
    ///     default used by the DNS client class.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>MessageTTL</td>
    ///     <td>15m</td>
    ///     <td>
    ///     <para>
    ///     The maximum delta to be allowed between the timestamp of messages received from
    ///     UDP broadcast clients and servers and the current system time.
    ///     </para>
    ///     <para>
    ///     Messages transmitted between clients and servers in the UDP broadcast cluster are
    ///     timestamped with the time they were sent (UTC) to avoid replay attacks.  This
    ///     setting controls which messages will be discarded for being having a timestamp
    ///     too far in the past or too far into the future.
    ///     </para>
    ///     <para>
    ///     Ideally, this value would represent the maximum time a message could realistically
    ///     remain in transit on the network (a few seconds), but this setting also needs to
    ///     account for the possibility that the server system clocks may be out of sync.  So,
    ///     this value is a tradeoff between security and reliability.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BkInterval</td>
    ///     <td>1s</td>
    ///     <td>
    ///     Minimum interval for which background activities will be scheduled (when not
    ///     running in CLUSTER or BOTH mode).
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>RegistrationTTL</td>
    ///     <td>185s</td>
    ///     <td>
    ///     The maximum time a UDP host registration will remain active unless it
    ///     is renewed via another registration message from the dynamic DNS client.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>ResponseTTL</td>
    ///     <td>5s</td>
    ///     <td>
    ///     Specifies the default time-to-live (TTL) setting to use when replying
    ///     to DNS queries.  This indicates how long the operating system
    ///     on the client side as well as any intermediate DNS servers should cache 
    ///     the response.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>LogFailures</td>
    ///     <td>no</td>
    ///     <td>
    ///     Indicates whether DNS host lookup failures should be logged
    ///     as warnings.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>NameServer[#]</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     <para>
    ///     The set of static host entries that list the DNS name servers for the domain.  Only 
    ///     <b>host-mode=ADDRESS</b> is supported for name server entries. These entries are 
    ///     formatted as:
    ///     </para>
    ///     <code lang="none">
    ///     &lt;host name&gt; "," &lt;ip&gt; [ "," &lt;TTL&gt; [ "," "ADDRESS" ] ]
    ///     </code>
    ///     <para>
    ///     where <b>host name</b> is the host name for the name server, <b>ip</b> specifies the IP 
    ///     address of the host, <b>TTL</b> is the time-to-live (TTL) to use for the entry in seconds,
    ///     and <b>host-mode</b> is <b>ADDRESS</b>.
    ///     </para>
    ///     <para>
    ///     The <b>TTL</b> value defaults to 300 seconds (5 minutes) and the <b>host-mode</b>
    ///     defaults to <b>ADDRESS</b>.
    ///     </para>
    ///     <note>
    ///     The DNS service will respond to all NS queries as if the server is the authoritative
    ///     name server for the requested domain.  This means that the DNS server configuration
    ///     does not have to change to serve names for additional domains.  You just need to
    ///     point the new domain's zone to the name server IP addresses and then begin registering
    ///     hosts using the dynamic DNS client.
    ///     </note>
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
    ///     &lt;host name&gt; "," &lt;ip/cname&gt; [ "," &lt;TTL&gt; [ "," &lt;host-mode&gt; ] ]
    ///     </code>
    ///     <para>
    ///     where <b>host name</b> is the DNS name being registered, <b>ip</b>/<b>cname</b>
    ///     specifies the IP address or CNAME reference to the host, <b>TTL</b> is the optional
    ///     time-to-live (TTL) to use for the entry in seconds, and <b>host-mode</b> is the optional 
    ///     host entry mode, one of <b>ADDRESS</b>, <b>ADDRESSLIST</b>, <b>CNAME</b>, or <b>MX</b>.
    ///     </para>
    ///     <para>
    ///     The <b>TTL</b> value defaults to 300 seconds (5 minutes) and the <b>host-mode</b>
    ///     defaults to <b>ADDRESS</b> for IP addresses or <b>CNAME</b> for CNAME references.
    ///     </para>
    ///     <note>
    ///     A host mode of <b>ADDRESS</b> or <b>ADDRESSLIST</b> can only be specified for IP
    ///     addresses and <b>CNAME</b> can only be specified for CNAME entries.  IP addresses
    ///     or host names can be specified for <b>MX</b> records.
    ///     </note>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>AddressCache[#]</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     <para>
    ///     Specifies zero or more hosts for which the DNS server will proactively maintain the 
    ///     current address resolutions.  The format for each cache entry is:
    ///     </para>
    ///     <code lang="none">
    ///     &lt;host&gt; [ "," &lt;min-TTL&gt; ]
    ///     </code>
    ///     <para>
    ///     where <b>host</b> is the host name and <b>min-TTL</b> optionally specifies the
    ///     minimum time-to-live (in seconds) to use for cached resolutions (overriding the TTL
    ///     returned by the host name's origin DNS server).
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SOA-Refresh</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     The interval between the time when a secondary nameserver gets a copy of the
    ///     zone and the next time it checks to see if it needs an update.  Defaults to
    ///     <b>2 hours</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SOA-Retry</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     The interval a secondary nameserver waits before retrying to update the zone
    ///     from the primary nameserver after a failure.  Defaults to <b>30 minutes</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SOA-Expire</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     The interval a secondary nameserver can retain zone information
    ///     and still have it considered to be authoritative.  Defaults to <b>7 days</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SOA-Minimum</td>
    ///     <td>(optional)</td>
    ///     <td>
    ///     The minimum TTL for zone records.  This is ignored for responses returned
    ///     by the server itself.  Defaults to <b>60 minutes</b>.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Cluster</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     <b>Cluster</b> is a subsection in the configuration that
    ///     that specifies the settings required to establish a cooperative
    ///     cluster of Dynamic DNS instances to the network.  The media
    ///     router uses the <see cref="ClusterMember" /> class to perform
    ///     the work necessary to join the cluster.  The <b>ClusterBaseEP</b>
    ///     setting is required.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// <para><b><u>Performance Counters</u></b></para>
    /// <para>
    /// The class can be configured to expose performance counters.  Call the
    /// static <see cref="InstallPerfCounters" /> method to add the class performance
    /// counters to a <see cref="PerfCounterSet" /> during application installation
    /// and then pass a set instance to the <see cref="Start" /> method.
    /// </para>
    /// <para>
    /// The table below describes the performance counters exposed
    /// by the Dynamic DNS Service.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>        
    /// <th width="1">Type</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Runtime</td>
    ///     <td>Count</td>
    ///     <td>Elapsed service runtime in minutes.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Queries/Sec</td>
    ///     <td>Rate</td>
    ///     <td>Number of DNS queries per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Errors/Sec</td>
    ///     <td>Rate</td>
    ///     <td>Number of DNS failed lookups per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Host Registrations/Sec</td>
    ///     <td>Rate</td>
    ///     <td>Number UDP host registrations received per second.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Host Entries</td>
    ///     <td>Count</td>
    ///     <td>Total number of host entries.</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class DynDnsHandler : ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string Queries_Name           = "Queries/sec";
            const string Errors_Name            = "Errors/sec";
            const string HostEntries_Name       = "Host Entries";
            const string HostRegistrations_Name = "Host Registrations/sec";
            const string Runtime_Name           = "Runtime (min)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "Number of DNS queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Errors_Name, "Number of failed DNS lookups/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + HostEntries_Name, "Total number of host entries.", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + HostRegistrations_Name, "Number UDP host registrations received per second.", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Queries;            // # queries/sec
            public PerfCounter Errors;             // # errors/sec
            public PerfCounter HostEntries;        // # of DNS host entries
            public PerfCounter HostRegistrations;  // # of UDP registrations received/sec
            public PerfCounter Runtime;            // Service runtime in minutes

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
                    Queries           = perfCounters[perfPrefix + Queries_Name];
                    Errors            = perfCounters[perfPrefix + Errors_Name];
                    HostEntries       = perfCounters[perfPrefix + HostEntries_Name];
                    HostRegistrations = perfCounters[perfPrefix + HostRegistrations_Name];
                    Runtime           = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {
                    Queries           =
                    Errors            =
                    HostEntries       =
                    HostRegistrations =
                    Runtime           = PerfCounter.Stub;
                }
            }
        }

        /// <summary>
        /// Used to keep track of cached host addresses.
        /// </summary>
        internal class HostAddress
        {
            public string       HostName;   // Host name (without a terminating dot)
            public DateTime     TTR;        // Time-to-renew (SYS)
            public int          MinTTL;     // Minimum TTL (seconds) or 0
            public int          TTL;        // Response time to live (seconds) or 0
            public IPAddress[]  Addresses;  // Host addresses (or null if resolution failed)

            // Used to temporarily save DNS resolution results

            public int          tmpTTL;
            public IPAddress[]  tmpAddresses;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="hostName"></param>
            /// <param name="minTTL"></param>
            public HostAddress(string hostName, int minTTL)
            {
                this.HostName  = hostName;
                this.TTR       = SysTime.Now;
                this.TTL       =
                this.MinTTL    = minTTL;
                this.Addresses = null;
            }

            /// <summary>
            /// Returns a shallow clone of the instance.
            /// </summary>
            /// <returns>The clone.</returns>
            public HostAddress Clone()
            {
                return new HostAddress(this.HostName, this.TTL)
                {
                    TTR       = this.TTR,
                    Addresses = this.Addresses
                };
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

        /// <summary>
        /// The service's default configuration key prefix.
        /// </summary>
        public const string ConfigPrefix = "LillTek.Datacenter.DynDNS";

        /// <summary>
        /// <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.DynDNS";

        private MsgRouter       router;             // The associated router (or null)
        private bool            isRunning;          // True if the handler is running
        private object          syncLock;           // Instance used for thread synchronization
        private ClusterMember   cluster;            // Dynamic DNS cluster state
        private Perf            perf;               // Performance counters
        private DateTime        startTime;          // Time the service was started (UTC)
        private TimeSpan        responseTTL;        // Default TTL for DNS responses
        private bool            logFailures;        // True to log DNS lookup failures as warnings
        private NetworkBinding  udpBinding;         // Binding for the registration socket
        private TimeSpan        bkInterval;         // Interval for background task execution
        private TimeSpan        registrationTTL;    // Maximum lifespan for UDP host registration
        private TimeSpan        messageTTL;         // Maximum delta from the current time for a
                                                    // received UDP message
        private DynDnsMode      mode;               // Operating mode
        private SymmetricKey    sharedKey;          // Encryption key used to encrypt UDP registration messages
        private DnsServer       dnsServer;          // The DNS server
        private GatedTimer      bkTimer;            // Background task timer
        private EnhancedSocket  socket;             // Used to receive UDP registration messages
        private byte[]          recvBuf;            // UDP receive buffer
        private EndPoint        rawRecvEP;          // Packet receive source endpoint
        private AsyncCallback   onUdpReceive;       // UDP Receive handler
        private bool            udpHostsChanged;    // Set to true for UDP sourced host entry changes

        // SOA record related settings

        private DateTime        serialStartTime;    // Zero time for SOA serial numbers
        private TimeSpan        soaRefresh;
        private TimeSpan        soaRetry;
        private TimeSpan        soaExpire;
        private TimeSpan        soaMinimum;

        // Domain name server references.

        private List<DynDnsHostEntry> nameServers;

        // Static host entries defined in the server configuration.

        private List<DynDnsHostEntry> staticHosts;

        // Host entries registered via UDP messages.  The dictionary key is the
        // serialized form of the DNS host entry.

        private Dictionary<string, DynDnsHostEntry> udpHostMap;

        // This table maps a host name to the set of host entries
        // currently registered for that host.  This is constructed
        // from the cluster member status combined with UDP endpoint
        // registrations as well as static registrations configured
        // on the dynamic DNS server.

        private Dictionary<string, List<DynDnsHostEntry>> hostMap;

        // Maps host names (terminated with a period) to cached IP address
        // resolutions.

        private Dictionary<string, HostAddress> addressCache;
        private Thread                          addressCacheThread;

        //---------------------------------------
        // Clustering Implementation Note:
        //
        // DynDnsClients expose their host/entry mappings using zero or more
        // cluster member property values.  These values are formatted as:
        //
        //      host[<host> ":" <index>] = <DynDnsHostEntry>
        //
        // where <DynDnsHostEntry> is the host entry serialized into its string form
        // and <index> is a unique index value.
        //
        // For example, a client that wishes to expose its services 
        // at IP addresses 10.0.0.2 and 10.0.0.3 on host name auth.lilltek.net
        // will expose the following member properties:
        //
        //      host[auth.lilltek.net:0] = auth.lilltek.net,10.0.0.2,300,ADDRESSLIST
        //      host[auth.lilltek.net:1] = auth.lilltek.net,10.0.0.3,300,ADDRESSLIST

        /// <summary>
        /// Constructs a dynamic DNS service handler instance.
        /// </summary>
        public DynDnsHandler()
        {
            this.serialStartTime = new DateTime(2010, 1, 1);
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.DynDNS</b>).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix)
        {
            var config = new Config(keyPrefix != null ? keyPrefix : ConfigPrefix);

            if (this.isRunning)
                throw new InvalidOperationException("This handler has already been started.");

            // Make sure the syncLock is set early.

            this.syncLock = router != null ? router.SyncRoot : this;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // General initialization

            mode            = config.Get<DynDnsMode>("Mode", DynDnsMode.Cluster);
            udpBinding      = config.Get("UdpBinding", new NetworkBinding(IPAddress.Any, NetworkPort.DynamicDns));
            sharedKey       = new SymmetricKey(config.Get("SharedKey", "aes:BcskocQ2W4aIGEemkPsy5dhAxuWllweKLVToK1NoYzg=:5UUVxRPml8L4WH82unR74A=="));
            messageTTL      = config.Get("MessageTTL", TimeSpan.FromMinutes(15));
            responseTTL     = config.Get("ResponseTTL", TimeSpan.FromSeconds(5));
            bkInterval      = config.Get("BkInterval", TimeSpan.FromSeconds(1));
            registrationTTL = config.Get("RegistrationTTL", TimeSpan.FromSeconds(185));
            logFailures     = config.Get("LogFailures", false);
            soaRefresh      = config.Get("SOA-Refresh", TimeSpan.FromHours(2));
            soaRetry        = config.Get("SOA-Retry", TimeSpan.FromMinutes(30));
            soaExpire       = config.Get("SOA-Expire", TimeSpan.FromDays(7));
            soaMinimum      = config.Get("SOA-Minimum", TimeSpan.FromMinutes(60));
            hostMap         = new Dictionary<string, List<DynDnsHostEntry>>(StringComparer.OrdinalIgnoreCase);
            udpHostMap      = new Dictionary<string, DynDnsHostEntry>(StringComparer.OrdinalIgnoreCase);
            udpHostsChanged = true;     // Set this to make sure that the static hosts are loaded in the OnBkTask() call.

            staticHosts = new List<DynDnsHostEntry>();
            foreach (var hostEntry in config.GetArray("Host"))
            {
                try
                {
                    staticHosts.Add(new DynDnsHostEntry(hostEntry));
                }
                catch (Exception e)
                {
                    SysLog.LogWarning("DynDnsHandler: Invalid Host[{0}] entry: {1}", hostEntry, e.Message);
                }
            }

            nameServers = new List<DynDnsHostEntry>();
            foreach (var hostEntry in config.GetArray("NameServer"))
            {
                try
                {
                    var entry = new DynDnsHostEntry(hostEntry);

                    if (entry.HostMode != DynDnsHostMode.Address)
                        throw new FormatException(string.Format("DynDnsHandler: Does not specify ADDRESS host mode.", hostEntry));

                    nameServers.Add(entry);
                    staticHosts.Add(entry);     // Name servers are also considered to static hosts
                }
                catch (Exception e)
                {
                    SysLog.LogWarning("DynDnsHandler: Invalid NameServer[{0}] entry: {1}", hostEntry, e.Message);
                }
            }

            this.addressCache = new Dictionary<string, HostAddress>(StringComparer.OrdinalIgnoreCase);
            foreach (var hostEntry in config.GetArray("AddressCache"))
            {
                string[]        fields = hostEntry.Split(',');
                string          host;
                int             minTTL;

                for (int i = 0; i < fields.Length; i++)
                    fields[i] = fields[i].Trim();

                host = fields[0];

                if (!Helper.IsValidDomainName(host))
                {
                    SysLog.LogWarning("DynDnsHandler: Invalid host name [{0}] in [AddressCache] configuration.", host);
                    continue;
                }

                if (!host.EndsWith("."))
                    host += ".";

                minTTL = 0;
                if (fields.Length > 1)
                {
                    if (!int.TryParse(fields[1], out minTTL) || minTTL < 0)
                    {
                        SysLog.LogWarning("DynDnsHandler: Invalid MIN-TTL [{0}] in [AddressCache] configuration.", fields[1]);
                        continue;
                    }
                }

                if (addressCache.ContainsKey(host))
                {
                    SysLog.LogWarning("DynDnsHandler: Host [{0}] is defined more than once in [AddressCache] configuration.", host);
                    continue;
                }

                addressCache.Add(host, new HostAddress(host, minTTL));
            }

            // Initialize the performance counters

            startTime = DateTime.UtcNow;
            perf = new Perf(perfCounters, perfPrefix);

            try
            {
                // Start a background timer if we didn's start with a cluster.

                if (mode == DynDnsMode.Udp)
                    this.bkTimer = new GatedTimer(new TimerCallback(OnBkTask), null, bkInterval);

                // Initialize the UDP socket if required.

                if (mode == DynDnsMode.Udp || mode == DynDnsMode.Both)
                {
                    socket                          = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.IgnoreUdpConnectionReset = true;
                    socket.ReceiveBufferSize        =
                    socket.SendBufferSize           = 1024 * 1024;   // $todo(jeff.lill): Hardcoded
                    onUdpReceive                    = new AsyncCallback(OnUdpReceive);
                    recvBuf                         = new byte[TcpConst.MTU];
                    rawRecvEP                       = new IPEndPoint(IPAddress.Any, 0);

                    socket.Bind(udpBinding);
                    socket.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref rawRecvEP, onUdpReceive, null);
                }

                // Initialize the router

                this.router = router;

                // Start the DNS server

                dnsServer               = new DnsServer();
                dnsServer.RequestEvent += new DnsServerDelegate(OnDnsRequest);
                dnsServer.Start(DnsServerSettings.LoadConfig(keyPrefix));

                // Join the cluster, initializing this instance's state.

                if (mode == DynDnsMode.Cluster || mode == DynDnsMode.Both)
                {
                    if (router == null)
                        throw new InvalidOperationException("A valid [router] must be passed when associating a dynamic DNS server to a cluster.");

                    cluster                      = new ClusterMember(router, ClusterMemberSettings.LoadConfig(config.KeyPrefix + "Cluster"));
                    cluster.ClusterStatusUpdate += new ClusterMemberEventHandler(OnClusterStatusUpdate);
                    cluster.SlaveTask           += new ClusterMemberEventHandler(OnBkTask);
                    cluster.MasterTask          += new ClusterMemberEventHandler(OnBkTask);
                    cluster.Start();
                }

                this.isRunning = true;

                // Rather than calling cluster.JoinWait() which could take a really long
                // time, I'm going to sleep for two seconds.  There are three scenarios:
                //
                //      1. This is the first Dynamic DNS instance.
                //
                //      2. Other instances are running but they haven't
                //         organized into a cluster.
                //
                //      3. A cluster is already running.
                //
                // If #1 is the current situation, then it will take a very long time
                // for JoinWait() to return because we have to go through the entire
                // missed master broadcast and election periods.  Since we're the only
                // instance, we could have started serving content well before this.
                //
                // #2 won't be very common but if it is the case, the worst thing
                // that will happen is that it will take a while to elect the master.
                //
                // If #3 is the case, then two seconds should be long enough for the
                // master to send the instance a cluster update.

                Thread.Sleep(2000);

                // Start the address cache thread.

                addressCacheThread      = new Thread(AddressCacheThread);
                addressCacheThread.Name = "LillTek-DnsAddressCache";
                addressCacheThread.Start();
            }
            catch
            {
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        public void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            using (TimedLock.Lock(syncLock))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (dnsServer != null)
                {
                    dnsServer.Stop();
                    dnsServer = null;
                }

                if (cluster != null)
                {
                    cluster.Stop();
                    cluster = null;
                }

                router = null;

                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                hostMap     = null;
                staticHosts = null;
                udpHostMap  = null;
                isRunning   = false;

                if (addressCacheThread != null)
                {
                    if (!addressCacheThread.Join(4000))
                        addressCacheThread.Abort();

                    addressCacheThread = null;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="ClusterMember" /> class used by this instance
        /// to implement clustering support.
        /// </summary>
        public ClusterMember Cluster
        {
            get { return cluster; }
        }

        /// <summary>
        /// Used by unit testing to retrieve the cached host addresses.
        /// </summary>
        /// <returns>The list of address entries.</returns>
        internal List<HostAddress> GetCachedAddresses()
        {
            var entries = new List<HostAddress>();

            using (TimedLock.Lock(syncLock))
            {
                foreach (var entry in addressCache.Values)
                    entries.Add(entry.Clone());
            }

            return entries;
        }

        /// <summary>
        /// Handles DNS requests made to the server.
        /// </summary>
        /// <param name="server">The <see cref="DnsServer" /> that raised the event.</param>
        /// <param name="args">The event arguments.</param>
        private void OnDnsRequest(DnsServer server, DnsServerEventArgs args)
        {
            DnsRequest      request  = args.Request;
            DnsResponse     response = new DnsResponse(request);
            bool            fail     = false;

            NetTrace.Write(TraceSubsystem, 1, string.Format("DNS: Request[\"{0}\":{1}]", request.QName, request.QType), null, null);
            perf.Queries.Increment();

            if (request.QClass != DnsQClass.IN || request.Opcode != DnsOpcode.QUERY)
            {
                NetTrace.Write(TraceSubsystem, 1, "DNS: Refused", null, null);
                perf.Errors.Increment();

                response.RCode = DnsFlag.RCODE_REFUSED;
                args.Response  = response;
                return;
            }

            switch (request.QType)
            {
                case DnsQType.A:
                case DnsQType.CNAME:
                case DnsQType.MX:
                case DnsQType.NS:
                case DnsQType.SOA:

                    // These queries are implemented.

                    break;

                default:

                    // Anything else is not implemented.

                    NetTrace.Write(TraceSubsystem, 1, "DNS: Not implemented", null, null);
                    perf.Errors.Increment();

                    response.RCode = DnsFlag.RCODE_NOTIMPL;
                    args.Response = response;
                    return;
            }

            using (TimedLock.Lock(syncLock))
            {
                List<DynDnsHostEntry> entryList;

                if (!isRunning)
                    return;

                switch (request.QType)
                {
                    case DnsQType.SOA:

                        if (nameServers.Count == 0)
                        {
                            fail = true;
                            break;
                        }

                        if (!hostMap.TryGetValue(request.QName, out entryList) || entryList.Count == 0)
                        {
                            NetTrace.Write(TraceSubsystem, 1, "DNS: Not found", request.QName, null);
                            perf.Errors.Increment();

                            response.RCode = DnsFlag.RCODE_NAME;
                            fail           = true;
                        }
                        else
                        {
                            if (NetTrace.TraceDetail(TraceSubsystem) >= 1)
                            {
                                var sb = new StringBuilder(32);

                                for (int i = 0; i < entryList.Count; i++)
                                    sb.AppendFormat("{0} ", entryList[i]);

                                NetTrace.Write(TraceSubsystem, 1, "DNS: Resolved", sb.ToString(), null);
                            }

                            // Add the answer to the response.

                            response.Answers.Add(new SOA_RR(request.QName,                                                      // Request host
                                                            NetHelper.GetCanonicalHost(nameServers[0].Host),                   // Primary nameserver
                                                            "dnsadmin." + NetHelper.GetCanonicalSecondLevelHost(request.QName),// Admin email
                                                            (uint)(DateTime.UtcNow - serialStartTime).TotalSeconds,            // Serial
                                                            (uint)soaRefresh.TotalSeconds,                                     // Refresh
                                                            (uint)soaRetry.TotalSeconds,                                       // Retry
                                                            (uint)soaExpire.TotalSeconds,                                      // Expire
                                                            (uint)soaMinimum.TotalSeconds));                                   // Minimum

                            // Add NS and A records for the nameservers to the response's additional information.

                            foreach (var entry in nameServers)
                                response.Additional.Add(new NS_RR(request.QName, entry.Host, (int)entry.TTL.TotalSeconds));

                            foreach (var entry in nameServers)
                            {
                                if (entry.HostMode == DynDnsHostMode.Address)
                                    response.Additional.Add(new A_RR(entry.Host, entry.Address, (int)entry.TTL.TotalSeconds));
                            }
                        }
                        break;

                    case DnsQType.NS:

                        if (nameServers.Count == 0)
                        {
                            fail = true;
                            break;
                        }

                        // $todo(jeff.lill):
                        //
                        // At this time, I don't support CNAME nameserver entries.  I think it would
                        // be easy to add this.  All I'd need to do is output CNAME_RR records instead
                        // A_RR records in the additional collection in the response.  I can't think 
                        // of a real use case for this at the moment, so I'm going to punt and keep
                        // things simple.

                        // Add NS_RR answer records and A_RR additional records.

                        foreach (var entry in nameServers)
                            if (entry.HostMode == DynDnsHostMode.Address)
                            {
                                response.Answers.Add(new NS_RR(request.QName, entry.Host, (int)entry.TTL.TotalSeconds));
                                response.Additional.Add(new A_RR(entry.Host, entry.Address, (int)entry.TTL.TotalSeconds));
                            }

                        break;

                    case DnsQType.MX:

                        if (!hostMap.TryGetValue(request.QName, out entryList) || entryList.Count == 0)
                        {
                            NetTrace.Write(TraceSubsystem, 1, "DNS: Not found", request.QName, null);
                            perf.Errors.Increment();

                            response.RCode = DnsFlag.RCODE_NAME;
                            fail           = true;
                        }
                        else
                        {
                            // I'm going to return any MX records as the query answers.

                            if (NetTrace.TraceDetail(TraceSubsystem) >= 1)
                            {
                                var sb = new StringBuilder(32);

                                for (int i = 0; i < entryList.Count; i++)
                                    sb.AppendFormat("{0} ", entryList[i]);

                                NetTrace.Write(TraceSubsystem, 1, "DNS: Resolved", sb.ToString(), null);
                            }

                            // $todo(jeff.lill):
                            //
                            // To avoid another DNS request, I should perform a lookup on
                            // any CNAME references I find and add A records to the additional
                            // response information.

                            foreach (var entry in entryList)
                            {
                                if (entry.HostMode == DynDnsHostMode.MX)
                                    response.Answers.Add(new MX_RR(request.QName, 0, entry.CName, (int)entry.TTL.TotalSeconds));
                            }
                        }

                        break;

                    case DnsQType.A:
                    case DnsQType.CNAME:

                        if (!hostMap.TryGetValue(request.QName, out entryList) || entryList.Count == 0)
                        {
                            NetTrace.Write(TraceSubsystem, 1, "DNS: Not found", request.QName, null);
                            perf.Errors.Increment();

                            response.RCode = DnsFlag.RCODE_NAME;
                            fail           = true;
                        }
                        else
                        {
                            if (NetTrace.TraceDetail(TraceSubsystem) >= 1)
                            {
                                var sb = new StringBuilder(32);

                                for (int i = 0; i < entryList.Count; i++)
                                    sb.AppendFormat("{0} ", entryList[i]);

                                NetTrace.Write(TraceSubsystem, 1, "DNS: Resolved", sb.ToString(), null);
                            }

                            // Determine the host mode for the entries.  If the mode for all of the
                            // entries don't match, then we're going to choose ADDRESSLIST over
                            // ADDRESS, and ADDRESS or ADDRESSLIST over CNAME.
                            //
                            // We're also going to determine the maximum TTL across all of the entries.

                            DynDnsHostMode  hostMode = DynDnsHostMode.Unknown;
                            TimeSpan        maxTTL  = TimeSpan.FromSeconds(-1);
                            int             actualTTL;

                            foreach (var entry in entryList)
                            {
                                if (entry.TTL > maxTTL)
                                    maxTTL = entry.TTL;

                                if (entry.HostMode == hostMode || entry.HostMode == DynDnsHostMode.MX)
                                    continue;

                                switch (hostMode)
                                {
                                    case DynDnsHostMode.Address:

                                        if (entry.HostMode == DynDnsHostMode.AddressList)
                                            hostMode = DynDnsHostMode.AddressList;

                                        break;

                                    case DynDnsHostMode.AddressList:

                                        // Always wins

                                        break;

                                    case DynDnsHostMode.CName:

                                        hostMode = entry.HostMode;  // Always loses
                                        break;

                                    case DynDnsHostMode.MX:

                                        break;  // Ignore MX records

                                    case DynDnsHostMode.Unknown:

                                        if (entry.HostMode == DynDnsHostMode.Address || entry.HostMode == DynDnsHostMode.AddressList || entry.HostMode == DynDnsHostMode.CName)
                                            hostMode = entry.HostMode;

                                        break;

                                    default:

                                        SysLog.LogWarning("Unexpected DNS host mode.");
                                        break;
                                }
                            }

                            if (maxTTL < TimeSpan.Zero)
                                actualTTL = (int)responseTTL.TotalSeconds;
                            else
                                actualTTL = (int)maxTTL.TotalSeconds;

                            // Get the list of entries that match the winning mode.

                            var matchingEntries = new List<DynDnsHostEntry>(entryList.Count);

                            switch (hostMode)
                            {
                                case DynDnsHostMode.Address:
                                case DynDnsHostMode.AddressList:

                                    foreach (var entry in entryList)
                                        if (entry.HostMode == DynDnsHostMode.Address || entry.HostMode == DynDnsHostMode.AddressList)
                                            matchingEntries.Add(entry);

                                    break;

                                case DynDnsHostMode.CName:

                                    foreach (var entry in entryList)
                                        if (entry.HostMode == DynDnsHostMode.CName)
                                            matchingEntries.Add(entry);

                                    break;
                            }

                            // Generate the DNS answer records.

                            switch (hostMode)
                            {
                                case DynDnsHostMode.Address:

                                    // Randomly pick one of the hosts and return an A record.

                                    response.Answers.Add(new A_RR(request.QName, matchingEntries[Helper.RandIndex(matchingEntries.Count)].Address, actualTTL));
                                    break;

                                case DynDnsHostMode.AddressList:

                                    // Return A records for all of the hosts.

                                    foreach (var entry in matchingEntries)
                                        if (entry.HostMode == DynDnsHostMode.Address || entry.HostMode == DynDnsHostMode.AddressList)
                                            response.Answers.Add(new A_RR(request.QName, entry.Address, actualTTL));

                                    break;

                                case DynDnsHostMode.CName:

                                    // Randomly pick one of the hosts and return a CNAME record.

                                    string          host = matchingEntries[Helper.RandIndex(matchingEntries.Count)].CName;
                                    HostAddress     hostAddress;

                                    response.Answers.Add(new CNAME_RR(request.QName, host, actualTTL));

                                    // Look for any cached address records for the host we selected and add a randomly selected
                                    // address to the response's Additional section as an A record.

                                    if (addressCache.TryGetValue(host, out hostAddress) && hostAddress.Addresses != null && hostAddress.Addresses.Length > 0)
                                    {
                                        var ipAddress = hostAddress.Addresses[Helper.RandIndex(hostAddress.Addresses.Length)];

                                        response.Additional.Add(new A_RR(host, ipAddress, hostAddress.TTL));
                                    }
                                    break;

                                default:
                                case DynDnsHostMode.Unknown:

                                    NetTrace.Write(TraceSubsystem, 1, "DNS: Not found", request.QName, null);
                                    perf.Errors.Increment();

                                    response.RCode = DnsFlag.RCODE_NAME;
                                    fail           = true;
                                    break;
                            }
                        }
                        break;
                }
            }

            if (!fail && response.Answers.Count == 0)
            {
                NetTrace.Write(TraceSubsystem, 1, "DNS: Not found", request.QName, null);
                perf.Errors.Increment();

                response.RCode = DnsFlag.RCODE_NAME;
                fail           = true;
            }

            response.Flags |= DnsFlag.AA;
            args.Response = response;

            if (fail && logFailures)
                SysLog.LogWarning("Dynamic DNS lookup for [{0}] from [{1}] failed.", request.QName, args.RemoteEP);
        }

        /// <summary>
        /// Handles background task callbacks from the cluster.
        /// </summary>
        /// <param name="sender">The <see cref="ClusterMember" /> instance that raised the event.</param>
        /// <param name="args">The cluster event arguments.</param>
        private void OnBkTask(ClusterMember sender, ClusterMemberEventArgs args)
        {
            OnBkTask(null);
        }

        /// <summary>
        /// Implements the background tasks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            if (!isRunning)
                return;

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    // Purge any timed-out UDP registered host entries.

                    if (udpHostMap != null)
                    {
                        var delList = new List<string>();
                        var now     = SysTime.Now;

                        foreach (var entry in udpHostMap.Values)
                            if (entry.TTD <= now)
                                delList.Add(entry.ToString());

                        foreach (var key in delList)
                        {
                            udpHostMap.Remove(key);
                            udpHostsChanged = true;
                        }
                    }

                    // Handle changes to the UDP sourced host entries.

                    if (udpHostsChanged)
                    {
                        udpHostsChanged = false;

                        // If the cluster isn't running then we need to add the static and
                        // UDP registered hosts here.  If the cluster is running then 
                        // we're going to simulate a cluster status update for force the
                        // regeneration of the combined host map.

                        if (mode == DynDnsMode.Udp)
                        {
                            hostMap.Clear();
                            AddStaticAndUdpEntries();
                        }
                        else
                            OnClusterStatusUpdate(null, null);
                    }

                    perf.Runtime.RawValue     = (int)(DateTime.UtcNow - startTime).TotalMinutes;
                    perf.HostEntries.RawValue = hostMap.Count;
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Implements the address caching background thread.
        /// </summary>
        private void AddressCacheThread()
        {
            // $todo(jeff.lill):
            //
            // I'm going to use the .NET DNS lookup class rather than the LillTek equivalent
            // for the time being to reduce risk.  This means that I won't be able to get the
            // TTL from the DNS response.  I'm going to assume a TTL of 1 minute instead.

            while (true)
            {
                if (!isRunning)
                    return;

                try
                {
                    // Get the set of hosts that are due for an address lookup.

                    var renewList = new List<HostAddress>(addressCache.Count);
                    var now       = SysTime.Now;

                    using (TimedLock.Lock(syncLock))
                    {
                        foreach (var host in addressCache.Values)
                        {
                            if (host.TTR <= now)
                                renewList.Add(host);
                        }
                    }

                    if (renewList.Count > 0)
                    {
                        // Perform the lookups in parallel.

                        int cRemaining = renewList.Count;

                        foreach (var host in renewList)
                        {
                            host.tmpTTL       = 0;
                            host.tmpAddresses = null;

                            Dns.BeginGetHostEntry(host.HostName,
                                ar =>
                                {
                                    HostAddress entry = (HostAddress)ar.AsyncState;
                                    IPHostEntry response;

                                    try
                                    {
                                        response = Dns.EndGetHostEntry(ar);
                                        entry.tmpTTL = Math.Max(host.MinTTL, host.tmpTTL); // $hack(jeff.lill): Hardcoded TTL at 1 min
                                        entry.tmpAddresses = response.AddressList.IPv4Only();
                                    }
                                    catch (Exception e)
                                    {
                                        SysLog.LogWarning("Unable to resolve DNS address for [{0}] (exception to follow).", host.HostName);
                                        SysLog.LogException(e);

                                        // Don't overload DNS servers that appear to be down with requests.

                                        entry.TTR = SysTime.Now + TimeSpan.FromMinutes(15);     // $hack(jeff.lill): Hardcoded
                                    }

                                    Interlocked.Decrement(ref cRemaining);
                                },
                                host);
                        }

                        // Wait for the lookups to complete.

                        while (cRemaining > 0)
                            Thread.Sleep(100);

                        // Update the host records.

                        now = SysTime.Now;

                        using (TimedLock.Lock(syncLock))
                        {
                            foreach (var host in renewList)
                            {
                                host.TTL = host.tmpTTL;
                                host.Addresses = host.tmpAddresses;
                                host.TTR = now + TimeSpan.FromSeconds(Math.Max(host.MinTTL, host.tmpTTL) + 1.1);    // Add a little slop
                                host.tmpTTL = 0;
                                host.tmpAddresses = null;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        /// <summary>
        /// Adds any static and UDP host entries to the host map, as appropriate.
        /// </summary>
        private void AddStaticAndUdpEntries()
        {
            using (TimedLock.Lock(syncLock))
            {
                List<DynDnsHostEntry> entryList;

                // Add the static hosts.

                foreach (var entry in staticHosts)
                {
                    if (!hostMap.TryGetValue(entry.Host, out entryList))
                    {
                        entryList = new List<DynDnsHostEntry>();
                        hostMap.Add(entry.Host, entryList);
                    }

                    entryList.Add(entry);
                }

                // Add the UDP registered hosts.

                if (udpHostMap != null)
                {
                    foreach (var entry in udpHostMap.Values)
                    {
                        if (!hostMap.TryGetValue(entry.Host, out entryList))
                        {
                            entryList = new List<DynDnsHostEntry>();
                            hostMap.Add(entry.Host, entryList);
                        }

                        entryList.Add(entry);
                    }
                }
            }
        }

        /// <summary>
        /// This is called by the <see cref="ClusterMember" /> instance when the
        /// master's cluster status broadcast is received.
        /// </summary>
        /// <param name="sender">The sending cluster member.</param>
        /// <param name="args">The event arguments.</param>
        private void OnClusterStatusUpdate(ClusterMember sender, ClusterMemberEventArgs args)
        {
            try
            {
                ClusterStatus                               status = cluster.ClusterStatus;
                Dictionary<string, List<DynDnsHostEntry>>   newMap;

                // Construct a new host map from the cluster status.

                newMap = new Dictionary<string, List<DynDnsHostEntry>>(StringComparer.OrdinalIgnoreCase);

                if (status != null)
                {
                    foreach (ClusterMemberStatus member in status.Members)
                        foreach (string key in member.Keys)
                        {
                            const int hostPrefixLen = 5;  // "host[".Length;

                            List<DynDnsHostEntry>   entryList;
                            string                  host;
                            DynDnsHostEntry         entry;
                            int                     indexPos;

                            // Parse keys of the form "host[<host>]"

                            host = key.ToLowerInvariant();
                            if (!host.StartsWith("host[") || !host.EndsWith("]"))
                                continue;   // Invalid host entry

                            indexPos = host.IndexOf(':', hostPrefixLen);
                            if (indexPos == -1)
                                continue;   // Invalid host entry

                            host = host.Substring(hostPrefixLen, indexPos - hostPrefixLen);
                            if (!host.EndsWith("."))
                                host += ".";

                            try
                            {
                                entry = new DynDnsHostEntry(member[key]);
                            }
                            catch
                            {
                                continue;
                            }

                            if (newMap.TryGetValue(host, out entryList))
                            {
                                bool found = false;

                                for (int i = 0; i < entryList.Count; i++)
                                    if (entryList[i].Equals(entry))
                                    {
                                        found = true;
                                        break;
                                    }

                                if (!found)
                                    entryList.Add(entry);
                            }
                            else
                            {
                                entryList = new List<DynDnsHostEntry>();
                                entryList.Add(entry);

                                newMap.Add(host, entryList);
                            }
                        }
                }

                if (NetTrace.TraceDetail(TraceSubsystem) >= 1)
                {
                    var sb = new StringBuilder(512);

                    foreach (string host in newMap.Keys)
                    {
                        sb.AppendFormat("{0}:", host);
                        foreach (var entry in newMap[host])
                            sb.AppendFormat(" {0}", entry);

                        sb.AppendFormat("\r\n");
                    }

                    NetTrace.Write(TraceSubsystem, 1, "DNS: Status", string.Format("{0} entries", newMap.Count), sb.ToString());
                }

                using (TimedLock.Lock(syncLock))
                {
                    this.hostMap = newMap;
                    AddStaticAndUdpEntries();
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Handles the reception of a UDP packet.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnUdpReceive(IAsyncResult ar)
        {
            DynDnsMessage       message;
            string              key;
            DynDnsHostEntry     entry;
            byte[]              packet;
            int                 cbRecv;
            IPEndPoint          recvEP;

            using (TimedLock.Lock(syncLock))
            {
                if (socket == null)
                    return;

                try
                {
                    perf.HostRegistrations.Increment();

                    cbRecv  = socket.EndReceiveFrom(ar, ref rawRecvEP);
                    recvEP  = (IPEndPoint)rawRecvEP;
                    packet  = Helper.Extract(recvBuf, 0, cbRecv);
                    message = new DynDnsMessage(packet, sharedKey);
                    key     = message.HostEntry.ToString();

                    if (!Helper.Within(DateTime.UtcNow, message.TimeStampUtc, messageTTL))
                        return;     // Ignore messages from far in the past or far in the
                                    // future to avoid replay attacks

                    switch (message.Flags & DynDnsMessageFlag.OpMask)
                    {
                        case DynDnsMessageFlag.OpRegister:

                            // If the host entry is already in the table, then refresh its TTD,
                            // otherwise add it to the table.

                            if (!udpHostMap.TryGetValue(key, out entry))
                            {
                                entry = message.HostEntry;
                                udpHostMap.Add(key, entry);
                            }

                            entry.TTD = SysTime.Now + registrationTTL;

                            // If the host entry is an address and IsNAT=true then register
                            // the UDP packet's source address.

                            if (entry.IsNAT && (entry.HostMode == DynDnsHostMode.Address || entry.HostMode == DynDnsHostMode.AddressList))
                                entry.Address = recvEP.Address;

                            udpHostsChanged = true;
                            break;

                        case DynDnsMessageFlag.OpUnregister:

                            // Remove the entry from the host map if it is present.

                            if (udpHostMap.ContainsKey(key))
                            {
                                udpHostMap.Remove(key);
                                udpHostsChanged = true;
                            }

                            break;
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
                finally
                {
                    if (socket != null)
                    {
                        rawRecvEP = new IPEndPoint(IPAddress.Any, 0);
                        socket.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref rawRecvEP, onUdpReceive, null);
                    }
                }
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
