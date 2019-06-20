//-----------------------------------------------------------------------------
// FILE:        AppStoreClientSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the configuration settings for AppStoreClient.

using System;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Defines the configuration settings for <see cref="AppStoreClient" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default constructor initializes an instance with reaasonable default values for all settings
    /// so application need only modify those settings that are important.  The <see cref="LoadConfig" />
    /// method can also be used to initialize an instance by reading from the application configuration.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>ClusterEP</td>
    ///     <td><see cref="AppStoreClient.AbstractClusterEP" />.</td>
    ///     <td>
    ///     The broadcast endpoint for the application store cluster.
    ///     This defaults to <b>abstract://LillTek/DataCenter/AppStore/*</b>
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>LocalCache</td>
    ///     <td>false</td>
    ///     <td>
    ///     Specifies whether packages will be cached locally by the <see cref="AppStoreClient" />.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>PackageFolder</td>
    ///     <td>Packages</td>
    ///     <td>
    ///     Specifies the file system folder where the application packages are to be cached.
    ///     This can be an absolute or relative path.  Note that relative paths are relative
    ///     to the folder where the application's entry assembly resides, using the entry
    ///     assembly returned by <see cref="Helper.GetEntryAssembly" />.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>PackageTTL</td>
    ///     <td>7d</td>
    ///     <td>
    ///     The maximum time a package will be cached locally by an <see cref="AppStoreClient" />.
    ///     Set this to <see cref="TimeSpan.Zero" /> to disable caching.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>PurgeInterval</td>
    ///     <td>60m</td>
    ///     <td>
    ///     The interval at which the package cache should be scan and purge packages.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>PurgeInterval</td>
    ///     <td>5m</td>
    ///     <td>
    ///     The interval at which the package cache should be scanned for packages to be purged.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>BkTaskInterval</td>
    ///     <td>1m</td>
    ///     <td>
    ///     The interval at which engine background tasks are scheduled.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public sealed class AppStoreClientSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads the <see cref="AppStoreClient" /> settings from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>The settings.</returns>
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
        ///     <td>ClusterEP</td>
        ///     <td><see cref="AppStoreClient.AbstractClusterEP" />.</td>
        ///     <td>
        ///     The broadcast endpoint for the application store cluster.
        ///     This defaults to <b>abstract://LillTek/DataCenter/AppStore/*</b>
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>LocalCache</td>
        ///     <td>false</td>
        ///     <td>
        ///     Specifies whether packages will be cached locally by the <see cref="AppStoreClient" />.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>PackageFolder</td>
        ///     <td>Packages</td>
        ///     <td>
        ///     Specifies the file system folder where the application packages are to be cached.
        ///     This can be an absolute or relative path.  Note that relative paths are relative
        ///     to the folder where the application's entry assembly resides, using the entry
        ///     assembly returned by <see cref="Helper.GetEntryAssembly" />.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>PackageTTL</td>
        ///     <td>7d</td>
        ///     <td>
        ///     The maximum time a package will be cached locally by an <see cref="AppStoreClient" />.
        ///     Set this to <see cref="TimeSpan.Zero" /> to disable caching.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>PurgeInterval</td>
        ///     <td>5m</td>
        ///     <td>
        ///     The interval at which the package cache should be scanned for packages to be purged.
        ///     </td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1m</td>
        ///     <td>
        ///     The interval at which engine background tasks are scheduled.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static AppStoreClientSettings LoadConfig(string keyPrefix)
        {
            var settings = new AppStoreClientSettings();
            var config   = new Config(keyPrefix);

            settings.ClusterEP     = config.Get("ClusterEP", settings.ClusterEP.ToString());
            settings.PackageFolder = config.Get("PackageFolder", "Packages");
            settings.PackageTTL    = config.Get("PackageTTL", settings.PackageTTL);
            settings.PurgeInterval = config.Get("PurgeInterval", settings.PurgeInterval);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The broadcast endpoint for the application store cluster.  This defaults 
        /// to <b>abstract://LillTek/DataCenter/AppStore/*</b> 
        /// </summary>
        public MsgEP ClusterEP = AppStoreClient.AbstractClusterEP;

        /// <summary>
        /// Specifies whether local package caching is enabled.  This defaults to <c>false</c>.
        /// </summary>
        public bool LocalCache = false;

        /// <summary>
        /// Specifies the file system folder where the application packages are to be cached.
        /// This can be an absolute or relative path.  Note that relative paths are relative
        /// to the folder where the application's entry assembly resides, using the entry
        /// assembly returned by <see cref="Helper.GetEntryAssembly" />.  This defaults to
        /// <b>Packages</b>.
        /// </summary>
        public string PackageFolder = "Packages";

        /// <summary>
        /// The maximum time a package will be cached locally by an <see cref="AppStoreClient" />.
        /// This defaults to <b>7d</b>
        /// </summary>
        public TimeSpan PackageTTL = TimeSpan.FromDays(7);

        /// <summary>
        /// The interval at which the package cache should be scanned packages to be purged.
        /// This defaults to <b>5m</b>.
        /// </summary>
        public TimeSpan PurgeInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The interval at which engine background tasks are scheduled.  This defaults
        /// to <b>1m</b>.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppStoreClientSettings()
        {
        }
    }
}
