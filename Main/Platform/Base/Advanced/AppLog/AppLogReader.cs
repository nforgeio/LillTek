//-----------------------------------------------------------------------------
// FILE:        AppLogReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an application log reader.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements an application log reader.  This is a thin wrapper of the
    /// <see cref="AppLog" /> class that specializes in reading/processing log records.
    /// </summary>
    /// <threadsafety instance="true" />
    public class AppLogReader : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Opens a log reader for the named application log.  The log position will
        /// be restored to the record position at the time a log reader was last closed.
        /// </summary>
        /// <param name="logName">The application log name.</param>
        /// <returns>The application log instance.</returns>
        /// <remarks>
        /// <note>
        /// Every log opened successfully with this method should
        /// be closed or disposed to release any underlying resources.
        /// </note>
        /// </remarks>
        /// <exception cref="LogLockedException">Thrown when another reader has already opened the log.</exception>
        public static AppLogReader Open(string logName)
        {
            return new AppLogReader(logName);
        }

        //---------------------------------------------------------------------
        // Instance members

        private AppLog appLog;

        /// <summary>
        /// This event is raised after <see cref="Read" /> or <see cref="ReadDelete" /> 
        /// has returned null and subsequently, an additional set of one or more records
        /// have been made available to be read.
        /// </summary>
        public event LogRecordAvailableHandler RecordAvailable;

        /// <summary>
        /// Constructs and opens an application log reader.
        /// </summary>
        /// <param name="logName">The log name.</param>
        private AppLogReader(string logName)
        {
            appLog = AppLog.OpenReader(logName);
            appLog.RecordAvailable += new LogRecordAvailableHandler(OnRecordAvailable);
        }

        /// <summary>
        /// Closes the log reader, releasing any resources held.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that the record position will be persisted when the reader is closed
        /// and restored the next time the log is opened for reading.
        /// </para>
        /// <para>
        /// Every log reader successfully opened should be closed by calling this method
        /// or <see cref="Dispose" />.  Note that it is not an error to close a
        /// log that has already been closed.
        /// </para>
        /// </remarks>
        public void Close()
        {
            appLog.Close();
        }

        /// <summary>
        /// Closes the log reader, releasing any resources held.  This is equivalent to
        /// to calling <see cref="Close" />.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Removes all commited entries in the log.
        /// </summary>
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        public void Clear()
        {
            appLog.Clear();
        }

        /// <summary>
        /// Called when the underlying applog's RecordAvailable event is raised.
        /// This simply raises this instance's RecordAvailable event which will
        /// communicate the new state to the application.
        /// </summary>
        private void OnRecordAvailable()
        {
            if (RecordAvailable != null)
                RecordAvailable();
        }

        /// <summary>
        /// Returns the next committed record in the application log if there is one,
        /// null otherwise.  This method does not advance the log's record position.
        /// </summary>
        /// <returns>The next log record or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// Note that this method returns only committed log records.  Records written
        /// by the writer logs will be cached for some period of time before being
        /// commited to the log (for efficiency).  Cached records will not be returned
        /// by this method.
        /// </para>
        /// <para>
        /// After this method indicates that it has run out of committed records to be read 
        /// (by returning null), applications should monitor the <see cref="AppLog.RecordAvailable" />
        /// event. This event will be raised when one or more committed events become available to
        /// be read.
        /// </para>
        /// </remarks>
        public AppLogRecord Peek()
        {
            return appLog.Peek();
        }

        /// <summary>
        /// Returns the next committed record in the application log if there is one,
        /// null otherwise.
        /// </summary>
        /// <returns>The next log record or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// Note that this method returns only committed log records.  Records written
        /// by the writer logs will be cached for some period of time before being
        /// commited to the log (for efficiency).  Cached records will not be returned
        /// by this method.
        /// </para>
        /// <para>
        /// After this method indicates that it has run out of committed records to be read 
        /// (by returning null), applications should monitor the <see cref="AppLog.RecordAvailable" />
        /// event. This event will be raised when one or more committed events become available to
        /// be read.
        /// </para>
        /// </remarks>
        public AppLogRecord Read()
        {
            return appLog.Read();
        }

        /// <summary>
        /// Returns the next committed record in the application log and then marks
        /// if for deletion if there is one.  The method returns null if there
        /// are no records remaining to be read.
        /// </summary>
        /// <returns>The next log record or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// Records marked for deletion will be skipped the next time 
        /// a log reader processes the log and if all of the records in
        /// a given log file are deleted, then the log file itself will
        /// be deleted.
        /// </para>
        /// <para>
        /// Note that this method returns only committed log records.  Records written
        /// by the writer logs will be cached for some period of time before being
        /// commited to the log (for efficiency).  Cached records will not be returned
        /// by this method.
        /// </para>
        /// <para>
        /// After this method indicates that it has run out of committed records to be read 
        /// (by returning null), applications should monitor the <see cref="AppLog.RecordAvailable" />
        /// event.  This event will be raised when one or more committed events become available to
        /// be read.
        /// </para>
        /// </remarks>
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        public AppLogRecord ReadDelete()
        {
            return appLog.ReadDelete();
        }

        /// <summary>
        /// Specifies the position within the application log of the next record to be
        /// read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The main design goal behind this property is to provide a way for log
        /// reading applications to be able to stop processing the log and store
        /// the current position persistently somewhere and the be able to come\
        /// back later continue where it left off.
        /// </para>
        /// <para>
        /// This may be set to "BEGINNING" (case insenstive) to position the
        /// log reader at the beginning of the set of commited records.  Record
        /// positions between the beginning and end of the set will be specified by 
        /// a string generated by the class.  This string should be considered to be
        /// opaque by the calling application.
        /// </para>
        /// </remarks>
        public string Position
        {
            get { return appLog.Position; }
            set { appLog.Position = value; }
        }
    }
}
