//-----------------------------------------------------------------------------
// FILE:        TextLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simple text file based logging mechanism.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections;

namespace LillTek.Common
{
    /// <summary>
    /// A simple text file based logging mechanism.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class manages the creation of text log files within a specified file
    /// system folder.  This folder is specified in the call to the constructor
    /// <see cref="TextLog" /> along with the approximate maximum size each log
    /// file can reach before another log file is created.
    /// </para>
    /// <para>
    /// Log files files are created as necessary by the class.  Each file file
    /// is named using a the time (UTC) when the file was created, e.g. "2007-07-20T14-51-15Z.log".
    /// Log files are text encoded as UTF-8.  Each line of the log file is a log entry, including
    /// the time of the entry, the entry category, the entry title, and the entry details, each
    /// of these fields being separated by TABs.
    /// </para>
    /// <para>
    /// Use <see cref="Log" /> to append an entry onto the end of the log.  This method will
    /// create a log file if one doesn't already exists and if the most recent log file 
    /// exceeds the maximum size specified to the constructor, then a new log file will be
    /// created.
    /// </para>
    /// <note>
    /// This class does not support having multiple instances managing log files in the
    /// same folder.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class TextLog : IDisposable
    {
        private object      syncLock = new object();
        private string      folderPath;
        private int         cbMaxLog;
        private string      curLogPath;
        private int         cbCurFile;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="folderPath">Path of the folder where the log files are to be written.</param>
        /// <param name="maxFileSize">The approximate maximum size of a log file in bytes.</param>
        /// <remarks>
        /// <note>
        /// The constructor will create the folder if it doesn't already exist.
        /// </note>
        /// </remarks>
        public TextLog(string folderPath, int maxFileSize)
        {
            string[]    logs;
            string      lastFile;
            int         cbLastFile;

            if (maxFileSize < 0)
                maxFileSize = 0;

            this.folderPath = Helper.AddTrailingSlash(folderPath);
            this.cbMaxLog = maxFileSize;

            // Continue using the most recent log file if it's not
            // to big, otherwise start a new file.  Note that this
            // code assumes that the files returned by GetFilesByPattern()
            // are sorted in ascending order by file name and that the
            // log files are named using the sortable ISO date format.

            Helper.CreateFolderTree(this.folderPath);

            logs       = Helper.GetFilesByPattern(this.folderPath + "*.log", SearchOption.TopDirectoryOnly);
            lastFile   = null;
            cbLastFile = 0;

            if (logs.Length > 0)
            {
                try
                {
                    using (FileStream fs = new FileStream(logs[logs.Length - 1], FileMode.Open, FileAccess.Read))
                    {

                        if (fs.Length < cbMaxLog)
                        {
                            lastFile = logs[logs.Length - 1];
                            cbLastFile = (int)fs.Length;
                        }
                    }
                }
                catch
                {
                    // Create a new log file if we can't access the existing one.
                }
            }

            if (lastFile == null)
            {
                this.curLogPath = this.folderPath + Helper.ToIsoDate(DateTime.UtcNow).Replace(':', '-') + ".log";
                this.cbCurFile = 0;
            }
            else
            {
                this.curLogPath = lastFile;
                this.cbCurFile = cbLastFile;
            }

            Helper.CreateFileTree(curLogPath);
        }

        /// <summary>
        /// Releases all resources associated with this instance.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Writes an entry to the log.
        /// </summary>
        /// <param name="category">The entry category.</param>
        /// <param name="title">The entry title.</param>
        /// <param name="details">The entry details.</param>
        /// <remarks>
        /// Note that the strings passed should not include TAB, CR, or LF characters.
        /// </remarks>
        public void Log(string category, string title, string details)
        {
            byte[]      entry = Helper.ToUTF8(string.Format("{0}Z\t{1}\t{2}\t{3}\r\n", DateTime.UtcNow.ToString("MM-dd-yyyy HH:mm:ss"), category, title, details));
            FileStream  fs;

            lock (syncLock)
            {
                using (fs = new FileStream(curLogPath, FileMode.Append))
                {
                    fs.Write(entry, 0, entry.Length);
                }

                cbCurFile += entry.Length;
                if (cbCurFile >= cbMaxLog)
                {
                    curLogPath = folderPath + Helper.ToIsoDate(DateTime.UtcNow).Replace(':', '-') + ".log";
                    cbCurFile  = 0;

                    Helper.CreateFileTree(curLogPath);
                }
            }
        }
    }
}
