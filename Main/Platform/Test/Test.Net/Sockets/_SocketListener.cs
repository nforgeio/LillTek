//-----------------------------------------------------------------------------
// FILE:        _SocketListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the SocketListener class

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _SocketListener
    {

        private EnhancedSocket sockAccept = null;
        private IPEndPoint acceptEP = null;
        private AutoResetEvent wait = null;

        private void Init(SocketListener listener)
        {
            if (sockAccept != null)
            {
                sockAccept.Close();
                sockAccept = null;
            }

            if (wait != null)
            {
                wait.Close();
                wait = null;
            }

            wait = new AutoResetEvent(false);
            listener.SocketAcceptEvent += new SocketAcceptDelegate(OnAccept);
        }

        private void OnAccept(EnhancedSocket sock, IPEndPoint endPoint)
        {
            sockAccept = sock;
            acceptEP = endPoint;
            wait.Set();
        }

        private EnhancedSocket WaitForAccept()
        {
            try
            {
                wait.WaitOne();
                return sockAccept;
            }
            finally
            {
                sockAccept = null;
            }
        }

        public bool Connect(IPEndPoint endPoint)
        {
            EnhancedSocket sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnhancedSocket sockAccept = null;

            try
            {
                sock.Connect(endPoint);
                sockAccept = WaitForAccept();
                return sockAccept != null;
            }
            catch
            {
                return false;
            }
            finally
            {
                sock.Close();
                if (sockAccept != null)
                    sockAccept.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void Single()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 8888);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
                Assert.IsTrue(Connect(ep1));
            }
            finally
            {
                listener.StopAll();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void Multiple()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 8888);
            IPEndPoint ep2 = new IPEndPoint(IPAddress.Loopback, 8889);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
                listener.Start(ep2, 10);
                Assert.IsTrue(Connect(ep1));
                Assert.AreEqual(ep1, acceptEP);
                Assert.IsTrue(Connect(ep2));
                Assert.AreEqual(ep2, acceptEP);
            }
            finally
            {
                listener.StopAll();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void Restart()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 8888);
            IPEndPoint ep2 = new IPEndPoint(IPAddress.Loopback, 8889);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
                listener.Start(ep2, 10);
                Assert.IsTrue(Connect(ep1));
                Assert.IsTrue(Connect(ep2));
                listener.StopAll();

                listener.Start(ep1, 10);
                listener.Start(ep2, 10);
                Assert.IsTrue(Connect(ep1));
                Assert.IsTrue(Connect(ep2));
                listener.StopAll();
            }
            finally
            {
                listener.StopAll();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void Stop()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 8888);
            IPEndPoint ep2 = new IPEndPoint(IPAddress.Loopback, 8889);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
                listener.Start(ep2, 10);
                Assert.IsTrue(Connect(ep1));
                Assert.IsTrue(Connect(ep2));

                listener.Stop(ep1);
                Assert.IsFalse(Connect(ep1));
                Assert.IsTrue(Connect(ep2));

                listener.Stop(ep2);
                Assert.IsFalse(Connect(ep1));
                Assert.IsFalse(Connect(ep2));
            }
            finally
            {
                listener.StopAll();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void SocketListener_BadPort_0()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 0);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ArgumentException).Name, e.GetType().Name);
                return;
            }
            finally
            {
                listener.StopAll();
            }

            Assert.Fail();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void SocketListener_BadPort_Duplicate()
        {
            SocketListener listener = new SocketListener();
            IPEndPoint ep1 = new IPEndPoint(IPAddress.Loopback, 8888);

            try
            {
                Init(listener);
                listener.Start(ep1, 10);
                listener.Start(ep1, 10);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException).Name, e.GetType().Name);
                return;
            }
            finally
            {
                listener.StopAll();
            }

            Assert.Fail();
        }
    }
}

