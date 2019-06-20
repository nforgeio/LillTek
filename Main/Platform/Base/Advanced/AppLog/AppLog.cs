//-----------------------------------------------------------------------------
// FILE:        AppLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements application specific logging.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// Implement a scheme to save log files in subdirectories so that
// we don't exceed 10K files per folder.

// $todo(jeff.lill): 
//
// Add the concept of Flush() along with a periodic flush time.
// The idea is to flush the *.new log file so that it is complete
// so that if the application crashes, the log file can be opened
// and logging can continue where we left off.

namespace LillTek.Advanced
{
    /// <summary>
    /// Delegate describing the target of an event triggered when one or more
    /// log records have been made available for reading.
    /// </summary>
    public delegate void LogRecordAvailableHandler();

    /// <summary>
    /// Implements application specific logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic design goal for the LillTek application log classes is to 
    /// provide a general purpose, self describing log implementation that 
    /// provides many of the benefits of an XML based implementation without
    /// the performance and size overheads that come with XML.
    /// </para>
    /// <para>
    /// An application log is logically an ordered sequence of <see cref="AppLogRecord" /> 
    /// records, each containing zero or more application defined name/value pairs.
    /// An application log is opened by calling one of <see cref="OpenReader" /> or
    /// <see cref="OpenWriter" /> passing the application log name.  Log records are 
    /// written by calling <see cref="Write" /> and are read by calling <see cref="Read" />.  
    /// The <see cref="Clear" /> method is used to delete all records from a log and
    /// <see cref="ReadDelete" /> is used to read and then delete a record from a log.
    /// </para>
    /// <para>
    /// Application logs are named.  This name can consist of up to 64 characters
    /// including A-Z, a-z, 0-9, the dash (-) and the underscore (_).  Log names 
    /// are case insensitve.
    /// </para>
    /// <para>
    /// The current implementation of application logs is limited to supporting
    /// a single reader and a single writer on a specific log.  I have some
    /// ideas on how to relax this restriction using shared memory to coordinate
    /// writes, but this is beyond the scope of what I need right now.
    /// </para>
    /// <para>
    /// Log readers persist the current record position when the log is closed so 
    /// that the next time a reader is opened on the log, the position will
    /// be restored.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// This class loads its settings from the <b>LillTek.AppLog</b> section of the 
    /// application configuration settings.  
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td><b>RootFolder</b></td>
    ///     <td>&lt;ProgramData&gt;\LillTek\AppLogs</td>
    ///     <td>
    ///     Fully qualified path of the root folder for all application logs.
    ///     </td>
    /// </tr>
    /// <tr valign="top"><td><b>MaxFileSize</b></td><td>128K</td><td>The approximate maximum size an uncommited file can reach before being committed.</td></tr>
    /// <tr valign="top"><td><b>BufferSize</b></td><td>128K</td><td>The size to allocate for log file buffers.</td></tr>
    /// <tr valign="top"><td><b>IdleCommitInterval</b></td><td>5m</td><td>Interval to wait before committing an non-empty idle log file.</td></tr>
    /// <tr valign="top"><td><b>PurgeInterval</b></td><td>5m</td><td>Interval at which log writers prune log files to limit the overall log size.</td></tr>
    /// </table>
    /// </div>
    /// <para><b><u>Implementation Notes</u></b></para>
    /// <para>
    /// Application logs are implemented as a set of files located in a folder
    /// on the local file system.  This file system folder has the same name
    /// as the application log.  By default, this folder will be located within
    /// a global folder created and maintained by the AppLog class.  Other application
    /// logs will also be located in this folder, so some care should be taken to
    /// ensure that application log names are unique across applications.
    /// </para>
    /// <para>
    /// Each application log is composed of a number of log files, with each
    /// file being managed internally by a <see cref="AppLogFile" /> instance.
    /// Log file names specify the date and time (UTC) when the file was
    /// created:
    /// </para>
    /// <code language="none">
    ///     2006-08-11T13-12-56-0201.*   (where *="log" or "new")
    /// </code>
    /// <para>
    /// Where the date time values are ordered as year, month, day, hour
    /// minute, second, and milliseconds.  The file names are generated
    /// like this so that a simple string sort of the file names will
    /// sort them by create date as well.
    /// </para>
    /// <para>
    /// The other files are created in the log folder: <b>Reader.lock</b>,
    /// <b>Writer.lock</b>, and <b>Reader.pos</b>.  The two lock files files 
    /// are used to ensure exclusive access to the log by a single reader and 
    /// a single writer and the last file is used to store the current position
    /// when a log reader is closed.
    /// </para>
    /// <para>
    /// The <see cref="AppLogFile" /> class manages the actual creation and
    /// record I/O to the log files.  Log files in the process of being
    /// written have the <b>.new</b> file extension whereas committed log
    /// files have the <b>.log</b> extension.  Log entries will be written
    /// to a particular log file until the file reaches a configurable
    /// size, at which point the file will be closed and renamed using the
    /// <b>log</b> extension.  The log writers hold the <b>.new</b> files open, 
    /// where as log readers process the <b>.log</b> files.
    /// </para>
    /// <para>
    /// Log writers are opened with a <b>maxLogSize</b> parameter.  This is used to 
    /// limit the overall size of the files created for an application log.  Pass 
    /// the limit as the approximate maximum size in bytes or 0 for unlimited.  
    /// Note that the actual log size may exceed this limit somewhat at times.  
    /// The application log implements this by periodically scanning the log folder, 
    /// totalling up the sizes of the <b>.log</b> files present.  If the total exceeds
    /// the limit, then the oldest log files will be deleted until we are back under
    /// the limit or there is only one remaining log file.  The <b>gPurgeInterval</b>
    /// configuration parameter controls how often the writer performs this operation.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class AppLog : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The default log file buffer size in bytes.
        /// </summary>
        internal const int DefBufferSize = 128 * 1024;

        /// <summary>
        /// Version number of the application log implementation, used within
        /// log files to specify of the file format version.
        /// </summary>
        internal static Version Version = new Version("1.0.0.0");

        /// <summary>
        /// The magic number to be used in log files to indicate that the file
        /// is indeed a log file.
        /// </summary>
        internal const int FileMagic = (int)0x7CDC5167;

        /// <summary>
        /// Magic number used to head a log record on disk.
        /// </summary>
        internal const int RecordMagic = (int)0x00006764;

        /// <summary>
        /// Magic number used to indicate end last record in the
        /// file has been processed.
        /// </summary>
        internal const int RecordEnd = (int)0x00007764;

        /// <summary>
        /// Format string used to generate the log file names.
        /// </summary>
        internal const string FileDateFormat = "yyyy-MM-ddTHH-mm-ss-fff";

        /// <summary>
        /// Opens a log reader for the named application log.
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
        public static AppLog OpenReader(string logName)
        {
            return new AppLog(logName, true, null, null, 0);
        }

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
        /// The <paramref name="schemaName" /> and <paramref name="schemaVersion" /> parameters
        /// are used to identify the  formatting of the records that will be written to the log.  
        /// Each record written will include this information.  This makes it possible for
        /// log files to be archived long term across format changes and yet
        /// provide a standard mechanism for future log readers to determine the
        /// particular record format and handle them appropriately.
        /// </para>
        /// <para>
        /// <paramref name="maxLogSize" /> is used to limit the overall size of the files
        /// created for an application log.  Pass the limit as the approximate maximum size
        /// in bytes or 0 for unlimited.  Note that the actual log size may exceed this 
        /// limit somewhat at times.  The application log implements this by periodically
        /// scanning the log folder, totalling up the sizes of the <b>.log</b> files present.
        /// If the total exceeds the limit, then the oldest log files will be deleted until
        /// we are back under the limit or there is only one remaining log file.
        /// </para>
        /// <note>
        /// Every log opened successfully with this method should
        /// be closed or disposed to release any underlying resources.
        /// </note>
        /// </remarks>
        public static AppLog OpenWriter(string logName, string schemaName, Version schemaVersion, long maxLogSize)
        {
            return new AppLog(logName, false, schemaName, schemaVersion, maxLogSize);
        }

        //---------------------------------------------------------------------
        // Instance members

        private object              syncLock = new object();
        private bool                isOpen;                 // True if the log is open
        private bool                readMode;               // True for a log reader, false for a writer
        private string              schemaName;             // Application log schema name (if writing)
        private Version             schemaVersion;          // Application log schema version (if writing)
        private string              logFolder;              // Fully qualified path to the log's file system folder
        private int                 cbBuffer;               // Log file buffer size in bytes
        private int                 maxFileSize;            // Maximum size of a log file before a new one is started
        private long                maxLogSize;             // Approximate maximum size total log size (or 0 if no limit)
        private TimeSpan            idleCommitInterval;     // Interval a writer will cache log entries before forcing a commit
        private TimeSpan            purgeInterval;          // Interval a writer will scan the log to prune files
        private PolledTimer         purgeTimer;             // Fires when its time to scan the log to prune files
        private DateTime            lastWriteTime;          // Last log write time (SYS)                                                    
        private AppLogFile          logFile;                // The current log file (or null if none exists)
        private FileStream          lockFile;               // The lock file (either "Reader.lock", or "Writer.lock")
        private string              lastReadFile;           // Fully qualified path of the last file successfully
                                                            // opened for reading (or null)
        private string              lastWriteFile;          // Fully qualified path of the last file successfully
                                                            // opened for writing (or null)
        private FileSystemWatcher   fileWatcher;            // Used by log readers to watch for newly added 
                                                            // log files so that the RecordAvailable event can
                                                            // be raised.
        private GatedTimer          bkTimer;                // Used by log writers to implement idle commit and
                                                            // to limit the total size size of the log.

        /// <summary>
        /// This event is raised after <see cref="Read" /> or <see cref="ReadDelete" /> 
        /// has returned null and subsequently, an additional set of one or more records
        /// have been made available to be read.
        /// </summary>
        public event LogRecordAvailableHandler RecordAvailable;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="logName">The log name.</param>
        /// <param name="openForRead">Pass <c>true</c> to open the log for reading, false for writing.</param>
        /// <param name="schemaName">The application record schema name if opening the log for writing (null for reading).</param>
        /// <param name="schemaVersion">The application record schema version if opening the log for writing (null for reading).</param>
        /// <param name="maxLogSize">Approximate maximum total log size in bytes or 0 for unlimited (ignored for reading).</param>
        /// <remarks>
        /// Log names can range from 1 to 64 characters including A-Z, a-z, 0-9, the dash (-) and the underscore (_).
        /// </remarks>
        private AppLog(string logName, bool openForRead, string schemaName, Version schemaVersion, long maxLogSize)
        {
            lock (syncLock)
            {
                Config config = new Config("LillTek.AppLog");
                string rootFolder;

                rootFolder              = config.Get("RootFolder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\AppLogs"));
                this.cbBuffer           = config.Get("BufferSize", DefBufferSize);
                this.isOpen             = false;
                this.maxFileSize        = config.Get("MaxFileSize", 131072);
                this.maxLogSize         = Math.Max(maxLogSize, 0);
                this.idleCommitInterval = config.Get("IdleCommitInterval", TimeSpan.FromMinutes(5.0));
                this.purgeInterval      = config.Get("PurgeInterval", TimeSpan.FromMinutes(5));
                this.purgeTimer         = new PolledTimer(purgeInterval, false);
                this.readMode           = openForRead;
                this.schemaName         = schemaName;
                this.schemaVersion      = schemaVersion;
                this.lastWriteTime      = SysTime.Now;
                this.bkTimer            = null;

                // Initialize the log directory if necessary.

                if (logName == null)
                    throw new ArgumentNullException("logName");

                if (logName.Length == 0 || logName.Length > 64)
                    throw new ArgumentException("Name length must range from 1..64 characters.", "logName");

                for (int i = 0; i < logName.Length; i++)
                    if (!Char.IsLetterOrDigit(logName[i]) && logName[i] != '-' && logName[i] != '_')
                        throw new ArgumentException("Log names may include only, letters, digits, dashes (-) or the underscore (_).", "logName");

                logFolder = rootFolder;
                if (!logFolder.EndsWith(Helper.PathSepString))
                    logFolder += Helper.PathSepString;

                logFolder += logName;
                Helper.CreateFolderTree(logFolder);

                // Create the appropriate lock file and open it with exclusive access.

                try
                {
                    lockFile = new FileStream(logFolder + Helper.PathSepString + (openForRead ? "Reader.lock" : "Writer.lock"), FileMode.Create, FileAccess.ReadWrite);
                }
                catch
                {
                    throw new LogException(readMode ? "Another log reader holds the lock on this log."
                                                    : "Another log writer holds the lock on this log.");
                }

                // Initialize the file watcher if this is a log reader

                if (readMode)
                {
                    fileWatcher                       = new FileSystemWatcher(logFolder, "*.log");
                    fileWatcher.EnableRaisingEvents   = true;
                    fileWatcher.IncludeSubdirectories = false;
                    fileWatcher.Renamed              += new RenamedEventHandler(OnFileRenamed);
                }
                else
                    fileWatcher = null;

                // Open/Create the log file

                lastReadFile  = null;
                lastWriteFile = null;
                logFile       = null;

                if (openForRead)
                {
                    // Setting the record position from the Reader.pos file if
                    // this file exists.

                    try
                    {
                        var posFileName = logFolder + Helper.PathSepString + "Reader.pos";

                        if (File.Exists(posFileName))
                        {
                            var reader = new StreamReader(posFileName);
                            string readPos;

                            readPos = reader.ReadLine();
                            reader.Close();
                            File.Delete(posFileName);

                            if (readPos != null)
                            {
                                try
                                {
                                    this.isOpen = true;   // A bit of a hack to disable a check in Position.
                                    this.Position = readPos;
                                }
                                finally
                                {
                                    this.isOpen = false;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    // There was no Reader.pos file or there was an error setting the
                    // position so open the first log file and set the position to
                    // the first record in the log.

                    if (logFile == null)
                        logFile = OpenLogFile();
                }
                else
                {
                    logFile = CreateLogFile();
                    bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, TimeSpan.FromSeconds(1.0));
                }

                isOpen = true;
            }
        }

        /// <summary>
        /// Opens the next log file for reading for log readers, closing the
        /// current file if one is open.  Note that the current record position
        /// will be restored to the position when the log was last closed.
        /// </summary>
        /// <returns>Returns the next log file or <c>null</c> if there are none.</returns>
        private AppLogFile OpenLogFile()
        {
            Assertion.Test(readMode);

            lock (syncLock)
            {
                // Open the log file with the lexically lowest file name 
                // that is greater than the file currently open (if there is one)
                // or the last file read (if there was one) for reading, deleting 
                // any files that appear to be corrupted.

                string      curFile;
                string[]    files;

                if (logFile == null)
                    curFile = lastReadFile;
                else
                {
                    curFile = logFile.FullPath;
                    logFile.Close();
                    logFile = null;
                }

                files = Directory.GetFiles(logFolder, "*.log");
                if (files.Length == 0)
                {
                    logFile = null;     // No log files found
                    return null;
                }

                Array.Sort(files);

                for (int i = 0; i < files.Length; i++)
                {
                    if (curFile != null && String.Compare(Path.GetFileName(files[i]), Path.GetFileName(curFile), true) <= 0)
                        continue;

                    try
                    {
                        logFile = new AppLogFile();
                        logFile.Open(files[i], cbBuffer);
                        lastReadFile = logFile.FullPath;
                        break;
                    }
                    catch (LogCorruptedException)
                    {
                        SysLog.LogWarning("Deleting corrupted application log file [{0}].", files[i]);
                        File.Delete(files[i]);
                    }
                }

                return logFile;
            }
        }

        /// <summary>
        /// Creates the next uncommited log file for log writers, commiting the
        /// current file if one is open.
        /// </summary>
        /// <returns>The new uncommited log file.</returns>
        private AppLogFile CreateLogFile()
        {
            var retry = false;

            Assertion.Test(!readMode);

            lock (syncLock)
            {
                string[] files;

                // Close/commit an existing open log file

                if (logFile != null)
                {
                    logFile.Close();
                    logFile = null;
                }

                // Delete any files that appear to be uncommited (and probably damaged)
                // log files in the folder.

                files = Directory.GetFiles(logFolder, "*.new");
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch
                    {
                    }
                }

                // Create and return a write log file.

                tryAgain: try
                {
                    var fileName = logFolder + Helper.PathSepString + DateTime.UtcNow.ToString(FileDateFormat) + ".new";

                    if (lastWriteFile != null && fileName == lastWriteFile)
                    {
                        // This is another one of those rare situations where the system
                        // is so fast that the milliseconds on the clock hasn't changed
                        // since the last log file was created resulting in duplicate names.
                        // Wait 20ms to ensure that the clock ticks and we get a new name.

                        Thread.Sleep(20);
                        fileName = logFolder + Helper.PathSepString + DateTime.UtcNow.ToString(FileDateFormat) + ".new";
                    }

                    logFile = new AppLogFile();
                    logFile.Create(fileName, cbBuffer, schemaName, schemaVersion);
                    lastWriteFile = fileName;
                }
                catch (IOException)
                {
                    // It's possible under rare circumstances to get a file name collision
                    // if the system is running so fast that we're on the same millisecond
                    // as when the last log file was created.  This will probably never happen
                    // in real life, but just in case, I'm going to retry the file creation
                    // once after sleeping for 20ms.

                    if (retry)
                        throw;

                    retry = true;
                    Thread.Sleep(20);
                    goto tryAgain;
                }

                lastWriteTime = SysTime.Now;
                return logFile;
            }
        }

        /// <summary>
        /// Closes the log, committing any cached log entries and releasing any resources held.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every log successfully opened should be closed by calling this method
        /// or <see cref="Dispose" />.  Note that it is not an error to close a
        /// log that has already been closed.
        /// </para>
        /// <para>
        /// Application logs opened for reading will persistently save the current 
        /// record position when the log is closed so that this can be restored the
        /// next time the log is opened for reading.
        /// </para>
        /// </remarks>
        public void Close()
        {
            lock (syncLock)
            {
                if (isOpen && readMode)
                {
                    using (var writer = new StreamWriter(logFolder + Helper.PathSepString + "Reader.pos"))
                    {
                        writer.WriteLine(this.Position);
                    }
                }

                isOpen = false;

                if (fileWatcher != null)
                {
                    fileWatcher.Dispose();
                    fileWatcher = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (logFile != null)
                {
                    logFile.Close();
                    logFile = null;
                }

                if (lockFile != null)
                {
                    lockFile.Close();
                    lockFile = null;
                }
            }
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
        /// <para>
        /// Specifies the approximate maximum size when an committed log file
        /// will be committed.
        /// </para>
        /// <note>
        /// This property is available only for logs opened for writing.
        /// </note>
        /// </summary>
        /// <remarks>
        /// This property can be used to override the value specified in the
        /// confiuration file after the log has been opened.
        /// </remarks>
        public int MaxFileSize
        {
            get
            {
                lock (syncLock)
                {
                    if (readMode)
                        throw new LogAccessException(readMode);

                    if (!isOpen)
                        throw new LogException("Log is closed.");

                    return maxFileSize;
                }
            }

            set
            {
                if (readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                maxFileSize = value;
            }
        }

        /// <summary>
        /// <para>
        /// Interval to wait before committing an non-empty idle log file.  
        /// </para>
        /// <note>
        /// This property is available only for logs opened for writing.
        /// </note>
        /// </summary>
        /// <remarks>
        /// This property can be used to override the value specified in the
        /// confiuration file after the log has been opened.
        /// </remarks>
        public TimeSpan IdleCommitInterval
        {
            get
            {
                lock (syncLock)
                {
                    if (readMode)
                        throw new LogAccessException(readMode);

                    if (!isOpen)
                        throw new LogException("Log is closed.");

                    return this.idleCommitInterval;
                }
            }

            set
            {
                if (readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                idleCommitInterval = value;
            }
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
            get
            {
                lock (syncLock)
                {
                    if (readMode)
                        throw new LogAccessException(readMode);

                    if (!isOpen)
                        throw new LogException("Log is closed.");

                    return this.purgeInterval;
                }
            }

            set
            {
                if (readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                purgeInterval = value;
                purgeTimer = new PolledTimer(purgeInterval, false);
            }
        }

        /// <summary>
        /// Removes all commited entries in the log.  This method is available
        /// only for logs opened for reading.
        /// </summary>
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        public void Clear()
        {
            lock (syncLock)
            {
                if (!readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                // Close the current log file if one is open and then delete
                // all log files in the folder.

                if (logFile != null)
                {
                    logFile.Close();
                    logFile = null;
                }

                Helper.DeleteFile(logFolder + Helper.PathSepString + "*.log");
            }
        }

        /// <summary>
        /// Called by the file watcher when an uncommitted log file is committed
        /// and renamed to *.log.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="args">The event arguments.</param>
        private void OnFileRenamed(object sender, RenamedEventArgs args)
        {
            lock (syncLock)
            {
                // Raise the RecordAvailable event if this is a log reader,
                // if we're not in the process of reading a log file and the
                // new log file is lexically greater the last log file read.

                if (!readMode || logFile != null || String.Compare(Path.GetExtension(args.Name), ".log", true) != 0)
                    return;

                if (lastReadFile == null || String.Compare(Path.GetFileName(lastReadFile), Path.GetFileName(args.FullPath), true) > 0)
                    RecordAvailable();
            }
        }

        /// <summary>
        /// Returns the next committed record from the file but does not
        /// advance the record pointer.
        /// </summary>
        /// <returns>The next record or <c>null</c> if there are no more records to be read.</returns>
        public AppLogRecord Peek()
        {
            AppLogRecord record;

            lock (syncLock)
            {
                if (!readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                if (logFile == null)
                {
                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.Peek();
                }

                record = logFile.Peek();
                if (record == null)
                {
                    logFile.Close();
                    logFile = null;

                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.Peek();
                }

                return record;
            }
        }

        /// <summary>
        /// Returns the next committed record in the application log if there is one,
        /// null otherwise.  This method is available only for logs opened for
        /// reading.
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
        /// (by returning null), applications should monitor the <see cref="RecordAvailable" />
        /// event.  This event will be raised when one or more committed events become available to
        /// be read.
        /// </para>
        /// </remarks>
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        public AppLogRecord Read()
        {
            AppLogRecord record;

            lock (syncLock)
            {
                if (!readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                if (logFile == null)
                {
                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.Read();
                }

                record = logFile.Read();
                if (record == null)
                {
                    logFile.Close();
                    logFile = null;

                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.Read();
                }

                return record;
            }
        }

        /// <summary>
        /// Returns the next committed record in the application log and then marks
        /// if for deletion if there is one.  The method returns null if there
        /// are no records remaining to be read.  This method is available only for 
        /// logs opened for reading.
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
        /// (by returning null), applications should monitor the <see cref="RecordAvailable" />
        /// event.  This event will be raised when one or more committed events become available to
        /// be read.
        /// </para>
        /// </remarks>
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        public AppLogRecord ReadDelete()
        {
            AppLogRecord record;

            lock (syncLock)
            {
                if (!readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                if (logFile == null)
                {
                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.ReadDelete();
                }

                record = logFile.ReadDelete();
                if (record == null)
                {
                    logFile.Close();
                    logFile = null;

                    logFile = OpenLogFile();
                    if (logFile == null)
                        return null;
                    else
                        return logFile.ReadDelete();
                }

                return record;
            }
        }

        /// <summary>
        /// Specifies the position within the application log of the next record to be
        /// read.  This method is available only for logs opened for reading.
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
        /// <exception cref="LogAccessException">Thrown for log writers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        public string Position
        {
            get
            {
                lock (syncLock)
                {
                    if (!readMode)
                        throw new LogAccessException(readMode);

                    if (!isOpen)
                        throw new LogException("Log is closed.");

                    if (logFile != null)
                        return Path.GetFileName(logFile.FullPath) + ":" + logFile.Position;

                    // No log file is open then we're at either the
                    // beginning or end of the log.  Determining this is
                    // a bit ambiguous.  I'm going to resolve this by 
                    // listing the committed log files in the log folder.
                    // If there are no files, then I'm going to assume that
                    // I'm at the beginning of the log.
                    //
                    // If there are some files then I'm going to assume that we 
                    // must have processed them all so we must be at the end of 
                    // the last file processed.  I'm going to return:
                    //
                    //      <log file>:END
                    //
                    // in this case.

                    if (lastReadFile == null || Directory.GetFiles(logFolder, "*.log").Length == 0)
                        return "BEGINNING";
                    else
                        return Path.GetFileName(lastReadFile) + ":END";
                }
            }

            set
            {
                // $todo(jeff.lill): 
                //
                // I could optimize this by checking to see if
                // the file already open is the one requested.
                // This isn't a super high priority though since
                // the Position property was really designed originally
                // as a way for log reading application to persistenty
                // store the current position and then come back to
                // it later.

                int         pos;
                string      fileName;
                string      filePos;

                lock (syncLock)
                {
                    if (!readMode)
                        throw new LogAccessException(readMode);

                    if (!isOpen)
                        throw new LogException("Log is closed.");

                    // Close the open log file if there is one.

                    if (logFile != null)
                    {
                        logFile.Close();
                        logFile = null;
                    }

                    // There are three cases here for the position format value:
                    //
                    //      BEGINNING       - Indicates that we should rewind to the 
                    //                        beginning of the log.
                    //
                    //      <file>:END      - Indicates that we should seek to the first record 
                    //                        in first log file after the file named in
                    //                        the position value.
                    //
                    //      <file>:<offset> - Indicates that we should open the specified
                    //                        log file and seek to the specified offset
                    //                        within it.

                    if (String.Compare(value, "BEGINNING", true) == 0)
                    {
                        lastReadFile = null;
                        logFile = OpenLogFile();
                        return;
                    }

                    pos = value.IndexOf(':'); ;
                    if (pos == -1)
                        throw new LogException("Invalid position string.");

                    fileName = logFolder + Helper.PathSepString + value.Substring(0, pos);
                    filePos = value.Substring(pos + 1);

                    if (fileName.Length == 0 || !fileName.ToLowerInvariant().EndsWith(".log") || filePos.Length == 0)
                        throw new LogException("Invalid position string.");

                    if (String.Compare(filePos, "END", true) == 0)
                    {
                        lastReadFile = fileName;
                        logFile = OpenLogFile();
                        return;
                    }

                    // First try opening the specified log file and seeking to
                    // the specified position.  If this doesn't work then log
                    // a warning and seek to the first record of the next log file.

                    try
                    {
                        logFile = new AppLogFile();
                        logFile.Open(fileName, cbBuffer);
                        logFile.Position = filePos;
                        lastReadFile = fileName;
                        return;
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e, "Unable to seek to application log position [{0}].", value);

                        logFile.Close();
                        logFile = null;
                    }

                    lastReadFile = fileName;
                    logFile = OpenLogFile();
                }
            }
        }

        /// <summary>
        /// Called periodically on a background thread to handle idle commits
        /// for log writers.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTimer(object state)
        {
            lock (syncLock)
            {
                if (readMode || !isOpen)
                {
                    Assertion.Fail();
                    return;
                }

                // Handle idle commits.

                try
                {
                    if (lastWriteTime + idleCommitInterval <= SysTime.Now && logFile != null && logFile.WriteCount > 0)
                        this.Commit();
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                // Handle log directory pruning.

                if (maxFileSize > 0 && purgeTimer.HasFired)
                {
                    try
                    {
                        // Create a list of files ordered in ascending order by date (oldest first)
                        // using the date in the file name to perform the sort.

                        var query =
                            from path in Directory.GetFiles(logFolder, "*.log", SearchOption.TopDirectoryOnly)
                            orderby path.ToLower() ascending
                            select path;

                        string[] files = query.ToArray();
                        long totalSize = 0;
                        int i;

                        // Total the sizes of all of the log files.

                        foreach (var path in files)
                            totalSize += new FileInfo(path).Length;

                        // Delete log files, oldest first until we're within the size limit
                        // or there's only one log file remaining.

                        i = 0;
                        while (totalSize > maxFileSize && i < files.Length - 1)
                        {

                            long fileSize = new FileInfo(files[i]).Length;

                            try
                            {

                                File.Delete(files[i]);
                                totalSize -= fileSize;
                            }
                            catch (IOException)
                            {

                                // I'm going to ignore I/O errors deleting specific log
                                // files because they may be opened by a reader.  Instead,
                                // we'll skip files with errors and continue trying to
                                // delete the remaining files.
                            }

                            i++;
                        }
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                    finally
                    {
                        purgeTimer.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Appends a record to the uncommitted section of the log.  If this has become
        /// large enough, then the method will commit records in the cache.  This method
        /// is available only for logs opened for writing.
        /// </summary>
        /// <param name="record">The record to be written.</param>
        /// <exception cref="LogAccessException">Thrown for log readers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        /// <remarks>
        /// <note>
        /// Any exceptions thown by this method indicate a serious
        /// nonrecoverable error.  The application log should be closed.
        /// </note>
        /// </remarks>
        public void Write(AppLogRecord record)
        {
            lock (syncLock)
            {
                if (readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                try
                {
                    logFile.Write(record);
                    lastWriteTime = SysTime.Now;

                    if (logFile.Size >= maxFileSize)
                    {
                        logFile.Close();
                        logFile = null;
                        logFile = CreateLogFile();
                    }
                }
                catch
                {
                    if (logFile != null)
                    {
                        logFile.Close();

                        try
                        {
                            File.Delete(logFile.FullPath);
                        }
                        catch
                        {
                        }

                        logFile = null;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Commits the uncommitted log records to disk if there are any but keeps the
        /// log open.  This method is available only for logs opened for writing.
        /// </summary>
        /// <exception cref="LogAccessException">Thrown for log readers.</exception>
        /// <exception cref="LogClosedException">Thrown for if the log is not open.</exception>
        public void Commit()
        {
            lock (syncLock)
            {
                if (readMode)
                    throw new LogAccessException(readMode);

                if (!isOpen)
                    throw new LogException("Log is closed.");

                if (logFile.WriteCount == 0)
                    return;

                try
                {
                    logFile.Close();
                    logFile = null;
                    logFile = CreateLogFile();
                }
                catch
                {
                    if (logFile != null)
                    {
                        logFile.Close();

                        try
                        {
                            File.Delete(logFile.FullPath);
                        }
                        catch
                        {
                        }

                        logFile = null;
                    }

                    throw;
                }
            }
        }
    }
}
