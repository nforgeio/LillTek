//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastServerSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the settings to be used when starting a UdpBroadcastServer.

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
    /// Specifies the settings to be used when starting a <see cref="UdpBroadcastServer" />.
    /// </summary>
    public class UdpBroadcastServerSettings
    {
        /// <summary>
        /// The network binding to be used for the UDP broadcast server instance.
        /// </summary>
        public NetworkBinding NetworkBinding { get; set; }

        /// <summary>
        /// Specifies the size of the underlying socket's send and receive buffers.
        /// </summary>
        public int SocketBufferSize { get; set; }

        /// <summary>
        /// The network bindings for all of the UDP broadcast servers in the cluster.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This must include the network binding for the current UDP broadcast server instance.
        /// </note>
        /// </remarks>
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
        /// The interval at which the server will wake up to handle background activities.
        /// </summary>
        public TimeSpan BkTaskInterval { get; set; }

        /// <summary>
        /// The interval between the automatic sending of <b>server</b> messages to the 
        /// UDP servers (essentially the keep-alive interval).
        /// </summary>
        public TimeSpan ClusterKeepAliveInterval { get; set; }

        /// <summary>
        /// Maximum time to maintain a broadcast server registration without receiving
        /// a <b>server</b> message from the server.  This should be a reasonable multiple 
        /// (2-3) of <see cref="ClusterKeepAliveInterval" />.
        /// </summary>
        public TimeSpan ServerTTL { get; set; }

        /// <summary>
        /// Maximum time to maintain a broadcast client registration without recieving
        /// a <b>register</b> message from the client.  This should be a reasonable
        /// multiple (2-3) of the <see cref="UdpBroadcastClientSettings" />.<see cref="UdpBroadcastClientSettings.KeepAliveInterval" />
        /// property.
        /// </summary>
        public TimeSpan ClientTTL { get; set; }

        /// <summary>
        /// Constructs an instance with reasonable default values.
        /// </summary>
        public UdpBroadcastServerSettings()
        {
            this.NetworkBinding           = new NetworkBinding(IPAddress.Any, NetworkPort.UdpBroadcast);
            this.SocketBufferSize         = 1024 * 1024;
            this.Servers                  = new NetworkBinding[] { new NetworkBinding(IPAddress.Any, NetworkPort.UdpBroadcast) };
            this.SharedKey                = new SymmetricKey(UdpBroadcastHelper.DefaultSharedKey);
            this.MessageTTL               = TimeSpan.FromMinutes(15);
            this.BkTaskInterval           = TimeSpan.FromSeconds(1);
            this.ClusterKeepAliveInterval = TimeSpan.FromSeconds(15);
            this.ServerTTL                = TimeSpan.FromSeconds(50);
            this.ClientTTL                = TimeSpan.FromSeconds(95);
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
        ///     <td>ANY:UDP-BROADCAST</td>
        ///     <td>
        ///     The <see cref="NetworkBinding" /> to be used for the UDP broadcast server instance.
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
        ///     in the cluster.  Note that this must include the binding for the current UDP broadcast
        ///     server instance.
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
        ///     <td>BkTaskInterval</td>
        ///     <td>1s</td>
        ///     <td>
        ///     The interval at which the server will wake up to handle background activities.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>ClusterKeepAliveInterval</td>
        ///     <td>15s</td>
        ///     <td>
        ///     The interval between the automatic sending of <b>server</b> messages to the 
        ///     UDP servers (essentially the keep-alive interval).
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>ServerTTL</td>
        ///     <td>50s</td>
        ///     <td>
        ///     Maximum time to maintain a broadcast server registration without receiving
        ///     a <b>server</b> message from the server.  This should be a reasonable multiple 
        ///     (2-3) of <see cref="ClusterKeepAliveInterval" />.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>ClientTTL</td>
        ///     <td>95s</td>
        ///     <td>
        ///     Maximum time to maintain a broadcast client registration without recieving
        ///     a <b>register</b> message from the client.  This should be a reasonable
        ///     multiple (2-3) of the <see cref="UdpBroadcastClientSettings" />.<see cref="UdpBroadcastClientSettings.KeepAliveInterval" />
        ///     property.
        ///     </td>
        ///  </tr>
        /// </table>
        /// </div>
        /// </remarks>
        /// <exception cref="FormatException">Thrown if the settings are not valid.</exception>
        public UdpBroadcastServerSettings(string keyPrefix)
            : this()
        {
            Config                  config = new Config(keyPrefix);
            string[]                array;
            List<NetworkBinding>    servers;

            this.NetworkBinding = config.Get("NetworkBinding", this.NetworkBinding);

            array = config.GetArray("Server");
            if (array != null && array.Length > 0)
            {
                servers = new List<NetworkBinding>(array.Length);
                foreach (var v in array)
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
            }
            else
            {
                servers = new List<NetworkBinding>(1);
                servers.Add(this.NetworkBinding);
            }

            this.Servers                  = servers.ToArray();
            this.SocketBufferSize         = config.Get("SocketBufferSize", this.SocketBufferSize);
            this.SharedKey                = new SymmetricKey(config.Get("SharedKey", UdpBroadcastHelper.DefaultSharedKey));
            this.MessageTTL               = config.Get("MessageTTL", this.MessageTTL);
            this.BkTaskInterval           = config.Get("BkTaskInterval", this.BkTaskInterval);
            this.ClusterKeepAliveInterval = config.Get("ClusterKeepAliveInterval", this.ClusterKeepAliveInterval);
            this.ServerTTL                = config.Get("ServerTTL", this.ServerTTL);
            this.ClientTTL                = config.Get("ClientTTL", this.ClientTTL);
        }
    }
}
