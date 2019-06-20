//-----------------------------------------------------------------------------
// FILE:        _EntityFixes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Data;
using LillTek.Data.Install;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Server;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _Archivers
    {

        private const string RouterConfig =
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

        private string AppLogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\AppLogs\GeoFix");
        private LeafRouter router;
        private GeoTrackerClient client;
        private GeoTrackerNode server;

        private void TestInit(string appConfig)
        {
            Config.SetConfig((RouterConfig + appConfig).Replace('&', '#'));
            router = new LeafRouter();
            router.Start();

            client = new GeoTrackerClient(router, null);

            var serverSettings = GeoTrackerServerSettings.LoadConfig("LillTek.GeoTracker.Server");

            serverSettings.IPGeocodeEnabled = false;

            server = new GeoTrackerNode();
            server.Start(router, serverSettings, null, null);
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
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_AppLogArchiver_Single()
        {
            // Submit a single location fix to a GeoTrackerNode configured with an AppLog archiver
            // and then verify that the fix was persisted to the archive.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.AppLogGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        LogName = GeoFix
    }}

&endsection
";

            try
            {
                Helper.DeleteFile(AppLogFolder, true);
                TestInit(appConfig);

                client.SubmitEntityFix("jeff", "group", new GeoFix() { Latitude = 10, Longitude = 20 });
                server.Stop();

                AppLogReader logReader = AppLogReader.Open("GeoFix");
                AppLogRecord record;

                try
                {
                    record = logReader.ReadDelete();

                    Assert.IsNotNull(record);
                    Assert.AreEqual(AppLogGeoFixArchiver.SchemaName, record.SchemaName);
                    Assert.AreEqual(AppLogGeoFixArchiver.SchemaVersion, record.SchemaVersion);
                    Assert.AreEqual("jeff", record["entityID"]);
                    Assert.AreEqual("group", record["groupID"]);
                    Assert.AreEqual("10", record["Latitude"]);
                    Assert.AreEqual("20", record["Longitude"]);
                }
                finally
                {
                    logReader.Close();
                }
            }
            finally
            {
                TestCleanup();
                Helper.DeleteFile(AppLogFolder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_AppLogArchiver_Multiple()
        {
            // Submit a 10000 location fixes to a GeoTrackerNode configured with an AppLog archiver
            // and then verify that they were persisted to the archive.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.AppLogGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        LogName       = GeoFix
        MaxSize       = 5GB
        PurgeInterval = 5m
    }}

&endsection
";
            const int cFixes = 10000;

            try
            {
                Helper.DeleteFile(AppLogFolder, true);
                TestInit(appConfig);

                for (int i = 0; i < cFixes; i++)
                    client.SubmitEntityFix("E" + i.ToString(), "G" + i.ToString(), new GeoFix() { Latitude = 10, Longitude = 20 });

                server.Stop();

                AppLogReader logReader = AppLogReader.Open("GeoFix");
                AppLogRecord record;

                try
                {
                    for (int i = 0; i < cFixes; i++)
                    {
                        record = logReader.ReadDelete();

                        Assert.IsNotNull(record);
                        Assert.AreEqual(AppLogGeoFixArchiver.SchemaName, record.SchemaName);
                        Assert.AreEqual(AppLogGeoFixArchiver.SchemaVersion, record.SchemaVersion);
                        Assert.AreEqual("E" + i.ToString(), record["EntityID"]);
                        Assert.AreEqual("G" + i.ToString(), record["GroupID"]);
                        Assert.AreEqual("10", record["Latitude"]);
                        Assert.AreEqual("20", record["Longitude"]);
                    }
                }
                finally
                {
                    logReader.Close();
                }
            }
            finally
            {
                TestCleanup();
                Helper.DeleteFile(AppLogFolder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_NullArchiver()
        {
            // Submit a single location fix to a GeoTrackerNode configured with a Null archiver\
            // and make sure we don't see something barf.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.NullGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

    }}

&endsection
";

            try
            {
                Helper.DeleteFile(AppLogFolder, true);
                TestInit(appConfig);

                client.SubmitEntityFix("jeff", "group", new GeoFix() { Latitude = 10, Longitude = 20 });
                server.Stop();
            }
            finally
            {
                TestCleanup();
                Helper.DeleteFile(AppLogFolder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_SqlArchiver_FlushOnClose()
        {
            // Create a test SQL database with a GeoFixes table where we'll have the
            // SQL archiver write the fixes.  Then submit a single fix to the server
            // and then stop the server.  Verify that the fix is flushed when the server
            // is closed.  Note that the BufferInterval is set to 5 minutes so the
            // fix should not have already been flushed.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.SqlGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        ConnectionString = {0}
        BufferSize       = 100
        BufferInterval   = 5m

        AddScript        = insert into GeoFixes(TimeUtc,EntityID,GroupID,Technology,Latitude,Longitude,Altitude,Course,Speed,HorizontalAccuracy,VerticalAccurancy,NetworkStatus) values (@(TimeUtc),@(EntityID),@(GroupID),@(Technology),@(Latitude),@(Longitude),@(Altitude),@(Course),@(Speed),@(HorizontalAccuracy),@(VerticalAccurancy),@(NetworkStatus))
    }}

&endsection
";

            const string createTableScript =
@"create table GeoFixes (

	ID					int			primary key identity,
	TimeUtc				datetime    not null,
	EntityID			varchar(32)	not null,
	GroupID				varchar(32) null,
	Technology			varchar(32) not null,
	Latitude			float(53) 	not null,
	Longitude			float(53)   not null,
	Altitude			float(53)   null,
	Course				float(53)   null,
	Speed				float(53)	null,
	HorizontalAccuracy	float(53)   null,
	VerticalAccurancy	float(53)   null,
	NetworkStatus		varchar(32) not null
)
";

            using (var dbTest = SqlTestDatabase.Create())
            {
                DateTime time = new DateTime(2011, 4, 27, 9, 58, 0);
                SqlContext ctx = null;
                DataTable dt;

                try
                {
                    ctx = new SqlContext(dbTest.ConnectionInfo);

                    ctx.Open();
                    ctx.Execute(createTableScript);

                    try
                    {
                        TestInit(appConfig.Replace("{0}", dbTest.ConnectionInfo));

                        client.SubmitEntityFix("jeff", "group", new GeoFix() { TimeUtc = time, Latitude = 10, Longitude = 20 });
                        server.Stop();

                        dt = ctx.ExecuteTable("select * from GeoFixes");

                        Assert.AreEqual(1, dt.Rows.Count);
                        Assert.AreEqual(time, dt.Rows[0].Field<DateTime>(dt.Columns["TimeUtc"]));
                        Assert.AreEqual("jeff", dt.Rows[0].Field<string>(dt.Columns["EntityID"]));
                        Assert.AreEqual("group", dt.Rows[0].Field<string>(dt.Columns["GroupID"]));
                        Assert.AreEqual(10.0, dt.Rows[0].Field<double>(dt.Columns["Latitude"]));
                        Assert.AreEqual(20.0, dt.Rows[0].Field<double>(dt.Columns["Longitude"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Altitude"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Course"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Speed"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["HorizontalAccuracy"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["VerticalAccurancy"]));
                        Assert.AreEqual("Unknown", dt.Rows[0].Field<string>(dt.Columns["NetworkStatus"]));
                    }
                    finally
                    {
                        TestCleanup();
                        Helper.DeleteFile(AppLogFolder, true);
                    }
                }
                finally
                {
                    if (ctx != null)
                        ctx.Close();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_SqlArchiver_Flush()
        {
            // Create a test SQL database with a GeoFixes table where we'll have the
            // SQL archiver write the fixes.  Configure a 1 second archiver buffer interval,
            // start the GeoTracker, submit a single fix to the server and and pause for 5 
            // seconds and verify that the buffered fix was persisted to the database.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.SqlGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        ConnectionString = {0}
        BufferSize       = 100
        BufferInterval   = 1s

        AddScript        = insert into GeoFixes(TimeUtc,EntityID,GroupID,Technology,Latitude,Longitude,Altitude,Course,Speed,HorizontalAccuracy,VerticalAccurancy,NetworkStatus) values (@(TimeUtc),@(EntityID),@(GroupID),@(Technology),@(Latitude),@(Longitude),@(Altitude),@(Course),@(Speed),@(HorizontalAccuracy),@(VerticalAccurancy),@(NetworkStatus))
    }}

&endsection
";

            const string createTableScript =
@"create table GeoFixes (

	ID					int			primary key identity,
	TimeUtc				datetime    not null,
	EntityID			varchar(32)	not null,
	GroupID				varchar(32) null,
	Technology			varchar(32) not null,
	Latitude			float(53) 	not null,
	Longitude			float(53)   not null,
	Altitude			float(53)   null,
	Course				float(53)   null,
	Speed				float(53)	null,
	HorizontalAccuracy	float(53)   null,
	VerticalAccurancy	float(53)   null,
	NetworkStatus		varchar(32) not null
)
";

            using (var dbTest = SqlTestDatabase.Create())
            {
                DateTime time = new DateTime(2011, 4, 27, 9, 58, 0);
                SqlContext ctx = null;
                DataTable dt;

                try
                {
                    ctx = new SqlContext(dbTest.ConnectionInfo);

                    ctx.Open();
                    ctx.Execute(createTableScript);

                    try
                    {
                        TestInit(appConfig.Replace("{0}", dbTest.ConnectionInfo));

                        client.SubmitEntityFix("jeff", "group", new GeoFix() { TimeUtc = time, Latitude = 10, Longitude = 20 });
                        Thread.Sleep(5000);

                        dt = ctx.ExecuteTable("select * from GeoFixes");

                        Assert.AreEqual(1, dt.Rows.Count);
                        Assert.AreEqual(time, dt.Rows[0].Field<DateTime>(dt.Columns["TimeUtc"]));
                        Assert.AreEqual("jeff", dt.Rows[0].Field<string>(dt.Columns["EntityID"]));
                        Assert.AreEqual("group", dt.Rows[0].Field<string>(dt.Columns["GroupID"]));
                        Assert.AreEqual(10.0, dt.Rows[0].Field<double>(dt.Columns["Latitude"]));
                        Assert.AreEqual(20.0, dt.Rows[0].Field<double>(dt.Columns["Longitude"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Altitude"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Course"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["Speed"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["HorizontalAccuracy"]));
                        Assert.IsNull(dt.Rows[0].Field<double?>(dt.Columns["VerticalAccurancy"]));
                        Assert.AreEqual("Unknown", dt.Rows[0].Field<string>(dt.Columns["NetworkStatus"]));
                    }
                    finally
                    {
                        TestCleanup();
                        Helper.DeleteFile(AppLogFolder, true);
                    }
                }
                finally
                {
                    if (ctx != null)
                        ctx.Close();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_SqlArchiver_FillBuffer()
        {
            // Create a test SQL database with a GeoFixes table where we'll have the
            // SQL archiver write the fixes.  Configure a buffer size of 100 fixes,
            // submit 100 fixes and then verify that they were written immediately 
            // to the database (e.g. the buffer was flushed).

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.SqlGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        ConnectionString = {0}
        BufferSize       = 100
        BufferInterval   = 5m

        AddScript        = insert into GeoFixes(TimeUtc,EntityID,GroupID,Technology,Latitude,Longitude,Altitude,Course,Speed,HorizontalAccuracy,VerticalAccurancy,NetworkStatus) values (@(TimeUtc),@(EntityID),@(GroupID),@(Technology),@(Latitude),@(Longitude),@(Altitude),@(Course),@(Speed),@(HorizontalAccuracy),@(VerticalAccurancy),@(NetworkStatus))
    }}

&endsection
";

            const string createTableScript =
@"create table GeoFixes (

	ID					int			primary key identity,
	TimeUtc				datetime    not null,
	EntityID			varchar(32)	not null,
	GroupID				varchar(32) null,
	Technology			varchar(32) not null,
	Latitude			float(53) 	not null,
	Longitude			float(53)   not null,
	Altitude			float(53)   null,
	Course				float(53)   null,
	Speed				float(53)	null,
	HorizontalAccuracy	float(53)   null,
	VerticalAccurancy	float(53)   null,
	NetworkStatus		varchar(32) not null
)
";

            using (var dbTest = SqlTestDatabase.Create())
            {
                DateTime time = new DateTime(2011, 4, 27, 9, 58, 0);
                SqlContext ctx = null;
                DataTable dt;

                try
                {
                    ctx = new SqlContext(dbTest.ConnectionInfo);

                    ctx.Open();
                    ctx.Execute(createTableScript);

                    try
                    {
                        TestInit(appConfig.Replace("{0}", dbTest.ConnectionInfo));

                        for (int i = 0; i < 100; i++)
                            client.SubmitEntityFix(i.ToString(), "group", new GeoFix() { TimeUtc = time, Latitude = 10, Longitude = 20 });

                        // Wait 4 times the server's background task scheduling interval to give the
                        // archiver a fair chance to perform the operation.

                        Thread.Sleep(Helper.Multiply(server.Settings.BkInterval, 4));

                        dt = ctx.ExecuteTable("select * from GeoFixes");

                        Assert.AreEqual(100, dt.Rows.Count);

                        for (int i = 0; i < 100; i++)
                        {
                            Assert.AreEqual(time, dt.Rows[i].Field<DateTime>(dt.Columns["TimeUtc"]));
                            Assert.AreEqual(i.ToString(), dt.Rows[i].Field<string>(dt.Columns["EntityID"]));
                            Assert.AreEqual("group", dt.Rows[i].Field<string>(dt.Columns["GroupID"]));
                            Assert.AreEqual(10.0, dt.Rows[i].Field<double>(dt.Columns["Latitude"]));
                            Assert.AreEqual(20.0, dt.Rows[i].Field<double>(dt.Columns["Longitude"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Altitude"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Course"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Speed"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["HorizontalAccuracy"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["VerticalAccurancy"]));
                            Assert.AreEqual("Unknown", dt.Rows[i].Field<string>(dt.Columns["NetworkStatus"]));
                        }
                    }
                    finally
                    {
                        TestCleanup();
                        Helper.DeleteFile(AppLogFolder, true);
                    }
                }
                finally
                {
                    if (ctx != null)
                        ctx.Close();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void Archivers_SqlArchiver_Blast()
        {
            // Archive 25K fixes in random blocks over an extended period
            // of time with the buffer interval set to 2 seconds to exercise
            // the interaction between archival submissions and the buffer handling
            // background thread.

            const string appConfig =
@"
&section LillTek.GeoTracker.Server

    GeoFixArchiver = LillTek.GeoTracker.Server.SqlGeoFixArchiver:LillTek.GeoTracker.Server.dll

    // Archiver implementation specific arguments (such as a database connection string)
    // formatted as name=value pairs separated by semicolons.

    GeoFixArchiverArgs = {{

        ConnectionString = {0}
        BufferSize       = 100
        BufferInterval   = 2s

        AddScript        = insert into GeoFixes(TimeUtc,EntityID,GroupID,Technology,Latitude,Longitude,Altitude,Course,Speed,HorizontalAccuracy,VerticalAccurancy,NetworkStatus) values (@(TimeUtc),@(EntityID),@(GroupID),@(Technology),@(Latitude),@(Longitude),@(Altitude),@(Course),@(Speed),@(HorizontalAccuracy),@(VerticalAccurancy),@(NetworkStatus))
    }}

&endsection
";

            const string createTableScript =
@"create table GeoFixes (

	ID					int			primary key identity,
	TimeUtc				datetime    not null,
	EntityID			varchar(32)	not null,
	GroupID				varchar(32) null,
	Technology			varchar(32) not null,
	Latitude			float(53) 	not null,
	Longitude			float(53)   not null,
	Altitude			float(53)   null,
	Course				float(53)   null,
	Speed				float(53)	null,
	HorizontalAccuracy	float(53)   null,
	VerticalAccurancy	float(53)   null,
	NetworkStatus		varchar(32) not null
)
";

            using (var dbTest = SqlTestDatabase.Create())
            {
                DateTime time = new DateTime(2011, 4, 27, 9, 58, 0);
                SqlContext ctx = null;
                DataTable dt;

                try
                {
                    ctx = new SqlContext(dbTest.ConnectionInfo);

                    ctx.Open();
                    ctx.Execute(createTableScript);

                    try
                    {
                        TestInit(appConfig.Replace("{0}", dbTest.ConnectionInfo));

                        const int cTotalFixes = 25000;
                        const int maxBlock = 300;
                        Random rand = new Random(0);
                        int cSubmitted = 0;

                        while (cSubmitted < cTotalFixes)
                        {
                            // Blast out a block of between 0...maxBlock-1 fixes.

                            int cBlock = rand.Next(maxBlock);

                            if (cBlock + cSubmitted > cTotalFixes)
                                cBlock = cTotalFixes - cSubmitted;

                            for (int i = 0; i < cBlock; i++)
                                client.SubmitEntityFix((cSubmitted + i).ToString(), "group", new GeoFix() { TimeUtc = time, Latitude = 10, Longitude = 20 });

                            cSubmitted += cBlock;

                            // Wait a random time between 0 and 100ms

                            Thread.Sleep(rand.Next(101));
                        }

                        // Stop the server so that any buffered fixes will be flushed.

                        server.Stop();

                        // Verify that the fixes are in the database.

                        dt = ctx.ExecuteTable("select * from GeoFixes");
                        Assert.AreEqual(cTotalFixes, dt.Rows.Count);

                        for (int i = 0; i < cTotalFixes; i++)
                        {
                            Assert.AreEqual(time, dt.Rows[i].Field<DateTime>(dt.Columns["TimeUtc"]));
                            Assert.AreEqual(i.ToString(), dt.Rows[i].Field<string>(dt.Columns["EntityID"]));
                            Assert.AreEqual("group", dt.Rows[i].Field<string>(dt.Columns["GroupID"]));
                            Assert.AreEqual(10.0, dt.Rows[i].Field<double>(dt.Columns["Latitude"]));
                            Assert.AreEqual(20.0, dt.Rows[i].Field<double>(dt.Columns["Longitude"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Altitude"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Course"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["Speed"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["HorizontalAccuracy"]));
                            Assert.IsNull(dt.Rows[i].Field<double?>(dt.Columns["VerticalAccurancy"]));
                            Assert.AreEqual("Unknown", dt.Rows[i].Field<string>(dt.Columns["NetworkStatus"]));
                        }
                    }
                    finally
                    {
                        TestCleanup();
                        Helper.DeleteFile(AppLogFolder, true);
                    }
                }
                finally
                {
                    if (ctx != null)
                        ctx.Close();
                }
            }
        }
    }
}

