//-----------------------------------------------------------------------------
// FILE:        _LiteSocket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manual Unit tests for the _LiteSocket class

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Diagnostics;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Sockets.Test
{
    [TestClass]
    public class _LiteSocket
    {
        //---------------------------------------------------------------------
        // Private classes

        private class NetReflectorEventArgs : EventArgs
        {
            public IPEndPoint Endpoint;
            public byte[] Data;
        }

        private class NetReflector
        {
            private class TcpRecvState
            {
                public EnhancedSocket Socket;
                public byte[] Buffer;
            }

            const int bufSize = 8192;
            const int prefixWidth = 25;

            object syncLock = new object();
            EnhancedSocket udpSock;
            byte[] udpRecvBuf;
            EndPoint udpRemoteEP;
            AsyncCallback onUdpReceive;
            SocketListener listener;
            List<EnhancedSocket> tcpConnections;
            AsyncCallback onTcpReceive;

            public event EventHandler<NetReflectorEventArgs> TcpConnected;
            public event EventHandler<NetReflectorEventArgs> TcpReceived;
            public event EventHandler<NetReflectorEventArgs> TcpDisconnected;
            public event EventHandler<NetReflectorEventArgs> UdpReceived;

            public NetReflector(int port)
            {
                // Initialize the UDP service.

                udpSock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSock.Bind(new IPEndPoint(IPAddress.Any, port));

                udpRecvBuf = new byte[bufSize];
                udpRemoteEP = new IPEndPoint(IPAddress.Any, 0);
                onUdpReceive = new AsyncCallback(OnUdpReceive);
                udpSock.BeginReceiveFrom(udpRecvBuf, 0, udpRecvBuf.Length, SocketFlags.None, ref udpRemoteEP, onUdpReceive, null);

                // Initialize the TCP service.

                onTcpReceive = new AsyncCallback(OnTcpReceive);
                tcpConnections = new List<EnhancedSocket>();

                listener = new SocketListener();
                listener.SocketAcceptEvent +=
                    (s, a) =>
                    {
                        lock (syncLock)
                        {
                            try
                            {
                                var sock = (EnhancedSocket)s;
                                var remoteEP = (IPEndPoint)sock.RemoteEndPoint;
                                var prefix = PadPrefix(string.Format("TCP[{0}:{1}]:", remoteEP.Address, remoteEP.Port));
                                var recvState = new TcpRecvState() { Socket = sock, Buffer = new byte[bufSize] };

                                if (TcpConnected != null)
                                    TcpConnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });

                                Debug.WriteLine(string.Format("{0} Connect", prefix));
                                tcpConnections.Add(sock);

                                sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, SocketFlags.None, onTcpReceive, recvState);
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(string.Format("*** {0}: {1}", e.GetType().Name, e.Message));
                            }
                        }
                    };

                listener.Start(new IPEndPoint(IPAddress.Any, port), 100);
            }

            private string PadPrefix(string prefix)
            {
                if (prefix.Length >= prefixWidth)
                    return prefix;

                return prefix + new string(' ', prefixWidth - prefix.Length);
            }

            public void ShutdownAndCloseConnections()
            {
                lock (syncLock)
                {
                    foreach (var sock in tcpConnections)
                    {
                        sock.Shutdown(SocketShutdown.Both);
                        sock.Close();
                    }
                }
            }

            public void CloseConnections()
            {
                lock (syncLock)
                {
                    foreach (var sock in tcpConnections)
                        sock.Close();
                }
            }

            public void Stop()
            {
                lock (syncLock)
                {
                    udpSock.Close();
                    listener.StopAll();
                    CloseConnections();
                }
            }

            private void OnUdpReceive(IAsyncResult ar)
            {
                try
                {
                    var cbRecv = udpSock.EndReceiveFrom(ar, ref udpRemoteEP);
                    var remoteEP = (IPEndPoint)udpRemoteEP;
                    var prefix = PadPrefix(string.Format("UDP[{0}:{1}]:", remoteEP.Address, remoteEP.Port));

                    lock (syncLock)
                    {
                        Debug.WriteLine(string.Format("{0} Echoing [{1}] bytes", prefix, cbRecv));
                    }

                    var udpSendBuf = Helper.Extract(udpRecvBuf, 0, cbRecv);

                    udpSock.BeginSendTo(udpSendBuf, 0, cbRecv, SocketFlags.None, remoteEP,
                        ar2 =>
                        {
                            try
                            {
                                udpSock.EndSendTo(ar2);
                            }
                            catch (SocketClosedException)
                            {
                                return;
                            }
                            catch
                            {
                                // Ignore
                            }
                        },
                        null);

                    if (UdpReceived != null)
                        UdpReceived(this, new NetReflectorEventArgs() { Endpoint = remoteEP, Data = udpSendBuf });

                    udpSock.BeginReceiveFrom(udpRecvBuf, 0, udpRecvBuf.Length, SocketFlags.None, ref udpRemoteEP, onUdpReceive, null);
                }
                catch (SocketClosedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    lock (syncLock)
                    {
                        Debug.WriteLine(string.Format("*** {0}: {1}", e.GetType().Name, e.Message));
                    }
                }
            }

            private void OnTcpReceive(IAsyncResult ar)
            {
                var recvState = (TcpRecvState)ar.AsyncState;
                var sock = recvState.Socket;
                var remoteEP = (IPEndPoint)sock.RemoteEndPoint;
                var prefix = PadPrefix(string.Format("TCP[{0}:{1}]:", remoteEP.Address, remoteEP.Port));

                lock (syncLock)
                {
                    try
                    {
                        var cbRecv = sock.EndReceive(ar);

                        if (cbRecv == 0)
                        {
                            Debug.WriteLine(string.Format("{0} Closed by client", prefix));
                            tcpConnections.Remove(sock);

                            if (TcpDisconnected != null)
                                TcpDisconnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });
                        }
                        else
                        {
                            Debug.WriteLine(string.Format("{0} Echoing [{1}] bytes", prefix, cbRecv));

                            sock.BeginSendAll(recvState.Buffer, 0, cbRecv, SocketFlags.None,
                                ar2 =>
                                {
                                    try
                                    {
                                        sock.EndSendAll(ar2);
                                        sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, SocketFlags.None, onTcpReceive, recvState);
                                    }
                                    catch (SocketClosedException)
                                    {
                                        tcpConnections.Remove(sock);

                                        if (TcpDisconnected != null)
                                            TcpDisconnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine(string.Format("*** {0}: {1}", e.GetType().Name, e.Message));
                                        tcpConnections.Remove(sock);

                                        if (TcpDisconnected != null)
                                            TcpDisconnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });
                                    }
                                },
                                null);

                            if (TcpReceived != null)
                                TcpReceived(this, new NetReflectorEventArgs() { Endpoint = remoteEP, Data = Helper.Extract(recvState.Buffer, 0, cbRecv) });
                        }
                    }
                    catch (SocketClosedException)
                    {
                        tcpConnections.Remove(sock);

                        if (TcpDisconnected != null)
                            TcpDisconnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(string.Format("*** {0}: {1}", e.GetType().Name, e.Message));
                        tcpConnections.Remove(sock);

                        if (TcpDisconnected != null)
                            TcpDisconnected(this, new NetReflectorEventArgs() { Endpoint = remoteEP });
                    }
                }
            }
        }

        //---------------------------------------------------------------------
        // Tests

        private const int port = 6666;
        private TimeSpan waitTime = TimeSpan.FromSeconds(30);

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_BasicIP()
        {

            // Establish a TCP connection simulated server on the local machine
            // via an IP address and then send/receive a packet of data, then
            // close the socket gracefully on the client and verify that everything
            // is OK.

            var reflector = new NetReflector(port);
            var connected = false;
            var closed = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpDisconnected += (s, a) => closed = true;

                using (var sock = LiteSocket.CreateTcp())
                {

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[1024];

                                sock.BeginReceive(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            var cbRecv = sock.EndReceive(ar2);

                                            received = true;
                                            recvData = Helper.Extract(recvBuf, 0, cbRecv);
                                            sock.ShutdownAndClose();
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && closed) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_BasicHost()
        {

            // Establish a TCP connection simulated server on the local machine
            // via a host name and then send/receive a packet of data, then
            // close the socket gracefully on the client and verify that everything
            // is OK.

            var reflector = new NetReflector(port);
            var connected = false;
            var closed = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpDisconnected += (s, a) => closed = true;

                using (var sock = LiteSocket.CreateTcp())
                {

                    sock.BeginConnect(new NetworkBinding("dev.lilltek.com", port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[1024];

                                sock.BeginReceive(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            var cbRecv = sock.EndReceive(ar2);

                                            received = true;
                                            recvData = Helper.Extract(recvBuf, 0, cbRecv);
                                            sock.ShutdownAndClose();
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && closed) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ClientReset()
        {

            // Establish a TCP connection simulated server on the local machine
            // via an IP address and then send/receive a packet of data, then
            // reset the socket on the client and verify that everything
            // is OK.

            var reflector = new NetReflector(port);
            var connected = false;
            var closed = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpDisconnected += (s, a) => closed = true;

                using (var sock = LiteSocket.CreateTcp())
                {

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[1024];

                                sock.BeginReceive(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            var cbRecv = sock.EndReceive(ar2);

                                            received = true;
                                            recvData = Helper.Extract(recvBuf, 0, cbRecv);
                                            sock.Close();
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && closed) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ServerClose()
        {

            // Establish a TCP connection simulated server on the local machine
            // via an IP address and then send/receive a packet of data, then
            // close the socket gracefully on the server and verify that everything
            // is OK.

            var reflector = new NetReflector(port);
            var connected = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;
            var gotEOF = false;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpReceived += (s, a) => reflector.ShutdownAndCloseConnections();

                using (var sock = LiteSocket.CreateTcp())
                {

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[1024];

                                sock.BeginReceive(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            var cbRecv = sock.EndReceive(ar2);

                                            received = true;
                                            recvData = Helper.Extract(recvBuf, 0, cbRecv);

                                            sock.BeginReceive(new byte[1024], 0, 1024,
                                                ar3 =>
                                                {

                                                    try
                                                    {

                                                        gotEOF = sock.EndReceive(ar) == 0;
                                                    }
                                                    catch (Exception e)
                                                    {

                                                        exception = e;
                                                    }
                                                },
                                                null);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && gotEOF) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ServerReset()
        {

            // Establish a TCP connection simulated server on the local machine
            // via an IP address and then send/receive a packet of data, then
            // reset the socket on the server and verify that everything
            // is OK.

            var reflector = new NetReflector(port);
            var connected = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;
            var gotEOF = false;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpReceived += (s, a) => reflector.CloseConnections();

                using (var sock = LiteSocket.CreateTcp())
                {

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[1024];

                                sock.BeginReceive(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            var cbRecv = sock.EndReceive(ar2);

                                            received = true;
                                            recvData = Helper.Extract(recvBuf, 0, cbRecv);

                                            sock.BeginReceive(new byte[1024], 0, 1024,
                                                ar3 =>
                                                {

                                                    try
                                                    {

                                                        gotEOF = sock.EndReceive(ar) == 0;
                                                    }
                                                    catch (Exception e)
                                                    {

                                                        exception = e;
                                                    }
                                                },
                                                null);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && gotEOF) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ExceedsBuffers()
        {

            // Establish a TCP connection simulated server on the local machine
            // via an IP address and then send a block of data much larger than
            // the socket's buffers and the make sure that all of the data is
            // echoed back correctly.

            const int blockSize = 128 * 1024;

            var reflector = new NetReflector(port);
            var connected = false;
            var closed = false;
            var received = false;
            var exception = (Exception)null;
            var recvData = (byte[])null;

            try
            {

                reflector.TcpConnected += (s, a) => connected = true;
                reflector.TcpDisconnected += (s, a) => closed = true;

                using (var sock = LiteSocket.CreateTcp(8192, 8192))
                {

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), null,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                var recvBuf = new byte[blockSize];

                                sock.BeginReceiveAll(recvBuf, 0, recvBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndReceiveAll(ar2);

                                            received = true;
                                            recvData = recvBuf;
                                            sock.ShutdownAndClose();
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);

                                var sendBuf = new byte[blockSize];

                                for (int i = 0; i < sendBuf.Length; i++)
                                    sendBuf[i] = (byte)i;

                                sock.BeginSend(sendBuf, 0, sendBuf.Length,
                                    ar2 =>
                                    {

                                        try
                                        {

                                            sock.EndSend(ar2);
                                        }
                                        catch (Exception e)
                                        {

                                            exception = e;
                                        }
                                    },
                                    null);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && received && closed) || exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    var checkBuf = new byte[blockSize];

                    for (int i = 0; i < checkBuf.Length; i++)
                        checkBuf[i] = (byte)i;

                    CollectionAssert.AreEqual(checkBuf, recvData);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        private void Fill(byte[] buffer, int value)
        {

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)value;
        }

        private class UdpSendState
        {

            public LiteSocket Sock;
            public int Count;
            public Exception Exception;
        }

        public class UdpRecvState
        {

            public LiteSocket Sock;
            public byte[] Buffer = new byte[1024];
            public List<byte[]> Packets = new List<byte[]>();
            public Exception Exception;
            public int Count;
        }

        private const int TestPacketCount = 100;

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Udp_BasicIP()
        {

            // Send/receive [TestPacketCount] UDP packets from a local reflector using an IP address.

            var reflector = new NetReflector(port);
            var connected = false;
            var exception = (Exception)null;
            var sendState = new UdpSendState();
            var recvState = new UdpRecvState();

            try
            {

                using (var sock = LiteSocket.CreateUdp(8192, 8192))
                {

                    sendState.Sock = sock;
                    recvState.Sock = sock;

                    var sendBuf = new byte[256];

                    Fill(sendBuf, 0);

                    sock.BeginConnect(new NetworkBinding(IPAddress.Loopback, port), sendBuf,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, new AsyncCallback(OnUdpBasicRecv), recvState);

                                sendBuf = new byte[256];

                                Fill(sendBuf, 1);
                                sock.BeginSend(sendBuf, 0, sendBuf.Length, new AsyncCallback(OnUdpBasicSent), sendState);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && recvState.Count == TestPacketCount + 1) || exception != null || sendState.Exception != null || recvState.Exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    if (sendState.Exception != null)
                        throw sendState.Exception;

                    if (recvState.Exception != null)
                        throw recvState.Exception;

                    Assert.AreEqual(TestPacketCount + 1, recvState.Count);

                    for (int i = 0; i < recvState.Packets.Count; i++)
                    {

                        var p = new byte[256];

                        Fill(p, i);
                        CollectionAssert.AreEqual(p, recvState.Packets[i]);
                    }
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Udp_BasicHost()
        {

            // Send/receive [TestPacketCount] UDP packets from a local reflector using a host name.

            var reflector = new NetReflector(port);
            var connected = false;
            var exception = (Exception)null;
            var sendState = new UdpSendState();
            var recvState = new UdpRecvState();

            try
            {

                using (var sock = LiteSocket.CreateUdp(8192, 8192))
                {

                    sendState.Sock = sock;
                    recvState.Sock = sock;

                    var sendBuf = new byte[256];

                    Fill(sendBuf, 0);

                    sock.BeginConnect(new NetworkBinding("dev.lilltek.com", port), sendBuf,
                        ar =>
                        {

                            try
                            {

                                sock.EndConnect(ar);
                                connected = true;

                                sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, new AsyncCallback(OnUdpBasicRecv), recvState);

                                sendBuf = new byte[256];

                                Fill(sendBuf, 1);
                                sock.BeginSend(sendBuf, 0, sendBuf.Length, new AsyncCallback(OnUdpBasicSent), sendState);
                            }
                            catch (Exception e)
                            {

                                exception = e;
                            }
                        },
                        null);

                    Helper.WaitFor(() => (connected && recvState.Count == TestPacketCount + 1) || exception != null || sendState.Exception != null || recvState.Exception != null, waitTime);

                    if (exception != null)
                        throw exception;

                    if (sendState.Exception != null)
                        throw sendState.Exception;

                    if (recvState.Exception != null)
                        throw recvState.Exception;

                    Assert.AreEqual(TestPacketCount + 1, recvState.Count);

                    for (int i = 0; i < recvState.Packets.Count; i++)
                    {

                        var p = new byte[256];

                        Fill(p, i);
                        CollectionAssert.AreEqual(p, recvState.Packets[i]);
                    }
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        private void OnUdpBasicSent(IAsyncResult ar)
        {

            var sendState = (UdpSendState)ar.AsyncState;

            try
            {

                sendState.Sock.EndSend(ar);
                sendState.Count++;

                if (sendState.Count < TestPacketCount + 1)
                {

                    Thread.Sleep(50);       // Wait a bit before sending the text packet to avoid overrunning
                    // the socket buffer and loosing packets

                    var sendBuf = new byte[256];

                    Fill(sendBuf, sendState.Count + 1);
                    sendState.Sock.BeginSend(sendBuf, 0, sendBuf.Length, new AsyncCallback(OnUdpBasicSent), sendState);
                }
            }
            catch (Exception e)
            {

                sendState.Exception = e;
            }
        }

        private void OnUdpBasicRecv(IAsyncResult ar)
        {

            var recvState = (UdpRecvState)ar.AsyncState;

            try
            {

                var cbRecv = recvState.Sock.EndReceive(ar);

                recvState.Packets.Add(Helper.Extract(recvState.Buffer, 0, cbRecv));
                recvState.Count++;

                if (recvState.Count < TestPacketCount + 1)
                    recvState.Sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, new AsyncCallback(OnUdpBasicRecv), recvState);
            }
            catch (Exception e)
            {

                recvState.Exception = e;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ReceiveSync()
        {

            // Test the synchronous TCP socket methods.

            var reflector = new NetReflector(port);
            var buffer = new byte[1024];
            var cb = 0;

            try
            {

                using (var sock = LiteSocket.CreateTcp(8192, 8192))
                {

                    sock.Connect(new NetworkBinding("dev.lilltek.com", port), null);

                    // Verify that we can receive partial data.

                    Fill(buffer, 128);
                    sock.Send(buffer, 0, 256);

                    Fill(buffer, 0);
                    cb = sock.Receive(buffer, 0, 1024);

                    Assert.AreEqual(256, cb);
                    for (int j = 0; j < 256; j++)
                        if (buffer[j] != 128)
                            Assert.Fail("Bad data");

                    // Send/receive 1024 blocks.

                    for (int i = 0; i < 1024; i++)
                    {

                        Fill(buffer, i);
                        sock.Send(buffer, 0, 256);

                        Fill(buffer, 0);
                        cb = sock.Receive(buffer, 0, 256);

                        Assert.AreEqual(256, cb);
                        for (int j = 0; j < 256; j++)
                            if (buffer[j] != (byte)i)
                                Assert.Fail("Bad data");
                    }

                    // Initiate another receive and then gracefully close the connection
                    // on the server and verify that Receive() returns 0.

                    Helper.EnqueueAction(
                        () =>
                        {

                            Thread.Sleep(500);
                            reflector.ShutdownAndCloseConnections();
                        });

                    cb = sock.Receive(buffer, 0, 256);
                    Assert.AreEqual(0, cb);
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Tcp_ReceiveAllSync()
        {

            // Test the synchronous TCP socket methods.

            var reflector = new NetReflector(port);
            var buffer = new byte[1024];

            try
            {

                using (var sock = LiteSocket.CreateTcp(8192, 8192))
                {

                    sock.Connect(new NetworkBinding("dev.lilltek.com", port), null);

                    // Send/receive 1024 blocks.

                    for (int i = 0; i < 1024; i++)
                    {

                        Fill(buffer, i);
                        sock.Send(buffer, 0, 256);

                        Fill(buffer, 0);
                        sock.ReceiveAll(buffer, 0, 256);

                        for (int j = 0; j < 256; j++)
                            if (buffer[j] != (byte)i)
                                Assert.Fail("Bad data");
                    }

                    // Initiate another receive and then gracefully close the connection
                    // on the server and verify that ReceiveAll() throws ServerClosedException.

                    Helper.EnqueueAction(
                        () =>
                        {

                            Thread.Sleep(500);
                            reflector.ShutdownAndCloseConnections();
                        });

                    try
                    {

                        sock.ReceiveAll(buffer, 0, 256);
                    }
                    catch (SocketClosedException e)
                    {

                        Assert.AreEqual(10057, e.ErrorCode);     // WSAENOTCONN
                    }
                }
            }
            finally
            {

                reflector.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Sockets")]
        public void LiteSocket_Udp_Sync()
        {

            // Test the synchronous UDP socket methods.

            var reflector = new NetReflector(port);
            var buffer = new byte[256];
            var cb = 0;

            try
            {

                using (var sock = LiteSocket.CreateUdp(8192, 8192))
                {

                    // Connect and send/receive 1024 packets.

                    Fill(buffer, 0);
                    sock.Connect(new NetworkBinding("dev.lilltek.com", port), buffer);

                    for (int i = 0; i < 1024; i++)
                    {

                        Fill(buffer, 0);
                        cb = sock.Receive(buffer, 0, 256);

                        Assert.AreEqual(256, cb);
                        for (int j = 0; j < 256; j++)
                            if (buffer[j] != (byte)i)
                                Assert.Fail("Bad data");

                        Fill(buffer, i + 1);
                        sock.Send(buffer, 0, 256);
                    }
                }
            }
            finally
            {

                reflector.Stop();
            }
        }
    }
}

