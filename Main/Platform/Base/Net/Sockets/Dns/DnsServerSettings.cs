//-----------------------------------------------------------------------------
// FILE:        DnsServerSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the configuration settings used by the DnsServer class.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines the configuration settings used by the <see cref="DnsServer" /> class.
    /// </summary>
    public class DnsServerSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads settings from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>A <see cref="DnsServerSettings" /> instance.</returns>
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
        ///     <td>ANY:DNS</td>
        ///     <td>
        ///     Specifies the <see cref="NetworkBinding" /> the server should listen on.
        ///     </td>
        ///  </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static DnsServerSettings LoadConfig(string keyPrefix)
        {
            Config              config;
            DnsServerSettings   settings;

            config = new Config(keyPrefix);
            settings = new DnsServerSettings();
            settings.NetworkBinding = config.Get("NetworkBinding", settings.NetworkBinding);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Specifies the <see cref="NetworkBinding" /> the server should listen on.
        /// This defaults to <b>ANY:DNS</b>.
        /// </summary>
        public NetworkBinding NetworkBinding = new NetworkBinding(IPAddress.Any, NetworkPort.DNS);

        /// <summary>
        /// Constructs a settings instance with reasonable default settings.
        /// </summary>
        public DnsServerSettings()
        {
        }
    }
}
