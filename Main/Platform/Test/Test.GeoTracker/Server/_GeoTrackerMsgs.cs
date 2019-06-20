//-----------------------------------------------------------------------------
// FILE:        _GeoTrackerMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.GeoTracker.Msgs;
using LillTek.GeoTracker.Server;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Test
{
    [TestClass]
    public class _GeoTrackerMsgs
    {

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerMsgs_IPToGeoFixMsg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            IPToGeoFixMsg msgIn, msgOut;

            Msg.ClearTypes();
            LillTek.GeoTracker.Global.RegisterMsgTypes();

            msgOut = new IPToGeoFixMsg(IPAddress.Parse("192.168.1.2"));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (IPToGeoFixMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(IPAddress.Parse("192.168.1.2"), msgIn.Address);

            // Test Clone()

            msgIn = (IPToGeoFixMsg)msgOut.Clone();
            Assert.IsNotNull(msgIn);
            Assert.AreEqual(IPAddress.Parse("192.168.1.2"), msgIn.Address);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerMsgs_IPToGeoFixAck()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            IPToGeoFixAck msgIn, msgOut;
            GeoFix fix;

            Msg.ClearTypes();
            LillTek.GeoTracker.Global.RegisterMsgTypes();

            // Test the return of an actual fix.

            fix = new GeoFix()
            {
                TimeUtc = new DateTime(2011, 3, 3, 10, 55, 15),
                Latitude = 1.1,
                Longitude = 2.2,
                Altitude = 3.3,
                Course = 4.4,
                Speed = 5.5,
                HorizontalAccuracy = 6.6,
                VerticalAccurancy = 7.7,
                Technology = GeoFixTechnology.GPS,
                NetworkStatus = NetworkStatus.Cdma
            };

            msgOut = new IPToGeoFixAck(fix);

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (IPToGeoFixAck)Msg.Load(es);

            Assert.IsNotNull(msgIn);

            fix = msgIn.GeoFix;
            Assert.AreEqual(new DateTime(2011, 3, 3, 10, 55, 15), fix.TimeUtc);
            Assert.AreEqual(1.1, fix.Latitude);
            Assert.AreEqual(2.2, fix.Longitude);
            Assert.AreEqual(3.3, fix.Altitude);
            Assert.AreEqual(4.4, fix.Course);
            Assert.AreEqual(5.5, fix.Speed);
            Assert.AreEqual(6.6, fix.HorizontalAccuracy);
            Assert.AreEqual(7.7, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.GPS, fix.Technology);
            Assert.AreEqual(NetworkStatus.Cdma, fix.NetworkStatus);

            // Test the return of a NULL fix.

            msgOut = new IPToGeoFixAck((GeoFix)null);

            es.Position = 0;
            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (IPToGeoFixAck)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.IsNull(msgIn.GeoFix);

            // Test exception encoding.

            msgOut = new IPToGeoFixAck(new NotImplementedException("This is a test"));

            es.Position = 0;
            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (IPToGeoFixAck)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.IsNull(msgIn.GeoFix);
            Assert.AreEqual("System.NotImplementedException", msgOut.ExceptionTypeName);
            Assert.AreEqual("This is a test", msgOut.Exception);

            // Test Clone()

            msgIn = (IPToGeoFixAck)new IPToGeoFixAck(fix).Clone();
            fix = msgIn.GeoFix;
            Assert.AreEqual(new DateTime(2011, 3, 3, 10, 55, 15), fix.TimeUtc);
            Assert.AreEqual(1.1, fix.Latitude);
            Assert.AreEqual(2.2, fix.Longitude);
            Assert.AreEqual(3.3, fix.Altitude);
            Assert.AreEqual(4.4, fix.Course);
            Assert.AreEqual(5.5, fix.Speed);
            Assert.AreEqual(6.6, fix.HorizontalAccuracy);
            Assert.AreEqual(7.7, fix.VerticalAccurancy);
            Assert.AreEqual(GeoFixTechnology.GPS, fix.Technology);
            Assert.AreEqual(NetworkStatus.Cdma, fix.NetworkStatus);

            msgIn = (IPToGeoFixAck)new IPToGeoFixAck(new NotImplementedException("This is a test")).Clone();
            Assert.IsNotNull(msgIn);
            Assert.IsNull(msgIn.GeoFix);
            Assert.AreEqual("System.NotImplementedException", msgOut.ExceptionTypeName);
            Assert.AreEqual("This is a test", msgOut.Exception);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.GeoTracker.Server")]
        public void GeoTrackerMsgs_GeoFixMsg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            GeoFixMsg msgIn, msgOut;
            DateTime nowUtc = DateTime.UtcNow;

            Msg.ClearTypes();
            LillTek.GeoTracker.Global.RegisterMsgTypes();

            msgOut = new GeoFixMsg("Jeff", "Developer", new GeoFix() { TimeUtc = nowUtc, Latitude = 10, Longitude = 20 });

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (GeoFixMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("Jeff", msgIn.EntityID);
            Assert.AreEqual("Developer", msgIn.GroupID);
            Assert.AreEqual(nowUtc, msgIn.Fixes[0].TimeUtc);
            Assert.AreEqual(10, msgIn.Fixes[0].Latitude);
            Assert.AreEqual(20, msgIn.Fixes[0].Longitude);

            // Try with a GroupID=null

            msgOut = new GeoFixMsg("Joe", null, new GeoFix() { TimeUtc = nowUtc, Latitude = 30, Longitude = 40 });

            es.Position = 0;
            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (GeoFixMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("Joe", msgIn.EntityID);
            Assert.IsNull(msgIn.GroupID);
            Assert.AreEqual(nowUtc, msgIn.Fixes[0].TimeUtc);
            Assert.AreEqual(30, msgIn.Fixes[0].Latitude);
            Assert.AreEqual(40, msgIn.Fixes[0].Longitude);

            // Test multiple fixes

            var fixes = new List<GeoFix>();

            fixes.Add(new GeoFix() { TimeUtc = nowUtc, Latitude = 30, Longitude = 40 });
            fixes.Add(new GeoFix() { TimeUtc = nowUtc, Latitude = 50, Longitude = 60 });

            msgOut = new GeoFixMsg("Joe", "group", fixes);

            es.Position = 0;
            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (GeoFixMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("Joe", msgIn.EntityID);
            Assert.AreEqual("group", msgIn.GroupID);
            Assert.AreEqual(2, fixes.Count);
            Assert.AreEqual(nowUtc, msgIn.Fixes[0].TimeUtc);
            Assert.AreEqual(30, msgIn.Fixes[0].Latitude);
            Assert.AreEqual(40, msgIn.Fixes[0].Longitude);
            Assert.AreEqual(nowUtc, msgIn.Fixes[1].TimeUtc);
            Assert.AreEqual(50, msgIn.Fixes[1].Latitude);
            Assert.AreEqual(60, msgIn.Fixes[1].Longitude);

            // Test Clone()

            msgIn = (GeoFixMsg)msgOut.Clone();

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("Joe", msgIn.EntityID);
            Assert.AreEqual("group", msgIn.GroupID);
            Assert.AreEqual(2, fixes.Count);
            Assert.AreEqual(nowUtc, msgIn.Fixes[0].TimeUtc);
            Assert.AreEqual(30, msgIn.Fixes[0].Latitude);
            Assert.AreEqual(40, msgIn.Fixes[0].Longitude);
            Assert.AreEqual(nowUtc, msgIn.Fixes[1].TimeUtc);
            Assert.AreEqual(50, msgIn.Fixes[1].Latitude);
            Assert.AreEqual(60, msgIn.Fixes[1].Longitude);
        }
    }
}

