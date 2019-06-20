//-----------------------------------------------------------------------------
// FILE:        IPGeocoder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles IP address to GeoFix lookups as well as management of the
//              source database downloading.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;

// $todo(jeff.lill):
//
// At some point it would be nice to implement a provider model around the IP-Geocoding
// database rather than hardcoding this to MaxMind.

namespace LillTek.GeoTracker.Server
{
    /// <summary>
    /// Handles IP address to <see cref="GeoFix" /> lookups as well as management of the
    /// source database downloading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used by <see cref="GeoTrackerNode" /> to manage the downloading of the
    /// GeoIP City or GeoLite City database files from an external website and then to perform
    /// IP address to location mappins.  The database files are published monthly by 
    /// <a href="http://maxmind.com">MaxMind.com</a> and need to be manually downloaded and 
    /// decompressed before being uploaded to the website.  The file URI defaults to a location
    /// on <a href="http://www.lilltek.com">www.LillTek.com</a> but this can be customized in 
    /// the server configuration settings.
    /// </para>
    /// <para>
    /// The constructor creates a background thread which runs until <see cref="Stop" />
    /// is called.  This thread polls the source website periodically for updated database
    /// files using the HTTP <b>If-Modified-Since</b> header.
    /// </para>
    /// <para>
    /// The local database file will be saved in the <b>CommonApplicationData\LillTek\GeoTracker</b> folder.  
    /// The current database file will be named <b>IP2City.dat</b> and the temporary database file
    /// will be named <b>IP2City.dat.tmp</b> while it is being downloaded and verified.  The create 
    /// and modify dates of the database file will be set to the <b>Last-Modified</b> date returned 
    /// when the file is downloaded.  This date will be used for future polling requests.
    /// </para>
    /// <para>
    /// IP address to location mapping can be performed by calling <see cref="MapIPAddress" />.
    /// </para>
    /// <note>
    /// The class will not start a background thread if IP geocoding is disabled.
    /// </note>
    /// </remarks>
    internal class IPGeocoder
    {
        //---------------------------------------------------------------------
        // Static members

        private static string dataFolder    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LillTek\\GeoTracker");
        private static string dataPath      = Path.Combine(dataFolder, "IP2City.dat");
        private static string downloadPath  = Path.Combine(dataFolder, "IP2City.download.dat");
        private static string decryptedPath = Path.Combine(dataFolder, "IP2City.decrypted.dat");

        //---------------------------------------------------------------------
        // Instance members

        private object                      syncLock = new object();
        private GeoTrackerServerSettings    settings;
        private Thread                      downloadThread;
        private bool                        running;
        private bool                        stopPending;
        private bool                        pollDataNow;
        private LookupService               maxMind;

        /// <summary>
        /// Constructs the instance and starts the background downloading thread.
        /// </summary>
        /// <param name="node">The parent <see cref="GeoTrackerNode" /> instance.</param>
        /// <remarks>
        /// <note>
        /// The constructor will not start a background thread if IP geocoding is disabled.
        /// </note>
        /// </remarks>
        public IPGeocoder(GeoTrackerNode node)
        {
            settings = node.Settings;

            if (!settings.IPGeocodeEnabled)
                return;

            // Initialize the service including loading the MaxMind database if present.

            running     = true;
            stopPending = false;
            pollDataNow = false;
            maxMind     = null;

            try
            {
                if (File.Exists(dataPath))
                {
                    maxMind = new LookupService(dataPath, LookupService.GEOIP_MEMORY_CACHE);
                    maxMind.close();
                }
            }
            catch (Exception e)
            {
                // Assume that the database file is corrupted if there's an exception
                // and delete it so the download thread will download a new copy.

                SysLog.LogException(e);
                SysLog.LogError("GeoTracker: The MaxMind database file [{0}] appears to be corrupted.  This will be deleted so the downloader can get a fresh copy.", dataPath);

                Helper.DeleteFile(dataPath);
            }

            // Start the background downloader thread.

            downloadThread      = new Thread(new ThreadStart(DownloadThread));
            downloadThread.Name = "GeoTracker: GeoData Downloader";
            downloadThread.Start();
        }

        /// <summary>
        /// Stops the downloader.
        /// </summary>
        public void Stop()
        {
            if (!running)
                return;

            stopPending = true;

            // Waits up to a minute for the thread to stop before aborting
            // it if necessary.

            try
            {
                if (StopImmediately)
                {
                    downloadThread.Abort();
                    return;
                }
                else
                    Helper.WaitFor(() => !running,TimeSpan.FromMinutes(1));
            }
            catch
            {
                downloadThread.Abort();
                running = false;
            }
        }

        /// <summary>
        /// Implements the background thread.
        /// </summary>
        private void DownloadThread()
        {
            DateTime    lastWarningTime = DateTime.MinValue;
            PolledTimer pollTimer;
            bool        resetTimer;

            try
            {
                // Initialize the GeoTracker file folder

                try
                {
                    Helper.CreateFileTree(dataPath);

                    if (File.Exists(downloadPath))
                    {
                        SysLog.LogWarning("GeoTracker: Deleting existing temporary [{0}] file on startup.", downloadPath);
                        Helper.DeleteFile(downloadPath);
                    }

                    if (File.Exists(decryptedPath))
                    {
                        SysLog.LogWarning("GeoTracker: Deleting existing temporary [{0}] file on startup.", decryptedPath);
                        Helper.DeleteFile(decryptedPath);
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                // Initalize the poll timer.  We'll schedule an immediate download if the data file does
                // not exist, otherwise we'll delay the polling for a random period of time between
                // 0 and 15 minutes in the hope that we'll end up staggering the polling times across
                // the server cluster (so we won't hammer the source website).

                pollTimer  = new PolledTimer(settings.IPGeocodeSourcePollInterval, false);
                resetTimer = false;

                if (!File.Exists(dataPath))
                    pollTimer.FireNow();
                else
                    pollTimer.ResetRandomTemporary(TimeSpan.Zero, TimeSpan.FromMinutes(15));

                // The polling loop.

                while (true)
                {
                    if (stopPending)
                        return;

                    try
                    {
                        if (pollDataNow)
                        {
                            pollTimer.FireNow();
                            pollDataNow = false;
                        }

                        if (pollTimer.HasFired)
                        {

                            DateTime        fileDateUtc = DateTime.UtcNow;
                            bool            isUpdate    = false;
                            double          fileSize    = 0;
                            ElapsedTimer    downloadTimer;
                            HttpWebRequest  request;
                            HttpWebResponse response;
                            HttpStatusCode  statusCode;

                            resetTimer = true;

                            // If a database file already exists then extract its last modify
                            // date and use this in an If-Modified-Since request to the source
                            // website to see if there's an updated file.

                            if (File.Exists(dataPath))
                            {
                                request = (HttpWebRequest)WebRequest.Create(settings.IPGeocodeSourceUri);
                                request.Timeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

                                isUpdate = true;
                                fileDateUtc = File.GetLastWriteTimeUtc(dataPath);

                                request.Method = "HEAD";
                                request.IfModifiedSince = fileDateUtc;

                                try
                                {
                                    using (response = (HttpWebResponse)request.GetResponse())
                                        statusCode = response.StatusCode;
                                }
                                catch (WebException e)
                                {
                                    statusCode = ((HttpWebResponse)e.Response).StatusCode;
                                }

                                if (statusCode == HttpStatusCode.NotModified)
                                {
                                    // The source website does not have an updated file.  I'm going to
                                    // do one extra check to see if the file we have is more than 45 
                                    // days old and log a warning.  Note that we're going to issue this
                                    // warning only once a week while the service is running.

                                    if (DateTime.UtcNow - fileDateUtc < TimeSpan.FromDays(45) || DateTime.UtcNow - lastWarningTime >= TimeSpan.FromDays(7))
                                        continue;

                                    lastWarningTime = DateTime.UtcNow;

                                    const string warning =
@"GeoTracker: The local copy of the MaxMind GeoIP City or GeoLite City database is [{0}] days old 
and should be updated.  You may need to download a new copy of the database from http://maxmind.com,
decompress it and upload it to the source website at [{1}].

Note: Make sure that the website is configured with the [.DAT=application/octet-stream] MIME mapping.";

                                    SysLog.LogWarning(warning, (int)(DateTime.UtcNow - fileDateUtc).TotalDays, settings.IPGeocodeSourceUri);
                                    continue;
                                }
                            }

                            // Download the database to the temporary download file.

                            Helper.DeleteFile(downloadPath);

                            downloadTimer = new ElapsedTimer(true);
                            fileSize = Helper.WebDownload(settings.IPGeocodeSourceUri, downloadPath, settings.IPGeocodeSourceTimeout, out response);
                            downloadTimer.Stop();

                            // Set the file times to match the Last-Modified header received from the website (it any).

                            string lastModified = response.Headers["Last-Modified"];

                            if (lastModified != null)
                            {
                                try
                                {
                                    fileDateUtc = Helper.ParseInternetDate(lastModified);
                                    File.SetCreationTimeUtc(downloadPath, fileDateUtc);
                                    File.SetLastWriteTimeUtc(downloadPath, fileDateUtc);
                                }
                                catch (Exception e)
                                {
                                    SysLog.LogException(e, "GeoTracker: Website for [{0}] returned invalid Last-Modified header [{1}].",
                                                          settings.IPGeocodeSourceUri, lastModified);
                                }
                            }

                            // Decrypt the file and set its file dates.

                            var keyChain = new KeyChain(settings.IPGeocodeSourceRsaKey);

                            using (var secureFile = new SecureFile(downloadPath, keyChain))
                            {
                                secureFile.DecryptTo(decryptedPath);
                            }

                            File.SetCreationTimeUtc(decryptedPath, fileDateUtc);
                            File.SetLastWriteTimeUtc(decryptedPath, fileDateUtc);

                            // Verify the decrypted data file and then swap in new file.

                            const string info =
@"GeoTracker: {0} of IP-to-location database from [{1}] completed.
Downloaded [{2:#.#}MB] bytes in [{3}].";

                            SysLog.LogInformation(info, isUpdate ? "Update download" : "Initial download", settings.IPGeocodeSourceUri, fileSize / (1024 * 1024), downloadTimer.ElapsedTime);

                            // Create a new MaxMind lookup intance and then swap it in without interrupting
                            // any queries in progress.

                            try
                            {
                                LookupService newMaxMind;

                                newMaxMind = new LookupService(decryptedPath, LookupService.GEOIP_MEMORY_CACHE);
                                newMaxMind.close();

                                maxMind = newMaxMind;
                                UpdateCount++;
                            }
                            catch (Exception e)
                            {

                                SysLog.LogException(e);
                                SysLog.LogError("GeoTracker: The MaxMind downloaded database file [{0}] appears to be corrupted.  This will be deleted so the downloader can get a fresh copy.", downloadPath);
                            }

                            lock (syncLock)
                            {
                                Helper.DeleteFile(dataPath);
                                File.Copy(decryptedPath, dataPath);
                                File.SetCreationTimeUtc(dataPath, fileDateUtc);
                                File.SetLastWriteTimeUtc(dataPath, fileDateUtc);
                            }

                            // Delete the temporary files.

                            Helper.DeleteFile(decryptedPath);
                            Helper.DeleteFile(downloadPath);
                        }
                    }
                    catch (WebException e)
                    {
                        SysLog.LogException(e);
                        SysLog.LogWarning("GeoTracker: The download of the MaxMind database file has failed. The service will try again in 1 minute.");

                        pollTimer.ResetTemporary(TimeSpan.FromMinutes(1));
                        resetTimer = false;
                    }
                    catch (ThreadAbortException e)
                    {
                        SysLog.LogException(e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                    finally
                    {
                        if (resetTimer)
                        {
                            resetTimer = false;
                            pollTimer.Reset();
                        }
                    }

                    Thread.Sleep(settings.BkInterval);
                }
            }
            finally
            {
                running = false;
            }
        }

        /// <summary>
        /// Attempts to map an <see cref="IPAddress" /> to a <see cref="GeoFix" />.
        /// </summary>
        /// <param name="address">The input address.</param>
        /// <returns>
        /// The <see cref="GeoFix" /> instance if one could be mapped or 
        /// <c>null</c> if the address could not be mapped to a location.
        /// </returns>
        /// <exception cref="NotAvailableException">Thrown if IP geocoding has been disabled or of the gecoder has does not have a local database or if it has been stopped.</exception>
        /// <exception cref="NotSupportedException">Thrown for non-IPv4 addresses.</exception>
        public GeoFix MapIPAddress(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException(string.Format("GeoTracker: [{0}] network addresses cannot be geocoded. Only IPv4 addresses are supported.", address.AddressFamily));

            if (!running || maxMind == null)
                throw new NotAvailableException("GeoTracker: IP Geocoder is disabled or stopped.");

            var location = maxMind.getLocation(address);

            if (location == null)
                return null;

            return new GeoFix()
            {
                TimeUtc    = DateTime.UtcNow,
                Technology = GeoFixTechnology.IP,
                Latitude   = location.latitude,
                Longitude  = location.longitude
            };
        }

        //---------------------------------------------------------------------
        // Unit test related extensions.

        /// <summary>
        /// <b>Unit test only:</b> Returns the path to the IP-to-location data file.
        /// </summary>
        internal static string DataPath 
        {
            get { return dataPath; }
        }

        /// <summary>
        /// <b>Unit test only:</b> Returns the path to the IP-to-location temporary download file.
        /// </summary>
        internal static string DownloadPath
        {
            get { return downloadPath; }
        }

        /// <summary>
        /// <b>Unit test only:</b> Returns the path to the IP-to-location temporary decrypted file.
        /// </summary>
        internal static string DecryptedPath 
        {
            get { return decryptedPath; }
        }

        /// <summary>
        /// <b>Unit test only:</b> Returns the last write time (UTC) or <see cref="DateTime.MinValue" /> if the
        /// file does not exist.
        /// </summary>
        internal DateTime DataFileDateUtc 
        {
            get 
            {
                lock (syncLock) 
                {
                    try 
                    {
                        return File.GetLastWriteTimeUtc(dataPath);
                    }
                    catch 
                    {
                        return DateTime.MinValue;
                    }
                }
            }

            set 
            {
                lock (syncLock) 
                {
                    File.SetCreationTimeUtc(IPGeocoder.DataPath,value);
                    File.SetLastWriteTimeUtc(IPGeocoder.DataPath,value);
                    File.SetLastAccessTimeUtc(IPGeocoder.DataPath,value);
                }
            }
        }

        /// <summary>
        /// <b>Unit test only:</b> Prods the geocoder to immediately poll the source website for updated data files.
        /// </summary>
        internal void PollForUpdates() 
        {
            pollDataNow = true;
        }

        /// <summary>
        /// <b>Unit test only:</b> Counts the number of times the Maxmind database has been downloaded since the
        /// class was instantiated.
        /// </summary>
        internal int UpdateCount { get; set; }

        /// <summary>
        /// <b>Unit test only:</b> Indicates that <see cref="Stop" /> should abort the download thread immediately,
        /// without waiting for it to terminate cleanly.  This is useful for unit tests that
        /// might have a download in progress they need to stop when the test terminates.
        /// </summary>
        internal bool StopImmediately { get; set; }
    }
}
