//-----------------------------------------------------------------------------
// FILE:        FileSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a simple ISysLogProvider for that logs to files.

using System;
using System.IO;

// $todo(jeff.lill): 
// 
// I'm having trouble with obtaining write access to the security
// in some services when running under the LocalSystem account.
// For now, I'm going to just write security related events to the
// application event log as a work-around.

namespace LillTek.Common
{
    /// <summary>
    /// This is an implementation of <see cref="ISysLogProvider"/> that writes logs to
    /// local files.
    /// </summary>
    public class FileSysLogProvider : SysLogProvider
    {
        private object      syncLock = new object();
        private string      logFolder;          // Path to the log file folder
        private string      applicationName;    // Identifies the application.
        private int         maxSize;            // Approximate maximum size of a log file
        private string      currentFile;        // Fully qualified path to the current log file

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logFolder">Path to the folder where the log files will be written.</param>
        /// <param name="applicationName">Identifies the application.  This will be prepended to the log file names.</param>
        /// <param name="maxSize">The approximate maximum size of a log file before a new one will be started.</param>
        public FileSysLogProvider(string logFolder, string applicationName, int maxSize = 1048576 /* 1MB */)
        {
            this.logFolder       = Path.GetFullPath(logFolder);
            this.applicationName = applicationName;
            this.maxSize         = maxSize;

            CreateNewFile();
        }

        /// <summary>
        /// Creates a new empty log file.
        /// </summary>
        private void CreateNewFile()
        {
            Helper.CreateFolderTree(logFolder);

            var fileName = string.Format("{0}-{1}.log", applicationName, Helper.ToIsoDate(DateTime.UtcNow));
            
            fileName    = fileName.Replace(':', '-'); // Replace the colons from the time with dashes so we'll end up with a kosher file name.
            currentFile = Path.Combine(logFolder, fileName);

            File.WriteAllText(this.currentFile, string.Empty);
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public override void Flush()
        {
            // This is a NOP.
        }

        /// <summary>
        /// Appends a <see cref="SysLogEntry" /> to the event log.
        /// </summary>
        /// <param name="entry">The log entry.</param>
        protected override void Append(SysLogEntry entry)
        {
            bool createNew;

            lock (syncLock)
            {
                try
                {
                    using (var output = File.AppendText(currentFile))
                    {
                        output.WriteLine(entry.ToString(SysLogEntryFormat.ShowAll));
                        createNew = output.BaseStream.Length >= maxSize;
                    }

                    if (createNew)
                    {
                        CreateNewFile();
                    }
                }
                catch
                {
                    // Absorb any exceptions to avoid impacting the application due to logging issues.
                }
            }
        }
    }
}
