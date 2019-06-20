//-----------------------------------------------------------------------------
// FILE:        _Request.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _Request
    {
        private void OnRequest(object sender, SipRequestEventArgs args)
        {
            SipRequest request = args.Request;

            switch (request["Test"].Text)
            {
                case "OK":

                    args.Response = request.CreateResponse(SipStatus.OK, null);
                    break;

                case "Error":

                    args.Response = request.CreateResponse(SipStatus.ServerError, null);
                    break;

                default:

                    args.Response = request.CreateResponse(SipStatus.NotImplemented, null);
                    break;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Request_Basic()
        {
            SipBasicCore core = null;
            SipCoreSettings settings;
            SipRequest request;
            SipResult result;
            string serviceUri;

            try
            {
                settings = new SipCoreSettings();
                settings.UserName = "jeff";
                settings.Password = "lill";

                serviceUri = "sip:" + settings.TransportSettings[0].ExternalBinding.ToString();

                core = new SipBasicCore(settings);
                core.RequestReceived += new SipRequestDelegate(OnRequest);
                core.Start();

                request = new SipRequest(SipMethod.Info, serviceUri, null);
                request.AddHeader("Test", "OK");
                result = core.Request(request);

                Assert.AreEqual(SipStatus.OK, result.Status);
            }
            finally
            {
                if (core != null)
                    core.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Request_Async()
        {
            SipBasicCore core = null;
            SipCoreSettings settings;
            SipRequest request;
            SipResult result;
            string serviceUri;
            IAsyncResult ar;

            try
            {
                settings = new SipCoreSettings();
                settings.UserName = "jeff";
                settings.Password = "lill";

                serviceUri = "sip:" + settings.TransportSettings[0].ExternalBinding.ToString();

                core = new SipBasicCore(settings);
                core.RequestReceived += new SipRequestDelegate(OnRequest);
                core.Start();

                request = new SipRequest(SipMethod.Info, serviceUri, null);
                request.AddHeader("Test", "OK");

                ar = core.BeginRequest(request, null, "Hello world");
                Assert.AreEqual("Hello world", ar.AsyncState);
                result = core.EndRequest(ar);

                Assert.AreEqual(SipStatus.OK, result.Status);
            }
            finally
            {
                if (core != null)
                    core.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Request_Error()
        {
            SipBasicCore core = null;
            SipCoreSettings settings;
            SipRequest request;
            SipResult result;
            string serviceUri;

            try
            {
                settings = new SipCoreSettings();
                settings.UserName = "jeff";
                settings.Password = "lill";

                serviceUri = "sip:" + settings.TransportSettings[0].ExternalBinding.ToString();

                core = new SipBasicCore(settings);
                core.RequestReceived += new SipRequestDelegate(OnRequest);
                core.Start();

                request = new SipRequest(SipMethod.Info, serviceUri, null);
                request.AddHeader("Test", "Error");
                result = core.Request(request);

                Assert.AreEqual(SipStatus.ServerError, result.Status);
            }
            finally
            {
                if (core != null)
                    core.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Register_Vitelity()
        {
            Assert.Inconclusive("Manual Test: Comment this out to perform this test.");

            // Attempt a registration against the Vitelty.com SIP trunking service.

            SipBasicCore core = null;
            SipCoreSettings settings;

            try
            {
                // These settings are hardcoded for my Vitelity.com account
                // and my home network.  This test also assumes that the router
                // is forwarding UDP port 5060 packets to the test computer.

                settings = new SipCoreSettings();
                settings.LocalContact = "sip:jslill@" + Dns.GetHostEntry("www.lilltek.com").AddressList.IPv4Only()[0].ToString() + ":5060";
                settings.UserName = "jslill";
                settings.Password = "q0jsrd7y";

                core = new SipBasicCore(settings);
                core.Start();

                core.StartAutoRegistration("sip:sip4.vitelity.net", "sip:jslill@sip4.vitelity.net");
                Assert.IsTrue(core.AutoRegistration);
                Assert.IsTrue(core.IsRegistered);

                // Sleep a while and watch for registration traffic on NetMon.

                Thread.Sleep(5 * 1000 * 60);

                core.StopAutoRegistration();
                Assert.IsFalse(core.AutoRegistration);
                Assert.IsFalse(core.IsRegistered);
            }
            finally
            {
                if (core != null)
                    core.Stop();
            }
        }
    }
}

