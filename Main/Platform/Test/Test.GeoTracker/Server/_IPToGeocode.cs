//-----------------------------------------------------------------------------
// FILE:        _IPToGeocode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _IPToGeocode
    {
        private IPAddress testIP = IPAddress.Parse("72.14.213.147");  // Google IP address in MountainView, CA
        private string exteralDataPath = EnvironmentVars.Expand(@"$(LT_ROOT)\External\MaxMind\IP2City\IP2City.dat");
        private LeafRouter router;
        private GeoTrackerClient client;
        private GeoTrackerNode server;

        private void TestInit(bool enable, bool copyDatabase)
        {
            const string cfg =
@"
MsgRouter.AppName               = Test
MsgRouter.AppDescription        = Test Description
MsgRouter.DiscoveryMode         = MULTICAST
MsgRouter.RouterEP				= physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
MsgRouter.CloudEP    			= $(LillTek.DC.CloudEP)
MsgRouter.CloudAdapter    		= ANY
MsgRouter.UdpEP					= ANY:0
MsgRouter.TcpEP					= ANY:0
MsgRouter.TcpBacklog			= 100
MsgRouter.TcpDelay				= off
MsgRouter.BkInterval			= 1s
MsgRouter.MaxIdle				= 5m
MsgRouter.EnableP2P             = yes
MsgRouter.AdvertiseTime			= 1m
MsgRouter.DefMsgTTL				= 5
MsgRouter.SharedKey 			= PLAINTEXT
MsgRouter.SessionCacheTime      = 2m
MsgRouter.SessionRetries        = 3
MsgRouter.SessionTimeout        = 10s
";

            Config.SetConfig(cfg);
            router = new LeafRouter();
            router.Start();
            Thread.Sleep(1000);

            client = new GeoTrackerClient(router, null);

            var serverSettings = new GeoTrackerServerSettings()
            {

                IPGeocodeEnabled = enable,
                IPGeocodeSourceTimeout = TimeSpan.FromMinutes(2),
            };

            if (copyDatabase)
            {
                Helper.DeleteFile(IPGeocoder.DataPath);
                File.Copy(exteralDataPath, IPGeocoder.DataPath);
            }

            server = new GeoTrackerNode();
            server.Start(router, serverSettings, null, null);
            Thread.Sleep(2000);
        }

        private void TestCleanup()
        {
            client = null;

            if (server != null)
            {
                server.Stop();
                server = null;
            }

            if (router != null)
            {
                router.Stop();
                router = null;
            }

            Helper.DeleteFile(IPGeocoder.DataPath);
            Helper.DeleteFile(IPGeocoder.DownloadPath);
            Helper.DeleteFile(IPGeocoder.DecryptedPath);
        }

        private void VerifyTestFix(GeoFix fix)
        {
            Assert.IsNotNull(fix);

            // The lat/lon may change.  I got these values from visiting http://maxmind.com and
            // performing a manual lookup on the test IP address.  These coordinates are
            // for Google in MountainView, CA.

            Assert.AreEqual(37.4192, Math.Round(fix.Latitude, 4, MidpointRounding.AwayFromZero));
            Assert.AreEqual(-122.0574, Math.Round(fix.Longitude, 4, MidpointRounding.AwayFromZero));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Disabled()
        {
            // Verify that the server returns a NotAvailableException if IPGeoCoding is disabled.

            try
            {
                TestInit(false, false);

                client.IPToGeoFix(IPAddress.Parse("72.14.213.147"));
                Assert.Fail("NotAvailableException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(NotAvailableException));
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_DownloadData()
        {
            // Delete any existing data files and then start up a GeoTrackerNode and verify that 
            // it downloads the data file correctly.

            try
            {
                Helper.DeleteFile(IPGeocoder.DataPath);
                Helper.DeleteFile(IPGeocoder.DownloadPath);
                Helper.DeleteFile(IPGeocoder.DecryptedPath);

                TestInit(true, false);

                try
                {
                    Helper.WaitFor(() => File.Exists(IPGeocoder.DataPath), TimeSpan.FromMinutes(3));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("IPGeocoder failed to download data file within 3 minutes.");
                }

                // Temporary file should have been deleted

                Thread.Sleep(3000);     // Wait a bit to allow the temp file to be deleted
                Assert.IsFalse(File.Exists(IPGeocoder.DownloadPath));

                // Verify that the timestamps on the data file match the Last-Modified header
                // returned by the source website.

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(server.Settings.IPGeocodeSourceUri);
                DateTime lastModifiedUtc;

                request.Method = "HEAD";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                    lastModifiedUtc = Helper.ParseInternetDate(response.Headers["Last-Modified"]);
                    Assert.AreEqual(lastModifiedUtc, File.GetCreationTimeUtc(IPGeocoder.DataPath));
                    Assert.AreEqual(lastModifiedUtc, File.GetLastWriteTimeUtc(IPGeocoder.DataPath));
                }
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_DownloadData_TempExists()
        {
            // Delete any existing data file and create a temporary downloaded and 
            // encrypted files to verify that service can overwrite the temp files
            // while performing a new download.

            try
            {
                Helper.DeleteFile(IPGeocoder.DataPath);
                Helper.DeleteFile(IPGeocoder.DownloadPath);
                Helper.DeleteFile(IPGeocoder.DecryptedPath);
                File.WriteAllText(IPGeocoder.DownloadPath, "Hello World!");
                File.WriteAllText(IPGeocoder.DecryptedPath, "Hello World!");

                TestInit(true, false);

                try
                {
                    Helper.WaitFor(() => File.Exists(IPGeocoder.DataPath), TimeSpan.FromMinutes(3));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("IPGeocoder failed to download data file within 3 minutes.");
                }

                // Temporary file should have been deleted

                Thread.Sleep(3000);     // Wait a bit to allow the temp file to be deleted
                Assert.IsFalse(File.Exists(IPGeocoder.DownloadPath));

                // Verify that the timestamps on the data file match the Last-Modified header
                // returned by the source website.

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(server.Settings.IPGeocodeSourceUri);
                DateTime lastModifiedUtc;

                request.Method = "HEAD";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                    lastModifiedUtc = Helper.ParseInternetDate(response.Headers["Last-Modified"]);
                    Assert.AreEqual(lastModifiedUtc, File.GetCreationTimeUtc(IPGeocoder.DataPath));
                    Assert.AreEqual(lastModifiedUtc, File.GetLastWriteTimeUtc(IPGeocoder.DataPath));
                }
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_UpdateData_Startup()
        {
            // Verify that the service will update an existing data file upon startup by:
            // 
            //     1. Copying the data file from $(LT_ROOT)\External\MaxMind\IP2City\IP2City.dat
            //     2. Setting the file date to something far into the past
            //     3. Then starting the service and waiting to confirm that a new file was downloaded

            try
            {
                Helper.DeleteFile(IPGeocoder.DataPath);
                Helper.DeleteFile(IPGeocoder.DownloadPath);
                Helper.DeleteFile(IPGeocoder.DecryptedPath);
                File.Copy(exteralDataPath, IPGeocoder.DataPath);

                var oldDate = new DateTime(2000, 1, 1);

                File.SetCreationTimeUtc(IPGeocoder.DataPath, oldDate);
                File.SetLastWriteTimeUtc(IPGeocoder.DataPath, oldDate);
                File.SetLastAccessTimeUtc(IPGeocoder.DataPath, oldDate);

                TestInit(true, false);
                server.IPGeocoder.PollForUpdates();

                try
                {
                    Helper.WaitFor(
                        () =>
                        {
                            try
                            {
                                return server.IPGeocoder.DataFileDateUtc > oldDate;
                            }
                            catch
                            {
                                return false;
                            }
                        },
                        TimeSpan.FromMinutes(3));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("IPGeocoder failed to download data new file within 3 minutes.");
                }

                // Temporary file should have been deleted

                Thread.Sleep(3000);     // Wait a bit to allow the temp file to be deleted
                Assert.IsFalse(File.Exists(IPGeocoder.DownloadPath));

                // Verify that the timestamps on the data file match the Last-Modified header
                // returned by the source website.

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(server.Settings.IPGeocodeSourceUri);
                DateTime lastModifiedUtc;

                request.Method = "HEAD";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                    lastModifiedUtc = Helper.ParseInternetDate(response.Headers["Last-Modified"]);
                    Assert.AreEqual(lastModifiedUtc, File.GetCreationTimeUtc(IPGeocoder.DataPath));
                    Assert.AreEqual(lastModifiedUtc, File.GetLastWriteTimeUtc(IPGeocoder.DataPath));
                }
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_UpdateData_Running()
        {
            // Verify that the service will update an existing data file after running for a while:
            // 
            //     1. Copying the data file from $(LT_ROOT)\External\MaxMind\IP2City\IP2City.dat
            //     2. Setting the file date to something far into the past
            //     3. Then starting the service and waiting to confirm that a new file was downloaded

            try
            {
                Helper.DeleteFile(IPGeocoder.DataPath);
                Helper.DeleteFile(IPGeocoder.DownloadPath);
                Helper.DeleteFile(IPGeocoder.DecryptedPath);
                File.Copy(exteralDataPath, IPGeocoder.DataPath);

                var oldDate = new DateTime(2000, 1, 1);
                var now = DateTime.UtcNow;

                File.SetCreationTimeUtc(IPGeocoder.DataPath, now);
                File.SetLastWriteTimeUtc(IPGeocoder.DataPath, now);
                File.SetLastAccessTimeUtc(IPGeocoder.DataPath, now);

                TestInit(true, false);
                Thread.Sleep(5000);    // Give the server a chance to start

                server.IPGeocoder.DataFileDateUtc = oldDate;
                server.IPGeocoder.PollForUpdates();

                try
                {
                    Helper.WaitFor(
                        () =>
                        {
                            try
                            {
                                return server.IPGeocoder.DataFileDateUtc > oldDate;
                            }
                            catch
                            {
                                return false;
                            }
                        },
                        TimeSpan.FromMinutes(3));
                }
                catch (TimeoutException)
                {
                    Assert.Fail("IPGeocoder failed to download data updated file within 3 minutes.");
                }

                // Temporary file should have been deleted

                Thread.Sleep(3000);     // Wait a bit to allow the temp file to be deleted
                Assert.IsFalse(File.Exists(IPGeocoder.DownloadPath));

                // Verify that the timestamps on the data file match the Last-Modified header
                // returned by the source website.

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(server.Settings.IPGeocodeSourceUri);
                DateTime lastModifiedUtc;

                request.Method = "HEAD";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                    lastModifiedUtc = Helper.ParseInternetDate(response.Headers["Last-Modified"]);
                    Assert.AreEqual(lastModifiedUtc, File.GetCreationTimeUtc(IPGeocoder.DataPath));
                    Assert.AreEqual(lastModifiedUtc, File.GetLastWriteTimeUtc(IPGeocoder.DataPath));
                }
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Query_Single()
        {
            // Perform a synchronous query to look up of a valid Google IP address.

            try
            {
                TestInit(true, true);

                var fix = client.IPToGeoFix(testIP);

                VerifyTestFix(fix);
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Query_Lynnwood()
        {
            // Perform a synchronous query to look up of my Lynnwood/Frontier home IP address
            // just to see what we get back.

            try
            {
                TestInit(true, true);

                var fix = client.IPToGeoFix(IPAddress.Parse("50.46.137.105"));

                // Note: 
                //
                // The open source GeoLite City database didn't return much here except that
                // the IP address is in the US.  The $370 GeoIP database returned much more
                // data including the location as Lynnwood and Frontier as the ISP.

                Debug.WriteLine("Home Fix: {0}", fix);
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Query_AsyncSingle()
        {
            // Perform a asynchronous query to look up a valid Google IP address.

            try
            {
                TestInit(true, true);

                IAsyncResult ar;
                GeoFix fix;
                bool gotCallback = false;

                ar = client.BeginIPToGeoFix(testIP, ar2 => { gotCallback = true; }, "Test");
                fix = client.EndIPToGeoFix(ar);

                Assert.IsTrue(gotCallback);
                Assert.AreEqual("Test", ar.AsyncState);
                VerifyTestFix(fix);
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Query_AsyncBlast()
        {
            // Blast 100,000 parallel async queries at the the service
            // in waves of a 1000 queries at a time.

            try
            {
                TestInit(true, true);

                int cQueries = 100000;
                int cWave = 1000;
                int cCompleted = 0;
                bool fail = false;
                ElapsedTimer timer = new ElapsedTimer();
                double rate;

                for (int i = 0; i < cQueries / cWave; i++)
                {
                    cCompleted = 0;
                    timer.Start();

                    for (int j = 0; j < cWave; j++)
                    {
                        client.BeginIPToGeoFix(testIP,
                            ar =>
                            {
                                var c = Interlocked.Increment(ref cCompleted);

                                try
                                {
                                    VerifyTestFix(client.EndIPToGeoFix(ar));
                                }
                                catch
                                {
                                    fail = true;
                                }

                                if (cCompleted == cWave)
                                    timer.Stop();
                            },
                            null);
                    }

                    Helper.WaitFor(() => cCompleted == cWave, TimeSpan.FromSeconds(60));
                }

                rate = cQueries / timer.ElapsedTime.TotalSeconds;
                Debug.WriteLine("{0} seconds elapsed for {1} queries", timer.ElapsedTime, cQueries);
                Debug.WriteLine("Query Rate: {0}/sec", rate);

                if (fail)
                    Assert.Fail("One or more of the query operations failed.");
            }
            finally
            {
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_UpdateData_WithQueries()
        {
            // Blast 100,000 parallel async queries at the the service
            // in waves of a 1000 queries at a time and during the middle of
            // this activity, prod the server to download a new database file
            // as often as possible.

            try
            {
                TestInit(true, true);

                int cQueries = 100000;
                int cWave = 1000;
                int cCompleted = 0;
                bool fail = false;
                ElapsedTimer timer = new ElapsedTimer();
                int lastUpdateCount = 0;
                double rate;

                for (int i = 0; i < cQueries / cWave; i++)
                {
                    cCompleted = 0;

                    timer.Start();

                    for (int j = 0; j < cWave; j++)
                    {
                        client.BeginIPToGeoFix(testIP,
                            ar =>
                            {
                                var c = Interlocked.Increment(ref cCompleted);

                                try
                                {
                                    VerifyTestFix(client.EndIPToGeoFix(ar));
                                }
                                catch
                                {
                                    fail = true;
                                }

                                if (cCompleted == cWave)
                                    timer.Stop();
                            },
                            null);
                    }

                    Helper.WaitFor(() => cCompleted == cWave, TimeSpan.FromSeconds(60));

                    // Prod the server the first time through and then after the server
                    // has completed downloading the previous update.

                    if (i == 0 || server.IPGeocoder.UpdateCount > lastUpdateCount)
                    {
                        var oldDate = new DateTime(2000, 1, 1);

                        try
                        {
                            File.SetCreationTimeUtc(IPGeocoder.DataPath, oldDate);
                            File.SetLastWriteTimeUtc(IPGeocoder.DataPath, oldDate);
                            File.SetLastAccessTimeUtc(IPGeocoder.DataPath, oldDate);

                            lastUpdateCount = server.IPGeocoder.UpdateCount;
                            server.IPGeocoder.PollForUpdates();
                        }
                        catch (IOException)
                        {
                            // I'm going to ignore I/O errors because there's a decent
                            // chance that the data file may be open in the geocoder's
                            // download thread.
                        }
                    }
                }

                rate = cQueries / timer.ElapsedTime.TotalSeconds;
                Debug.WriteLine("{0} seconds elapsed for {1} queries", timer.ElapsedTime, cQueries);
                Debug.WriteLine("Query Rate: {0}/sec", rate);

                if (fail)
                    Assert.Fail("One or more of the query operations failed.");
            }
            finally
            {
                server.IPGeocoder.StopImmediately = true;
                TestCleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void IPToGeocode_Query_Private()
        {
            // Perform a synchronous queries to look up a private IP addresses.  These should fail.

            try
            {
                TestInit(true, true);

                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("10.1.2.3")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("192.168.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.16.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.17.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.18.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.19.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.20.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.21.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.22.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.23.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.24.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.25.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.26.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.27.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.28.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.29.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.30.1.1")));
                Assert.IsNull(client.IPToGeoFix(IPAddress.Parse("172.31.1.1")));
            }
            finally
            {
                TestCleanup();
            }
        }
    }
}

