//-----------------------------------------------------------------------------
// FILE:        _EnhancedSocket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for the EnhancedSocket class

// $todo(jeff.lill): Implement a comprehensive set of tests.

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
    public class _EnhancedSocket
    {
        private const int MaxWaitTime = 2000;     // Milliseconds

        /// <summary>
        /// A socket implementation that uses async calls to implement sync behaviours.
        /// This is used in some test suites below.
        /// </summary>
        private class AsyncSocket
        {
            private EnhancedSocket sock;
            private AutoResetEvent asyncEvent = new AutoResetEvent(false);
            private Exception asyncException;
            private int cbTransfer;
            private bool syncCompletion;
            private EnhancedSocket acceptSock;

            private AsyncCallback onConnect;
            private AsyncCallback onAccept;
            private AsyncCallback onSendBuf;
            private AsyncCallback onRecvBuf;

            public AsyncSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
                : this(new EnhancedSocket(addressFamily, socketType, protocolType))
            {
            }

            public AsyncSocket(EnhancedSocket sock)
            {
                this.sock = sock;
                this.onConnect = new AsyncCallback(OnConnect);
                this.onAccept = new AsyncCallback(OnAccept);
                this.onSendBuf = new AsyncCallback(OnSendBuf);
                this.onRecvBuf = new AsyncCallback(OnRecvBuf);

                Reset();
            }

            private void Reset()
            {
                cbTransfer = 0;
                syncCompletion = false;
                asyncException = null;
                acceptSock = null;

                asyncEvent.Reset();
            }

            private void Verify()
            {
                if (asyncException != null)
                    throw asyncException;

                if (syncCompletion)
                    throw new InvalidOperationException("Asynchronous operation completed synchronously.");
            }

            public EnhancedSocket Socket
            {
                get { return sock; }
            }

            private void OnConnect(IAsyncResult ar)
            {
                try
                {
                    syncCompletion = ar.CompletedSynchronously;
                    sock.EndConnect(ar);
                }
                catch (Exception e)
                {
                    asyncException = e;
                }
                finally
                {
                    asyncEvent.Set();
                }
            }

            public void Connect(IPEndPoint remoteEP)
            {
                Reset();
                sock.BeginConnect(remoteEP, onConnect, null);
                asyncEvent.WaitOne(MaxWaitTime, false);
                Verify();
            }

            public void Connect(string host, int port)
            {
                Reset();
                sock.BeginConnect(host, port, onConnect, null);
                asyncEvent.WaitOne(MaxWaitTime, false);
                Verify();
            }

            public void ListenOn(IPEndPoint localEP)
            {
                sock.Bind(localEP);
                sock.Listen(100);
            }

            private void OnAccept(IAsyncResult ar)
            {
                try
                {
                    syncCompletion = ar.CompletedSynchronously;
                    acceptSock = sock.EndAccept(ar);
                }
                catch (Exception e)
                {

                    asyncException = e;
                }
                finally
                {
                    asyncEvent.Set();
                }
            }

            public AsyncSocket Accept()
            {
                Reset();
                sock.BeginAccept(onAccept, null);
                asyncEvent.WaitOne(MaxWaitTime, false);
                Verify();

                return new AsyncSocket(acceptSock);
            }

            public void Close()
            {
                sock.Close();
            }

            public void Shutdown(SocketShutdown how)
            {
                sock.Shutdown(how);
            }

            private void OnSendBuf(IAsyncResult ar)
            {
                try
                {
                    syncCompletion = ar.CompletedSynchronously;
                    cbTransfer = sock.EndSend(ar);
                }
                catch (Exception e)
                {
                    asyncException = e;
                }
                finally
                {
                    asyncEvent.Set();
                }
            }


            public int Send(byte[] buf)
            {
                Reset();
                sock.BeginSend(buf, 0, buf.Length, SocketFlags.None, onSendBuf, null);
                asyncEvent.WaitOne(MaxWaitTime, false);
                Verify();

                return cbTransfer;
            }

            private void OnRecvBuf(IAsyncResult ar)
            {
                try
                {
                    syncCompletion = ar.CompletedSynchronously;
                    cbTransfer = sock.EndReceive(ar);
                }
                catch (Exception e)
                {
                    asyncException = e;
                }
                finally
                {
                    asyncEvent.Set();
                }
            }


            public int Receive(byte[] buf)
            {
                Reset();
                sock.BeginReceive(buf, 0, buf.Length, SocketFlags.None, onRecvBuf, null);
                asyncEvent.WaitOne(MaxWaitTime, false);
                Verify();

                return cbTransfer;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedSocket_CreateDatagramPair()
        {
            for (int i = 0; i < 1000; i++)
            {
                EnhancedSocket sock0 = null;
                EnhancedSocket sock1 = null;
                int port0, port1;

                try
                {
                    EnhancedSocket.CreateDatagramPair(IPAddress.Any, out sock0, out sock1);

                    port0 = ((IPEndPoint)sock0.LocalEndPoint).Port;
                    port1 = ((IPEndPoint)sock1.LocalEndPoint).Port;
                }
                finally
                {
                    if (sock0 != null)
                        sock0.Close();

                    if (sock1 != null)
                        sock1.Close();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedSocket_Async_ConnectToHost()
        {
            EnhancedSocket sock;
            IAsyncResult ar;
            IPAddress addr;

            sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ar = sock.BeginConnect("www.google.com", 80, null, null);
            sock.EndConnect(ar);
            addr = ((IPEndPoint)sock.RemoteEndPoint).Address;
            sock.Close();

            sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ar = sock.BeginConnect(addr.ToString(), 80, null, null);
            sock.EndConnect(ar);
            sock.Close();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void EnhancedSocket_Async_SendRecv()
        {
            AsyncSocket sockListen = null;
            AsyncSocket sock1 = null;
            AsyncSocket sock2 = null;
            int cb;

            try
            {
                sockListen = new AsyncSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sockListen.ListenOn(new IPEndPoint(IPAddress.Any, 45001));

                sock1 = new AsyncSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock1.Connect(new IPEndPoint(IPAddress.Loopback, 45001));

                sock2 = sockListen.Accept();

                for (int i = 0; i < 1000; i++)
                {
                    byte[] buf10 = new byte[10];
                    byte[] buf5 = new byte[5];

                    sock1.Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    cb = sock2.Receive(buf10);
                    Assert.AreEqual(10, cb);
                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, buf10);

                    sock2.Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    cb = sock1.Receive(buf5);
                    Assert.AreEqual(5, cb);
                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, buf5);
                    cb = sock1.Receive(buf5);
                    Assert.AreEqual(5, cb);
                    CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8, 9 }, buf5);
                }
            }
            finally
            {
                if (sockListen != null)
                    sockListen.Close();

                if (sock1 != null)
                    sock1.Close();

                if (sock2 != null)
                    sock2.Close();
            }
        }
    }
}

