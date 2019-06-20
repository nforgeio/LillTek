//-----------------------------------------------------------------------------
// FILE:        _SentinelServiceMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs.SentinelService;
using LillTek.Messaging;
using LillTek.Service;
using LillTek.Testing;
using LillTek.Windows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _SentinelServiceMsgs
    {
        private void TestBaseCloning(PropertyMsg msg1)
        {
            PropertyMsg msg2;

            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;

            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        private void TestBaseCloning(BlobPropertyMsg msg1)
        {
            BlobPropertyMsg msg2;

            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;

            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (BlobPropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        [TestInitialize]
        public void Initialize()
        {
            Msg.LoadTypes(typeof(LillTek.Datacenter.Global).Assembly);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Msg.ClearTypes();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_ConnectMsg_Serialize()
        {
            ConnectMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            msgOut = new ConnectMsg();

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (ConnectMsg)Msg.Load(es);
            Assert.IsNotNull(msgIn);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_ConnectMsg_Clone()
        {
            ConnectMsg msgIn, msgOut;

            msgOut = new ConnectMsg();
            msgIn = (ConnectMsg)msgOut.Clone();
            Assert.IsNotNull(msgIn);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_ConnectAck_Serialize()
        {
            ConnectAck msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            msgOut = new ConnectAck("logical://foo");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (ConnectAck)Msg.Load(es);
            Assert.AreEqual(MsgEP.Parse("logical://foo"), msgIn.InstanceEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_ConnectAck_Clone()
        {
            ConnectAck msgIn, msgOut;

            msgOut = new ConnectAck("logical://foo");
            msgIn = (ConnectAck)msgOut.Clone();
            Assert.AreEqual(MsgEP.Parse("logical://foo"), msgIn.InstanceEP);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_LogEventMsg_Serialize()
        {
            LogEventMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();
            EventLog log = new EventLog("Application");
            EventLogEntry entry;

            // Clear the log and then add an entry so we can retrieve it right
            // away (hopefully getting the same entry back).

            log.Source = "Application";
            log.Clear();
            log.WriteEntry("Test entry", EventLogEntryType.Information, 0, 0, new byte[] { 0, 1, 2, 3 });
            entry = log.Entries[0];

            msgOut = new LogEventMsg("Application", entry);

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (LogEventMsg)Msg.Load(es);

            Assert.AreEqual("Application", msgIn.LogName);
            Assert.AreEqual(entry.EntryType, msgIn.EntryType);
            Assert.AreEqual(entry.TimeGenerated.ToUniversalTime(), msgIn.Time);
            Assert.AreEqual(entry.MachineName, msgIn.MachineName);
            Assert.AreEqual(entry.Message, msgIn.Message);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, msgIn.Data);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_LogEventMsg_Clone()
        {
            LogEventMsg msgIn, msgOut;
            EventLog log = new EventLog("Application");
            EventLogEntry entry;

            // Clear the log and then add an entry so we can retrieve it right
            // away (hopefully getting the same entry back).

            log.Source = "Application";
            log.Clear();
            log.WriteEntry("Test entry", EventLogEntryType.Information, 0, 0, new byte[] { 0, 1, 2, 3 });
            entry = log.Entries[0];

            msgOut = new LogEventMsg("Application", entry);
            msgIn = (LogEventMsg)msgOut.Clone();

            Assert.AreEqual("Application", msgIn.LogName);
            Assert.AreEqual(entry.EntryType, msgIn.EntryType);
            Assert.AreEqual(entry.TimeGenerated.ToUniversalTime(), msgIn.Time);
            Assert.AreEqual(entry.MachineName, msgIn.MachineName);
            Assert.AreEqual(entry.Message, msgIn.Message);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, msgIn.Data);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_LogEventAck_Serialize()
        {
            LogEventAck msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            msgOut = new LogEventAck();

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (LogEventAck)Msg.Load(es);
            Assert.IsNotNull(msgIn);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void SentinelServiceMsgs_LogEventAck_Clone()
        {
            LogEventAck msgIn, msgOut;

            msgOut = new LogEventAck();
            msgIn = (LogEventAck)msgOut.Clone();
            Assert.IsNotNull(msgIn);

            TestBaseCloning(msgOut);
        }
    }
}

