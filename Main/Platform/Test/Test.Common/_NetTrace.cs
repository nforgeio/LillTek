//-----------------------------------------------------------------------------
// FILE:        _NetTrace.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NetTrace and NetTraceSink UNIT tests

using System;
using System.Text;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    internal class _NetTraceSink
    {
        private object syncLock = new object();
        private Queue recvQueue;

        public _NetTraceSink()
        {
            recvQueue = new Queue();
        }

        public void Wait(int count)
        {
            var timeOut = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (true)
            {
                lock (syncLock)
                {
                    if (recvQueue.Count >= count)
                        return;
                }

                Thread.Sleep(15);
                if (DateTime.UtcNow >= timeOut)
                    throw new TimeoutException();
            }
        }

        public void OnReceive(NetTracePacket[] packets)
        {
            lock (syncLock)
            {
                for (int i = 0; i < packets.Length; i++)
                    recvQueue.Enqueue(packets[i]);
            }
        }

        public NetTracePacket Dequeue()
        {
            lock (syncLock)
                return (NetTracePacket)recvQueue.Dequeue();
        }
    }

    [TestClass]
    public class _NetTrace
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetTrace_Default_Config()
        {
            _NetTraceSink sink;
            NetTracePacket packet;

            try
            {
                sink = new _NetTraceSink();
                Config.SetConfig(null);
                NetTrace.Start();
                NetTrace.Enable("subsystem", 10);

                NetTraceSink.Start(new NetTraceSinkDelegate(sink.OnReceive));

                NetTrace.Write("subsystem", 10, "event", "summary", "details");
                sink.Wait(1);
                packet = sink.Dequeue();

                Assert.AreEqual("subsystem", packet.Subsystem);
                Assert.AreEqual(10, packet.Detail);
                Assert.AreEqual("event", packet.Event);
                Assert.AreEqual("summary", packet.Summary);
                Assert.AreEqual("details", packet.Details);
            }
            finally
            {
                NetTrace.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetTrace_Setting_Config()
        {
            _NetTraceSink sink;
            NetTracePacket packet;

            try
            {
                sink = new _NetTraceSink();
                Config.SetConfig(@"

Diagnostics.TraceEP        = 231.222.0.77:44411
Diagnostics.TraceAdapter   = $(ip-address)
Diagnostics.TraceEnable[0] = 255:subsystem
");

                NetTrace.Start();
                NetTraceSink.Start(new NetTraceSinkDelegate(sink.OnReceive));

                NetTrace.Write("subsystem", 255, "event", "summary", "details");
                sink.Wait(1);
                packet = sink.Dequeue();

                Assert.AreEqual("subsystem", packet.Subsystem);
                Assert.AreEqual(255, packet.Detail);
                Assert.AreEqual("event", packet.Event);
                Assert.AreEqual("summary", packet.Summary);
                Assert.AreEqual("details", packet.Details);
                Assert.AreEqual(44411, packet.SourceEP.Port);
            }
            finally
            {
                NetTrace.Stop();
            }
        }
    }
}

