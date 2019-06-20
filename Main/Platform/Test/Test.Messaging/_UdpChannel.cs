//-----------------------------------------------------------------------------
// FILE:        _UdpChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for UdpChannel/MsgRouter

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Net.Sockets;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _UdpChannel
    {
        private const int BlastCount = 100;

        private IPEndPoint group1 = new IPEndPoint(IPAddress.Parse("231.222.0.1"), 6001);
        private IPEndPoint group2 = new IPEndPoint(IPAddress.Parse("231.222.0.2"), 6002);

        private _SyncRouter g1r1;   // Group=1 Router=1
        private _SyncRouter g1r2;
        private _SyncRouter g1r3;
        private _SyncRouter g2r1;
        private _SyncRouter g2r2;

        public class UdpTestMsg : Msg
        {
            public string Value;

            public UdpTestMsg()
            {
            }

            public UdpTestMsg(string v)
            {
                this.Value = v;
            }

            protected override void WritePayload(EnhancedStream es)
            {
                es.WriteString16(Value);
            }

            protected override void ReadPayload(EnhancedStream es, int cbPayload)
            {
                Value = es.ReadString16();
            }

            public override string ToString()
            {
                return this.GetType().Name + ": " + Value;
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 2);

            AsyncTracker.Enable = false;
            AsyncTracker.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
            AsyncTracker.Stop();
        }

        public void StartRouters(bool encrypt)
        {
            string encryption;
            byte[] key;
            byte[] IV;

            if (encrypt)
            {
                encryption = CryptoAlgorithm.RC2;
                EncryptionConfig.GenKeyIV(encryption, 128, out key, out IV);
            }
            else
            {
                encryption = CryptoAlgorithm.PlainText;
                key = new byte[1];
                IV = new byte[1];
            }

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            g1r1 = new _SyncRouter("g1r1", group1, new IPEndPoint(IPAddress.Any, 5550), new IPEndPoint(IPAddress.Any, 5560),
                                  encryption, key, IV);
            g1r2 = new _SyncRouter("g1r2", group1, new IPEndPoint(IPAddress.Any, 5551), new IPEndPoint(IPAddress.Any, 5561),
                                  encryption, key, IV);
            g1r3 = new _SyncRouter("g1r3", group1, new IPEndPoint(IPAddress.Any, 5552), new IPEndPoint(IPAddress.Any, 5562),
                                  encryption, key, IV);
            g2r1 = new _SyncRouter("g2r1", group2, new IPEndPoint(IPAddress.Any, 5553), new IPEndPoint(IPAddress.Any, 5563),
                                  encryption, key, IV);
            g2r2 = new _SyncRouter("g2r2", group2, new IPEndPoint(IPAddress.Any, 5554), new IPEndPoint(IPAddress.Any, 5564),
                                  encryption, key, IV);

            g1r1.Start();
            g1r2.Start();
            g1r3.Start();
            g2r1.Start();
            g2r2.Start();
        }

        public void StopRouters()
        {
            Msg.ClearTypes();

            g1r1.Stop();
            g1r2.Stop();
            g1r3.Stop();
            g2r1.Stop();
            g2r2.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_ransmitSelf()
        {
            UdpTestMsg msg;

            StartRouters(false);

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Udp, g1r1.UdpEP), new UdpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                msg = (UdpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Udp, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
                Assert.AreEqual(g1r1.UdpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r2.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_TransmitSelf_Encrypt()
        {
            UdpTestMsg msg;

            StartRouters(true);

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Udp, g1r1.UdpEP), new UdpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                msg = (UdpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Udp, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
                Assert.AreEqual(g1r1.UdpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r2.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Send()
        {
            UdpTestMsg msg;

            StartRouters(false);

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (UdpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Udp, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
                Assert.AreEqual(g1r1.UdpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r1.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Send_Encrypt()
        {
            UdpTestMsg msg;

            StartRouters(true);

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (UdpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Udp, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
                Assert.AreEqual(g1r1.UdpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r1.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Send_Blast()
        {
            // This test is dependant on the fact that the UDP packets will delivered
            // in the same order that they were sent.  This is probably a reasonable
            // assumption since this test is being done on the loopback network driver.

            UdpTestMsg msg;

            StartRouters(false);

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg(i.ToString()));
                    g1r2.WaitReceived(1);
                    msg = (UdpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual(i.ToString(), msg.Value);
                }
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Send_BlastEncrypted()
        {
            // This test is dependant on the fact that the UDP packets will delivered
            // in the same order that they were sent.  This is probably a reasonable
            // assumption since this test is being done on the loopback network driver.

            UdpTestMsg msg;

            StartRouters(true);

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg(i.ToString()));
                    g1r2.WaitReceived(1);
                    msg = (UdpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual(i.ToString(), msg.Value);
                }
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Multicast()
        {
            UdpTestMsg msg;

            StartRouters(false);

            try
            {
                g1r1.Multicast(new UdpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                g1r2.WaitReceived(1);
                g1r3.WaitReceived(1);
                msg = (UdpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g1r3.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);

                g2r1.Multicast(new UdpTestMsg("Hello World #2!"));
                g2r1.WaitReceived(1);
                g2r2.WaitReceived(1);
                msg = (UdpTestMsg)g2r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World #2!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g2r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g2r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World #2!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g2r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r1.ReceiveCount);
                Assert.AreEqual(0, g1r2.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Multicast_Encrypt()
        {
            UdpTestMsg msg;

            StartRouters(true);

            try
            {
                g1r1.Multicast(new UdpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                g1r2.WaitReceived(1);
                g1r3.WaitReceived(1);
                msg = (UdpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g1r3.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g2r1.ReceiveCount);
                Assert.AreEqual(0, g2r2.ReceiveCount);

                g2r1.Multicast(new UdpTestMsg("Hello World #2!"));
                g2r1.WaitReceived(1);
                g2r2.WaitReceived(1);
                msg = (UdpTestMsg)g2r1.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World #2!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g2r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                msg = (UdpTestMsg)g2r2.DequeueReceived();
                Assert.AreEqual(msg.Value, "Hello World #2!");
                Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                Assert.AreEqual(g2r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                Thread.Sleep(50);
                Assert.AreEqual(0, g1r1.ReceiveCount);
                Assert.AreEqual(0, g1r2.ReceiveCount);
                Assert.AreEqual(0, g1r3.ReceiveCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Send_Queued()
        {
            // This test is dependant on the fact that the UDP packets will delivered
            // in the same order that they were sent.  This is probably a reasonable
            // assumption since this test is being done on the loopback network driver.

            UdpTestMsg msg;
            int cSent, cQueued1, cQueued2;

            StartRouters(false);

            try
            {
                g1r1.QueueTo(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg("Queued #1"));
                g1r1.QueueTo(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg("Queued #2"));
                g1r1.Transmit(new ChannelEP(Transport.Udp, g1r2.UdpEP), new UdpTestMsg("Sent"));
                g1r2.WaitReceived(3);

                cSent = 0;
                cQueued1 = 0;
                cQueued2 = 0;

                msg = (UdpTestMsg)g1r2.DequeueReceived();
                switch (msg.Value)
                {
                    case "Sent":

                        cSent++;
                        break;

                    case "Queued #1":

                        cQueued1++;
                        break;

                    case "Queued #2":

                        cQueued2++;
                        break;
                }

                msg = (UdpTestMsg)g1r2.DequeueReceived();
                switch (msg.Value)
                {
                    case "Sent":

                        cSent++;
                        break;

                    case "Queued #1":

                        cQueued1++;
                        break;

                    case "Queued #2":

                        cQueued2++;
                        break;
                }

                msg = (UdpTestMsg)g1r2.DequeueReceived();
                switch (msg.Value)
                {
                    case "Sent":

                        cSent++;
                        break;

                    case "Queued #1":

                        cQueued1++;
                        break;

                    case "Queued #2":

                        cQueued2++;
                        break;
                }

                Assert.AreEqual(1, cQueued1);
                Assert.AreEqual(1, cQueued2);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Multicast_Queued()
        {
            UdpTestMsg msg;
            int cSent, cQueued1, cQueued2;

            StartRouters(false);

            try
            {
                g1r1.QueueTo(new ChannelEP(Transport.Multicast, g1r1.CloudEP), new UdpTestMsg("Queued #1"));
                g1r1.QueueTo(new ChannelEP(Transport.Multicast, g1r1.CloudEP), new UdpTestMsg("Queued #2"));
                g1r1.Multicast(new UdpTestMsg("Sent"));
                g1r1.WaitReceived(3);
                g1r2.WaitReceived(3);
                g1r3.WaitReceived(3);

                cSent = cQueued1 = cQueued2 = 0;
                for (int i = 0; i < 3; i++)
                {

                    msg = (UdpTestMsg)g1r1.DequeueReceived();
                    switch (msg.Value)
                    {
                        case "Sent":

                            cSent++;
                            break;

                        case "Queued #1":

                            cQueued1++;
                            break;

                        case "Queued #2":

                            cQueued2++;
                            break;
                    }
                }

                Assert.AreEqual(1, cSent);
                Assert.AreEqual(1, cQueued1);
                Assert.AreEqual(1, cQueued2);

                cSent = cQueued1 = cQueued2 = 0;
                for (int i = 0; i < 3; i++)
                {
                    msg = (UdpTestMsg)g1r2.DequeueReceived();
                    switch (msg.Value)
                    {
                        case "Sent":

                            cSent++;
                            break;

                        case "Queued #1":

                            cQueued1++;
                            break;

                        case "Queued #2":

                            cQueued2++;
                            break;
                    }
                }

                Assert.AreEqual(1, cSent);
                Assert.AreEqual(1, cQueued1);
                Assert.AreEqual(1, cQueued2);

                cSent = cQueued1 = cQueued2 = 0;
                for (int i = 0; i < 3; i++)
                {
                    msg = (UdpTestMsg)g1r3.DequeueReceived();
                    switch (msg.Value)
                    {
                        case "Sent":

                            cSent++;
                            break;

                        case "Queued #1":

                            cQueued1++;
                            break;

                        case "Queued #2":

                            cQueued2++;
                            break;
                    }
                }

                Assert.AreEqual(1, cSent);
                Assert.AreEqual(1, cQueued1);
                Assert.AreEqual(1, cQueued2);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Multicast_Blast()
        {
            // This test is dependant on the fact that the UDP packets will delivered
            // in the same order that they were sent.  This is probably a reasonable
            // assumption since this test is being done on the loopback network driver.

            UdpTestMsg msg;

            StartRouters(false);

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    string s = "Hello World: " + i.ToString();

                    g1r1.Multicast(new UdpTestMsg(s));
                    g1r1.WaitReceived(1);
                    g1r2.WaitReceived(1);
                    g1r3.WaitReceived(1);
                    msg = (UdpTestMsg)g1r1.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                    msg = (UdpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                    msg = (UdpTestMsg)g1r3.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                }
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void UdpChannel_Multicast_BlastEncrypted()
        {
            // This test is dependant on the fact that the UDP packets will delivered
            // in the same order that they were sent.  This is probably a reasonable
            // assumption since this test is being done on the loopback network driver.

            UdpTestMsg msg;

            StartRouters(true);

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    string s = "Hello World: " + i.ToString();

                    g1r1.Multicast(new UdpTestMsg(s));
                    g1r1.WaitReceived(1);
                    g1r2.WaitReceived(1);
                    g1r3.WaitReceived(1);
                    msg = (UdpTestMsg)g1r1.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                    msg = (UdpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                    msg = (UdpTestMsg)g1r3.DequeueReceived();
                    Assert.AreEqual(msg.Value, s);
                    Assert.AreEqual(Transport.Multicast, msg._FromEP.ChannelEP.Transport);
                    Assert.AreEqual(g1r1.CloudEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                }
            }
            finally
            {
                StopRouters();
            }
        }
    }
}

