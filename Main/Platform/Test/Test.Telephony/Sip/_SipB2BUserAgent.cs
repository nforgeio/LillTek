//-----------------------------------------------------------------------------
// FILE:        _SipB2BUserAgent.cs
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
    public class _SipB2BUserAgent
    {
        private SipTraceMode traceMode = SipTraceMode.None;
        private TimeSpan yieldTime;

        private SipUri core1Uri;
        private SipUri core2Uri;
        private SipUri b2bUAUri;
        private SipBasicCore core1;
        private SipBasicCore core2;
        private SipBasicCore coreB2BUA;
        private SipB2BUserAgent<object> b2bua;

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            traceMode = SipTraceMode.All;
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        private void StartCores()
        {
            SipCoreSettings settings;
            IPAddress address;
            IPAddress subnet;

            settings = new SipCoreSettings();
            yieldTime = Helper.Multiply(settings.BkInterval, 2);

            Helper.GetNetworkInfo(out address, out subnet);
            core1Uri = (SipUri)string.Format("sip:{0}:7725;transport=udp", address);
            b2bUAUri = (SipUri)string.Format("sip:{0}:7726;transport=udp", address);
            core2Uri = (SipUri)string.Format("sip:{0}:7727;transport=udp", address);

            settings = new SipCoreSettings();
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(core1Uri.Host, core1Uri.Port), 0) };
            core1 = new SipBasicCore(settings);
            core1.SetTraceMode(traceMode);

            settings = new SipCoreSettings();
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(core2Uri.Host, core2Uri.Port), 0) };
            core2 = new SipBasicCore(settings);
            core2.SetTraceMode(traceMode);

            settings = new SipCoreSettings();
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(b2bUAUri.Host, b2bUAUri.Port), 0) };
            coreB2BUA = new SipBasicCore(settings);
            coreB2BUA.SetTraceMode(traceMode);

            try
            {
                core1.Start();
                core2.Start();
                coreB2BUA.Start();

                b2bua = new SipB2BUserAgent<object>(coreB2BUA);
                b2bua.Start();
            }
            catch
            {
                if (core1 != null)
                    core1.Stop();

                if (core2 != null)
                    core2.Stop();

                if (b2bua != null)
                    b2bua.Stop();

                if (coreB2BUA != null)
                    coreB2BUA.Stop();

                throw;
            }
        }

        private void StopCores()
        {
            if (core1 != null)
                core1.Stop();

            if (core2 != null)
                core2.Stop();

            if (b2bua != null)
                b2bua.Stop();

            if (coreB2BUA != null)
                coreB2BUA.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Connect_Close_Initiating()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from the initiating side and 
            // have the close related event handlers called.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Connect_Close_Accepting()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from the accepting side and 
            // have the close related event handlers called.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {

                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the dialog

                acceptingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Connect_Close_B2BUA()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from at the B2BUA side and 
            // have the close related event handlers called.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the session at the B2BUA

                b2bua.CloseSession(b2bUASession);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Connect_NoHook()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA with the minimal set of event handlers to verify that
            // the stack handles null event handlers.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Close the session in the initiating side

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Request_FromInitiating()
        {
            // Establish a dialog through a B2BUA and submit a request
            // from the initiating side without any kind of modification at
            // the B2BUA and verify that the requests and responses
            // got through.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From accepting");
                };

                // Submit a request from the initiating side.

                SipRequest testRequest;
                SipResult testResult;
                SipResponse testResponse;

                testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                testResult = initiatingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From accepting", testResponse.ReasonPhrase);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Request_FromAccepting()
        {
            // Establish a dialog through a B2BUA and submit a request
            // from the accepting side without any kind of modification at
            // the B2BUA and verify that the requests and responses
            // got through.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the initiating dialog event handlers

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From initiating");
                };

                // Submit a request from the accepting side

                SipRequest testRequest;
                SipResult testResult;
                SipResponse testResponse;

                testRequest = new SipRequest(SipMethod.Info, (string)core1Uri, null);
                testResult = acceptingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From initiating", testResponse.ReasonPhrase);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Request_FromBoth()
        {
            // Establish a dialog through a B2BUA and submit requests
            // in both directions without any kind of modification at
            // the B2BUA and verify that the requests and responses
            // got through.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From accepting");
                };

                // Initialize the initiating dialog event handlers

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From initiating");
                };

                // Submit a request from the initiating side.

                SipRequest testRequest;
                SipResult testResult;
                SipResponse testResponse;

                testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                testResult = initiatingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From accepting", testResponse.ReasonPhrase);

                // Submit a request from the accepting side

                testRequest = new SipRequest(SipMethod.Info, (string)core1Uri, null);
                testResult = acceptingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From initiating", testResponse.ReasonPhrase);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Default_Request_Blast()
        {
            // Establish a dialog through a B2BUA and blast requests
            // in both directions without any kind of modification at
            // the B2BUA and verify that the requests and responses
            // got through.
            //
            // Note that I'm going to submit twice as many requests from
            // the initiating side so make sure there isn't any bugs
            // where the B2BUA code confuses the message sequence numbers.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From accepting: " + Helper.FromUTF8(args.Request.Contents));
                };

                // Initialize the initiating dialog event handlers

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From initiating: " + Helper.FromUTF8(args.Request.Contents));
                };

                for (int i = 0; i < 100; i++)
                {
                    // Submit two requests from the initiating side.

                    SipRequest testRequest;
                    SipResult testResult;
                    SipResponse testResponse;

                    testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    testRequest.SetHeader(SipHeader.ContentType, "text/plain");
                    testRequest.Contents = Helper.ToUTF8(i.ToString());

                    testResult = initiatingDialog.Request(testRequest);
                    testResponse = testResult.Response;
                    Assert.AreEqual(SipStatus.OK, testResult.Status);
                    Assert.AreEqual("From accepting: " + i.ToString(), testResponse.ReasonPhrase);

                    testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    testRequest.SetHeader(SipHeader.ContentType, "text/plain");
                    testRequest.Contents = Helper.ToUTF8((-i).ToString());

                    testResult = initiatingDialog.Request(testRequest);
                    testResponse = testResult.Response;
                    Assert.AreEqual(SipStatus.OK, testResult.Status);
                    Assert.AreEqual("From accepting: " + (-i).ToString(), testResponse.ReasonPhrase);

                    // Submit a request from the accepting side

                    testRequest = new SipRequest(SipMethod.Info, (string)core1Uri, null);
                    testRequest.SetHeader(SipHeader.ContentType, "text/plain");
                    testRequest.Contents = Helper.ToUTF8((i + 100000).ToString());

                    testResult = acceptingDialog.Request(testRequest);
                    testResponse = testResult.Response;
                    Assert.AreEqual(SipStatus.OK, testResult.Status);
                    Assert.AreEqual("From initiating: " + (i + 100000).ToString(), testResponse.ReasonPhrase);
                }

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Modify_Connect_Close_Initiating()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from the initiating side and 
            // have the close related event handlers called.
            //
            // This test also verifies that the B2BUA can modify both
            // the INVITE request forwarded to the accepting dialog as
            // well as the INVITE response returned to the initiating dialog.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    if (args.ClientRequest.GetHeaderText("x-Test") != "INVITE request")
                        response = args.ClientRequest.CreateResponse(SipStatus.Decline, null);
                    else
                    {
                        response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                        response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                        response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                        response.Contents = new byte[] { 0, 1, 2, 3, 4 };
                    }

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;

                    args.Request.SetHeader("x-Test", "INVITE request");
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;

                    args.Response.SetHeader("x-Test", "INVITE response");
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteResponse.Status);
                Assert.AreEqual("INVITE response", initiatingDialog.InviteResponse.GetHeaderText("x-Test"));
                Assert.AreEqual("INVITE request", acceptingDialog.InviteRequest.GetHeaderText("x-Test"));

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Modify_Connect_Close_Accepting()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from the accepting side and 
            // have the close related event handlers called.
            //
            // This test also verifies that the B2BUA can modify both
            // the INVITE request forwarded to the accepting dialog as
            // well as the INVITE response returned to the initiating dialog.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    if (args.ClientRequest.GetHeaderText("x-Test") != "INVITE request")
                        response = args.ClientRequest.CreateResponse(SipStatus.Decline, null);
                    else
                    {
                        response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                        response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                        response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                        response.Contents = new byte[] { 0, 1, 2, 3, 4 };
                    }

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;

                    args.Request.SetHeader("x-Test", "INVITE request");
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;

                    args.Response.SetHeader("x-Test", "INVITE response");
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteResponse.Status);
                Assert.AreEqual("INVITE response", initiatingDialog.InviteResponse.GetHeaderText("x-Test"));
                Assert.AreEqual("INVITE request", acceptingDialog.InviteRequest.GetHeaderText("x-Test"));

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the dialog

                acceptingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Modify_Connect_Close_B2BUA()
        {
            // Verify that we can establish a dialog from core1 to core2 through
            // a B2BUA and that all of the appropriate event handles are called 
            // and then that we can close the dialog from the B2BUA session and 
            // have the close related event handlers called.
            //
            // This test also verifies that the B2BUA can modify both
            // the INVITE request forwarded to the accepting dialog as
            // well as the INVITE response returned to the initiating dialog.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;
            SipB2BUASession<object> b2bUASession = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    if (args.ClientRequest.GetHeaderText("x-Test") != "INVITE request")
                        response = args.ClientRequest.CreateResponse(SipStatus.Decline, null);
                    else
                    {
                        response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                        response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                        response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                        response.Contents = new byte[] { 0, 1, 2, 3, 4 };
                    }

                    args.Dialog.SendInviteResponse(response);
                };

                // Initialize the B2BUA event handlers

                bool b2buaInviteRequestReceived = false;
                bool b2buaInviteResponseReceived = false;
                bool b2buaSessionConfirmed = false;
                bool b2buaSessionClosing = false;

                b2bua.InviteRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2bUASession = args.Session;
                    b2buaInviteRequestReceived = true;

                    args.Request.SetHeader("x-Test", "INVITE request");
                };

                b2bua.InviteResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaInviteResponseReceived = true;

                    args.Response.SetHeader("x-Test", "INVITE response");
                };

                b2bua.SessionConfirmed += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionConfirmed = true;
                };

                b2bua.SessionClosing += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    b2buaSessionClosing = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteResponse.Status);
                Assert.AreEqual("INVITE response", initiatingDialog.InviteResponse.GetHeaderText("x-Test"));
                Assert.AreEqual("INVITE request", acceptingDialog.InviteRequest.GetHeaderText("x-Test"));

                // Initialize the accepting dialog event handlers

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Initialize the initiating dialog event handlers

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the B2BUA event handlers were called.

                Assert.IsTrue(b2buaInviteRequestReceived);
                Assert.IsTrue(b2buaInviteResponseReceived);
                Assert.IsTrue(b2buaSessionConfirmed);

                // Verify the session state

                Assert.IsNotNull(b2bUASession);

                Assert.IsNotNull(b2bUASession.ClientDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ClientDialog.State);
                Assert.IsNotNull(initiatingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, initiatingDialog.State);

                Assert.IsNotNull(b2bUASession.ServerDialog);
                Assert.AreEqual(SipDialogState.Confirmed, b2bUASession.ServerDialog.State);
                Assert.IsNotNull(acceptingDialog);
                Assert.AreEqual(SipDialogState.Confirmed, acceptingDialog.State);

                // Close the dialog

                b2bua.CloseSession(b2bUASession);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the B2BUA SessionClosing and dialog Closed events were raised.

                Assert.IsTrue(b2buaSessionClosing);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Modify_Request()
        {
            // Establish a dialog through a B2BUA and submit requests
            // in both directions, modifying both the requests and responses 
            // at the B2BUA and verify that the requests and responses
            // got through.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    bool valid = args.Request.GetHeaderText("x-Test") == "B2BUA: Request from client";

                    args.Response = args.Request.CreateResponse(valid ? SipStatus.OK : SipStatus.ServerError, "From accepting");
                };

                // Initialize the initiating dialog event handlers

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    bool valid = args.Request.GetHeaderText("x-Test") == "B2BUA: Request from server";

                    args.Response = args.Request.CreateResponse(valid ? SipStatus.OK : SipStatus.ServerError, "From initiating");
                };

                // Initialize the B2BUA event handlers

                b2bua.ClientRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Request.SetHeader("x-Test", "B2BUA: Request from client");
                };

                b2bua.ServerRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {

                    args.Request.SetHeader("x-Test", "B2BUA: Request from server");
                };

                b2bua.ClientResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Response.SetHeader("x-Test", "B2BUA: Response from client");
                };

                b2bua.ServerResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Response.SetHeader("x-Test", "B2BUA: Response from server");
                };

                // Submit a request from the initiating side.

                SipRequest testRequest;
                SipResult testResult;
                SipResponse testResponse;

                testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                testResult = initiatingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From accepting", testResponse.ReasonPhrase);
                Assert.AreEqual("B2BUA: Response from server", testResponse.GetHeaderText("x-Test"));

                // Submit a request from the accepting side

                testRequest = new SipRequest(SipMethod.Info, (string)core1Uri, null);
                testResult = acceptingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.OK, testResult.Status);
                Assert.AreEqual("From initiating", testResponse.ReasonPhrase);
                Assert.AreEqual("B2BUA: Response from client", testResponse.GetHeaderText("x-Test"));

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Modify_Request_Blast()
        {
            // Establish a dialog through a B2BUA and blast requests
            // in both directions, modifying both the requests and responses 
            // at the B2BUA and verify that the requests and responses
            // got through.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    bool valid = args.Request.GetHeaderText("x-Test") == "B2BUA: Request from client";

                    args.Response = args.Request.CreateResponse(valid ? SipStatus.OK : SipStatus.ServerError, "From accepting");
                };

                // Initialize the initiating dialog event handlers

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    bool valid = args.Request.GetHeaderText("x-Test") == "B2BUA: Request from server";

                    args.Response = args.Request.CreateResponse(valid ? SipStatus.OK : SipStatus.ServerError, "From initiating");
                };

                // Initialize the B2BUA event handlers

                b2bua.ClientRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Request.SetHeader("x-Test", "B2BUA: Request from client");
                };

                b2bua.ServerRequestReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Request.SetHeader("x-Test", "B2BUA: Request from server");
                };

                b2bua.ClientResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Response.SetHeader("x-Test", "B2BUA: Response from client");
                };

                b2bua.ServerResponseReceived += delegate(object sender, SipB2BUAEventArgs<object> args)
                {
                    args.Response.SetHeader("x-Test", "B2BUA: Response from server");
                };

                for (int i = 0; i < 100; i++)
                {
                    // Submit a request from the initiating side.

                    SipRequest testRequest;
                    SipResult testResult;
                    SipResponse testResponse;

                    testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    testResult = initiatingDialog.Request(testRequest);
                    testResponse = testResult.Response;
                    Assert.AreEqual(SipStatus.OK, testResult.Status);
                    Assert.AreEqual("From accepting", testResponse.ReasonPhrase);
                    Assert.AreEqual("B2BUA: Response from server", testResponse.GetHeaderText("x-Test"));

                    // Submit a request from the accepting side

                    testRequest = new SipRequest(SipMethod.Info, (string)core1Uri, null);
                    testResult = acceptingDialog.Request(testRequest);
                    testResponse = testResult.Response;
                    Assert.AreEqual(SipStatus.OK, testResult.Status);
                    Assert.AreEqual("From initiating", testResponse.ReasonPhrase);
                    Assert.AreEqual("B2BUA: Response from client", testResponse.GetHeaderText("x-Test"));
                }

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Timeout_Connect()
        {
            // Verify dialog connection attempt from core1 to core2 through
            // a B2BUA will timeout if core2 has a simulated failure.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Simulate a failure of core2 and then attempt to create the dialog.

                SipRequest inviteRequest;

                core2.DisableTransports();

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);
                Assert.AreEqual(SipStatus.RequestTimeout, initiatingDialog.InviteStatus);

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Timeout_Close()
        {
            // Verify dialog close attempt from core1 to core2 through
            // a B2BUA will timeout if core2 has a simulated failure.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Establish the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);
                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);

                // Simulate a core2 failure and close the dialog

                core2.DisableTransports();
                initiatingDialog.Close();

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipB2BUserAgent_Timeout_Request()
        {
            // Establish a dialog through a B2BUA, simulate a core2 network
            // failure and then submit a request from core1 and verify that
            // the request times out.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();
            core1.OutboundProxyUri = b2bUAUri;

            try
            {
                // Initialize the core1 event handlers

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    args.Dialog.SendAckRequest(null);
                };

                // Initialize the core2 event handlers

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Initialize the accepting dialog event handlers

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    args.Response = args.Request.CreateResponse(SipStatus.OK, "From accepting");
                };

                // Simulate the core2 failure and then submit a request from the initiating side.

                SipRequest testRequest;
                SipResult testResult;
                SipResponse testResponse;

                core2.DisableTransports();

                testRequest = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                testResult = initiatingDialog.Request(testRequest);
                testResponse = testResult.Response;
                Assert.AreEqual(SipStatus.RequestTimeout, testResult.Status);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the dialogs and sessions have been removed

                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);
                Assert.AreEqual(0, coreB2BUA.DialogCount);

                Assert.AreEqual(0, b2bua.SessionCount);
            }
            finally
            {
                StopCores();
            }
        }
    }
}



