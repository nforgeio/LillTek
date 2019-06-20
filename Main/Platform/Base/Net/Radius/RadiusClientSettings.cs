//-----------------------------------------------------------------------------
// FILE:        RadiusClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the settings for the RadiusClient class.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Specifies the settings for the <see cref="RadiusClient" /> class.
    /// </summary>
    public sealed class RadiusClientSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the RADIUS client settings from the application's configuration
        /// using the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>The server settings.</returns>
        /// <remarks>
        /// <para>
        /// The RADIUS client settings are loaded from the application
        /// configuration, using the specified key prefix.  The following
        /// settings are recognized by the class:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Server[#]</td>
        ///     <td>(required)</td>
        ///     <td>
        ///     An array specifying one or more RADIUS server network bindings
        ///     formatted as descibed in <see cref="NetworkBinding" />.  Authentication 
        ///     packets will be transmitted to these servers using a round robin 
        ///     mechanism to implement load balancing and failover.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SocketBuffer</td>
        ///     <td>32K</td>
        ///     <td>
        ///     Byte size of the client socket's send and receive buffers.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>NetworkBinding</td>
        ///     <td>ANY:0</td>
        ///     <td>
        ///     <para>
        ///     Specifies the IP address of the network card the client is
        ///     and port bindings.  Use an IP address of ANY to bind to 
        ///     all network interfaces.  ANY is suitable for single homed machines.
        ///     Machines that are actually connected to multiple networks should 
        ///     specify a specific network binding here to ensure that the NAS-IP-Address
        ///     of RADIUS authentication packets are initialized properly.
        ///     </para>
        ///     <para>
        ///     A specific port number may be selected or 0 can be specified,
        ///     indicating that the operating system should select a free port.
        ///     </para>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>PortCount</td>
        ///     <td>4</td>
        ///     <td>
        ///     The number of RADIUS client UDP ports to open.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Secret</td>
        ///     <td>(required)</td>
        ///     <td>
        ///     The secret shared by the RADIUS client and server.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>RetryInterval</td>
        ///     <td>5s</td>
        ///     <td>
        ///     Maximum time to wait for a response packet before retransmitting
        ///     an authentication request.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1s</td>
        ///     <td>
        ///     The interval at which background tasks such as retransmitting
        ///     a request should be processed.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxTransmissions</td>
        ///     <td>4</td>
        ///     <td>
        ///     The maximum number of transmission attempts before aborting an
        ///     authentication with a timeout.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>RealmFormat</td>
        ///     <td>Email</td>
        ///     <td>
        ///     Specifies how user names are to be generated from the
        ///     <b>realm</b> and <b>account</b> components.  See 
        ///     <see cref="RealmFormat" /> for more information.
        ///     The possible values are: <b>Slash</b> and <b>Email</b>.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static RadiusClientSettings LoadConfig(string keyPrefix)
        {
            RadiusClientSettings    settings;
            Config                  config;
            NetworkBinding[]        servers;
            string                  secret;

            config = new Config(keyPrefix);

            servers = config.GetNetworkBindingArray("Server");
            if (servers == null || servers.Length == 0)
                throw new RadiusException("[{0}Server] configuration setting is missing.", config.KeyPrefix);

            secret = config.Get("Secret", (string)null);
            if (secret == null)
                throw new RadiusException("[{0}Secret] configuration setting is missing.", config.KeyPrefix);

            settings                  = new RadiusClientSettings(servers, secret);
            settings.SocketBuffer     = config.Get("SocketBuffer", settings.SocketBuffer);
            settings.NetworkBinding   = config.Get("NetworkBinding", settings.NetworkBinding);
            settings.RetryInterval    = config.Get("RetryInterval", settings.RetryInterval);
            settings.BkTaskInterval   = config.Get("BkTaskInterval", settings.BkTaskInterval);
            settings.MaxTransmissions = config.Get("MaxTransmissions", settings.MaxTransmissions);
            settings.PortCount        = config.Get("PortCount", settings.PortCount);
            settings.RealmFormat      = config.Get<RealmFormat>("RealmFormat", settings.RealmFormat);

            if (settings.NetworkBinding.IsHost)
                throw new RadiusException("[{0}] is not a valid RADIUS client network binding.", settings.NetworkBinding);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The network bindings of the RADIUS servers.  This field must be specified 
        /// in the constructor.
        /// </summary>
        public readonly NetworkBinding[] Servers;

        /// <summary>
        /// The size of the client socket send and receive buffers.
        /// Default is 32K.
        /// </summary>
        public int SocketBuffer = 32 * 1024;

        /// <summary>
        /// The secret shared by this client and the RADIUS server.  This
        /// field must be specified in the constructor.
        /// </summary>
        public readonly string Secret;

        /// <summary>
        /// Specifies the IP address of the network card this client is
        /// to use.  This defaults to IPAddress.Any which is suitable for
        /// single homed machines.  This should be configured with a 
        /// proper IP address for machines that are actually connected
        /// to two different networks.
        /// </summary>
        public NetworkBinding NetworkBinding = NetworkBinding.Any;

        /// <summary>
        /// Maximum time to wait for a response packet before retransmitting.
        /// Default is 10 seconds.
        /// </summary>
        public TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Interval at which background client tasks (such as handling
        /// packet retransmissions) are scheduled.  Default is 1 second.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The maximum number of transmission attempts before aborting an
        /// authentication with a timeout.  Default is 4.
        /// </summary>
        public int MaxTransmissions = 4;

        /// <summary>
        /// The number of client RADIUS UDP ports to be opened by the 
        /// RADIUS client to avoid packet correlation ID wraparound under
        /// high loads.  Defaults to 4.
        /// </summary>
        public int PortCount = 4;

        /// <summary>
        /// Specifies how user names are to be serialized from <b>realm</b> and
        /// <b>account</b> components.  This defaults to <see cref="LillTek.Common.RealmFormat.Email" />.
        /// </summary>
        public RealmFormat RealmFormat = RealmFormat.Email;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="servers">The host names or IP addresses of the RADIUS servers.</param>
        /// <param name="secret">The secret shared by the RADIUS client and server.</param>
        public RadiusClientSettings(NetworkBinding[] servers, string secret)
        {
            this.Servers = servers;
            this.Secret  = secret;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="server">The network binding for the RADIUS server.</param>
        /// <param name="secret">The secret shared by the RADIUS client and server.</param>
        public RadiusClientSettings(NetworkBinding server, string secret)
        {
            this.Servers = new NetworkBinding[] { server };
            this.Secret  = secret;
        }
    }
}
