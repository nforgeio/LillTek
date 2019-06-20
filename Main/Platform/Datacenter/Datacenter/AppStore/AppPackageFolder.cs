//-----------------------------------------------------------------------------
// FILE:        AppPackageFolder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a file folder holding a collection of application
//              packages.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Encapsulates a file folder holding a collection of application package
    /// files.  This class is intended for internal use by the LillTek Platform
    /// and should not be instantiated directly by user applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class works by monitoring the contents of a file system folder
    /// containing application package ZIP files.  The path to this folder
    /// is passed to the constructor <see cref="AppPackageFolder" /> along
    /// with the object instance to be used for thread synchronization.  The
    /// constructor creates this folder if necessary and also creates a subfolder
    /// named <b>Pending</b> which will be used to hold application packages
    /// in the process of being uploaded or downloaded.  The <see cref="AppPackageFolder" />
    /// instance will immediately begin monitoring the package folder for changes.
    /// <see cref="Dispose" /> should be called promptly when the instance is
    /// no longer needed so that all associated resources will be released
    /// (ie. the <see cref="FileSystemWatcher" />).
    /// </para>
    /// <para>
    /// Applications can enlist in the <see cref="ChangeEvent" />.  This event
    /// will be raised after change to contents of the package folder is detected
    /// and the instance has updated its information.  The single parameter is
    /// the <see cref="AppPackageFolder" /> instance.  Applications can force
    /// an immediate rescan of the packages in the folder by calling <see cref="Scan" />.
    /// The <see cref="Purge" /> method should be called periodically to ensure
    /// that abandoned transit package files are deleted.
    /// </para>
    /// <para>
    /// The <see cref="PackageFolder" /> and <see cref="TransitFolder" /> properties
    /// can be used to discover where these folders are located on the file system.
    /// <see cref="GetPackageInfo" /> can be used to lookup information on a specific
    /// package based given its <see cref="AppRef" /> and <see cref="GetPackages" />
    /// can be called to get the current list of all packages in the folder.
    /// </para>
    /// <para>
    /// Packages can be added or removed from the collection simply by copying
    /// or deleting files directly to the package folder.  This will cause 
    /// <see cref="AppPackageFolder" /> to rescan all of the packages in the
    /// folder which can be a quite lengthly operation since each ZIP file
    /// needs to be opened to read metadata and then scanned in its entirety to 
    /// compute the MD5 hash.  To avoid this overhead, use <see cref="BeginTransit" />
    /// and <see cref="EndTransit" /> to add a package to the folder and
    /// <see cref="Remove" /> to remove a package.
    /// </para>
    /// <para>
    /// The <see cref="AutoScan" /> controls whether the class monitors the
    /// file system for changes in the package folder and rescans the
    /// package set when any changes are detected.  This is useful for
    /// temporarily disabling scanning when explicitly downloading or
    /// adding a new package.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class AppPackageFolder : IDisposable
    {
        /// <summary>
        /// The time the class will wait before retrying certain failed I/O operations.
        /// </summary>
        public static readonly TimeSpan RetryTime = TimeSpan.FromSeconds(5);

        private object                              syncLock;       // Thread synchronization instance
        private string                              packageFolder;  // The package folder path (including a terminating "\\"
        private string                              transitFolder;  // The transit folder path (including a terminating "\\"
        private Dictionary<AppRef, AppPackageInfo>  packages;       // Application package information keyed by appref
        private FileSystemWatcher                   fileWatcher;    // The file system watcher
        private bool                                isDisposed;     // True if the instance has been disposed
        private int                                 scanning;       // Non-zero if a Scan() is in progress

        /// <summary>
        /// Raised when a change to the package folder is detected.
        /// </summary>
        public event MethodArg1Invoker ChangeEvent;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="syncLock">The thread synchroniozation instance.</param>
        /// <param name="path">The package folder name.</param>
        /// <remarks>
        /// <note>
        /// All files located in the transit folder will be deleted.
        /// </note>
        /// </remarks>
        public AppPackageFolder(object syncLock, string path)
        {
            if (!Path.IsPathRooted(path))
            {
                if (path.EndsWith("\\") || path.EndsWith("/"))
                    path = path.Substring(0, path.Length - 1);

                path = Helper.GetAssemblyFolder(Helper.GetEntryAssembly()) + path;
                Helper.CreateFolderTree(path);      // Make sure the folder exists
            }

            this.packageFolder = Helper.AddTrailingSlash(Path.GetFullPath(path));

            path = packageFolder + "Transit";
            Helper.CreateFolderTree(path);
            this.transitFolder = Helper.AddTrailingSlash(Path.GetFullPath(path));
            Helper.DeleteFile(transitFolder + "*.*");

            this.isDisposed                        = false;
            this.scanning                          = 0;
            this.syncLock                          = syncLock;
            this.packages                          = new Dictionary<AppRef, AppPackageInfo>();
            this.fileWatcher                       = new FileSystemWatcher(packageFolder, "*.zip");
            this.fileWatcher.IncludeSubdirectories = false;
            this.fileWatcher.EnableRaisingEvents   = true;
            this.fileWatcher.Changed              += new FileSystemEventHandler(OnChange);
            this.fileWatcher.Created              += new FileSystemEventHandler(OnChange);
            this.fileWatcher.Deleted              += new FileSystemEventHandler(OnChange);
            this.fileWatcher.Renamed              += new RenamedEventHandler(OnRename);

            InternalScan();
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// All files in the transit folder will be deleted.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            FileSystemWatcher watcher = null;

            using (TimedLock.Lock(syncLock))
            {
                isDisposed = true;

                if (fileWatcher != null)
                {
                    watcher = fileWatcher;
                    fileWatcher = null;
                }

                Helper.DeleteFile(transitFolder + "*.*");
            }
        }

        /// <summary>
        /// Returns the fully qualified path of the application package folder including
        /// the terminating "\\".
        /// </summary>
        public string PackageFolder
        {
            get { return packageFolder; }
        }

        /// <summary>
        /// Returns the fully qualified path of the application package transit folder including
        /// the terminating "\\".
        /// </summary>
        public string TransitFolder
        {
            get { return transitFolder; }
        }

        /// <summary>
        /// Returns information about a specific application package if it's present
        /// in the package folder.
        /// </summary>
        /// <param name="appRef">The application package <see cref="AppRef" />.</param>
        /// <returns>The package information or <c>null</c>.</returns>
        public AppPackageInfo GetPackageInfo(AppRef appRef)
        {
            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            using (TimedLock.Lock(syncLock))
            {
                AppPackageInfo info;

                if (packages.TryGetValue(appRef, out info))
                    return info;
                else
                    return null;
            }
        }

        /// <summary>
        /// Returns information about the set of packages currently loaded into
        /// the folder.
        /// </summary>
        /// <returns>An array of <see cref="AppPackageInfo" /> instances.</returns>
        public AppPackageInfo[] GetPackages()
        {
            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            using (TimedLock.Lock(syncLock))
            {
                var arr = new AppPackageInfo[packages.Count];
                int i;

                i = 0;
                foreach (AppPackageInfo info in packages.Values)
                    arr[i++] = info;

                return arr;
            }
        }

        /// <summary>
        /// Controls whether the class watches for changes to the package file folder
        /// and automatically rescans the packages.  This defaults to <c>true</c>.
        /// </summary>
        public bool AutoScan
        {
            get { return fileWatcher.EnableRaisingEvents; }
            set { fileWatcher.EnableRaisingEvents = value; }
        }

        /// <summary>
        /// Scans the package folder for changes to the set of available application packages.
        /// </summary>
        public void Scan()
        {
            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            InternalScan();
        }

        /// <summary>
        /// The actual internal folder scan implementation.
        /// </summary>
        private void InternalScan()
        {
            if (isDisposed)
                return;

            try
            {
                if (Interlocked.Increment(ref scanning) > 1)
                    return;     // Don't allow multiple parallel scans

                Dictionary<AppRef, AppPackageInfo> newPackages = new Dictionary<AppRef, AppPackageInfo>();
                string[] files;

                files = Helper.GetFilesByPattern(packageFolder + "*.zip", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    bool isRetry = false;

                retry:
                    try
                    {
                        int size;

                        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                            size = (int)fs.Length;

                        using (AppPackage package = AppPackage.Open(file))
                            newPackages.Add(package.AppRef, new AppPackageInfo(package.AppRef,
                                                                              Path.GetFileName(file),
                                                                              file,
                                                                              package.MD5,
                                                                              size,
                                                                              File.GetLastWriteTimeUtc(file)));
                    }
                    catch (FileNotFoundException e)
                    {
                        SysLog.LogErrorStackDump("{0}", e.Message);
                    }
                    catch (DirectoryNotFoundException e)
                    {
                        SysLog.LogErrorStackDump("{0}", e.Message);
                    }
                    catch (IOException e)
                    {
                        // I've run into trouble where we've gotten notifications from the
                        // file watcher while a new file is still locked for writing.  In
                        // this situation, I'm going to wait a bit and then retry the
                        // operation once.

                        if (!isRetry)
                        {
                            Thread.Sleep(RetryTime);
                            isRetry = true;
                            goto retry;
                        }

                        SysLog.LogErrorStackDump("{0}", e.Message);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogErrorStackDump("Error [{0}] loading application package [{1}].", e.Message, file);
                    }
                }

                // Determine if there have been any changes to the set of
                // application packages and then update the cached package set 
                // and then raise the change event if there were any changes.

                bool changed = false;

                using (TimedLock.Lock(syncLock))
                {
                    foreach (var info in newPackages.Values)
                    {
                        if (!packages.ContainsKey(info.AppRef))
                        {
                            changed = true;
                            break;
                        }
                        else
                        {
                            var orgInfo = packages[info.AppRef];

                            if (info.FileName != orgInfo.FileName ||
                                !Helper.ArrayEquals(info.MD5, orgInfo.MD5))
                            {
                                changed = true;
                                break;
                            }
                        }
                    }

                    if (!changed)
                        foreach (AppPackageInfo info in packages.Values)
                            if (!newPackages.ContainsKey(info.AppRef))
                            {
                                changed = true;
                                break;
                            }

                    if (changed)
                        packages = newPackages;
                }

                if (changed)
                    RaiseChangeEvent();
            }
            finally
            {
                Interlocked.Decrement(ref scanning);
            }
        }

        /// <summary>
        /// Deletes files in the transit folder that appear to have been abandoned.
        /// </summary>
        public void Purge()
        {
            // Delete all transit files that haven't been touched for 1 day.

            Helper.CreateFileTree(Helper.StripTrailingSlash(transitFolder));

            foreach (string file in Helper.GetFilesByPattern(transitFolder + "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    if (DateTime.UtcNow - File.GetLastWriteTime(file) >= TimeSpan.FromDays(1))
                        File.Delete(file);
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        /// <summary>
        /// Deletes a package from the folder if it is present.
        /// </summary>
        /// <param name="appRef">The package <see cref="AppRef" />.</param>
        public void Remove(AppRef appRef)
        {
            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            AppPackageInfo  info;
            bool            deleted = false;

            using (TimedLock.Lock(syncLock))
            {
                if (packages.TryGetValue(appRef, out info))
                {
                    try
                    {
                        fileWatcher.EnableRaisingEvents = false;
                        File.Delete(info.FullPath);
                        packages.Remove(appRef);
                        deleted = true;
                    }
                    finally
                    {
                        fileWatcher.EnableRaisingEvents = true;
                    }
                }
            }

            if (deleted)
                RaiseChangeEvent();
        }

        /// <summary>
        /// Removes all packages from the folder.
        /// </summary>
        public void Clear()
        {
            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            bool deleted = false;

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    deleted = packages.Count > 0;
                    packages.Clear();
                    Helper.DeleteFile(packageFolder + "*.zip");
                }
            }
            catch (IOException)
            {
                // Ignore I/O errors which are probably due to scanning on 
                // another thread.
            }

            if (deleted)
                RaiseChangeEvent();
        }

        /// <summary>
        /// Initiates the loading of an application package file into the folder.
        /// </summary>
        /// <param name="appRef">The new application package's <see cref="AppRef" />.</param>
        /// <returns>The fully qualified path to where the new package should be written.</returns>
        public string BeginTransit(AppRef appRef)
        {
            string path;

            if (isDisposed)
                throw new ObjectDisposedException(typeof(AppPackageFolder).Name);

            path = transitFolder + appRef.FileName;
            Helper.CreateFileTree(path);

            return path;
        }

        /// <summary>
        /// Completes the loading of an application package file.
        /// </summary>
        /// <param name="path">The fully qualified path returned by <see cref="BeginTransit" />.</param>
        /// <param name="commit">
        /// Pass <c>true</c> to commit the package to the folder, <c>false</c> to 
        /// delete the transit file without committing it.
        /// </param>
        public void EndTransit(string path, bool commit)
        {
            if (commit)
            {
                AppPackage      package;
                AppPackageInfo  info;
                int             size;
                string          destPath;

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    size = (int)fs.Length;

                using (TimedLock.Lock(syncLock))
                {
                    try
                    {
                        fileWatcher.EnableRaisingEvents = false;

                        destPath = packageFolder + Path.GetFileName(path);
                        if (File.Exists(destPath))
                            File.Delete(destPath);

                        File.Move(path, destPath);

                        package = null;

                        try
                        {
                            package = AppPackage.Open(destPath);
                            info = new AppPackageInfo(package.AppRef, Path.GetFileName(destPath),
                                                      destPath, package.MD5, size, File.GetLastWriteTimeUtc(destPath));
                        }
                        catch
                        {
                            if (package == null)
                            {
                                // The package must be bad so delete it.

                                Helper.DeleteFile(destPath);
                            }

                            throw;
                        }
                        finally
                        {
                            if (package != null)
                                package.Close();
                        }

                        packages[info.AppRef] = info;
                    }
                    finally
                    {
                        fileWatcher.EnableRaisingEvents = true;
                    }
                }

                RaiseChangeEvent();
            }
            else
                Helper.DeleteFile(path);
        }

        /// <summary>
        /// Raises <see cref="ChangeEvent" />.
        /// </summary>
        private void RaiseChangeEvent()
        {
            if (ChangeEvent != null)
                ChangeEvent(this);
        }

        /// <summary>
        /// Handles queued scan operations.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnQueuedScan(object state)
        {
            if (isDisposed)
                return;

            Thread.Sleep(RetryTime);    // Give the I/O operation a fighting chance to complete
            InternalScan();
            RaiseChangeEvent();
        }

        /// <summary>
        /// Called by the file watcher when something changes in the package folder.
        /// </summary>
        /// <param name="source">The file watcher.</param>
        /// <param name="args">The event arguments.</param>
        private void OnChange(object source, FileSystemEventArgs args)
        {
            if (isDisposed)
                return;

            // Don't handle scanning on the file watcher thread.

            Helper.UnsafeQueueUserWorkItem(new WaitCallback(OnQueuedScan), null);
        }

        /// <summary>
        /// Called by the file watcher when something changes in the package folder.
        /// </summary>
        /// <param name="sender">The file watcher.</param>
        /// <param name="args">The event arguments.</param>
        private void OnRename(object sender, RenamedEventArgs args)
        {
            if (isDisposed)
                return;

            // Don't handle scanning on the file watcher thread.

            Helper.UnsafeQueueUserWorkItem(new WaitCallback(OnQueuedScan), null);
        }
    }
}
