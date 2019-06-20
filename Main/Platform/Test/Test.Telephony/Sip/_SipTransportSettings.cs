//-----------------------------------------------------------------------------
// FILE:        _SipTransportSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipTransportSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipTransportSettings_LoadConfig()
        {
            try
            {
                Config.SetConfig(@"
&section Transport

    Type       = TCP
    Binding    = 192.168.1.200:SIP
    BufferSize = 16000

    &section Timers
        T1     = 1s
        T2     = 2s
        T4     = 3s
    &endsection

&endsection
".Replace('&', '#'));
                SipTransportSettings settings;

                settings = SipTransportSettings.LoadConfig("Transport");
                Assert.AreEqual(SipTransportType.TCP, settings.TransportType);
                Assert.AreEqual(new NetworkBinding("192.168.1.200:SIP"), settings.Binding);
                Assert.AreEqual(new NetworkBinding("192.168.1.200:SIP"), settings.ExternalBinding);
                Assert.AreEqual(16000, settings.BufferSize);

                Config.SetConfig(@"
&section Transport

    Type       = TCP
    Binding    = ANY:SIP

&endsection
".Replace('&', '#'));

                settings = SipTransportSettings.LoadConfig("Transport");
                Assert.AreEqual(new NetworkBinding(NetHelper.GetActiveAdapter(), NetworkPort.SIP), settings.ExternalBinding);

                Config.SetConfig(@"
&section Transport

    Type            = TCP
    Binding         = ANY:SIP
    ExternalBinding = ANY:500

&endsection
".Replace('&', '#'));

                settings = SipTransportSettings.LoadConfig("Transport");
                Assert.AreEqual(new NetworkBinding(NetHelper.GetActiveAdapter(), 500), settings.ExternalBinding);

                Config.SetConfig(@"
&section Transport

    Type            = TCP
    Binding         = ANY:SIP
    ExternalBinding = 60.100.5.7:500

&endsection
".Replace('&', '#'));

                settings = SipTransportSettings.LoadConfig("Transport");
                Assert.AreEqual(new NetworkBinding("60.100.5.7:500"), settings.ExternalBinding);
            }
            finally
            {
                Config.SetConfig("");
            }
        }
    }
}

