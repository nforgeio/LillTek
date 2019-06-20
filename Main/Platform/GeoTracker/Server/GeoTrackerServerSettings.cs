//-----------------------------------------------------------------------------
// FILE:        GeoTrackerServerSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Configuration settings for a GeoTrackerNode.

using System;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Configuration settings for a <see cref="GeoTrackerNode" />.
    /// </summary>
    public class GeoTrackerServerSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the GeoTracker server settings from the application's configuration
        /// using the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>The server settings.</returns>
        /// <remarks>
        /// <para>
        /// The GeoTracker server settings are loaded from the application
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
        ///     <td><b>logical://LillTek/GeoTracker/Server</b></td>
        ///     <td>
        ///     The external LillTek Messaging endpoint for the GeoTracker server cluster.
        ///     This is the endpoint that <see cref="GeoTrackerClient" /> instances will
        ///     use to communicate with the cluster.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>ClusterEP</td>
        ///     <td><b>logical://LillTek/GeoTracker/Cluster</b></td>
        ///     <td>
        ///     The internal LillTek Messaging endpoint for the root of the GeoTracker server cluster.
        ///     This is the endpoint that cluster servers will use to communicate with each other.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>ClusterTopology</td>
        ///     <td><see cref="DynamicHashedTopology"/></td>
        ///     <td>
        ///     Describes the topology provider to be used to distribute traffic to
        ///     GeoTracker server instances within the cluster.  This provide must
        ///     implement <see cref="TopologyCapability" />.<see cref="TopologyCapability.Locality" />.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>ClusterArgs</td>
        ///     <td>None</td>
        ///     <td>
        ///     Cluster topology specific parameters formatted as <b>name=value</b> pairs separated
        ///     by LF ('\n') characters.  Use the <see cref="Config" /> "{{" ... ""}}" setting syntax
        ///     and place each argument on a separate line in the config file.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>GeoFixArchiver</td>
        ///     <td><see cref="NullGeoFixArchiver"/></td>
        ///     <td>
        ///     The pluggable <see cref="IGeoFixArchiver" /> type to be used to archive
        ///     <see cref="GeoFix" />es received by the server.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>GeoFixArchiverArgs</td>
        ///     <td>Empty</td>
        ///     <td>
        ///     Archiver specific parameters formatted as <b>name=value</b> pairs separated
        ///     by LF ('\n') characters.  Use the <see cref="Config" /> "{{" ... ""}}" setting syntax
        ///     and place each argument on a separate line in the config file.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>GeoFixRetentionInterval</td>
        ///     <td>1h</td>
        ///     <td>
        ///     The length of time entity <see cref="GeoFix" />es will be retained by GeoTracker.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>GeoFixPurgeInterval</td>
        ///     <td>1m</td>
        ///     <td>
        ///     The interval at which old cached entity <see cref="GeoFix" />es will be purged.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxEntityGeoFixes</td>
        ///     <td>50</td>
        ///     <td>
        ///     The maximum number of <see cref="GeoFix" />es to be cached in memory for an entity.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IndexHighWatermarkLimit</td>
        ///     <td>1000</td>
        ///     <td>
        ///     <para>
        ///     Used to decide when to attempt split an index block into sub-blocks when the number
        ///     of entities in a block is greater than or equal to this value.
        ///     </para>
        ///     <note>
        ///     <b>IndexHighWatermarkLimit</b> must be greater than or equal to <b>IndexLowWatermarkLimit</b>.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IndexLowWatermarkLimit</td>
        ///     <td>750</td>
        ///     <td>
        ///     <para>
        ///     Used to decide when to attempt coalesce sub-blocks when the number
        ///     of entities in a block is greater than or equal to this value.
        ///     </para>
        ///     <note>
        ///     <b>IndexHighWatermarkLimit</b> must be greater than or equal to <b>IndexLowWatermarkLimit</b>.
        ///     </note>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IndexMaxGroupTableLevel</td>
        ///     <td>2</td>
        ///     <td>
        ///     Controls how deep into the index heirarchy group hash tables will be maintained by
        ///     the GeoTracker server.  Enabling these tables deeper in the heirarchy may result in better
        ///     performance for queries with group constraints but potentially at the cost of
        ///     severe memory utilizaton.
        ///     </td>
        /// </tr>
        /// <tr>
        ///     <td>IndexBalancingInterval</td>
        ///     <td>5m</td>
        ///     <td>
        ///     The interval at which the server will attempt to rebalance the location index.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IPGeocodeEnabled</td>
        ///     <td><c>true</c></td>
        ///     <td>
        ///     Controls whether IP geocoding services are to be made available by the GeoTracker server.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IPGeocodeSourceUri</td>
        ///     <td><b>see note</b></td>
        ///     <td>
        ///     The URL where the current IP to location Geocoding database from <a href="http://maxmind.com">maxmind.com</a> 
        ///     can be downloaded.  This must be a decompressed GeoIP City or GeoLite City database file encrypted
        ///     into a <see cref="SecureFile" />.  This setting defaults to: http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IPGeocodeSourceRsaKey</td>
        ///     <td><b>See note</b></td>
        ///     <td>
        ///     The private RSA key used to decrypt the downloaded Geocoding database.  (defaults to
        ///     the value used to manually encrypt the file hosted on http://www.lilltek.com).
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IPGeocodeSourcePollInterval</td>
        ///     <td><b>1d</b></td>
        ///     <td>
        ///     The interval at which server instances will poll for updates to the IP Geocode database,
        ///     Note that server will use the HTTP <b>If-Modified-Since</b> header for efficency since this
        ///     database is updated only once a month.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>IPGeocodeSourceTimeout</td>
        ///     <td><b>5m</b></td>
        ///     <td>
        ///     The maximum time the server will wait for the download of the IP Geocode database file.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SweepInterval</td>
        ///     <td><b>2.5m</b></td>
        ///     <td>
        ///     The interval at which old <see cref="GeoFix" />es, entities, and groups will be
        ///     swept up and discarded by the server.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkInterval</td>
        ///     <td><b>1s</b></td>
        ///     <td>
        ///     The minimum interval at which background activities will be scheduled.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static GeoTrackerServerSettings LoadConfig(string keyPrefix)
        {
            var config   = new Config(keyPrefix);
            var settings = new GeoTrackerServerSettings();

            settings.ServerEP                    = config.Get("ServerEP", settings.ServerEP);
            settings.ClusterEP                   = config.Get("ClusterEP", settings.ClusterEP);
            settings.ClusterTopology             = config.Get("ClusterTopology", settings.ClusterTopology);
            settings.ClusterArgs                 = new ArgCollection(config.Get("ClusterArgs", settings.ClusterArgs.ToString()), '=', '\n');
            settings.GeoFixArchiver              = config.Get("GeoFixArchiver", settings.GeoFixArchiver);
            settings.GeoFixArchiverArgs          = ArgCollection.Parse(config.Get("GeoFixArchiverArgs", settings.GeoFixArchiverArgs.ToString()), '=', '\n');
            settings.GeoFixRetentionInterval     = config.Get("GeoFixRetentionInterval", settings.GeoFixRetentionInterval);
            settings.GeoFixPurgeInterval         = config.Get("GeoFixPurgeInterval", settings.GeoFixPurgeInterval);
            settings.MaxEntityGeoFixes           = config.Get("MaxEntityGeoFixes", settings.MaxEntityGeoFixes);
            settings.IndexHighWatermarkLimit     = config.Get("IndexHighWatermarkLimit", settings.IndexHighWatermarkLimit);
            settings.IndexLowWatermarkLimit      = config.Get("IndexLowWatermarkLimit", settings.IndexLowWatermarkLimit);
            settings.IndexMaxGroupTableLevel     = config.Get("IndexMaxGroupTableLevel", settings.IndexMaxGroupTableLevel);
            settings.IndexBalancingInterval      = config.Get("IndexBalancingInterval", settings.IndexBalancingInterval);
            settings.IPGeocodeEnabled            = config.Get("IPGeocodeEnabled", settings.IPGeocodeEnabled);
            settings.IPGeocodeSourceUri          = config.Get("IPGeocodeSourceUri", settings.IPGeocodeSourceUri);
            settings.IPGeocodeSourceRsaKey       = config.Get("IPGeocodeSourceRsaKey", settings.IPGeocodeSourceRsaKey);
            settings.IPGeocodeSourcePollInterval = config.Get("IPGeocodeSourcePollInterval", settings.IPGeocodeSourcePollInterval);
            settings.IPGeocodeSourceTimeout      = config.Get("IPGeocodeSourceTimeout", settings.IPGeocodeSourceTimeout);
            settings.SweepInterval               = config.Get("SweepInterval", settings.SweepInterval);
            settings.BkInterval                  = config.Get("BkInterval", settings.BkInterval);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The LillTek Messaging endpoint for the GeoTracker server cluster.
        /// This is the endpoint that <see cref="GeoTrackerClient" /> instances will
        /// use to communicate with the cluster.  (defaults to <b>logical://LillTek/GeoTracker/Server</b>).
        /// </summary>
        public string ServerEP { get; set; }

        /// <summary>
        /// The internal LillTek Messaging endpoint for the root of the GeoTracker server cluster.
        /// This is the endpoint that cluster servers will use to communicate with each other.
        /// (defaults to <b>logical://LillTek/GeoTracker/Cluster</b>).
        /// </summary>
        public string ClusterEP { get; set; }

        /// <summary>
        /// The pluggable cluster topology provider type. (defaults to <see cref="DynamicHashedTopology" />).
        /// </summary>
        public Type ClusterTopology { get; set; }

        /// <summary>
        /// Cluster topology specific parameters formatted as <b>name=value</b> pairs separated
        /// by semicolons.
        /// </summary>
        public ArgCollection ClusterArgs { get; set; }

        /// <summary>
        /// The pluggable <see cref="IGeoFixArchiver" /> type to be used to archive
        /// <see cref="GeoFix" />es received by the server.  (Defaults to <see cref="NullGeoFixArchiver" />).
        /// </summary>
        public Type GeoFixArchiver { get; set; }

        /// <summary>
        /// <see cref="IGeoFixArchiver" /> implementation specific arguments (such as a database connection string)
        /// formatted as <b>name=value</b> pairs separated by semicolons.
        /// (Defaults to an empty set).
        /// </summary>
        public ArgCollection GeoFixArchiverArgs { get; set; }

        /// <summary>
        /// The length of time entity <see cref="GeoFix" />es will be retained by GeoTracker.
        /// (Defaults to <b>one hour</b>).
        /// </summary>
        public TimeSpan GeoFixRetentionInterval { get; set; }

        /// <summary>
        /// The interval at which old cached entity <see cref="GeoFix" />es will be purged.
        /// (Defaults to <b>one minute</b>).
        /// </summary>
        public TimeSpan GeoFixPurgeInterval { get; set; }

        /// <summary>
        /// The maximum number of <see cref="GeoFix" />es to be cached in memory for an entity.
        /// (Defaults to <b>30</b>).
        /// </summary>
        public int MaxEntityGeoFixes { get; set; }

        /// <summary>
        /// <para>
        /// Used to decide when to attempt split an index block into sub-blocks when the number
        /// of entities in a block is greater than or equal to this value.  (defaults to <b>1000</b>).
        /// </para>
        /// <note>
        /// <b>IndexHighWatermarkLimit</b> must be greater than or equal to <b>IndexLowWatermarkLimit</b>.
        /// </note>
        /// </summary>
        public int IndexHighWatermarkLimit { get; set; }

        /// <summary>
        /// <para>
        /// Used to decide when to attempt coalesce sub-blocks when the number of entities in a 
        /// block is greater than or equal to this value.  (defaults to <b>750</b>)
        /// </para>
        /// <note>
        /// <b>IndexHighWatermarkLimit</b> must be greater than or equal to <b>IndexLowWatermarkLimit</b>.
        /// </note>
        /// </summary>
        public int IndexLowWatermarkLimit { get; set; }

        /// <summary>
        /// Controls how deep into the index heirarchy group hash tables will be maintained by
        /// the GeoTracker server.  Enabling these tables deeper in the heirarchy may result in better
        /// performance for queries with group constraints but potentially at the cost of
        /// severe memory utilizaton.  (defaults to <b>2</b>)
        /// </summary>
        public double IndexMaxGroupTableLevel { get; set; }

        /// <summary>
        /// The interval at which the server will attempt to rebalance the location index. (defaults to <b>5 minutes</b>)
        /// </summary>
        public TimeSpan IndexBalancingInterval { get; set; }

        /// <summary>
        /// Controls whether IP geocoding services are to be made available by the GeoTracker server.
        /// (defaults to <c>true</c>).
        /// </summary>
        public bool IPGeocodeEnabled { get; set; }

        /// <summary>
        /// The URL where the current IP to location Geocoding database from <a href="http://maxmind.com">maxmind.com</a> 
        /// can be downloaded.  This must be a decompressed GeoIP City or GeoLite City database file encrypted
        /// into a <see cref="SecureFile" />. (defaults to <b>http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat</b>).
        /// </summary>
        public Uri IPGeocodeSourceUri { get; set; }

        /// <summary>
        /// The private RSA key used to decrypt the downloaded Geocoding database.  (defaults to
        /// the value used to manually encrypt the file hosted on http://www.lilltek.com).
        /// </summary>
        public string IPGeocodeSourceRsaKey { get; set; }

        /// <summary>
        /// The interval at which server instances will poll for updates to the IP Geocode database,
        /// Note that server will use the HTTP <b>If-Modified-Since</b> header for efficency since this
        /// database is updated only once a month. (defaults to <b>1d</b>).
        /// </summary>
        public TimeSpan IPGeocodeSourcePollInterval { get; set; }

        /// <summary>
        /// The maximum time the server will wait for the download of the IP Geocode database file.
        /// (defaults to <b>5m</b>).
        /// </summary>
        public TimeSpan IPGeocodeSourceTimeout { get; set; }

        /// <summary>
        /// The interval at which old <see cref="GeoFix" />es, entities, and groups will be
        /// swept up and discarded.
        /// </summary>
        public TimeSpan SweepInterval { get; set; }

        /// <summary>
        /// The minimum interval at which background activities will be scheduled
        /// (defaults to 1 second).
        /// </summary>
        public TimeSpan BkInterval { get; set; }

        /// <summary>
        /// Constructs a settings instance with reasonable defaults.
        /// </summary>
        public GeoTrackerServerSettings()
        {
            this.ServerEP                    = "logical://LillTek/GeoTracker/Server";
            this.ClusterEP                   = "logical://LillTek/GeoTracker/Cluster";
            this.ClusterTopology             = typeof(DynamicHashedTopology);
            this.ClusterArgs                 = new ArgCollection('=', '\n');
            this.GeoFixArchiver              = typeof(NullGeoFixArchiver);
            this.GeoFixArchiverArgs          = new ArgCollection('=', '\n');
            this.GeoFixRetentionInterval     = TimeSpan.FromHours(1);
            this.GeoFixPurgeInterval         = TimeSpan.FromMinutes(1);
            this.MaxEntityGeoFixes           = 30;
            this.IndexHighWatermarkLimit     = 1000;
            this.IndexLowWatermarkLimit      = 750;
            this.IndexMaxGroupTableLevel     = 2;
            this.IndexBalancingInterval      = TimeSpan.FromMinutes(5);
            this.IPGeocodeEnabled            = true;
            this.IPGeocodeSourceUri          = new Uri("http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat");
            this.IPGeocodeSourceRsaKey       = "<RSAKeyValue><Modulus>pCRHtqA872QYibpZif0Xo2xzNhTnXDsIwwTKdM1umBO7Dm+8NBcO23KJNTQQLGzOXtQ8rqMGfAEbXmk4+9pxxu7S5/shuKWV8MjUa1jeMvdfD3f1rh7xDZCoYtGPtMk6vjYM5jckJ4kaNqF7XT4zlEk6qM2am86xMMyThke7xBE=</Modulus><Exponent>AQAB</Exponent><P>3zMihEf+wPLMSonI76TEU3AFAlxFHFW+ZwZ4xmMClLBuQYXKpNbp4YJ6I5Bf2k6ToHtJPqUptZe2Aq93NXpw7Q==</P><Q>vENbvGlu3q/7OhfnScD7LKb+P6aQx1ok/ZLk+pCGkIp1e9dfkNOI278n9y4UQz65JFcuNezmk9J6aUoxPcaPNQ==</Q><DP>IgGHc8IIVVtotr6RZ7mh09iQWtC2EuAZd1bsFcXGAeNzmPYKbtzzm1EmzL5VbExmf5/pA+tkFG+94mDbd8Fk7Q==</DP><DQ>dvsvIA2WR2D7KsTupNs1IwxLRVj0yTj8hdHvqzfqA7Gt/F2qhTJbnV3bWUmi/rjGc+QxTV1ygFwWhzKfmkZCPQ==</DQ><InverseQ>a7E6CztwA2gDf5sSlrUOs95VrmmWISYa6PJOdqefF3+N/odlJ2bJaACjVDlQ7Edsnf2o6QGb0ImRTHW5Qx6kdQ==</InverseQ><D>WGQhKjuIFPI2NJTheumMPTk9obYIESbJRRvjWpr2H3cgmFmbZAG2wn4fXUM4InRFfdOVCgZIi6ac8m5/fUDZW4XUkisQJZaCp4pON25vEt79MXYr3D2sjeVEAVo8f1PFiATNvdSdkbrkWrdkK7alVIX9BfYIH/oZjh53PXoa0kE=</D></RSAKeyValue>";
            this.IPGeocodeSourcePollInterval = TimeSpan.FromDays(1);
            this.IPGeocodeSourceTimeout      = TimeSpan.FromMinutes(5);
            this.SweepInterval               = TimeSpan.FromMinutes(2.5);
            this.BkInterval                  = TimeSpan.FromSeconds(1);
        }
    }
}
