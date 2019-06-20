//-----------------------------------------------------------------------------
// FILE:        _MsgQueueSettings.cs
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
    public class _MsgQueueSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueSettingsDefault()
        {
            MsgQueueSettings settings = new MsgQueueSettings();

            Assert.AreEqual((MsgEP)MsgQueue.AbstractBaseEP, settings.BaseEP);
            Assert.AreEqual(TimeSpan.MaxValue, settings.Timeout);
            Assert.AreEqual(TimeSpan.Zero, settings.MessageTTL);
            Assert.AreEqual(Compress.Best, settings.Compress);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueSettingsLoad_Defaults()
        {
            try
            {
                MsgQueueSettings settings;

                Config.SetConfig(null);

                settings = MsgQueueSettings.LoadConfig("Queue");
                Assert.AreEqual((MsgEP)MsgQueue.AbstractBaseEP, settings.BaseEP);
                Assert.AreEqual(TimeSpan.MaxValue, settings.Timeout);
                Assert.AreEqual(TimeSpan.Zero, settings.MessageTTL);
                Assert.AreEqual(Compress.Best, settings.Compress);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueSettingsLoad()
        {
            try
            {
                MsgQueueSettings settings;

                Config.SetConfig(@"
&section Queue

    BaseEP         = logical://Test
    Timeout        = 1s
    MessageTTL     = 2s
    Compress       = None
    BkTaskInterval = 3s

&endsection
".Replace('&', '#'));

                settings = MsgQueueSettings.LoadConfig("Queue");
                Assert.AreEqual((MsgEP)"logical://Test", settings.BaseEP);
                Assert.AreEqual(TimeSpan.FromSeconds(1), settings.Timeout);
                Assert.AreEqual(TimeSpan.FromSeconds(2), settings.MessageTTL);
                Assert.AreEqual(Compress.None, settings.Compress);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

