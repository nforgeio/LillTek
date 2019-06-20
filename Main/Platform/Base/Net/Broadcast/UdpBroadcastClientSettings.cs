//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the settings to be used when starting a UdpBroadcastClient.
//
//              http://www.opensource.org/licenses/ms-pl.html/

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
    /// Specifies the settings to be used when starting a <see cref="UdpBroadcastClient" />.
    /// </summary>
    public class UdpBroadcastClientSettings
    {
        /// <summary>
        /// The network binding to be used for the UDP broadcast client instance.
        /// </summary>
        public NetworkBinding NetworkBinding { get; set; }

        /// <summary>
        /// Specifies the size of the underlying socket's send and receive buffers.
        /// </summary>
        public int SocketBufferSize { get; set; }

        /// <summary>
        /// The network bindings for all of the UDP broadcast servers in the cluster.
        /// </summary>
        public NetworkBinding[] Servers { get; set; }

        /// <summary>
        /// The shared encryption key used to secure messages sent between UDP clients and servers.
        /// </summary>
        public SymmetricKey SharedKey { get; set; }

        /// <summary>
        /// The maximum delta to be allowed between the timestamp of messages received from
        /// UDP broadcast clients and servers and the current system time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Messages transmitted between clients and servers in the UDP broadcast cluster are
        /// timestamped with the time they were sent (UTC) to avoid replay attacks.  This
        /// setting controls which messages will be discarded for being having a timestamp
        /// too far in the past or too far into the future.
        /// </para>
        /// <para>
        /// Ideally, this value would represent the maximum time a message could realistically
        /// remain in transit on the network (a few seconds), but this setting also needs to
        /// account for the possibility that the server system clocks may be out of sync.  So,
        /// this value is a tradeoff between security and reliability.
        /// </para>
        /// </remarks>
        public TimeSpan MessageTTL { get; set; }

        /// <summary>
        /// An integer between 0..255 that specifies the broadcast group the UDP broadcast
        /// client will join.
        /// </summary>
        public int BroadcastGroup { get; set; }

        /// <summary>
        /// The interval at which the server will wake up to handle background activities.
        /// </summary>
        public TimeSpan BkTaskInterval { get; set; }

        /// <summary>
        /// The interval between the automatic sending of <b>register</b> messages to the 
        /// UDP servers (essentially the keep-alive interval).
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// The interval at which the client will requery the DNS to resolve any host names
        /// in the <see cref="Servers" /> bindings into IP addresses.
        /// </summary>
        public TimeSpan ServerResolveInterval { get; set; }

        /// <summary>
        /// Constructs an instance with reasonable default values.
        /// </summary>
        public UdpBroadcastClientSettings()
        {
            this.NetworkBinding        = NetworkBinding.Any;
            this.SocketBufferSize      = 1024 * 1024;
            this.Servers               = new NetworkBinding[0];
            this.SharedKey             = new SymmetricKey(UdpBroadcastHelper.DefaultSharedKey);
            this.MessageTTL            = TimeSpan.FromMinutes(15);
            this.BroadcastGroup        = 0;
            this.BkTaskInterval        = TimeSpan.FromSeconds(1);
            this.KeepAliveInterval     = TimeSpan.FromSeconds(30);
            this.ServerResolveInterval = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Constructs the settings by reading from the application configuration at the
        /// specified prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration prefix string.</param>
        /// <remarks>
        /// <para>
        /// This method loads the following settings from the application
        /// configuration:
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
        ///     <td>0.0.0.0:0</td>
        ///     <td>
        ///     The <see cref="NetworkBinding" /> to be used for the UDP broadcast client instance.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>SocketBufferSize</td>
        ///     <td>1M</td>
        ///     <td>
        ///     Specifies the size of the underlying socket's send and receive buffers.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>Server[#]</td>
        ///     <td>(required)</td>
        ///     <td>
        ///     Specifies the array of <see cref="NetworkBinding" />s identifying the UDP broadcast servers
        ///     in the cluster.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>SharedKey</td>
        ///     <td>(empty)</td>
        ///     <td>
        ///     The shared symmetric encryption key used to secure messages sent between clients
        ///     and servers within the broadcast cluster.  This key string must be formatted as
        ///     required by the <see cref="SymmetricKey" /> class constructor.
        ///     </td>
        ///  </tr>
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
        ///  </tr>
        /// <tr valign="top">
        ///     <td>BroadcastGroup</td>
        ///     <td>0</td>
        ///     <td>
        ///     An integer between 0..255 that specifies the broadcast group the UDP broadcast
        ///     client will join.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1s</td>
        ///     <td>
        ///     The interval at which the server will wake up to handle background activities.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>KeepAliveInterval</td>
        ///     <td>30s</td>
        ///     <td>
        ///     The interval between the automatic sending of <b>register</b> messages to the 
        ///     UDP servers (essentially the keep-alive interval).
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>ServerResolveInterval</td>
        ///     <td>5m</td>
        ///     <td>
        ///     The interval at which the client will requery the DNS to resolve any host names
        ///     in the <see cref="Servers" /> bindings into IP addresses.
        ///     </td>
        ///  </tr>
        /// </table>
        /// </div>
        /// </remarks>
        /// <exception cref="FormatException">Thrown if the settings are not valid.</exception>
        public UdpBroadcastClientSettings(string keyPrefix)
            : this()
        {
            var config  = new Config(keyPrefix);
            var servers = new List<NetworkBinding>();

            foreach (var v in config.GetArray("Server"))
            {
                try
                {
                    servers.Add(new NetworkBinding(v));
                }
                catch
                {

                    throw new FormatException(string.Format("One or more UDP broadcast server endpoints specified in [{0}.Servers] configuration setting is invalid.", keyPrefix));
                }
            }

            this.SocketBufferSize      = config.Get("SocketBufferSize", this.SocketBufferSize);
            this.Servers               = servers.ToArray();
            this.NetworkBinding        = config.Get("NetworkBinding", this.NetworkBinding);
            this.SharedKey             = new SymmetricKey(config.Get("SharedKey", UdpBroadcastHelper.DefaultSharedKey));
            this.MessageTTL            = config.Get("MessageTTL", this.MessageTTL);
            this.BroadcastGroup        = config.Get("BroadcastGroup", this.BroadcastGroup);
            this.BkTaskInterval        = config.Get("BkTaskInterval", this.BkTaskInterval);
            this.KeepAliveInterval     = config.Get("KeepAliveInterval", this.KeepAliveInterval);
            this.ServerResolveInterval = config.Get("ServerResolveInterval", this.ServerResolveInterval);
        }
    }
}
