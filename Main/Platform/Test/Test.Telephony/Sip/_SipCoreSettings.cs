//-----------------------------------------------------------------------------
// FILE:        _SipCoreSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipCoreSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipCoreSettings_LoadConfig()
        {
            try
            {
                Config.SetConfig(@"
&section Core

    LocalContact         = sip:www.lilltek.com:5060;transport=tcp
    OutboundProxy        = sip:sip.lilltek.com:5060
    TraceMode            = ALL
    AutoAuthenticate     = no
    UserAgent            = Foo
    UserName             = Jeff
    Password             = Lill
    ServerTransactionTTL = 10m
    EarlyDialogTTL       = 5s

    &section Transport[0]

        Type       = TCP
        Binding    = 192.168.1.200:SIP
        BufferSize = 16000

        &section Timers
            T1     = 1s
            T2     = 2s
            T4     = 3s
        &endsection

    &endsection

    &section Transport[1]

        Type       = UDP
        Binding    = 127.0.0.1:1234
        BufferSize = 4000
        Timers.T1  = 100ms

    &endsection
&endsection
".Replace('&', '#'));
                SipCoreSettings settings = SipCoreSettings.LoadConfig("Core");
                SipTransportSettings tSettings;

                Assert.AreEqual("sip:www.lilltek.com:5060;transport=tcp", settings.LocalContact);
                Assert.AreEqual("sip:sip.lilltek.com:5060", (string)settings.OutboundProxyUri);
                Assert.AreEqual(SipTraceMode.All, settings.TraceMode);
                Assert.AreEqual("Foo", settings.UserAgent);
                Assert.IsFalse(settings.AutoAuthenticate);
                Assert.AreEqual("Jeff", settings.UserName);
                Assert.AreEqual("Lill", settings.Password);
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.ServerTransactionTTL);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.EarlyDialogTTL);

                Assert.AreEqual(2, settings.TransportSettings.Length);

                tSettings = settings.TransportSettings[0];
                Assert.AreEqual(SipTransportType.TCP, tSettings.TransportType);
                Assert.AreEqual(new NetworkBinding("192.168.1.200:SIP"), tSettings.Binding);
                Assert.AreEqual(16000, tSettings.BufferSize);

                Assert.AreEqual(TimeSpan.FromSeconds(1), tSettings.BaseTimers.T1);
                Assert.AreEqual(TimeSpan.FromSeconds(2), tSettings.BaseTimers.T2);
                Assert.AreEqual(TimeSpan.FromSeconds(3), tSettings.BaseTimers.T4);

                tSettings = settings.TransportSettings[1];
                Assert.AreEqual(SipTransportType.UDP, tSettings.TransportType);
                Assert.AreEqual(new NetworkBinding("127.0.0.1:1234"), tSettings.Binding);
                Assert.AreEqual(4000, tSettings.BufferSize);

                Assert.AreEqual(TimeSpan.FromMilliseconds(100), tSettings.BaseTimers.T1);

                settings = new SipCoreSettings();
                Assert.AreEqual(NetHelper.GetActiveAdapter().ToString(), settings.LocalContact);
                Assert.IsTrue(settings.AutoAuthenticate);
                Assert.AreEqual("LillTek SIP v" + Helper.GetVersionString(Assembly.GetExecutingAssembly()), settings.UserAgent);
            }
            finally
            {
                Config.SetConfig("");
            }
        }
    }
}

