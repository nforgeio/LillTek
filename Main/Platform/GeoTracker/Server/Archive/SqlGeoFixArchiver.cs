//-----------------------------------------------------------------------------
// FILE:        SqlGeoFixArchiver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements an IGeoFixArchiver that persists to a SQL Server database.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Data;
using LillTek.Messaging;

// $todo(jeff.lill):
//
// It might be interesting to add the ability to persist fixes to disk via an
// AppLog to be able to locally buffer fixes during network connectivity issues
// or across a service restart.

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Implements an <see cref="IGeoFixArchiver" /> that persists to a SQL Server database.
    /// See the <see cref="Start" /> method for detailed information on configuring the
    /// archiver.
    /// </summary>
    public class SqlGeoFixArchiver : IGeoFixArchiver
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to hold the information about a fix to be written.
        /// </summary>
        private struct FixRecord
        {
            public readonly string EntityID;
            public readonly string GroupID;
            public readonly GeoFix Fix;

            public FixRecord(string entityID, string groupID, GeoFix fix)
            {
                this.EntityID = entityID;
                this.GroupID  = groupID;
                this.Fix      = fix;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object                      syncLock = new object();
        private GeoTrackerNode              node;               // The GeoTracker
        private GeoTrackerServerSettings    settings;           // GeoTracker settings
        private bool                        isRunning;          // True if the archiver is running
        private bool                        isStopped;          // True if the archive was started and then stopped
        private bool                        stopPending;        // True to signal to flush thread to stop
        private List<FixRecord>             bufferedFixes;      // Fixes waiting to be persisted
        private Thread                      flushThread;        // Background flush thread
        private PolledTimer                 flushTimer;         // Fired when its time to flush buffered fixes
        private string                      conString;          // Database connection string
        private int                         bufferSize;         // Buffered fix count threshold for flushing before timer
        private TimeSpan                    bufferInterval;     // Buffer flush interval
        private string                      addScript;          // SQL template

        //---------------------------------------------------------------------
        // Implementation Note:
        //
        // I'm implementing this by buffering the received fixes in memory and then
        // using a periodic background timer to persist the fixes to SQL.

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlGeoFixArchiver()
        {
            this.isRunning = false;
            this.isStopped = false;
        }

        /// <summary>
        /// Initalizes the fix archiver instance.
        /// </summary>
        /// <param name="node">The parent <see cref="GeoTrackerNode" /> instance.</param>
        /// <param name="args">This implementation recognizes special arguments (see the remarks).</param>
        /// <remarks>
        /// <para>
        /// This archiver recognizes the following arguments:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term><b>ConnectionString</b></term>
        ///         <description>
        ///         <b>Required:</b> This specifies the SQL connection string the archiver will use
        ///         to connect to the database server.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><b>BufferSize</b></term>
        ///         <description>
        ///         <b>Optional:</b> This specifies the number of fixes the archiver will buffer before
        ///         submitting the fixes to the database in a batch.  (defaults to <b>1000</b>).
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><b>BufferInterval</b></term>
        ///         <description>
        ///         <b>Optional: </b> The maximum length of time the archiver will buffer fixes before
        ///         flushing to the database, regardless of how full thbe buffer is.  (defaults to <b>5 minutes</b>).
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><b>AddScript</b></term>
        ///         <description>
        ///         <para>
        ///         The SQL script template the archiver will use for submitting fixes to the database.
        ///         This script will include macros where the archiver will substitute the values
        ///         from the <see cref="GeoFix" /> being persisted.  Here are the supported macros:
        ///         </para>
        ///         <list type="table">
        ///             <item>
        ///                 <term><b>@(TimeUtc)</b></term>
        ///                 <description>
        ///                 The time (UTC) the fix was taken.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(EntityID)</b></term>
        ///                 <description>
        ///                 The ID of the entity being tracked.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(GroupID)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The ID of the group the entity belongs to.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Technology)</b></term>
        ///                 <description>
        ///                 The technology used to obtain the position.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Latitude)</b></term>
        ///                 <description>
        ///                 The latitude.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Longitude)</b></term>
        ///                 <description>
        ///                 The longitude.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Altitude)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The altitude in meters.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Course)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The direction of travel in degrees from true north.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(Speed)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The speed in kilometers per hour.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(HorizontalAccuracy)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The estimated horizontal accuracy of the fix in meters.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(VerticalAccurancy)</b></term>
        ///                 <description>
        ///                 <b>Nullable:</b> The estimated vertical accuracy of the fix in meters.
        ///                 </description>
        ///             </item>
        ///             <item>
        ///                 <term><b>@(NetworkStatus)</b></term>
        ///                 <description>
        ///                 Identifies how or if the entity was connected to the Internet when the fix was taken.
        ///                 </description>
        ///             </item>
        ///         </list>
        ///         <note>
        ///         We're using the <b>"@"</b> rather than the usual <b>"$"</b> character to mark the
        ///         macro name to avoid conflicting with environment variable expansions performed
        ///         when loading the application configuration.
        ///         </note>
        ///         </description>
        ///     </item>
        /// </list>
        /// <para>
        /// The most important archiver parameter is <b>AddScript</b>.  This is the template the archiver
        /// will use to generate the SQL statements that will add <see cref="GeoFix" />es to the database.
        /// The script can be as simple as an ad-hoc table <b>insert</b> or a call to a stored procedure.
        /// Add one or more of the macro identifiers listed above to the script.  The archiver will replace
        /// these macros with actual fields from the <see cref="GeoFix" />, quoting them as necessary.
        /// Null <see cref="GeoFix" /> fields or numeric fields set to <see cref="double.NaN" /> will
        /// be generated as <c>null</c>.  See the <b>Nullable</b> tags in the macro definitions above.
        /// </para>
        /// <para>
        /// Here's an exmple of a simple sample script that will insert the <see cref="GeoFix.TimeUtc" />,
        /// <b>entity ID</b>, <see cref="GeoFix.Latitude" />, and <see cref="GeoFix.Longitude" />
        /// <see cref="GeoFix" /> fields into a table.
        /// </para>
        /// <code language="none">
        /// insert into FixArchive(Time,Entity,Lat,Lon) values (@(TimeUtc),@(EntityID),@(Latitude),@(longitude))
        /// </code>
        /// <note>
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
                    if (isRunning)
                        throw new InvalidProgramException("SqlGeoFixArchiver: Archiver has already been started.");

                    if (isStopped)
                        throw new InvalidOperationException("SqlGeoFixArchiver: Cannot restart a stopped archiver.");

                    this.node           = node;
                    this.settings       = node.Settings;
                    this.bufferedFixes  = new List<FixRecord>();
                    this.conString      = args.Get("ConnectionString", string.Empty);
                    this.bufferSize     = args.Get("BufferSize", 1000);
                    this.bufferInterval = args.Get("BufferInterval", TimeSpan.FromMinutes(5));
                    this.addScript      = args.Get("AddScript", string.Empty).Replace("@(", "$(");

                    if (string.IsNullOrWhiteSpace(this.conString))
                        throw new ArgumentException("SqlGeoFixArchiver: The [ConnectionString] argument is required.");

                    if (string.IsNullOrWhiteSpace(this.addScript))
                        throw new ArgumentException("SqlGeoFixArchiver: The [AddScript] argument is required.");

                    this.isRunning   = true;
                    this.flushTimer  = new PolledTimer(bufferInterval, false);
                    this.flushThread = new Thread(new ThreadStart(FlushThread));
                    this.flushThread.Start();
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
                if (!isRunning)
                    return;

                bufferedFixes.Add(new FixRecord(entityID, groupID, fix));
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
                if (!isRunning || stopPending)
                    return;

                // Signal the background thread to stop and give it 30 seconds to stop cleanly
                // before aborting it.

                stopPending = true;
            }

            try
            {
                flushThread.Join(TimeSpan.FromSeconds(30));
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                flushThread.Abort();
            }
            finally
            {
                isRunning = false;
                isStopped = true;
            }
        }

        /// <summary>
        /// Converts the value passed into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value object.</param>
        /// <returns>The literal string.</returns>
        private static string ToSqlLiteral(object value)
        {
            if (value == null)
                return "null";
            else
                return ToSqlLiteral(value.ToString());
        }

        /// <summary>
        /// Converts the value passed into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value object.</param>
        /// <returns>The literal string.</returns>
        private static string ToSqlLiteral(double value)
        {
            if (double.IsNaN(value))
                return "null";
            else
                return value.ToString();
        }

        /// <summary>
        /// Converts the value passed into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value object.</param>
        /// <returns>The literal string.</returns>
        private static string ToSqlLiteral(string value)
        {
            if (value == null)
                return "null";
            else
                return SqlHelper.Literal(value);
        }

        /// <summary>
        /// Converts the value passed into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value object.</param>
        /// <returns>The literal string.</returns>
        private static string ToSqlLiteral(DateTime value)
        {
            return SqlHelper.Literal(value);
        }

        /// <summary>
        /// Converts the value passed into a T-SQL literal.
        /// </summary>
        /// <param name="value">The value object.</param>
        /// <returns>The literal string.</returns>
        private static string ToSqlLiteral(DateTime? value)
        {
            if (!value.HasValue)
                return "null";
            else
                return SqlHelper.Literal(value.Value);
        }

        /// <summary>
        /// Implements the background thread responsible for persisting buffered <see cref="GeoFix "/>es .
        /// </summary>
        private void FlushThread()
        {
            bool            stopCompleted = false;
            List<FixRecord> writeFixes;

            while (true)
            {
                writeFixes = null;

                try
                {
                    lock (syncLock)
                    {
                        if (!isRunning)
                            return;

                        if (stopPending || bufferedFixes.Count >= bufferSize || flushTimer.HasFired)
                        {
                            writeFixes = bufferedFixes;
                            bufferedFixes = new List<FixRecord>(writeFixes.Count);
                        }
                    }

                    if (writeFixes != null && writeFixes.Count > 0)
                    {
                        // Build SQL batch command that to add all of the buffered fixes using
                        // the SQL script template to generate the T-SQL statements for each fix.

                        var processor = new MacroProcessor();
                        var sqlBatch  = new StringBuilder(8192);

                        foreach (var record in writeFixes)
                        {
                            if (record.EntityID == null)
                                SysLog.LogWarning("SqlGeoFixArchiver: GeoFix has a [EntityID=NULL] field and will be ignored.");

                            if (!record.Fix.TimeUtc.HasValue)
                                SysLog.LogWarning("SqlGeoFixArchiver: GeoFix has a [TimeUtc=NULL] field and will be ignored.");

                            if (double.IsNaN(record.Fix.Latitude))
                                SysLog.LogWarning("SqlGeoFixArchiver: GeoFix has a [Latitude=NaN] field and will be ignored.");

                            if (double.IsNaN(record.Fix.Longitude))
                                SysLog.LogWarning("SqlGeoFixArchiver: GeoFix has a [Longitude=NaN] field and will be ignored.");

                            processor["EntityID"]           = ToSqlLiteral(record.EntityID);
                            processor["GroupID"]            = ToSqlLiteral(record.GroupID);
                            processor["TimeUtc"]            = ToSqlLiteral(record.Fix.TimeUtc);
                            processor["Technology"]         = ToSqlLiteral(record.Fix.Technology);
                            processor["Latitude"]           = ToSqlLiteral(record.Fix.Latitude);
                            processor["Longitude"]          = ToSqlLiteral(record.Fix.Longitude);
                            processor["Altitude"]           = ToSqlLiteral(record.Fix.Altitude);
                            processor["Course"]             = ToSqlLiteral(record.Fix.Course);
                            processor["Speed"]              = ToSqlLiteral(record.Fix.Speed);
                            processor["HorizontalAccuracy"] = ToSqlLiteral(record.Fix.HorizontalAccuracy);
                            processor["VerticalAccurancy"]  = ToSqlLiteral(record.Fix.VerticalAccurancy);
                            processor["NetworkStatus"]      = ToSqlLiteral(record.Fix.NetworkStatus);

                            sqlBatch.AppendLine(processor.Expand(addScript));
                        }

                        // Submit the T-SQL batch to the server.

                        var sqlCtx = new SqlContext(conString);

                        sqlCtx.Open();
                        try
                        {
                            sqlCtx.Execute(sqlBatch.ToString());
                            node.IncrementFixesReceivedBy(writeFixes.Count);
                        }
                        catch (SqlException e)
                        {
                            const string msgTemplate =
@"SQL Error [Line {1}]: {0}

{2}
";
                            SysLog.LogException(e);
                            SysLog.LogError(msgTemplate, e.Message, e.LineNumber, sqlBatch);
                        }
                        finally
                        {
                            sqlCtx.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
                finally
                {
                    if (writeFixes != null)
                    {
                        writeFixes = null;
                        flushTimer.Reset();
                    }
                }

                if (stopCompleted)
                    return;

                if (stopPending)
                {
                    // Loop one more time to archive any fixes cached while we were
                    // persisting to the database.

                    stopCompleted = true;
                    continue;
                }

                Thread.Sleep(settings.BkInterval);
            }
        }
    }
}
