//-----------------------------------------------------------------------------
// FILE:        AppLogGeoFixArchiver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a IGeoFixArchiver that persists fixes to an AppLog.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Implements a <see cref="IGeoFixArchiver" /> that persists fixes to an <see cref="AppLog" />.
    /// See the <see cref="Start" /> method for detailed information on configuring the
    /// archiver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This archiver persists to <see cref="GeoFix" />es to self-describing file based <see cref="AppLog" />.
    /// </para>
    /// <note>
    /// Note that the application log settings are loaded directly from the application's <b>LillTek.AppLog</b> 
    /// configuration settings section as described in <see cref="AppLog" />.  See the <see cref="Start" /> method 
    /// for detailed information on configuring the archiver.
    /// </note>
    /// </remarks>
    public class AppLogGeoFixArchiver : IGeoFixArchiver
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The AppLog schema name.
        /// </summary>
        public const string SchemaName = "GeoTrackerArchive";

        /// <summary>
        /// The AppLog schema version.
        /// </summary>
        public static readonly Version SchemaVersion = new Version("1.0.0000.0");

        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock = new object();
        private GeoTrackerNode  node;
        private AppLogWriter    logWriter;
        private bool            isStopped;
        private string          logName;
        private long            maxSize;
        private TimeSpan        purgeInterval;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppLogGeoFixArchiver()
        {
        }

        /// <summary>
        /// Initalizes the fix archiver instance.
        /// </summary>
        /// <param name="node">The parent <see cref="GeoTrackerNode" /> instance.</param>
        /// <param name="args">
        /// This implementation's settings come directly from the application
        /// configuration as described below.
        /// </param>
        /// <remarks>
        /// <note>
        /// <para>
        /// Note that some of the application log settings are loaded directly from the application's <b>LillTek.AppLog</b> 
        /// configuration settings section as described in <see cref="AppLog" />.  The valid arguments passed to the
        /// <see cref="Start" /> are:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term><b>LogName</b></term>
        ///         <description>
        ///         The name of the application log.  This defaults to <b>GeoTrackerFixes</b>.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><b>MaxSize</b></term>
        ///         <description>
        ///         The approximate maximum size of the log on disk or zero for no limit.
        ///         This defaults to <b>5GB</b>.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><b>PurgeInterval</b></term>
        ///         <description>
        ///         The interval at which the log will purge old log files so that the
        ///         total log size remains within the <b>MaxSize</b> limit.
        ///         This defaults to <b>5 minutes</b>.
        ///         </description>
        ///     </item>
        /// </list>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        public void Start(GeoTrackerNode node, ArgCollection args)
        {
            lock (syncLock)
            {
                try
                {
                    if (this.logWriter != null)
                        throw new InvalidOperationException("AppLogGeoFixArchiver: Archiver has already been started.");

                    if (this.isStopped)
                        throw new InvalidOperationException("AppLogGeoFixArchiver: Cannot restart a stopped archiver.");

                    this.node                    = node;
                    this.logName                 = args.Get("LogName", "GeoTrackerFixes");
                    this.maxSize                 = args.Get("MaxSize", 5L * 1024L * 1024L * 1024L);
                    this.purgeInterval           = args.Get("PurgeInterval", TimeSpan.FromMinutes(5));
                    this.logWriter               = new AppLogWriter(logName, SchemaName, SchemaVersion, maxSize);
                    this.logWriter.PurgeInterval = purgeInterval;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Archives a location fix for an entity.
        /// </summary>
        /// <param name="entityID">The entity identifier.</param>
        /// <param name="groupID">The group identifier or <c>null</c>.</param>
        /// <param name="fix">The location fix.</param>
        /// <remarks>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        public void Archive(string entityID, string groupID, GeoFix fix)
        {
            lock (syncLock)
            {
                try
                {
                    if (isStopped)
                        throw new InvalidOperationException("AppLogGeoFixArchiver: Cannot log to a stopped archiver.");

                    if (logWriter == null)
                    {
                        logWriter = new AppLogWriter(logName, SchemaName, SchemaVersion, maxSize);
                        logWriter.PurgeInterval = purgeInterval;
                    }

                    var record = new AppLogRecord();

                    if (entityID != null)
                        record["EntityID"] = entityID;

                    if (groupID != null)
                        record["GroupID"] = groupID;

                    if (fix.TimeUtc.HasValue)
                        record["TimeUtc"] = fix.TimeUtc;

                    record["Technology"] = fix.Technology;

                    if (!double.IsNaN(fix.Latitude))
                        record["Latitude"] = fix.Latitude;

                    if (!double.IsNaN(fix.Longitude))
                        record["Longitude"] = fix.Longitude;

                    if (!double.IsNaN(fix.Altitude))
                        record["Altitude"] = fix.Altitude;

                    if (!double.IsNaN(fix.Course))
                        record["Course"] = fix.Course;

                    if (!double.IsNaN(fix.Speed))
                        record["Speed"] = fix.Speed;

                    if (!double.IsNaN(fix.HorizontalAccuracy))
                        record["HorizontalAccuracy"] = fix.HorizontalAccuracy;

                    if (!double.IsNaN(fix.VerticalAccurancy))
                        record["VerticalAccurancy"] = fix.VerticalAccurancy;

                    record["NetworkStatus"] = fix.NetworkStatus;

                    logWriter.Write(record);
                    node.IncrementFixesReceivedBy(1);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Performs any necessary shut down activites (flushing cached fixes, etc).
        /// </summary>
        /// <remarks>
        /// <note>
        /// <see cref="IGeoFixArchiver" /> implementations must silently handle any internal
        /// error conditions.  <see cref="GeoTrackerNode" /> does not expect to see any
        /// exceptions raised from calls to any of these methods.  Implementations should
        /// catch any exceptions thrown internally and log errors or warnings as necessary.
        /// </note>
        /// </remarks>
        public void Stop()
        {
            lock (syncLock)
            {
                try
                {
                    isStopped = true;

                    if (logWriter != null)
                    {

                        logWriter.Close();
                        logWriter = null;
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }
    }
}
