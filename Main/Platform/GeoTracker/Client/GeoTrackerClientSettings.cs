//-----------------------------------------------------------------------------
// FILE:        GeoTrackerClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the configuration settings for a GeoTrackerClient instance.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.GeoTracker
{
    /// <summary>
    /// Holds the configuration settings for a <see cref="GeoTrackerClient" /> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 
    /// </para>
    /// </remarks>
    public class GeoTrackerClientSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the GeoTracker client settings from the application's configuration
        /// using the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>The server settings.</returns>
        /// <remarks>
        /// <para>
        /// The GeoTracker client settings are loaded from the application
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
        ///     <td>ServerEP</td>
        ///     <td><b>abstract://LillTek/GeoTracker/Server</b></td>
        ///     <td>
        ///     The external LillTek Messaging endpoint used by the client to
        ///     communicate with the GeoTracker cluster.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static GeoTrackerClientSettings LoadConfig(string keyPrefix)
        {
            var config   = new Config(keyPrefix);
            var settings = new GeoTrackerClientSettings();

            settings.ServerEP = config.Get("ServerEP", settings.ServerEP);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The external LillTek Messaging endpoint used by the client to
        /// communicate with the GeoTracker cluster.
        /// (defaults to <b>logical://LillTek/GeoTracker/Server</b>).
        /// </summary>
        public string ServerEP { get; set; }

        /// <summary>
        /// Constructs a settings instance with reasonable defaults.
        /// </summary>
        public GeoTrackerClientSettings()
        {
            this.ServerEP = "logical://LillTek/GeoTracker/Server";
        }
    }
}
