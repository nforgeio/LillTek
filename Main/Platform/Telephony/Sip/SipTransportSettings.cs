//-----------------------------------------------------------------------------
// FILE:        SipTransportSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the transport settings used to configure a SipAgent.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes the transport settings used to configure a <see cref="ISipAgent" />.
    /// </summary>
    public sealed class SipTransportSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Constructs an instance from settings loaded from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
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
        ///     <td>Type</td>
        ///     <td>UDP</td>
        ///     <td>
        ///     Specifies the transport type.  This must be one
        ///     of the following values: <b>UDP</b>, <b>TCP</b>, or <b>TLS</b>.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Binding</td>
        ///     <td>ANY:SIP</td>
        ///     <td>
        ///     The <see cref="NetworkBinding" /> the transport should use
        ///     when binding to the network interface.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>ExternalBinding</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     The <see cref="NetworkBinding" /> that specifies how external clients
        ///     will access the transport.  This will typically be set to the IP
        ///     address and port on the WAN side of the firewall/router that is
        ///     mapped to the <b>Binding</b> specified above.  This defaults to
        ///     <b>Binding</b> unless its IP address is <see cref="IPAddress.Any" />.
        ///     If this is the case, then <b>ExternalBinding</b> will default to
        ///     the IP address of the first active network adapter found and the
        ///     port specified in <b>Binding</b>.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BufferSize</td>
        ///     <td>32K</td>
        ///     <td>
        ///     Byte size of the socket's send and receive buffers.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Timers.*</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     This subsection can be used to override the default 
        ///     timers used by user agents for this transport.
        ///     See the <see cref="SipBaseTimers" /> class' <see cref="SipBaseTimers.LoadConfig" />
        ///     method for more information.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// <para>
        /// Transport settings can be specified in an application configuration
        /// section.  Applications will often use a configuration array so that
        /// multiple transports can be specified.
        /// </para>
        /// </remarks>
        public static SipTransportSettings LoadConfig(string keyPrefix)
        {
            var settings = new SipTransportSettings();
            var config   = new Config(keyPrefix);

            settings.TransportType   = config.Get<SipTransportType>("Type", settings.TransportType);
            settings.Binding         = config.Get("Binding", settings.Binding);
            settings.ExternalBinding = config.Get("ExternalBinding", settings.Binding);
            settings.BufferSize      = config.Get("BufferSize", settings.BufferSize);
            settings.BaseTimers      = SipBaseTimers.LoadConfig(config.KeyPrefix + "Timers");

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The desired <see cref="SipTransportType" />.
        /// </summary>
        public SipTransportType TransportType = SipTransportType.UDP;

        /// <summary>
        /// The network binding to be associated with the transport.
        /// </summary>
        public NetworkBinding Binding = new NetworkBinding("ANY:SIP");

        private NetworkBinding externalBinding = null;

        /// <summary>
        /// The externally visible network binding for this transport.
        /// </summary>
        public NetworkBinding ExternalBinding
        {
            get
            {
                if (externalBinding == null)
                {
                    externalBinding = Binding.Clone();
                    if (externalBinding.Address.Equals(IPAddress.Any))
                        externalBinding = new NetworkBinding(NetHelper.GetActiveAdapter(), externalBinding.Port);
                }

                return externalBinding;
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException("ExternalBinding cannot be set to null.");

                externalBinding = value;
                if (value.Address.Equals(IPAddress.Any))
                    externalBinding = new NetworkBinding(NetHelper.GetActiveAdapter(), externalBinding.Port);
            }
        }

        /// <summary>
        /// Size of the transport socket's send and receive buffers.
        /// </summary>
        public int BufferSize = 32 * 1024;

        /// <summary>
        /// The transaction and dialog related timers to use with this transport.
        /// </summary>
        public SipBaseTimers BaseTimers;

        /// <summary>
        /// Constructs a settings instance using defauls.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default settings specify:
        /// </para>
        /// <list type="bullet">
        ///     <item>UDP tranmsport</item>
        ///     <item>Binding to all network interfaces on port 5060.</item>
        ///     <item>32K socket buffer.</item>
        /// </list>
        /// </remarks>
        public SipTransportSettings()
        {
            this.BaseTimers = new SipBaseTimers();
        }

        /// <summary>
        /// Constructs an instance from settings passed as explicit parameters.
        /// </summary>
        /// <param name="transportType">The desired <see cref="SipTransportType" />.</param>
        /// <param name="binding">The network binding to be associated with the transport.</param>
        /// <param name="bufferSize">Size of the transport socket's send and receive buffers (or 0 for a reasonable default).</param>
        public SipTransportSettings(SipTransportType transportType, NetworkBinding binding, int bufferSize)
        {
            this.TransportType = transportType;
            this.Binding       = binding;
            this.BaseTimers    = new SipBaseTimers();

            if (bufferSize > 0)
                this.BufferSize = bufferSize;
        }
    }
}
