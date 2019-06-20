//-----------------------------------------------------------------------------
// FILE:        _SipBasicCore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipBasicCore
    {
        private SipTraceMode traceMode = SipTraceMode.None;
        private TimeSpan yieldTime = TimeSpan.FromMilliseconds(250);

        private SipUri core1Uri;
        private SipUri core2Uri;
        private SipBasicCore core1;
        private SipBasicCore core2;

        [TestInitialize]
        public void Initialize()
        {
            //NetTrace.Start();
            traceMode = SipTraceMode.Send;
        }

        [TestCleanup]
        public void Cleanup()
        {
            //NetTrace.Stop();
        }

        private void StartCores()
        {
            SipCoreSettings settings;
            IPAddress address;
            IPAddress subnet;

            Helper.GetNetworkInfo(out address, out subnet);
            core1Uri = (SipUri)string.Format("sip:{0}:7725;transport=udp", address);
            core2Uri = (SipUri)string.Format("sip:{0}:7726;transport=udp", address);

            settings = new SipCoreSettings();
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(core1Uri.Host, core1Uri.Port), 0) };
            core1 = new SipBasicCore(settings);
            core1.SetTraceMode(traceMode);

            settings = new SipCoreSettings();
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(core2Uri.Host, core2Uri.Port), 0) };
            core2 = new SipBasicCore(settings);
            core2.SetTraceMode(traceMode);

            try
            {
                core1.Start();
                core2.Start();
            }
            catch
            {
                core1.Stop();
                core2.Stop();

                throw;
            }
        }

        private void StopCores()
        {
            if (core1 != null)
                core1.Stop();

            if (core2 != null)
                core2.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_SetResponse()
        {
            // Verify that we can submit a non-dialog request from
            // one core to another, respond to it by setting the 
            // Response property of the RequestReceived handler arguments
            // and then actually receive the response.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                SipRequest core2Request = null;

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    args.Response = core2Request.CreateResponse(SipStatus.OK, "Hello World!");
                    args.Response.Contents = new byte[] { 5, 6, 7, 8 };
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_Reply()
        {
            // Verify that we can submit a non-dialog request from
            // one core to another, respond to it by setting the 
            // calling the core's Reply() method and then verify that
            // we actually received the response.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                SipRequest core2Request = null;

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    core2Request = args.Request;
                    reply = core2Request.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    core2.Reply(args, reply);
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_ReplyAsync()
        {
            // Verify that we can submit a non-dialog request from
            // one core to another, respond to it asynchronously and
            // then verify that we actually received the response.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                SipRequest core2Request = null;

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    core2Request = args.Request;
                    reply = core2Request.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };

                    args.WillRespondAsynchronously = true;
                    AsyncCallback callback = delegate(IAsyncResult ar)
                    {
                        AsyncTimer.EndTimer(ar);
                        args.Transaction.SendResponse(reply);
                    };

                    AsyncTimer.BeginTimer(TimeSpan.FromMilliseconds(100), callback, null);
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_NotImplemented_NoHandler()
        {
            // Verify that the core sends a 501 (Not Implemented)
            // reply if there's no RequestReceived handler.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                Assert.AreEqual(SipStatus.NotImplemented, result.Status);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.NotImplemented, core1Response.Status);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_NotImplemented_NoResponse()
        {
            // Verify that the core sends a 501 (Not Implemented)
            // reply if there's a RequestReceived handler but
            // it doesn't return a response.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {

                    core1Response = args.Response;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {

                    // Not returning a response: Should cause 501 (Not Implemented) to be sent
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                Assert.AreEqual(SipStatus.NotImplemented, result.Status);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.NotImplemented, core1Response.Status);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_Timeout()
        {
            // Verify that a request to a non-existent server will timeout.

            StartCores();
            core2.DisableTransports();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                Assert.AreEqual(SipStatus.RequestTimeout, result.Status);

                // Verify that the core1 ResponseReceived event handler was not called

                Assert.IsNull(core1Response);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Request_Blast()
        {
            // Verify that we can submit multiple requests to a core.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                SipRequest core2Request = null;

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    args.Response = core2Request.CreateResponse(SipStatus.OK, "Hello World!");
                    args.Response.Contents = args.Request.Contents;
                };

                // Submit a bunch of request and verify the responses.

                for (int i = 0; i < 1000; i++)
                {
                    SipRequest request;
                    SipResult result;
                    SipResponse response;
                    byte[] data;

                    data = new byte[4];
                    Helper.Fill32(data, i);

                    request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    request.Contents = data;

                    result = core1.Request(request);
                    response = result.Response;
                    Assert.AreEqual(SipStatus.OK, result.Status);
                    Assert.AreEqual(SipStatus.OK, response.Status);
                    Assert.AreEqual("Hello World!", response.ReasonPhrase);
                    CollectionAssert.AreEqual(data, response.Contents);

                    // Verify that the core1 ResponseReceived event handler was called

                    Assert.IsNotNull(core1Response);
                    Assert.AreEqual(SipStatus.OK, core1Response.Status);
                    Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                    CollectionAssert.AreEqual(data, core1Response.Contents);

                    // Verify that the core2 RequestReceived event handler was called

                    Assert.IsNotNull(core2Request);
                    Assert.AreEqual(SipMethod.Info, core2Request.Method);
                    CollectionAssert.AreEqual(data, core2Request.Contents);
                }
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBasicCore_Outbound_Proxy()
        {
            // Verify that we can we can force a request to a destination
            // other than that specified in the request URI by specifying
            // the core's OutboundProxyUri.

            StartCores();

            try
            {
                SipResponse core1Response = null;

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                };

                SipRequest core2Request = null;

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    core2Request = args.Request;
                    reply = core2Request.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    core2.Reply(args, reply);
                };

                // Submit a request and verify the response.

                SipRequest request;
                SipResult result;
                SipResponse response;

                core1.OutboundProxyUri = core2Uri;
                request = new SipRequest(SipMethod.Info, "sip:www.lilltek.com:8080;transport=tcp", null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = core1.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);
            }
            finally
            {
                StopCores();
            }
        }
    }
}

