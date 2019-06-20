//-----------------------------------------------------------------------------
// FILE:        _Transactions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

// $todo(jeff.lill): 
//
// I need to come back here and implement tests that
// really exercise the transaction state machines by
// simulating packet retransmissions, timeouts, etc.
//
// I also need some tests that verify that the transport
// and endpoint selection rules are being followed
// (probably in a different source file).

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _Transactions
    {
        //---------------------------------------------------------------------
        // Private types

        private enum ServerOp
        {
            Abort,
            OK,
            ProvisionalOK,
            ProvisionalProvisionalOK,
            Error,
            Timeout
        }

        private class TestCore : SipCore, ISipMessageRouter
        {
            private ISipAgent agent;

            public TestCore(SipCoreSettings settings)
                : base(settings)
            {
            }

            public void Start(ISipAgent agent)
            {
                this.agent = agent;

                base.SetRouter(this);
                base.Agents.Add(agent);
                base.Start();
            }

            /// <summary>
            /// Routes a <see cref="SipMessage" /> received by an <see cref="ISipTransport" /> to the <see cref="ISipAgent" />
            /// instance that needs to handle it.
            /// </summary>
            /// <param name="transport">The <see cref="ISipTransport" /> that received the message.</param>
            /// <param name="message">The <see cref="SipMessage" /> received by the transport.</param>
            public void Route(ISipTransport transport, SipMessage message)
            {
                if (message is SipRequest && agent is SipServerAgent)
                    agent.OnReceive(transport, message);
                else if (message is SipResponse && agent is SipClientAgent)
                    agent.OnReceive(transport, message);
            }

            /// <summary>
            /// Returns the <see cref="ISipTransport" /> that will be used to
            /// deliver a <see cref="SipMessage" /> from a source <see cref="ISipAgent" />.
            /// </summary>
            /// <param name="agent">The source agent.</param>
            /// <param name="request">The <see cref="SipRequest" /> to be delivered.</param>
            /// <param name="remoteEP">Returns as the destination server's <see cref="NetworkBinding" />.</param>
            /// <returns>The <see cref="ISipTransport" /> that will be used for delivery (or <c>null</c>).</returns>
            /// <remarks>
            /// <note>
            /// <c>null</c> is a valid return value.  This indicates that there are
            /// no appropriate transports available to deliver this message.
            /// </note>
            /// </remarks>
            public ISipTransport SelectTransport(ISipAgent agent, SipRequest request, out NetworkBinding remoteEP)
            {
                SipTransportType transportType;

                if (!SipHelper.TryGetRemoteBinding(request.Uri, out remoteEP, out transportType))
                    return null;

                // Select the first transport that looks decent.  If the desired transport
                // is not specified, then favor UDP since most of the world is compatible
                // with that.

                if (transportType == SipTransportType.UDP || transportType == SipTransportType.Unspecified)
                {
                    foreach (ISipTransport transport in base.Transports)
                        if (transport.TransportType == SipTransportType.UDP)
                            return transport;

                    return null;
                }

                // Otherwise match the transport.

                foreach (ISipTransport transport in base.Transports)
                    if (transport.TransportType == transportType)
                        return transport;

                return null;
            }

            public void SetHandler(SipRequestDelegate handler)
            {
                base.RequestReceived += handler;
            }

            public void SetHandler(SipResponseDelegate handler)
            {
                base.ResponseReceived += handler;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object syncLock = new object();
        private ServerOp serverOp;
        private List<SipRequestEventArgs> requestArgs = new List<SipRequestEventArgs>();
        private List<SipResponseEventArgs> responseArgs = new List<SipResponseEventArgs>();

        private void OnRequest(object sender, SipRequestEventArgs args)
        {
            SipResponse response;

            lock (syncLock)
                requestArgs.Add(args);

            switch (serverOp)
            {
                case ServerOp.Abort:

                    args.Transaction.Abort();
                    break;

                case ServerOp.OK:

                    response = args.Request.CreateResponse(SipStatus.OK, null);
                    if (args.Request.Contents != null)
                    {

                        response.Contents = new byte[args.Request.ContentLength];
                        Array.Copy(args.Request.Contents, response.Contents, args.Request.ContentLength);
                    }

                    args.Transaction.SendResponse(response);
                    break;

                case ServerOp.ProvisionalOK:

                    args.Transaction.SendResponse(args.Request.CreateResponse(SipStatus.Trying, null));
                    args.Transaction.SendResponse(args.Request.CreateResponse(SipStatus.OK, null));
                    break;

                case ServerOp.ProvisionalProvisionalOK:

                    response = args.Request.CreateResponse(SipStatus.Trying, null);
                    response.AddHeader("Test", "0");
                    args.Transaction.SendResponse(response);
                    Thread.Sleep(50);

                    response = args.Request.CreateResponse(SipStatus.Trying, null);
                    response.AddHeader("Test", "1");
                    args.Transaction.SendResponse(response);
                    Thread.Sleep(50);

                    response = args.Request.CreateResponse(SipStatus.OK, null);
                    response.AddHeader("Test", "2");
                    args.Transaction.SendResponse(response);
                    Thread.Sleep(50);

                    break;

                case ServerOp.Error:

                    args.Transaction.SendResponse(args.Request.CreateResponse(SipStatus.NotImplemented, null));
                    break;

                case ServerOp.Timeout:

                    Thread.Sleep(500 * 64 + 1000);     // Wait 1 second longer than T1
                    break;
            }
        }

        private void OnResponse(object sender, SipResponseEventArgs args)
        {
            lock (syncLock)
                responseArgs.Add(args);
        }

        private void Clear()
        {
            requestArgs.Clear();
            responseArgs.Clear();
        }

        //---------------------------------------------------------------------
        // Tests

        private void Basic(SipTransportType transportType)
        {
            TestCore serverCore = null;
            TestCore clientCore = null;
            SipServerAgent serverAgent;
            SipClientAgent clientAgent;
            SipCoreSettings settings;
            SipRequest request;
            SipResponse response;
            SipResult result;

            try
            {
                // Initialize the test cores

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:5060"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:5060"),0)
                };

                serverCore = new TestCore(settings);
                serverAgent = new SipServerAgent(serverCore, serverCore);
                serverCore.Start(serverAgent);
                serverCore.SetHandler(new SipRequestDelegate(OnRequest));

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:0"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:0"),0)
                };

                clientCore = new TestCore(settings);
                clientAgent = new SipClientAgent(clientCore, clientCore);
                clientCore.Start(clientAgent);
                clientCore.SetHandler(new SipResponseDelegate(OnResponse));

                // Verify a basic Request/OK transaction

                Clear();

                request = new SipRequest(SipMethod.Register, string.Format("sip:127.0.0.1:5060;transport={0}", transportType.ToString().ToLowerInvariant()), null);
                request.AddHeader(SipHeader.To, "sip:jeff@lilltek.com");
                request.AddHeader(SipHeader.From, "sip:jeff@lill-home.com");

                serverOp = ServerOp.OK;
                result = clientAgent.Request(request, null);
                Thread.Sleep(100);

                Assert.AreEqual(SipStatus.OK, result.Status);

                // Verify the response headers

                request = result.Request;
                response = result.Response;

                Assert.AreEqual(1, requestArgs.Count);
                Assert.AreEqual(1, responseArgs.Count);

                Assert.AreEqual(request.Headers[SipHeader.To].Text, new SipValue(request.Headers[SipHeader.To].Text).Text);
                Assert.IsTrue(string.IsNullOrWhiteSpace(new SipValue(response.Headers[SipHeader.To].Text)["tag"]));

                Assert.AreEqual(request.Headers[SipHeader.From].Text, response.Headers[SipHeader.From].Text);
                Assert.AreEqual(request.Headers[SipHeader.CallID].Text, response.Headers[SipHeader.CallID].Text);
                Assert.AreEqual(request.Headers[SipHeader.CSeq].Text, response.Headers[SipHeader.CSeq].Text);
                Assert.AreEqual(request.Headers[SipHeader.Via].Text, response.Headers[SipHeader.Via].Text);

                // Verify that "Max-Forwards: 70" headers were added to the request message.

                Assert.AreEqual(SipHelper.MaxForwards, requestArgs[0].Request.GetHeaderText(SipHeader.MaxForwards));

                // Verify requests that result in errors.

                Clear();

                request = new SipRequest(SipMethod.Register, string.Format("sip:127.0.0.1:5060;transport={0}", transportType.ToString().ToLowerInvariant()), null);
                request.AddHeader(SipHeader.To, "sip:jeff@lilltek.com");
                request.AddHeader(SipHeader.From, "sip:jeff@lill-home.com");

                serverOp = ServerOp.Error;
                result = clientAgent.Request(request, null);
                Thread.Sleep(100);

                Assert.AreEqual(SipStatus.NotImplemented, result.Status);

                // Verify the response headers

                request = result.Request;
                response = result.Response;

                Assert.AreEqual(1, requestArgs.Count);
                Assert.AreEqual(1, responseArgs.Count);

                Assert.AreEqual(request.Headers[SipHeader.To].Text, new SipValue(request.Headers[SipHeader.To].Text).Text);
                Assert.IsTrue(string.IsNullOrWhiteSpace(new SipValue(response.Headers[SipHeader.To].Text)["tag"]));

                Assert.AreEqual(request.Headers[SipHeader.From].Text, response.Headers[SipHeader.From].Text);
                Assert.AreEqual(request.Headers[SipHeader.CallID].Text, response.Headers[SipHeader.CallID].Text);
                Assert.AreEqual(request.Headers[SipHeader.CSeq].Text, response.Headers[SipHeader.CSeq].Text);
                Assert.AreEqual(request.Headers[SipHeader.Via].Text, response.Headers[SipHeader.Via].Text);
            }
            finally
            {
                if (clientCore != null)
                    clientCore.Stop();

                if (serverCore != null)
                    serverCore.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_TCP_Basic()
        {
            Basic(SipTransportType.TCP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_UDP_Basic()
        {
            Basic(SipTransportType.UDP);
        }

        private void Provisional(SipTransportType transportType)
        {
            TestCore serverCore = null;
            TestCore clientCore = null;
            SipServerAgent serverAgent;
            SipClientAgent clientAgent;
            SipCoreSettings settings;
            SipRequest request;
            SipResponse response;
            SipResult result;

            try
            {

                // Initialize the test cores

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:5060"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:5060"),0)
                };

                serverCore = new TestCore(settings);
                serverAgent = new SipServerAgent(serverCore, serverCore);
                serverCore.Start(serverAgent);
                serverCore.SetHandler(new SipRequestDelegate(OnRequest));

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:0"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:0"),0)
                };

                clientCore = new TestCore(settings);
                clientAgent = new SipClientAgent(clientCore, clientCore);
                clientCore.Start(clientAgent);
                clientCore.SetHandler(new SipResponseDelegate(OnResponse));

                // Verify a transaction with a single provisional response.
                //
                // I'm also going to verify that setting SipRequest.ViaInstanceParam 
                // resulted in the "instance" parameter being added to the requests
                // Via header.

                Clear();

                request = new SipRequest(SipMethod.Register, string.Format("sip:127.0.0.1:5060;transport={0}", transportType.ToString().ToLowerInvariant()), null);
                request.AddHeader(SipHeader.To, "sip:jeff@lilltek.com");
                request.AddHeader(SipHeader.From, "sip:jeff@lill-home.com");

                serverOp = ServerOp.ProvisionalOK;
                result = clientAgent.Request(request, null);
                Thread.Sleep(100);

                Assert.AreEqual(SipStatus.OK, result.Status);

                // Verify the response headers

                request = result.Request;
                response = result.Response;

                Assert.AreEqual(1, requestArgs.Count);
                Assert.AreEqual(2, responseArgs.Count);
                Assert.AreEqual(SipStatus.Trying, responseArgs[0].Response.Status);
                Assert.AreEqual(SipStatus.OK, responseArgs[1].Response.Status);

                Assert.AreEqual(request.Headers[SipHeader.To].Text, new SipValue(request.Headers[SipHeader.To].Text).Text);
                Assert.IsTrue(string.IsNullOrWhiteSpace(new SipValue(response.Headers[SipHeader.To].Text)["tag"]));

                Assert.AreEqual(request.Headers[SipHeader.From].Text, response.Headers[SipHeader.From].Text);
                Assert.AreEqual(request.Headers[SipHeader.CallID].Text, response.Headers[SipHeader.CallID].Text);
                Assert.AreEqual(request.Headers[SipHeader.CSeq].Text, response.Headers[SipHeader.CSeq].Text);
                Assert.AreEqual(request.Headers[SipHeader.Via].Text, response.Headers[SipHeader.Via].Text);

                // Verify a transaction with two provisional responses

                Clear();

                request = new SipRequest(SipMethod.Register, string.Format("sip:127.0.0.1:5060;transport={0}", transportType.ToString().ToLowerInvariant()), null);
                request.AddHeader(SipHeader.To, "sip:jeff@lilltek.com");
                request.AddHeader(SipHeader.From, "sip:jeff@lill-home.com");

                serverOp = ServerOp.ProvisionalProvisionalOK;
                result = clientAgent.Request(request, null);
                Thread.Sleep(100);

                Assert.AreEqual(SipStatus.OK, result.Status);

                // Verify the response headers

                request = result.Request;
                response = result.Response;

                Assert.AreEqual(1, requestArgs.Count);
                Assert.AreEqual(3, responseArgs.Count);
                Assert.AreEqual(SipStatus.Trying, responseArgs[0].Response.Status);
                Assert.AreEqual("0", responseArgs[0].Response["Test"].Text);
                Assert.AreEqual(SipStatus.Trying, responseArgs[1].Response.Status);
                Assert.AreEqual("1", responseArgs[1].Response["Test"].Text);
                Assert.AreEqual(SipStatus.OK, responseArgs[2].Response.Status);
                Assert.AreEqual("2", responseArgs[2].Response["Test"].Text);

                Assert.AreEqual(request.Headers[SipHeader.To].Text, new SipValue(request.Headers[SipHeader.To].Text).Text);
                Assert.IsTrue(string.IsNullOrWhiteSpace(new SipValue(response.Headers[SipHeader.To].Text)["tag"]));

                Assert.AreEqual(request.Headers[SipHeader.From].Text, response.Headers[SipHeader.From].Text);
                Assert.AreEqual(request.Headers[SipHeader.CallID].Text, response.Headers[SipHeader.CallID].Text);
                Assert.AreEqual(request.Headers[SipHeader.CSeq].Text, response.Headers[SipHeader.CSeq].Text);
                Assert.AreEqual(request.Headers[SipHeader.Via].Text, response.Headers[SipHeader.Via].Text);
            }
            finally
            {
                if (clientCore != null)
                    clientCore.Stop();

                if (serverCore != null)
                    serverCore.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_TCP_Provisional()
        {
            Provisional(SipTransportType.TCP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_UDP_Provisional()
        {
            Provisional(SipTransportType.UDP);
        }

        private void Blast(SipTransportType transportType)
        {
            TestCore serverCore = null;
            TestCore clientCore = null;
            SipServerAgent serverAgent;
            SipClientAgent clientAgent;
            SipCoreSettings settings;
            SipRequest request;
            SipResponse response;
            SipResult result;

            try
            {
                // Initialize the test cores

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:5060"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:5060"),0)
                };

                serverCore = new TestCore(settings);
                serverAgent = new SipServerAgent(serverCore, serverCore);
                serverCore.Start(serverAgent);
                serverCore.SetHandler(new SipRequestDelegate(OnRequest));

                settings = new SipCoreSettings();
                settings.TransportSettings = new SipTransportSettings[] {

                    new SipTransportSettings(SipTransportType.UDP,NetworkBinding.Parse("ANY:0"),0),
                    new SipTransportSettings(SipTransportType.TCP,NetworkBinding.Parse("ANY:0"),0)
                };

                clientCore = new TestCore(settings);
                clientAgent = new SipClientAgent(clientCore, clientCore);
                clientCore.Start(clientAgent);
                clientCore.SetHandler(new SipResponseDelegate(OnResponse));

                // Do a bunch of requests

                for (int i = 0; i < 1000; i++)
                {

                    Clear();

                    request = new SipRequest(SipMethod.Register, string.Format("sip:127.0.0.1:5060;transport={0}", transportType.ToString().ToLowerInvariant()), null);
                    request.AddHeader(SipHeader.To, "sip:jeff@lilltek.com");
                    request.AddHeader(SipHeader.From, "sip:jeff@lill-home.com");
                    request.Contents = new byte[] { (byte)i };

                    serverOp = ServerOp.OK;
                    result = clientAgent.Request(request, null);
                    response = result.Response;
                    Thread.Sleep(100);

                    Assert.AreEqual(SipStatus.OK, result.Status);
                    CollectionAssert.AreEqual(request.Contents, response.Contents);
                }
            }
            finally
            {
                if (clientCore != null)
                    clientCore.Stop();

                if (serverCore != null)
                    serverCore.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_TCP_Blast()
        {
            Blast(SipTransportType.TCP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void Transactions_UDP_Blast()
        {
            Blast(SipTransportType.UDP);
        }
    }
}

