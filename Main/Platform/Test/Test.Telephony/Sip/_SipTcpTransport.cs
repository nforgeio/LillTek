//-----------------------------------------------------------------------------
// FILE:        _SipTcpTransport.cs
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
    public class _SipTcpTransport
    {
        private class TestTransport : SipTcpTransport, ISipMessageRouter
        {
            private object syncLock = new object();
            private Queue<SipMessage> queue;
            private AutoResetEvent recvEvt;
            private int timeout = 2000000;

            public void Start(NetworkBinding binding)
            {
                recvEvt = new AutoResetEvent(false);
                queue = new Queue<SipMessage>();
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
                lock (syncLock)
                {
                    queue.Enqueue(message);
                    recvEvt.Set();
                }
            }

            public ISipTransport SelectTransport(ISipAgent agent, SipRequest request, out NetworkBinding remoteEP)
            {
                remoteEP = null;    // NOP
                return null;
            }

            public SipMessage Receive()
            {
                lock (syncLock)
                {
                    if (queue.Count > 0)
                    {
                        recvEvt.Reset();
                        return queue.Dequeue();
                    }
                }

                if (!recvEvt.WaitOne(timeout, false))
                    throw new TimeoutException();

                lock (syncLock)
                {
                    return queue.Dequeue();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipTcpTransport_TransmitSingle()
        {
            // Create two transport instances and send a message from one to
            // the other and verify that it got through.

            TestTransport transport1 = new TestTransport();
            TestTransport transport2 = new TestTransport();
            SipRequest sentMsg;
            SipRequest recvMsg;

            try
            {
                transport1.Start(new NetworkBinding("127.0.0.1:5311"));
                transport2.Start(new NetworkBinding("127.0.0.1:5312"));

                Assert.IsTrue(transport1.IsStreaming);
                Assert.AreEqual(SipTransportType.TCP, transport1.TransportType);

                sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport1.LocalEndpoint));

                transport1.Send(transport2.LocalEndpoint, sentMsg);
                recvMsg = (SipRequest)transport2.Receive();

                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual(transport1.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);

                // Send a message the other way, this one with some content data

                sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport1.LocalEndpoint));
                sentMsg.Contents = new byte[] { 0, 1, 2, 3, 4 };

                transport2.Send(transport1.LocalEndpoint, sentMsg);
                recvMsg = (SipRequest)transport1.Receive();

                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, recvMsg.Contents);
                Assert.AreEqual(transport2.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);
            }
            finally
            {
                transport1.Stop();
                transport2.Stop();
            }
        }

        private byte[] GetContents(int cb)
        {
            byte[] arr = new byte[cb];

            for (int i = 0; i < cb; i++)
                arr[i] = (byte)i;

            return arr;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipTcpTransport_TransmitMultiple()
        {
            // Create two transport instances and send 10K messages from one to
            // the other and verify that they got through.

            TestTransport transport1 = new TestTransport();
            TestTransport transport2 = new TestTransport();
            SipRequest sentMsg;
            SipRequest recvMsg;

            try
            {
                transport1.Start(new NetworkBinding("127.0.0.1:5311"));
                transport2.Start(new NetworkBinding("127.0.0.1:5312"));

                for (int i = 0; i < 2500; i++)
                {
                    sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                    sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport1.LocalEndpoint));
                    sentMsg.AddHeader("Count", i.ToString());
                    sentMsg.Contents = GetContents(i);

                    transport1.Send(transport2.LocalEndpoint, sentMsg);
                    recvMsg = (SipRequest)transport2.Receive();

                    Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                    Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                    Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                    Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                    Assert.AreEqual(i.ToString(), recvMsg["Count"].FullText);
                    CollectionAssert.AreEqual(sentMsg.Contents, recvMsg.Contents);
                    Assert.AreEqual(transport1.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);

                    // Send a message the other way.

                    sentMsg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                    sentMsg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport1.LocalEndpoint));
                    sentMsg.Contents = GetContents(i);

                    transport2.Send(transport1.LocalEndpoint, sentMsg);
                    recvMsg = (SipRequest)transport1.Receive();

                    Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                    Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                    Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                    Assert.AreEqual(sentMsg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                    CollectionAssert.AreEqual(sentMsg.Contents, recvMsg.Contents);
                    Assert.AreEqual(transport2.LocalEndpoint.Address, recvMsg.RemoteEndpoint.Address);
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
        public void SipTcpTransport_External()
        {
            // Open a TCP transport on the standard SIP port,
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

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipTcpTransport_MultipleBuffered()
        {
            // Render two messages into a single buffer and transmit them
            // to a TCP transport in a single send.  This will result in
            // the two messages being processed out of the headerBuf which
            // is what we want to test here.

            TestTransport transport = new TestTransport();
            EnhancedSocket sock = null;
            SipRequest msg1, msg2;
            SipRequest recvMsg;
            byte[] buf;
            int cb;

            try
            {
                transport.Start(new NetworkBinding("127.0.0.1:5311"));

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("127.0.0.1", 5311);

                msg1 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg1.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg1.AddHeader("Count", "0");
                msg1.Contents = GetContents(0);

                msg2 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg2.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg2.AddHeader("Count", "1");
                msg2.Contents = GetContents(0);

                buf = Helper.Concat(msg1.ToArray(), msg2.ToArray());
                cb = sock.Send(buf);
                Assert.AreEqual(buf.Length, cb);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg1[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("0", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg1.Contents, recvMsg.Contents);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg2[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("1", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg2.Contents, recvMsg.Contents);

                // Try it again, this time with some data.

                msg1 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg1.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg1.AddHeader("Count", "0");
                msg1.Contents = GetContents(10);

                msg2 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg2.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg2.AddHeader("Count", "1");
                msg2.Contents = GetContents(20);

                buf = Helper.Concat(msg1.ToArray(), msg2.ToArray());
                cb = sock.Send(buf);
                Assert.AreEqual(buf.Length, cb);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg1[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("0", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg1.Contents, recvMsg.Contents);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg2[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("1", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg2.Contents, recvMsg.Contents);

                // Try it one more time, this time adding a leading CRLF

                msg1 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg1.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg1.AddHeader("Count", "0");
                msg1.Contents = GetContents(10);

                msg2 = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg2.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg2.AddHeader("Count", "1");
                msg2.Contents = GetContents(20);

                buf = Helper.Concat(new byte[] { 0x0D, 0x0A }, msg1.ToArray(), new byte[] { 0x0D, 0x0A }, msg2.ToArray());
                cb = sock.Send(buf);
                Assert.AreEqual(buf.Length, cb);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg1[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("0", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg1.Contents, recvMsg.Contents);

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg2[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("1", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg2.Contents, recvMsg.Contents);
            }
            finally
            {
                if (sock != null)
                    sock.Close();

                transport.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipTcpTransport_MultipleChunks()
        {
            // Transmit a message to a transport in several chunks to make
            // sure it can be reassembled properly.

            TestTransport transport = new TestTransport();
            EnhancedSocket sock = null;
            SipRequest msg;
            SipRequest recvMsg;
            byte[] buf;

            try
            {
                transport.Start(new NetworkBinding("127.0.0.1:5311"));

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("127.0.0.1", 5311);
                sock.NoDelay = true;

                msg = new SipRequest(SipMethod.Register, "sip:jeff@lilltek.com", SipHelper.SIP20);
                msg.AddHeader(SipHeader.Via, string.Format("SIP/2.0/TCP {0}", transport.LocalEndpoint));
                msg.AddHeader("Count", "0");
                msg.Contents = GetContents(25);

                buf = msg.ToArray();
                for (int i = 0; i < buf.Length; i++)
                {
                    sock.Send(buf, i, 1, SocketFlags.None);
                    Thread.Sleep(1);
                }

                recvMsg = (SipRequest)transport.Receive();
                Assert.AreEqual(SipMethod.Register, recvMsg.Method);
                Assert.AreEqual("sip:jeff@lilltek.com", recvMsg.Uri);
                Assert.AreEqual(SipHelper.SIP20, recvMsg.SipVersion);
                Assert.AreEqual(msg[SipHeader.Via].FullText, recvMsg[SipHeader.Via].FullText);
                Assert.AreEqual("0", recvMsg["Count"].FullText);
                CollectionAssert.AreEqual(msg.Contents, recvMsg.Contents);
            }
            finally
            {
                if (sock != null)
                    sock.Close();

                transport.Stop();
            }
        }
    }
}

