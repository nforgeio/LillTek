//-----------------------------------------------------------------------------
// FILE:        _MsgQueueEngineSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Queuing.Test
{
    [TestClass]
    public class _MsgQueueEngineSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueEngineSettings_Default()
        {
            MsgQueueEngineSettings settings = new MsgQueueEngineSettings();

            CollectionAssert.AreEqual(new MsgEP[] { MsgEP.Parse(MsgQueue.AbstractBaseEP) }, settings.QueueMap);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.FlushInterval);
            Assert.AreEqual(TimeSpan.FromDays(7), settings.DeadLetterTTL);
            Assert.AreEqual(3, settings.MaxDeliveryAttempts);
            Assert.AreEqual(TimeSpan.FromSeconds(30), settings.KeepAliveInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(95), settings.SessionTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkTaskInterval);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueEngineSettings_Load_Defaults()
        {
            try
            {
                MsgQueueEngineSettings settings;

                Config.SetConfig(null);

                settings = MsgQueueEngineSettings.LoadConfig("Engine");
                CollectionAssert.AreEqual(new MsgEP[] { MsgEP.Parse(MsgQueue.AbstractBaseEP) }, settings.QueueMap);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.FlushInterval);
                Assert.AreEqual(TimeSpan.FromDays(7), settings.DeadLetterTTL);
                Assert.AreEqual(3, settings.MaxDeliveryAttempts);
                Assert.AreEqual(TimeSpan.FromSeconds(30), settings.KeepAliveInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(95), settings.SessionTimeout);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.BkTaskInterval);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueEngineSettings_Load()
        {
            try
            {
                MsgQueueEngineSettings settings;

                Config.SetConfig(@"
&section Engine

    QueueMap[0]         = logical://test1
    QueueMap[1]         = logical://test2
    FlushInterval       = 1s
    DeadLetterTTL       = 2s
    MaxDeliveryAttempts = 3
    KeepAliveInterval   = 4s
    SessionTimeout      = 5s
    BkTaskInterval      = 6s

&endsection
".Replace('&', '#'));

                settings = MsgQueueEngineSettings.LoadConfig("Engine");
                CollectionAssert.AreEqual(new MsgEP[] { MsgEP.Parse("logical://test1"), MsgEP.Parse("logical://test2") }, settings.QueueMap);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.FlushInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(2), settings.DeadLetterTTL);
                Assert.AreEqual(3, settings.MaxDeliveryAttempts);
                Assert.AreEqual(TimeSpan.FromSeconds(4), settings.KeepAliveInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.SessionTimeout);
                Assert.AreEqual(TimeSpan.FromSeconds(6), settings.BkTaskInterval);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

