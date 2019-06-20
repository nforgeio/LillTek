//-----------------------------------------------------------------------------
// FILE:        AppLogWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an application log writer.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements an application log writer.  This is a thin wrapper of the
    /// <see cref="AppLog" /> class that specializes in writing log records.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <threadsafety instance="true" />
    public class AppLogWriter : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Opens a log writer for the named application log.
        /// </summary>
        /// <param name="logName">The application log name.</param>
        /// <param name="schemaName">Names the format of the data being written.</param>
        /// <param name="schemaVersion">Indicates the version of the data format.</param>
        /// <param name="maxLogSize">Approximate maximum total log size in bytes or 0 for unlimited (ignored for reading).</param>
        /// <returns>The application log instance.</returns>
        /// <remarks>
        /// <para>
        /// The formatName and formatVersion parameters are used to identify the 
        /// formatting of the records that will be written to the log.  Each record
        /// written will include this information.  This makes it possible for
        /// log files to be archived long term across format changes and yet
        /// provide a standard mechanism for future log readers to determine the
        /// particular record format and handle them appropriately.
        /// </para>
        /// <para>
        /// <remarks>
        /// <note>
        /// Every log opened successfully with this method should
        /// be closed or disposed to release any underlying resources.
        /// </note>
        /// </remarks>
        /// </para>
        /// </remarks>
        public static AppLogWriter Open(string logName, string schemaName, Version schemaVersion, long maxLogSize)
        {
            return new AppLogWriter(logName, schemaName, schemaVersion, maxLogSize);
        }

        //---------------------------------------------------------------------
        // Instance members

        private AppLog appLog;

        /// <summary>
        /// Constructs and opens an application log writer.
        /// </summary>
        /// <param name="logName">The log name.</param>
        /// <param name="schemaName">The application record schema name if opening the log for writing (null for reading).</param>
        /// <param name="schemaVersion">The application record schema version if opening the log for writing (null for reading).</param>
        /// <param name="maxLogSize">Approximate maximum total log size in bytes or 0 for unlimited (ignored for reading).</param>
        public AppLogWriter(string logName, string schemaName, Version schemaVersion, long maxLogSize)
        {
            appLog = AppLog.OpenWriter(logName, schemaName, schemaVersion, maxLogSize);
        }

        /// <summary>
        /// Closes the log, committing any cached log entries and releasing any resources held.
        /// </summary>
        /// <remarks>
        /// Every log successfully opened should be closed by calling this method
        /// or <see cref="Dispose" />.  Note that it is not an error to close a
        /// log that has already been closed.
        /// </remarks>
        public void Close()
        {
            appLog.Close();
        }

        /// <summary>
        /// Commits any uncommited log entries to disk but keeps the log open.
        /// </summary>
        public void Commit()
        {
            appLog.Commit();
        }

        /// <summary>
        /// Closes the log, releasing any resources held.  This is equivalent to
        /// to calling <see cref="Close" />.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Specifies the approximate maximum size when an committed log file
        /// will be committed.
        /// </summary>
        /// <remarks>
        /// This property can be used to override the value specified in the
        /// confiuration file after the log has been opened.
        /// </remarks>
        public int MaxFileSize
        {
            get { return appLog.MaxFileSize; }
            set { appLog.MaxFileSize = value; }
        }

        /// <summary>
        /// Interval to wait before committing an non-empty idle log file.  
        /// </summary>
        /// <remarks>
        /// This property can be used to override the value specified in the
        /// confiuration file after the log has been opened.
        /// </remarks>
        public TimeSpan IdleCommitInterval
        {
            get { return appLog.IdleCommitInterval; }
            set { appLog.IdleCommitInterval = value; }
        }

        /// <summary>
        /// <para>
        /// The interval at which the log scans the log folder and purges files
        /// to maintain the overall size limit.
        /// </para> 
        /// <note>
        /// This property is available only for logs opened for writing.
        /// </note>
        /// </summary>
        public TimeSpan PurgeInterval
        {
            get { return appLog.PurgeInterval; }
            set { appLog.PurgeInterval = value; }
        }

        /// <summary>
        /// Appends a record to the cached section of the log.  If the cache has become
        /// large enough, then the method will commit records in the cache.
        /// </summary>
        /// <param name="record">The record to be written.</param>
        public void Write(AppLogRecord record)
        {
            appLog.Write(record);
        }
    }
}
