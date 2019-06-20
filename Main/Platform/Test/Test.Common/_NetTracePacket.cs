//-----------------------------------------------------------------------------
// FILE:        _NetTracePacket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NetTracePacket UNIT tests

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
    [TestClass]
    public class _NetTracePacket
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetTracePacket_Basic()
        {
            byte[] buf = new byte[NetTracePacket.MaxPacket];
            byte[] bufIn;
            int cb;
            NetTracePacket inPacket, outPacket;
            DateTime now;

            now = DateTime.UtcNow;

            outPacket = new NetTracePacket(10, 20, "Test Subsystem", 3, "Test Event", "Test Summary", "Test Details");
            cb = outPacket.Write(buf);
            bufIn = new byte[cb];
            Array.Copy(buf, 0, bufIn, 0, cb);

            Thread.Sleep(1100);

            inPacket = new NetTracePacket();
            inPacket.Read(bufIn);

            Assert.AreEqual(10, inPacket.OriginID);
            Assert.AreEqual(20, inPacket.PacketNum);
            Assert.AreEqual("Test Subsystem", inPacket.Subsystem);
            Assert.AreEqual(3, inPacket.Detail);
            Assert.AreEqual("Test Event", inPacket.Event);
            Assert.AreEqual("Test Summary", inPacket.Summary);
            Assert.AreEqual("Test Details", inPacket.Details);
            Assert.IsTrue(inPacket.SendTime - now < TimeSpan.FromSeconds(0.5));
            Assert.IsTrue(inPacket.ReceiveTime - now >= TimeSpan.FromSeconds(1.0));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void NetTracePacket_Overflow()
        {
            byte[] buf = new byte[NetTracePacket.MaxPacket];
            byte[] bufIn;
            int cb;
            NetTracePacket inPacket, outPacket;
            DateTime now;
            string test = new String('a', NetTracePacket.MaxPacket);
            string s;

            //---------------------------

            now = DateTime.UtcNow;
            outPacket = new NetTracePacket(10, 20, "Subsystem", 0, test, "Summary", "Details");
            cb = outPacket.Write(buf);
            bufIn = new byte[cb];
            Array.Copy(buf, 0, bufIn, 0, cb);

            Thread.Sleep(1100);

            inPacket = new NetTracePacket();
            inPacket.Read(bufIn);

            Assert.IsTrue(inPacket.Event.Length < NetTracePacket.MaxPacket);
            Assert.IsTrue(inPacket.SendTime - now < TimeSpan.FromSeconds(0.5));
            Assert.IsTrue(inPacket.ReceiveTime - now >= TimeSpan.FromSeconds(1.0));

            s = inPacket.Event;
            for (int i = 0; i < s.Length; i++)
                Assert.AreEqual('a', s[i]);

            Assert.AreEqual("", inPacket.Summary);
            Assert.AreEqual("", inPacket.Details);

            //---------------------------

            now = DateTime.UtcNow;
            outPacket = new NetTracePacket(10, 20, "Subsystem", 255, "Event", test, "Details");
            cb = outPacket.Write(buf);
            bufIn = new byte[cb];
            Array.Copy(buf, 0, bufIn, 0, cb);

            Thread.Sleep(1100);

            inPacket = new NetTracePacket();
            inPacket.Read(bufIn);

            Assert.IsTrue(inPacket.SendTime - now < TimeSpan.FromSeconds(0.5));
            Assert.IsTrue(inPacket.ReceiveTime - now >= TimeSpan.FromSeconds(1.0));

            Assert.AreEqual("Event", inPacket.Event);

            s = inPacket.Summary;
            for (int i = 0; i < s.Length; i++)
                Assert.AreEqual('a', s[i]);

            Assert.AreEqual("", inPacket.Details);

            //---------------------------

            now = DateTime.UtcNow;
            outPacket = new NetTracePacket(10, 20, "Subsystem", 10, "Event", "Summary", test);
            cb = outPacket.Write(buf);
            bufIn = new byte[cb];
            Array.Copy(buf, 0, bufIn, 0, cb);

            Thread.Sleep(1100);

            inPacket = new NetTracePacket();
            inPacket.Read(bufIn);

            Assert.IsTrue(inPacket.SendTime - now < TimeSpan.FromSeconds(0.5));
            Assert.IsTrue(inPacket.ReceiveTime - now >= TimeSpan.FromSeconds(1.0));

            Assert.AreEqual("Subsystem", inPacket.Subsystem);
            Assert.AreEqual(10, inPacket.Detail);
            Assert.AreEqual("Event", inPacket.Event);
            Assert.AreEqual("Summary", inPacket.Summary);

            s = inPacket.Details;
            for (int i = 0; i < s.Length; i++)
                Assert.AreEqual('a', s[i]);
        }
    }
}

