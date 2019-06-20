//-----------------------------------------------------------------------------
// FILE:        _TcpChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for TcpChannel/MsgRouter

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class _TcpChannel
    {
        private const int BlastCount = 100;

        private IPEndPoint group1 = new IPEndPoint(IPAddress.Parse("127.222.0.1"), 6001);
        private IPEndPoint group2 = new IPEndPoint(IPAddress.Parse("127.222.0.2"), 6002);

        private _SyncRouter g1r1;   // Group=1 Router=1
        private _SyncRouter g1r2;
        private _SyncRouter g1r3;
        private _SyncRouter g2r1;
        private _SyncRouter g2r2;

        public class TcpTestMsg : Msg
        {
            public string Value;

            public TcpTestMsg()
            {
            }

            public TcpTestMsg(string v)
            {
                this.Value = v;
            }

            protected override void WritePayload(EnhancedStream es)
            {
                es.WriteString32(this.Value);
            }

            protected override void ReadPayload(EnhancedStream es, int cbPayload)
            {
                this.Value = es.ReadString32();
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

            TimedLock.FullDiagnostics = false;
            AsyncTracker.Enable = false;
            AsyncTracker.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            AsyncTracker.Stop();
            TimedLock.FullDiagnostics = false;
            NetTrace.Stop();
        }

        public void StartRouters(bool encrypt)
        {
            string encryption;
            byte[] key;
            byte[] IV;

            if (encrypt)
            {
                encryption = CryptoAlgorithm.TripleDES;
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

        public void StartRouters(TimeSpan maxIdle)
        {
            string encryption;
            byte[] key;
            byte[] IV;

            encryption = CryptoAlgorithm.PlainText;
            key = new byte[1];
            IV = new byte[1];

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

            g1r1.MaxIdle = maxIdle;
            g1r1.Start();

            g1r2.MaxIdle = maxIdle;
            g1r2.Start();

            g1r3.MaxIdle = maxIdle;
            g1r3.Start();

            g2r1.MaxIdle = maxIdle;
            g2r1.Start();

            g2r2.MaxIdle = maxIdle;
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
        public void TcpChannel_SendSelf()
        {
            TcpTestMsg msg;

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r1.TcpEP), new TcpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);

                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r1.TcpEP), new TcpTestMsg("#2"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("#2", msg.Value);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_SendSelf_Encrypt()
        {
            TcpTestMsg msg;

            StartRouters(true);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r1.TcpEP), new TcpTestMsg("Hello World!"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);

                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r1.TcpEP), new TcpTestMsg("#2"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("#2", msg.Value);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);

                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#2"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("#2", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send_Large()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();
            string largeString = new String('a', 70000);

            // Make sure the messages larger than 65K bytes can be delivered successfully.

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg(largeString));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(largeString, msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send_Blast()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                    g1r2.WaitReceived(1);
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual("Hello World!", msg.Value);
                    Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                    Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);

                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#2"));
                    g1r2.WaitReceived(1);
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual("#2", msg.Value);
                    Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                    Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
                }

                // Make sure we were using cached channels rather than
                // building a bunch of new ones.

                Assert.AreEqual(1, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(1, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r2.PendingTcpChannelCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send_Encrypt()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();

            StartRouters(true);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);

                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#2"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("#2", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send_EncryptBlast()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();

            StartRouters(true);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                    g1r2.WaitReceived(1);
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual("Hello World!", msg.Value);
                    Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                    Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);

                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#2"));
                    g1r2.WaitReceived(1);
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual("#2", msg.Value);
                    Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                    Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
                }

                // Make sure we were using cached channels rather than
                // building a bunch of new ones.

                Assert.AreEqual(1, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(1, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r2.PendingTcpChannelCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Connect_Queued()
        {
            // Verify that everything works if the multiple messages are
            // queued before a connection is established.

            Dictionary<string, string> received = new Dictionary<string, string>();
            TcpTestMsg msg;

            StartRouters(true);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                lock (g1r1.SyncRoot)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#2"));
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("#3"));
                }

                g1r2.WaitReceived(3);

                for (int i = 0; i < 3; i++)
                {
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    received.Add(msg.Value, msg.Value);
                }

                Assert.IsTrue(received.ContainsKey("Hello World!"));
                Assert.IsTrue(received.ContainsKey("#2"));
                Assert.IsTrue(received.ContainsKey("#3"));
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Reply()
        {
            TcpTestMsg msg;

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                g1r2.Transmit(msg._FromEP.ChannelEP, new TcpTestMsg("Hello Yourself!"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("Hello Yourself!", msg.Value);
                Assert.AreEqual(g1r2.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Reply_Fragmented()
        {
            TcpTestMsg msg;

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                g1r2.WaitReceived(1);
                msg = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual("Hello World!", msg.Value);
                Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                g1r2.Transmit(msg._FromEP.ChannelEP, new TcpTestMsg("Hello Yourself!"));
                g1r1.WaitReceived(1);
                msg = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual("Hello Yourself!", msg.Value);
                Assert.AreEqual(g1r2.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                Assert.AreEqual(NetHelper.GetActiveAdapter(), msg._FromEP.ChannelEP.NetEP.Address);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Reply_Blast()
        {
            TcpTestMsg msg;
            IPAddress activeAdapter = NetHelper.GetActiveAdapter();

            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                for (int i = 0; i < BlastCount; i++)
                {
                    g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Hello World!"));
                    g1r2.WaitReceived(1);
                    msg = (TcpTestMsg)g1r2.DequeueReceived();
                    Assert.AreEqual("Hello World!", msg.Value);
                    Assert.AreEqual(g1r1.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);

                    g1r2.Transmit(msg._FromEP.ChannelEP, new TcpTestMsg("Hello Yourself!"));
                    g1r1.WaitReceived(1);
                    msg = (TcpTestMsg)g1r1.DequeueReceived();
                    Assert.AreEqual("Hello Yourself!", msg.Value);
                    Assert.AreEqual(g1r2.TcpEP.Port, msg._FromEP.ChannelEP.NetEP.Port);
                    Assert.AreEqual(activeAdapter, msg._FromEP.ChannelEP.NetEP.Address);
                }

                // Make sure we were using cached channels rather than
                // building a bunch of new ones.

                Assert.AreEqual(1, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(1, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r2.PendingTcpChannelCount);
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Send_Queued()
        {
            StartRouters(false);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Sent #1"));
                g1r1.QueueTo(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Queued #1"));
                g1r1.QueueTo(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Queued #2"));
                g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Sent #2"));
                g1r2.WaitReceived(4);

                Dictionary<string, TcpTestMsg> received = new Dictionary<string, TcpTestMsg>();

                for (int i = 0; i < 4; i++)
                {
                    TcpTestMsg msg = (TcpTestMsg)g1r2.DequeueReceived();

                    received.Add(msg.Value, msg);
                }

                Assert.IsTrue(received.ContainsKey("Sent #1"));
                Assert.IsTrue(received.ContainsKey("Queued #1"));
                Assert.IsTrue(received.ContainsKey("Queued #2"));
                Assert.IsTrue(received.ContainsKey("Sent #2"));
            }
            finally
            {
                StopRouters();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void TcpChannel_Connect_Simultaneous()
        {
            TimeSpan maxIdle = new TimeSpan(0, 0, 0, 0, 500);
            TcpTestMsg msg1;
            TcpTestMsg msg2;

            StartRouters(maxIdle);
            g1r1.IdleCheck = false;
            g1r2.IdleCheck = false;

            try
            {
                // Simulate a simultaneous connection and make sure that
                // messages can be sent both ways.

                lock (g1r1.SyncRoot)
                    lock (g1r2.SyncRoot)
                    {
                        g1r1.Transmit(new ChannelEP(Transport.Tcp, g1r2.TcpEP), new TcpTestMsg("Send #1"));
                        g1r2.Transmit(new ChannelEP(Transport.Tcp, g1r1.TcpEP), new TcpTestMsg("Send #2"));
                    }

                g1r1.WaitReceived(1);
                g1r2.WaitReceived(1);

                Assert.AreEqual(1, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(1, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(1, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(1, g1r2.PendingTcpChannelCount);

                msg1 = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg1.Value, "Send #2");

                msg2 = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg2.Value, "Send #1");

                g1r1.Transmit(msg1._FromEP.ChannelEP, new TcpTestMsg("Send #3"));
                g1r2.Transmit(msg2._FromEP.ChannelEP, new TcpTestMsg("Send #4"));

                g1r1.WaitReceived(1);
                g1r2.WaitReceived(1);

                msg1 = (TcpTestMsg)g1r1.DequeueReceived();
                Assert.AreEqual(msg1.Value, "Send #4");

                msg2 = (TcpTestMsg)g1r2.DequeueReceived();
                Assert.AreEqual(msg2.Value, "Send #3");

                // Wait for the channels to be closed on idle

                Assert.AreEqual(1, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(1, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(1, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(1, g1r2.PendingTcpChannelCount);

                Thread.Sleep(1500);
                g1r1.CloseIdleTcp(TimeSpan.FromMilliseconds(500));
                g1r2.CloseIdleTcp(TimeSpan.FromMilliseconds(500));

                Assert.AreEqual(0, g1r1.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r1.PendingTcpChannelCount);
                Assert.AreEqual(0, g1r2.MappedTcpChannelCount);
                Assert.AreEqual(0, g1r2.PendingTcpChannelCount);
            }
            finally
            {
                StopRouters();
            }
        }
    }
}

