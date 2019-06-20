//-----------------------------------------------------------------------------
// FILE:        RadiusServerSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the settings for the RadiusServer class.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Specifies the settings for the <see cref="RadiusServer" /> class.
    /// </summary>
    public sealed class RadiusServerSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the RADIUS server settings from the application's configuration
        /// using the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>The server settings.</returns>
        /// <remarks>
        /// <para>
        /// The RADIUS server settings are loaded from the application
        /// configuration, under the specified key prefix.  The following
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
        ///     <td>NetworkBinding</td>
        ///     <td>ANY:RADIUS</td>
        ///     <td>
        ///     Specifies the network binding for the server expressed as an IP address and port.
        ///     An IP address of 0.0.0.0 specifies that the server is bound to all network interfaces.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SocketBuffer</td>
        ///     <td>128K</td>
        ///     <td>
        ///     Byte size of the server socket's send and receive buffers.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DnsRefreshInterval</td>
        ///     <td>15m</td>
        ///     <td>
        ///     Specifies the interval at which Network Access Service (NAS) DNS host
        ///     names will be requeried to resolve to the IP addresses used to 
        ///     identify the NAS device.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1m</td>
        ///     <td>
        ///     Specifies the interval at which the server will process background tasks.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Devices[#]</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     <para>
        ///     An optional array of entries describing the known NAS devices.  The format for 
        ///     each entry is:
        ///     </para>
        ///     <code lang="none">
        ///         &lt;host name or IP address&gt; ";" &lt;shared secret&gt;
        ///     </code>
        ///     <para>
        ///     which maps the devices host name or IP address to the shared secret (aka the password).
        ///     </para>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DefaultSecret</td>
        ///     <td>(none)</td>
        ///     <td>
        ///     <para>
        ///     This setting, if present specifies a default NAS shared secret.  This secret
        ///     will be used to attempt to decrypt passwords from RADIUS packets received
        ///     from NAS devices that we're not specifically listed in the <b>Devices[#]</b>
        ///     setting array.    This is a convenient feature for IT operations but its 
        ///     use will reduce the security of the service a bit.
        ///     </para>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>RealmFormat</td>
        ///     <td>Email</td>
        ///     <td>
        ///     Specifies how the <b>realm</b> and <b>account</b> components are
        ///     to be parsed from a user name.  See <see cref="LillTek.Common.RealmFormat" /> 
        ///     for more information.  The possible values are: <b>Slash</b> and <b>Email</b>.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static RadiusServerSettings LoadConfig(string keyPrefix)
        {
            var         config   = new Config(keyPrefix);
            var         settings = new RadiusServerSettings();
            string[]    devices;

            settings.NetworkBinding     = config.Get("NetworkBinding", settings.NetworkBinding);
            settings.SocketBuffer       = config.Get("SocketBuffer", settings.SocketBuffer);
            settings.DnsRefreshInterval = config.Get("DnsRefreshInterval", settings.DnsRefreshInterval);
            settings.BkTaskInterval     = config.Get("BkTaskInterval", settings.BkTaskInterval);
            settings.DefaultSecret      = config.Get("DefaultSecret", settings.DefaultSecret);
            settings.RealmFormat        = config.Get<RealmFormat>("RealmFormat", settings.RealmFormat);

            if (settings.NetworkBinding.IsHost)
                throw new RadiusException("[{0}] is not a valid RADIUS server network binding.", settings.NetworkBinding);

            // Load the NAS device information

            devices = config.GetArray("Devices", new string[0]);
            for (int i = 0; i < devices.Length; i++)
            {
                string      host;
                IPAddress   address;
                string      password;
                int         pos;

                pos = devices[i].IndexOf(';');
                if (pos == -1)
                    throw new RadiusException("{0}Devices[{1}] configuration setting missing a ':'.", config.KeyPrefix, i);

                host = devices[i].Substring(0, pos).Trim();
                password = devices[i].Substring(pos + 1).Trim();

                if (host == string.Empty)
                    throw new RadiusException("{0}Devices[{1}] configuration setting has an invalid device host name or IP address.", config.KeyPrefix, i);

                if (Helper.TryParseIPAddress(host, out address))
                    settings.Devices.Add(new RadiusNasInfo(address, password));
                else
                    settings.Devices.Add(new RadiusNasInfo(host, password));
            }

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The network endpoint the RADIUS server to which the server instance
        /// should be bound.  This defaults to <see cref="IPAddress.Any" />:1812.
        /// </summary>
        public NetworkBinding NetworkBinding = new NetworkBinding(IPAddress.Any, NetworkPort.RADIUS);

        /// <summary>
        /// The size of the server socket send and receive buffers.
        /// Default is 128K.
        /// </summary>
        public int SocketBuffer = 128 * 1024;

        /// <summary>
        /// The period at which the RADIUS server should requery the DNS for
        /// the IP addresses associated with NAS host names.  This defaults
        /// to 15 minutes.
        /// </summary>
        public TimeSpan DnsRefreshInterval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Background task scheduling interval.  This defaults to 1 minute.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The initial set of NAS devices information.  This defaults to an empty list.
        /// </summary>
        public List<RadiusNasInfo> Devices = new List<RadiusNasInfo>();

        /// <summary>
        /// This setting, if non-<c>null</c> specifies a default NAS shared secret.  This secret
        /// will be used to attempt to decrypt passwords from RADIUS packets received
        /// from NAS devices that we're not specifically listed in the <b>Devices[#]</b>
        /// setting array.
        /// </summary>
        public string DefaultSecret = null;

        /// <summary>
        /// Specifies how user names are to be parsed into <b>realm</b> and
        /// <b>account</b> components.  This defaults to <see cref="LillTek.Common.RealmFormat.Email" />.
        /// </summary>
        public RealmFormat RealmFormat = RealmFormat.Email;

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusServerSettings()
        {
        }
    }
}
