//-----------------------------------------------------------------------------
// FILE:        _SipUdpTransport.cs
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipUdpTransport
    {
        private class TestTransport : SipUdpTransport, ISipMessageRouter
        {
            private SipMessage received;
            private AutoResetEvent recvEvt;
            private int timeout = 2000;

            public void Start(NetworkBinding binding)
            {

                recvEvt = new AutoResetEvent(false);
                Start(binding, 32 * 1024, this);
            }

            public new void Stop()
            {
                try
                {
                    base.Stop();
                }
                finally
                {
                    recvEvt.Close();
                }
            }

            public int Timeout
            {
                get { return timeout; }
                set { timeout = value; }
            }

            public void Route(ISipTransport transport, SipMessage message)
            {
                received = message;
                recvEvt.Set();
            }

            public ISipTransport SelectTransport(ISipAgent agent, SipRequest request, out NetworkBinding remoteEP)
            {
                remoteEP = null;    // NOP
                return null;
            }

            public SipMessage Receive()
            {
                SipMessage message;

                if (received != null)
                {

                    message = received;
                    received = null;

                    recvEvt.Reset();
                    return message;
                }

                if (!recvEvt.WaitOne(timeout, false))
                    throw new TimeoutException();

                message = received;
                received = null;
                return message;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUdpTransport_TransmitSingle()
        {
            // Create two transport instances and send a message from one to
            // the other and verify that it got through.

            TestTransport transport1 = new TestTransport();
            TestTransport transport2 = new TestTransport();
            SipRequest sentMsg;
            SipRequest recvMsg;

            try
            {
                transport1.Start(new NetworkBinding("127.0.0.1:ANY"));
                transport2.Start(new NetworkBinding("127.0.0.1:ANY"));

                Assert.IsFalse(transport1.IsStreaming);
                Assert.AreEqual(SipTransportType.UDP, transport1.TransportType);

                sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/UDP {0}", transport1.LocalEndpoint));

                transport1.Send(transport2.LocalEndpoint, sentMsg);
                recvMsg = (SipRequest)transport2.Receive();

                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual(transport1.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);
                Assert.AreEqual(transport1.LocalEndpoint.Port, recvMsg.RemoteEndpoint.Port);
            }
            finally
            {
                transport1.Stop();
                transport2.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUdpTransport_TransmitMultiple()
        {
            // Create two transport instances and send 10K messages from one to
            // the other and verify that they got through.

            TestTransport transport1 = new TestTransport();
            TestTransport transport2 = new TestTransport();
            SipRequest sentMsg;
            SipRequest recvMsg;

            try
            {
                transport1.Start(new NetworkBinding("127.0.0.1:ANY"));
                transport2.Start(new NetworkBinding("127.0.0.1:ANY"));

                for (int i = 0; i < 10000; i++)
                {
                    sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                    sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/UDP {0}", transport1.LocalEndpoint));
                    sentMsg.AddHeader("Count", i.ToString());

                    transport1.Send(transport2.LocalEndpoint, sentMsg);
                    recvMsg = (SipRequest)transport2.Receive();

                    Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                    Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                    Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                    Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                    Assert.AreEqual(i.ToString(), recvMsg["Count"].FullText);
                    Assert.AreEqual(transport1.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);
                    Assert.AreEqual(transport1.LocalEndpoint.Port, recvMsg.RemoteEndpoint.Port);
                }
            }
            finally
            {
                transport1.Stop();
                transport2.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipUdpTransport_External()
        {
            // Open a UDP transport on the standard SIP port,
            // receive three messages, write their contents to
            // the debug output and then terminate.

            Assert.Inconclusive("Comment this out to enable the test");

            IPAddress address, subnet;
            TestTransport transport;
            SipMessage message;

            Helper.GetNetworkInfo(out address, out subnet);

            transport = new TestTransport();
            transport.Start(new NetworkBinding(address, NetworkPort.SIP));
            transport.Timeout = -1;

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    message = transport.Receive();
                    Debug.Write(message.ToString());

                    if (message.Contents.Length > 0)
                        Debug.Write(Helper.HexDump(message.Contents, 16, HexDumpOption.ShowAll));
                }
            }
            finally
            {
                transport.Stop();
            }
        }
    }
}

