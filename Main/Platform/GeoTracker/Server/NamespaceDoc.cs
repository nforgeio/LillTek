//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace documentation

using System;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Overview of the LillTek GeoTracker platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GeoTracker platform consists client and server class libraries as well as
    /// a server-side service application.  The platform is designed to act as a nexus
    /// for the collection and management of geolocation fixes for client devices.
    /// The platform implements the following essential capabilities:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Geocoding</term>
    ///         <description>
    ///         Geocoding is the process of determining the geographic coordinates (e.g. latitide
    ///         and longitude) from and other geographic information such as street address
    ///         or indirectly by mapping information such as an IP address to a country, city,
    ///         and location coodinates.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Reverse Geocoding</term>
    ///         <description>
    ///         Reverse geocoding is the process of mapping geographic coordinates to other
    ///         information, such as street address or points of interest.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Location Archival</term>
    ///         <description>
    ///         The GeoTracker server provides for the reliable archival of streams of
    ///         location fixes received from client devices.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Location Queries</term>
    ///         <description>
    ///         The GeoTracker server clusters track recent known location fixes for client
    ///         devices and/or groups of devices and supports distributed real-time
    ///         queries against this information.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Geofencing</term>
    ///         <description>
    ///         <para>
    ///         A geofence is a virtual boundry around a geographic point of interest.  The GeoTracker
    ///         service infrastructure provides for the maintenance collections of geofences and will
    ///         trigger events when devices enter or exit a fenced region.
    ///         </para>
    ///         <para>
    ///         The GeoTracker platform also provides tools for generating geofences from data sources
    ///         such the <a href="http://www.microsoft.com/maps/developers/web.aspx">Microsoft Bing Maps services</a>.
    ///         </para>
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// GeoTracker related classes are distributed across the <b>LillTek.Common</b>, <b>LillTek.GeoTracker</b>, and
    /// <b>LillTek.GeoTracker.Server</b> class libraries.  The Common library implements basic coordinate related classes
    /// such as <see cref="GeoCoordinate" />, <see cref="GeoFix" /> as well as the <see cref="IGeoFixSource" />
    /// interface.  These simple classes are located in the Common library so that code that only needs to
    /// serialize or manipulate location fixes will not have to import the GeoTracker libraries.
    /// </para>
    /// <para>
    /// The <b>LillTek.GeoTracker</b> and <b>LillTek.GeoTracker.Server</b> class libraries implement the key
    /// components of the GeoTracker platform.  <b>LillTek.GeoTracker</b> implements the client side classes
    /// as well as the types that are common across both the client and server implementations.  
    /// <b>LillTek.GeoTracker.Server</b> implements the server side behaviors and the <b>LillTek.GeoTracker.Service</b>
    /// project uses the other two class libraries to implement the GeoTracker service application.
    /// </para>
    /// <para><b><u>GeoTracker Client</u></b></para>
    /// <para>
    /// Applications use the <see cref="GeoTrackerClient" /> and related <see cref="GeoTrackerClientSettings" />
    /// classes to interact with the GeoTracking server infrastructure.  This interaction may include the submission
    /// of location fixes, geocoding and reverse geocoding, as well as location based queries.  The client class
    /// uses <b>LillTek Messaging</b> to communicate with the service infrastructure.
    /// </para>
    /// <para><b><u>GeoTracker Server</u></b></para>
    /// <para>
    /// The <b>LillTek.GeoTracker.Server</b> class library provides the <see cref="GeoTrackerNode" />,
    /// <see cref="GeoTrackerServerSettings" />, and other related classes that implement the essential
    /// server side functionality of of the GeoTracker platform.  <see cref="GeoTrackerNode" /> implements
    /// most of the server behavior by handling operations submitted by <see cref="GeoTrackerClient" />
    /// instances.  <see cref="GeoTrackerNode" /> also provides a pluggable model that allows applications
    /// to customize certain activities such how geocoding or reverse geocoding operations are resolved
    /// or how and where location fixes are archived.
    /// </para>
    /// <para>
    /// The <b>LillTek.GeoTracker.Service</b> is really just a thin wrapper over the <see cref="GeoTrackerNode" />
    /// class creating an executable that can be deployed as a native Windows service or as an interactive 
    /// application.  LillTek Platform users can also fairly easily create other custom server applications
    /// by including a <see cref="GeoTrackerNode" /> instance.
    /// </para>
    /// <para><b><u>Entities, Groups, and IDs</u></b></para>
    /// <para>
    /// GeoTracker is designed to track and act on the changes to the physical location of a collection of entities.
    /// These entities could be devices, people, or something else.  GeoTracker identifies specific entities using
    /// globally unique application-defined strings.  These strings may represent a device serial number, a unique
    /// user ID, a <see cref="Guid" />, or some other unique application defined value.  GeoTracker also organizes 
    /// entities into groups where each group has a unique application defined name or ID.
    /// </para>
    /// <note>
    /// It is possible for a single entity to belong to multiple groups.
    /// </note>
    /// <note>
    /// Both entity and group IDs are <b>case sensitive</b>.
    /// </note>
    /// <para><b><u>Server Clustering</u></b></para>
    /// <para>
    /// The GeoTracker server uses a pluggable <see cref="ITopologyProvider" /> implementation to implement
    /// scaleout and failover clusters of GeoTracker servers.  The topology must implement the <see cref="TopologyCapability.Locality" />
    /// capability so that traffic and information related to a particular entity will be routed to a specific
    /// server rather than distributed randomly across the cluster.  GeoTracker will use the entity ID as the
    /// topology key for server instance targeting purposes as well as for implementing queries that manage 
    /// or return information for specific entities.  
    /// </para>
    /// <para>
    /// The topology implementation and endpoint is specified by the <see cref="GeoTrackerServerSettings" />
    /// instance passed to the <see cref="GeoTrackerNode" /> when it is started.  <see cref="GeoTrackerServerSettings.ClusterEP" />
    /// defines the cluster endpoint and <see cref="GeoTrackerServerSettings.ClusterTopology" /> defines the
    /// pluggable <see cref="ITopologyProvider" /> implementation.
    /// </para>
    /// <para>
    /// All external messaging traffic to a GeoTracker cluster will be directed at the server endpoint
    /// specified by <see cref="GeoTrackerServerSettings" />.<see cref="GeoTrackerServerSettings.ServerEP" /> using
    /// standard LillTek Messaging <see cref="MsgRouter.Send(Msg)" /> and <see cref="MsgRouter.Query(MsgEP,Msg)" />
    /// method calls.  This provides for built-in load balancing and fail-over of this traffic across the
    /// cluster.  GeoTracker server instances implement message handlers for the cluster endpoint that will
    /// implement the operations directly or forward the traffic to cluster server instances using the topology 
    /// provider as required.  This means that <see cref="GeoTrackerClient" /> class does not need to have any
    /// knowledge of the cluster topology.
    /// </para>
    /// <para>
    /// Exactly how operations are performed on the cluster depend on the operation and also whether the
    /// <see cref="ITopologyProvider" /> supports a dynamically varying number of servers.  GeoTracker servers
    /// will examine the provider's <see cref="ITopologyProvider.Capabilities" /> property for the
    /// <see cref="TopologyCapability.Dynamic" /> flag to determine whether this is the case.
    /// </para>
    /// <para>
    /// Static cluster topologies have a fixed number of participating GeoTracker servers and an entity's state
    /// will be persisted to a particular server by hashing the entity ID.  If a server fails, the state for
    /// all entities hashed to that server will be unavailable until the server or a replacement is brought
    /// back online.  Additional servers cannot be added to the cluster without stopping and restarting the
    /// entire cluster.
    /// </para>
    /// <para>
    /// The nice thing about static cluster is that there is no question about where an entity's state is
    /// to be located; the hash identifies a specific server endpoint.  GeoTracker will target any <see cref="GeoFix" />
    /// update operations as well as any entity specific queries to that specific server.  For group related
    /// queries, GeoTracker will need to perform parallel queries against the cluster and consolidate the
    /// results received since entities belonging to a group will likely be distributed across multiple servers.
    /// </para>
    /// <para>
    /// Dynamic cluster topologies are a little different.  They allow the number of cluster servers to increase
    /// or decrease dynamically.  Entity state is still hashed to servers, but it is very likely that after the
    /// size of the cluster changes that any given entity's state will be hashed to a different server.  The advantage
    /// of dynamic clusters is that the number of servers can be easily varied to account for changes in load, the
    /// disadvantage is that parallel queries need to be performed.
    /// </para>
    /// <note>
    /// <b><font color="red">$todo(jeff.lill):</font></b> It could be possible to avoid performing parallel
    /// queries all of the time, by extending the <see cref="ITopologyProvider" /> implementations to track
    /// when the last topology change occured and perform parallel queries only if the change occurred
    /// more recently than the configured <see cref="GeoTrackerServerSettings.GeoFixRetentionInterval" />.
    /// </note>
    /// <para>
    /// GeoTracker servers in a dynamic cluster still target <see cref="GeoFix" /> update operations at the 
    /// server by the hashing the entity ID but it is possible for a given entity's state to be located on
    /// more than one server if the number of cluster servers has changed recently.  So entity specific queries
    /// will be performed in parallel against all servers.  Group related queries will also be performed in
    /// parallel.
    /// </para>
    /// <para><b><u>Geocoding and Reverse Geocoding</u></b></para>
    /// <para>
    /// GeoTracker uses the GeoIP City or GeoLite City databases from <a href="http://maxmind.com">MaxMind.com</a>
    /// and an open source API for resolving an IP address <see cref="GeoFix" />.  These databases are updated
    /// the first day of the month by MaxMind and will need to be manually downloaded, decompressed, and encrypted to
    /// an accessible web server.  GeoServer instances can be configured to periodically poll for changes to this
    /// file so that it can be downloaded and stored locally.  GeoTracker uses the HTTP <b>If-Modified-Since</b> header 
    /// to perform this polling efficently and only download the multi-megabyte file when it's actually necessary.
    /// </para>
    /// <para>
    /// The local database file will be saved in the <b>CommonApplicationData\LillTek\GeoTracker</b> folder.  The current
    /// database file will be named <b>IP2City.dat</b> and the temporary database file will be named <b>IP2City.download.dat</b>
    /// and <b>IP2City.decrypted.dat</b> while it is being downloaded, decrypted, and verified.  The create and modify dates 
    /// of the database file will be set to the <b>Last-Modified</b> date returned when the file is downloaded.  This date 
    /// will be used as the value passed in the <b>If-Modified-Since</b> header for future HTTP polling requests.
    /// </para>
    /// <para>
    /// Applications will use <see cref="GeoTrackerClient" /> to perform IP to <see cref="GeoFix" /> lookup
    /// operations using the synchronous <see cref="GeoTrackerClient.IPToGeoFix" /> method or the asynchronous 
    /// <see cref="GeoTrackerClient.BeginIPToGeoFix" /> and <see cref="GeoTrackerClient.EndIPToGeoFix" /> methods.
    /// </para>
    /// <note>
    /// Reverse geocoding is not implemented at this time.
    /// </note>
    /// <para><b><u>Location Archival</u></b></para>
    /// <para>
    /// The main purpose of the GeoTracker platform is to gather streams of location fixes for entities,
    /// caching the last few fixes locally to perform real-time queries as well as to persistently
    /// archive the fixes for future analysis.  Applications will use <see cref="GeoTrackerClient" />
    /// to submit streams of fixes using the synchronous <see cref="GeoTrackerClient.SubmitEntityFixes" /> method or
    /// the asynchronous <see cref="GeoTrackerClient.BeginSubmitEntityFixes" /> and <see cref="GeoTrackerClient.EndSubmitEntityFixes" /> methods
    /// with the application passing the current <see cref="GeoFix" /> as well as the entity and group
    /// IDs.
    /// </para>
    /// <para>
    /// GeoTracker servers will retain a few recent <see cref="GeoFix" />es in  memory for each entity being
    /// tracked.  This is controlled by the <see cref="GeoTrackerServerSettings" />.<see cref="GeoTrackerServerSettings.GeoFixRetentionInterval" />
    /// property.  Location related queries submitted to the cluster will operate on these fixes.  GeoTracker
    /// servers will periodically discard fixes that have been cached longer than the retention interval.
    /// </para>
    /// <para>
    /// <see cref="GeoFix" />es received by a server may also be archived to persistent storage using a
    /// provider model defined by the <see cref="IGeoFixArchiver" /> interface.  This interface requires
    /// the implementation of threee methods: <see cref="IGeoFixArchiver.Start" />, <see cref="IGeoFixArchiver.Archive" />,
    /// and <see cref="IGeoFixArchiver.Stop" />.  The <see cref="GeoTrackerServerSettings" />.<see cref="GeoTrackerServerSettings.GeoFixArchiver" />
    /// property determines which archiver plug-in the GeoTracker server will load and use.
    /// </para>
    /// <para>
    /// The GeoTracker platform provides three archiver implementations:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="NullGeoFixArchiver" /></term>
    ///         <description>
    ///         This plug-in does nothing and is used for situations where persitent archival of
    ///         <see cref="GeoFix" /> streams is not necessary.  This is the default setting.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="AppLogGeoFixArchiver" /></term>
    ///         <description>
    ///         Archives fixes to a LillTek <see cref="AppLog" />.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="SqlGeoFixArchiver" /></term>
    ///         <description>
    ///         Archives fixes to a SQL Server database using a configurable SQL script.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para><b><u>Location Queries</u></b></para>
    /// <para>
    /// The GeoTracker platform supports location based queries.  These queries filter the
    /// collection of entities being tracked by the cluster by their current location and
    /// also optionally by group.  GeoTracker supports the following location filters:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="GeoCircle" /></term>
    ///         <description>
    ///         Returns the entities within the specified distance of a
    ///         point on the globe.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="GeoRectangle" /></term>
    ///         <description>
    ///         Returns the entities within a rectangle.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="GeoPolygon" /></term>
    ///         <description>
    ///         Returns the entities within an arbitrary polygon.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Composition of Shapes</term>
    ///         <description>
    ///         A query can also return the entities within the union of a set
    ///         of circles, rectangles, or polygons.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para><b><u>Geofencing</u></b></para>
    /// <para>
    /// <b><font color="red">$todo(jeff.lill):</font></b> Not implemented at this time.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

