//-----------------------------------------------------------------------------
// FILE:        _SipDialog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;

using LillTek.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

// $todo(jeff.lill): 
//
// Add some tests for session refresh timers (once these have been
// implemented) as well general orphaned dialog situations.

// $todo(jeff.lill): Add tests for early dialog CANCEL related special cases.

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipDialog
    {
        private SipTraceMode traceMode = SipTraceMode.None;
        private TimeSpan yieldTime;
        private TimeSpan earlyTTL;

        private SipUri core1Uri;
        private SipUri core2Uri;
        private SipBasicCore core1;
        private SipBasicCore core2;

        [TestInitialize]
        public void Initialize()
        {
            //NetTrace.Start();
            //traceMode = SipTraceMode.Receive;
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

            settings = new SipCoreSettings();
            yieldTime = Helper.Multiply(settings.BkInterval, 2);
            earlyTTL = TimeSpan.FromSeconds(5);

            Helper.GetNetworkInfo(out address, out subnet);
            core1Uri = (SipUri)string.Format("sip:{0}:7725;transport=udp", address);
            core2Uri = (SipUri)string.Format("sip:{0}:7726;transport=udp", address);

            settings = new SipCoreSettings();
            settings.EarlyDialogTTL = earlyTTL - Helper.Multiply(settings.BkInterval, 2);
            settings.TransportSettings = new SipTransportSettings[] { new SipTransportSettings(SipTransportType.UDP, new NetworkBinding(core1Uri.Host, core1Uri.Port), 0) };

            core1 = new SipBasicCore(settings);
            core1.SetTraceMode(traceMode);

            settings = new SipCoreSettings();
            settings.EarlyDialogTTL = earlyTTL - Helper.Multiply(settings.BkInterval, 2);
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
        public void SipDialog_Connect_Close_Initiating()
        {
            // Verify that we can establish a dialog from core1 to core2,
            // that all of the appropriate event handles are called and then
            // that we can close the dialog from the initiating side and 
            // have the close related event handlers called.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Add event handlers to the initiating dialog

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Add event handles to the accepting dialog

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {

                    acceptingClosed = true;
                };

                // Verify that the client side core event handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Connect_Close_Accepting()
        {
            // Verify that we can establish a dialog from core1 to core2,
            // that all of the appropriate event handles are called and then
            // that we can close the dialog from the accepting side and 
            // have the close related event handlers called.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(response);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Add event handlers to the initiating dialog

                bool initiatingClosed = false;

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Add event handles to the accepting dialog

                bool acceptingClosed = false;

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                // Verify that the client side core event handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Close the dialog

                acceptingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Connect_Timeout()
        {
            // Verify that an attempt to establish a dialog with a non-existant
            // endpoint will eventually timeout.

            SipDialog initiatingDialog = null;

            StartCores();
            core2.Stop();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);

                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);
                Assert.AreEqual(SipStatus.RequestTimeout, initiatingDialog.InviteStatus);
                initiatingDialog.Close();

                // Verify that the client side core event handlers were called

                Assert.IsFalse(core1Created);
                Assert.IsFalse(core1Confirmed);
                Assert.IsFalse(core1Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Connect_Reject()
        {
            // Verify that we can explicitly reject a dialog.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    response = args.ClientRequest.CreateResponse(SipStatus.BusyHere, null);
                    args.Dialog.SendInviteResponse(response);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);

                // Verify the invite result 

                Assert.AreEqual(SipStatus.BusyHere, initiatingDialog.InviteStatus);

                // Verify that the client side core event handlers were called

                Assert.IsFalse(core1Created);
                Assert.IsFalse(core1Confirmed);
                Assert.IsFalse(core1Closed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsFalse(core2Confirmed);
                Assert.IsFalse(core2Closed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Connect_Close_Core()
        {
            // Verify that we can establish a dialog from core1 to core2,
            // that all of the appropriate event handles are called and then
            // that when we stop a core that its dialogs are also closed.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new SdpPayload().ToArray();

                    args.Dialog.SendInviteResponse(response);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Verify that the client side core event handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Stop core2 which should close the dialog as well.

                core2.Stop();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Connect_Close_Timeout()
        {
            // Verify that we can establish a dialog from core1 to core2,
            // that all of the appropriate event handles are called and then
            // after stopping core2's transports, that closing the dialog
            // will eventually complete, calling all of the event handlers.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse response;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    response = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    response.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    response.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    response.Contents = new SdpPayload().ToArray();

                    args.Dialog.SendInviteResponse(response);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Verify that the client side core event handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Stop core2's transports, simulating a network or hardware failure.

                core2.DisableTransports();

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsFalse(core2Closed);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Transaction_Single()
        {
            // Verify that we can establish a dialog from core1 to core2,
            // and then that we can perform a transaction from one core
            // to the other.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse serverResponse;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    serverResponse = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    serverResponse.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    serverResponse.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    serverResponse.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(serverResponse);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Verify that the client side core event handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Perform a transaction against the accepting side.

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    // Reflect the request content-type and contents back to the client

                    args.Response = args.Request.CreateResponse(SipStatus.OK, null);
                    args.Response.SetHeader(SipHeader.ContentType, args.Request.GetHeaderText(SipHeader.ContentType));
                    args.Response.Contents = args.Request.Contents;
                };

                SipRequest request;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, "sip:test", null);
                request.SetHeader(SipHeader.ContentType, "test");
                request.Contents = new byte[] { 4, 3, 2, 1 };

                response = initiatingDialog.Request(request).Response;

                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("test", response.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1 }, response.Contents);

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_CoreReply()
        {
            // Verify that we can respond to a dialog request by calling
            // the core's Reply() method.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {

                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipRequest acceptingRequest = null;

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    acceptingRequest = args.Request;
                    reply = acceptingRequest.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    core2.Reply(args, reply);
                };

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreSame(initiatingDialog, core1Dialog);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreSame(acceptingDialog, core2Dialog);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);

                // Verify that the client side dialog's ResponseReceived handler was called

                Assert.IsNotNull(initiatingResponse);
                Assert.AreEqual(SipStatus.OK, initiatingResponse.Status);
                Assert.AreEqual("Hello World!", initiatingResponse.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, initiatingResponse.Contents);

                // Verify that the server side dialog's RequestReceived handler was called

                Assert.IsNotNull(acceptingRequest);
                Assert.AreEqual(SipMethod.Info, acceptingRequest.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, acceptingRequest.Contents);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_DialogReply()
        {
            // Verify that we can respond to a dialog request by calling
            // the dialog's Reply() method.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipRequest acceptingRequest = null;

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    acceptingRequest = args.Request;
                    reply = acceptingRequest.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    acceptingDialog.Reply(args, reply);
                };

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreSame(initiatingDialog, core1Dialog);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreSame(acceptingDialog, core2Dialog);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);

                // Verify that the client side dialog's ResponseReceived handler was called

                Assert.IsNotNull(initiatingResponse);
                Assert.AreEqual(SipStatus.OK, initiatingResponse.Status);
                Assert.AreEqual("Hello World!", initiatingResponse.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, initiatingResponse.Contents);

                // Verify that the server side dialog's RequestReceived handler was called

                Assert.IsNotNull(acceptingRequest);
                Assert.AreEqual(SipMethod.Info, acceptingRequest.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, acceptingRequest.Contents);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_SetResponse()
        {
            // Verify that we can respond to a dialog request by setting
            // the response in the RequestReceived event's arguments.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {

                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipRequest acceptingRequest = null;

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    acceptingRequest = args.Request;
                    reply = acceptingRequest.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    args.Response = reply;
                };

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreSame(initiatingDialog, core1Dialog);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreSame(acceptingDialog, core2Dialog);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);

                // Verify that the client side dialog's ResponseReceived handler was called

                Assert.IsNotNull(initiatingResponse);
                Assert.AreEqual(SipStatus.OK, initiatingResponse.Status);
                Assert.AreEqual("Hello World!", initiatingResponse.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, initiatingResponse.Contents);

                // Verify that the server side dialog's RequestReceived handler was called

                Assert.IsNotNull(acceptingRequest);
                Assert.AreEqual(SipMethod.Info, acceptingRequest.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, acceptingRequest.Contents);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_ReplyAsync()
        {
            // Verify that we can respond to a dialog request asynchronously
            // and then verify that we actually received the response.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipRequest acceptingRequest = null;
                bool acceptingClosed = false;

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    acceptingRequest = args.Request;
                    reply = acceptingRequest.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };

                    args.WillRespondAsynchronously = true;
                    AsyncCallback callback = delegate(IAsyncResult ar)
                    {
                        AsyncTimer.EndTimer(ar);
                        args.Transaction.SendResponse(reply);
                    };

                    AsyncTimer.BeginTimer(TimeSpan.FromMilliseconds(100), callback, null);
                };

                acceptingDialog.Closed += delegate()
                {
                    acceptingClosed = true;
                };

                SipResponse initiatingResponse = null;
                bool initiatingClosed = false;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                initiatingDialog.Closed += delegate()
                {
                    initiatingClosed = true;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;
                SipResponse response;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                response = result.Response;
                Thread.Sleep(yieldTime);
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello World!", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Verify that the core1 ResponseReceived event handler was called

                Assert.IsNotNull(core1Response);
                Assert.AreSame(initiatingDialog, core1Dialog);
                Assert.AreEqual(SipStatus.OK, core1Response.Status);
                Assert.AreEqual("Hello World!", core1Response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, core1Response.Contents);

                // Verify that the core2 RequestReceived event handler was called

                Assert.IsNotNull(core2Request);
                Assert.AreSame(acceptingDialog, core2Dialog);
                Assert.AreEqual(SipMethod.Info, core2Request.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, core2Request.Contents);

                // Verify that the client side dialog's ResponseReceived handler was called

                Assert.IsNotNull(initiatingResponse);
                Assert.AreEqual(SipStatus.OK, initiatingResponse.Status);
                Assert.AreEqual("Hello World!", initiatingResponse.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, initiatingResponse.Contents);

                // Verify that the server side dialog's RequestReceived handler was called

                Assert.IsNotNull(acceptingRequest);
                Assert.AreEqual(SipMethod.Info, acceptingRequest.Method);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, acceptingRequest.Contents);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Verify that the client and server side dialog close event handlers were called.

                Assert.IsTrue(core1Closed);
                Assert.IsTrue(core2Closed);
                Assert.IsTrue(initiatingClosed);
                Assert.IsTrue(acceptingClosed);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_Timeout()
        {
            // Verify that a dialog request to a server that has gone offline
            // results in a timeout.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipRequest acceptingRequest = null;

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    acceptingRequest = args.Request;
                    reply = acceptingRequest.CreateResponse(SipStatus.OK, "Hello World!");
                    reply.Contents = new byte[] { 5, 6, 7, 8 };
                    core2.Reply(args, reply);
                };

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Simulate a network failure and then submit the request

                SipRequest request;
                SipResult result;

                core2.DisableTransports();
                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                Assert.AreEqual(SipStatus.RequestTimeout, result.Status);

                // Refernce these variables to avoid compiler warnings

                Assert.IsFalse(core1Closed);
                Assert.IsFalse(core2Closed);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_NotImplemented_NoHandler()
        {
            // Verify that a dialog request results in a 501 (Not Implemented)
            // response if there's no RequestReceived handler.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                Assert.AreEqual(SipStatus.NotImplemented, result.Status);

                // Refernce these variables to avoid compiler warnings

                Assert.IsFalse(core1Closed);
                Assert.IsFalse(core2Closed);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_NotImplemented_NoResponse()
        {
            // Verify that a dialog request results in a 501 (Not Implemented)
            // response if there's the RequestReceived handler doesn't
            // return a response.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Initialize the core1 event handlers

                bool core1Created = false;
                bool core1Confirmed = false;
                bool core1Closed = false;
                SipResponse core1Response = null;
                SipDialog core1Dialog = null;

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Created = true;
                    args.Dialog.SendAckRequest(null);
                };

                core1.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Confirmed = true;
                };

                core1.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core1Closed = true;
                };

                core1.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    core1Response = args.Response;
                    core1Dialog = args.Dialog;
                };

                // Initialize the core2 event handlers

                bool core2Created = false;
                bool core2Confirmed = false;
                bool core2Closed = false;
                SipRequest core2Request = null;
                SipDialog core2Dialog = null;

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;
                    core2Created = true;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);
                    reply.SetHeader(SipHeader.ContentType, SipHelper.SdpMimeType);
                    reply.Contents = new byte[] { 0, 1, 2, 3, 4 };

                    args.Dialog.SendInviteResponse(reply);
                };

                core2.DialogConfirmed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Confirmed = true;
                };

                core2.DialogClosed += delegate(object sender, SipDialogEventArgs args)
                {
                    core2Closed = true;
                };

                core2.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    core2Request = args.Request;
                    core2Dialog = args.Dialog;
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    // Not returning a response: Should cause 501 (Not Implemented) to be sent
                };

                // Setup the dialog event handlers now that the dialog is established

                SipResponse initiatingResponse = null;

                initiatingDialog.ResponseReceived += delegate(object sender, SipResponseEventArgs args)
                {
                    initiatingResponse = args.Response;
                };

                // Verify that the client side core event dialog related handlers were called

                Assert.IsTrue(core1Created);
                Assert.IsTrue(core1Confirmed);
                Assert.IsTrue(initiatingDialog.IsInitiating);
                Assert.IsFalse(initiatingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core2Uri, initiatingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core2Uri + ">", initiatingDialog.RemoteContact.ToString());

                // Verify the invite result payload

                Assert.AreEqual(SipStatus.OK, initiatingDialog.InviteStatus);
                Assert.AreEqual(SipHelper.SdpMimeType, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.ContentType));
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, initiatingDialog.InviteResponse.Contents);

                // Verify that the server side core dialog related event handlers were called

                Assert.IsTrue(core2Created);
                Assert.IsTrue(core2Confirmed);
                Assert.IsFalse(acceptingDialog.IsInitiating);
                Assert.IsTrue(acceptingDialog.IsAccepting);

                // Verify the client side state

                Assert.AreEqual((string)core1Uri, acceptingDialog.RemoteUri);
                Assert.AreEqual("<" + (string)core1Uri + ">", acceptingDialog.RemoteContact.ToString());

                // Submit the request

                SipRequest request;
                SipResult result;

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                Assert.AreEqual(SipStatus.NotImplemented, result.Status);

                // Refernce these variables to avoid compiler warnings

                Assert.IsFalse(core1Closed);
                Assert.IsFalse(core2Closed);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_Bidirectional()
        {
            // Verify that requests can be sent in both directions
            // on a dialog.

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Setup the handler to accept the dialog

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);

                    args.Dialog.SendInviteResponse(reply);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Initiating");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Accepting");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                SipRequest request;
                SipResult result;
                SipResponse response;

                // Submit a request from the initiating side.

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 1, 2, 3, 4 };

                result = initiatingDialog.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello from: Accepting", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, response.Contents);

                // Submit a request from the accepting side.

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.Contents = new byte[] { 5, 6, 7, 8 };

                result = acceptingDialog.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.OK, result.Status);
                Assert.AreEqual(SipStatus.OK, response.Status);
                Assert.AreEqual("Hello from: Initiating", response.ReasonPhrase);
                CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, response.Contents);

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {

                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_Bidirectional_Blast()
        {
            // Blast requests in both directions on a dialog

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Setup the handler to accept the dialog

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);

                    args.Dialog.SendInviteResponse(reply);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                // Setup the dialog event handlers now that the dialog is established

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Initiating");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Accepting");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                SipRequest request;
                SipResult result;
                SipResponse response;
                byte[] data;

                for (int i = 0; i < 1000; i++)
                {
                    // Submit a request from the initiating side.

                    data = new byte[4];
                    Helper.Fill32(data, i);

                    request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    request.Contents = data;

                    result = initiatingDialog.Request(request);
                    response = result.Response;
                    Assert.AreEqual(SipStatus.OK, result.Status);
                    Assert.AreEqual(SipStatus.OK, response.Status);
                    Assert.AreEqual("Hello from: Accepting", response.ReasonPhrase);
                    CollectionAssert.AreEqual(data, response.Contents);

                    // Submit a request from the accepting side.

                    data = new byte[4];
                    Helper.Fill32(data, -i);

                    request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                    request.Contents = data;

                    result = acceptingDialog.Request(request);
                    response = result.Response;
                    Assert.AreEqual(SipStatus.OK, result.Status);
                    Assert.AreEqual(SipStatus.OK, response.Status);
                    Assert.AreEqual("Hello from: Initiating", response.ReasonPhrase);
                    CollectionAssert.AreEqual(data, response.Contents);
                }

                // Close the dialog

                initiatingDialog.Close();

                Thread.Sleep(yieldTime);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Dialog_Blast()
        {
            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            // Verify that we can establish multiple dialogs and submit
            // requests in both directions on those dialogs.

            StartCores();

            try
            {
                // Setup the handler to accept the dialog

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);

                    args.Dialog.SendInviteResponse(reply);
                };

                for (int i = 0; i < 100; i++)
                {
                    // Create the dialog

                    SipRequest inviteRequest;

                    inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                    inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                    initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                    Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                    // Setup the dialog event handlers now that the dialog is established

                    initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                    {
                        SipResponse reply;

                        reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Initiating");
                        reply.Contents = args.Request.Contents;
                        args.Response = reply;
                    };

                    acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                    {
                        SipResponse reply;

                        reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Accepting");
                        reply.Contents = args.Request.Contents;
                        args.Response = reply;
                    };

                    SipRequest request;
                    SipResult result;
                    SipResponse response;
                    byte[] data;

                    for (int j = 0; j < 10; j++)
                    {
                        // Submit a request from the initiating side.

                        data = new byte[4];
                        Helper.Fill32(data, i * j);

                        request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                        request.Contents = data;

                        result = initiatingDialog.Request(request);
                        response = result.Response;
                        Assert.AreEqual(SipStatus.OK, result.Status);
                        Assert.AreEqual(SipStatus.OK, response.Status);
                        Assert.AreEqual("Hello from: Accepting", response.ReasonPhrase);
                        CollectionAssert.AreEqual(data, response.Contents);

                        // Submit a request from the accepting side.

                        data = new byte[4];
                        Helper.Fill32(data, -i * j);

                        request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                        request.Contents = data;

                        result = acceptingDialog.Request(request);
                        response = result.Response;
                        Assert.AreEqual(SipStatus.OK, result.Status);
                        Assert.AreEqual(SipStatus.OK, response.Status);
                        Assert.AreEqual("Hello from: Initiating", response.ReasonPhrase);
                        CollectionAssert.AreEqual(data, response.Contents);
                    }

                    // Close the dialog

                    initiatingDialog.Close();
                }

                Thread.Sleep(yieldTime);
                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipDialog_Request_RejectBadSequenceNumber()
        {
            // Verify that dialog requests with invalid sequence numbers are
            // rejected with 500 (Server Error).

            SipDialog initiatingDialog = null;
            SipDialog acceptingDialog = null;

            StartCores();

            try
            {
                // Setup the handler to ACK the dialog

                core1.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {

                    args.Dialog.SendAckRequest(null);
                };

                // Setup the handler to accept the dialog

                core2.DialogCreated += delegate(object sender, SipDialogEventArgs args)
                {
                    SipResponse reply;

                    acceptingDialog = args.Dialog;

                    reply = args.ClientRequest.CreateResponse(SipStatus.OK, null);
                    reply.SetHeader(SipHeader.Contact, (SipContactValue)core2Uri);

                    args.Dialog.SendInviteResponse(reply);
                };

                // Create the dialog

                SipRequest inviteRequest;

                inviteRequest = new SipInviteRequest((string)core2Uri, "sip:core2", "sip:core1", new SdpPayload());
                inviteRequest.SetHeader(SipHeader.Contact, (SipContactValue)core1Uri);
                initiatingDialog = core1.CreateDialog(inviteRequest, core1Uri, null);

                Assert.AreEqual(1, core1.DialogCount);
                Assert.AreEqual(1, core2.DialogCount);

                Thread.Sleep(yieldTime);    // Give the transaction the chance to complete

                // Setup the dialog event handlers now that the dialog is established

                initiatingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Initiating");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                acceptingDialog.RequestReceived += delegate(object sender, SipRequestEventArgs args)
                {
                    SipResponse reply;

                    reply = args.Request.CreateResponse(SipStatus.OK, "Hello from: Accepting");
                    reply.Contents = args.Request.Contents;
                    args.Response = reply;
                };

                SipRequest request;
                SipResult result;
                SipResponse response;

                // Submit a request with the proper Call-ID and tags but with 
                // a sequence number of 0 which will be invalid since the initial 
                // INVITE ensures that the current sequence number must be positive
                // at this point.

                request = new SipRequest(SipMethod.Info, (string)core2Uri, null);
                request.SetHeader(SipHeader.To, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.To));
                request.SetHeader(SipHeader.From, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.From));
                request.SetHeader(SipHeader.CallID, initiatingDialog.InviteResponse.GetHeaderText(SipHeader.CallID));
                request.SetHeader(SipHeader.CSeq, new SipCSeqValue(0, SipMethod.Info).ToString());

                result = core1.Request(request);
                response = result.Response;
                Assert.AreEqual(SipStatus.ServerError, result.Status);
                Assert.AreEqual(SipStatus.ServerError, response.Status);

                // Close the dialog

                acceptingDialog.Close();
                Thread.Sleep(yieldTime);

                Assert.AreEqual(0, core1.DialogCount);
                Assert.AreEqual(0, core2.DialogCount);
            }
            finally
            {
                StopCores();
            }
        }
    }
}

