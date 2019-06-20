//-----------------------------------------------------------------------------
// FILE:		FolderCleaner.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Utility class that periodically sweeps a folder to remove old files.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LillTek.Common
{
    /// <summary>
    /// A utility class that periodically sweeps a folder to remove old files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construct an instance specifying the folder path and optional file pattern, purge interval,
    /// minimum file age and an indication of whether to purge recursively or not.  The instance will 
    /// begin attempting to purge files and will continue until <see cref="Pause"/> or <see cref="Dispose()"/>
    /// is called.  <see cref="Resume"/> resumes purging after a pause.
    /// </para>
    /// <para>
    /// Only files that match the pattern that are older than the specified age will be deleted.  Files
    /// that are currently opened will not be deleted.
    /// </para>
    /// <note>
    /// Folders are not deleted, only files.  Note also that this class will tolerate the
    /// situation where a specified folder does not actually exist.
    /// </note>
    /// </remarks>
    public sealed class FolderCleaner : IDisposable
    {       
        private string      folderPath;
        private string      pattern;
        private TimeSpan    period;
        private TimeSpan    maxAge;
        private bool        recursive;
        private bool        isPaused;
        private bool        isDisposed;

        /// <summary>
        /// Constructs and starts a folder cleaner.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="pattern">The optional file pattern (defaults to <b>"*.*</b>).</param>
        /// <param name="period">The optional purge period (defaults to <b>1 minute</b>).</param>
        /// <param name="maxAge">The optional maximum file age (defaults to <b>1 minute</b>).</param>
        /// <param name="recursive">Specifies whether files in subfolders should also be purged (defaults to <c>false</c>).</param>
        public FolderCleaner(string folderPath, string pattern = "*.*", TimeSpan? period = null, TimeSpan? maxAge = null, bool recursive = false)
        {
            // Initialize the fields

            this.folderPath = folderPath;
            this.pattern    = pattern;
            this.period     = period ?? TimeSpan.FromSeconds(60);
            this.maxAge     = maxAge ?? TimeSpan.FromSeconds(60);
            this.recursive  = recursive;

            // Crank up the background task.

            Task.Run(async () => await PurgeLoop());
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~FolderCleaner()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing as opposed to finalizing.</param>
        private void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Pauses purging.
        /// </summary>
        public void Pause()
        {
            isPaused = true;
        }

        /// <summary>
        /// Resumes purging.
        /// </summary>
        public void Resume()
        {
            isPaused = false;
        }

        /// <summary>
        /// Implements the purging.
        /// </summary>
        /// <returns></returns>
        private async Task PurgeLoop()
        {
            while (!isDisposed)
            {
                if (isPaused)
                {
                    await Task.Delay(Helper.Min(period, TimeSpan.FromSeconds(15)));
                    continue;
                }

                var maxWriteTime = DateTime.UtcNow - maxAge;

                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        foreach (var filePath in Directory.EnumerateFiles(folderPath, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                if (File.GetLastWriteTimeUtc(filePath) <= maxWriteTime)
                                {
                                    File.Delete(filePath);
                                }
                            }
                            catch
                            {
                                // Ignoring errors
                            }
                        }
                    }
                }
                catch
                {
                    // Ignoring errors
                }

                await Task.Delay(period);
            }
        }
    }
}
