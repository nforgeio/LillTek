//-----------------------------------------------------------------------------
// FILE:        _SharedMemIO.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements UNIT tests for the SharedMemInbox and
//              SharedMemOutbox classes

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.LowLevel;
using LillTek.Windows;

namespace LillTek.LowLevel.Test
{
    [TestClass]
    public class _SharedMemIO
    {
        private bool ready;
        private Thread thread;
        private string recvMsg;

        private void OnReceive(byte[] buf)
        {
            recvMsg = Encoding.UTF8.GetString(buf);
        }

        private void BasicThreadFunc()
        {
            SharedMemInbox inBox;
            DateTime start;

            inBox = new SharedMemInbox();
            inBox.Open("BasicIn", 100, new SharedMemInboxReceiveDelegate(OnReceive));
            ready = true;

            start = DateTime.UtcNow;
            while (recvMsg == null)
            {

                if (DateTime.UtcNow - start >= TimeSpan.FromSeconds(5))
                    break;

                Thread.Sleep(100);
            }

            inBox.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMemIO_Basic()
        {
            SharedMemOutbox outBox;

            ready = false;
            recvMsg = null;
            thread = new Thread(new ThreadStart(BasicThreadFunc));
            thread.Start();

            while (!ready)
                Thread.Sleep(100);

            outBox = new SharedMemOutbox(100, TimeSpan.FromSeconds(10));
            outBox.Send("BasicIn", Encoding.UTF8.GetBytes("Hello World!"));
            outBox.Close();

            thread.Join();

            Assert.AreEqual("Hello World!", recvMsg);
        }
    }
}

