//-----------------------------------------------------------------------------
// FILE:        _SipMmsGateway.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipMssGateway
    {
        private SipTraceMode traceMode = SipTraceMode.None;

        [TestInitialize]
        public void Initialize()
        {
            //NetTrace.Start();
            traceMode = SipTraceMode.All;
        }

        [TestCleanup]
        public void Cleanup()
        {
            //NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipMssGateway_Basic()
        {
            Assert.Inconclusive("Manual Test: Comment this out to perform this test.");

            SipBasicCore core = null;
            SipMssGateway gateway = null;
            int quit;

            try
            {

                Config.SetConfig(@"
&section Core

    LocalContact     = sip:jslill@$(ip-address):8899
    AutoAuthenticate = yes
    UserName         = jslill
    Password         = q0jsrd7y
    Diagnostics      = yes

    &section Transport[0]

        Type    = UDP
        Binding = ANY:8899

    &endsection

    &section Transport[1]

        Type    = TCP
        Binding = ANY:8899

    &endsection

&endsection

&section Gateway

    SpeechServerUri = sip:$(ip-address):5060
    TrunkUri        = sip:sip4.vitelity.net
    Register[0]     = sip:jslill@sip4.vitelity.net

&endsection

".Replace('&', '#'));

                core = new SipBasicCore(SipCoreSettings.LoadConfig("Core"));
                core.SetTraceMode(traceMode);

                gateway = new SipMssGateway(core, SipMssGatewaySettings.LoadConfig("Gateway"));
                gateway.Start();

                quit = 0;
                while (quit == 0)
                {
                    quit = 0;
                    Thread.Sleep(500);  // Break here and manually set quit=1 to terminate the test
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (gateway != null)
                    gateway.Stop();

                if (core != null)
                    core.Stop();
            }
        }
    }
}

