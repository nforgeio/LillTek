//-----------------------------------------------------------------------------
// FILE:        NetCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the NET commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net;
using LillTek.Net.Sockets;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the NET commands.
    /// </summary>
    public static class NetCommand
    {
        /// <summary>
        /// Executes the specified NET command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {

            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic net reflector <port>

Starts a network service with both TCP and UDP bindings for all local
network adaptors on the specified <port>.  The service simply resends all
data it receives back to the source, while logging to the screen.  This 
can be useful while debugging and testing low-level network code.

";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "reflector":

                    if (args.Length < 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Reflector(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private class NetReflector
        {
            private class TcpRecvState
            {
                public EnhancedSocket   Socket;
                public byte[]           Buffer;
            }

            const int bufSize     = 8092;
            const int prefixWidth = 25;

            object                  syncLock = new object();
            EnhancedSocket          udpSock;
            byte[]                  udpRecvBuf;
            EndPoint                udpRemoteEP;
            AsyncCallback           onUdpReceive;
            SocketListener          listener;
            List<EnhancedSocket>    tcpConnections;
            AsyncCallback           onTcpReceive;

            public NetReflector(int port)
            {
                // Initialize the UDP service.

                udpSock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSock.Bind(new IPEndPoint(IPAddress.Any, port));

                udpRecvBuf   = new byte[bufSize];
                udpRemoteEP  = new IPEndPoint(IPAddress.Any, 0);
                onUdpReceive = new AsyncCallback(OnUdpReceive);
                udpSock.BeginReceiveFrom(udpRecvBuf, 0, udpRecvBuf.Length, SocketFlags.None, ref udpRemoteEP, onUdpReceive, null);

                // Initialize the TCP service.

                onTcpReceive   = new AsyncCallback(OnTcpReceive);
                tcpConnections = new List<EnhancedSocket>();

                listener = new SocketListener();
                listener.SocketAcceptEvent +=
                    (s, a) =>
                    {
                        lock (syncLock)
                        {
                            try
                            {
                                var sock      = (EnhancedSocket)s;
                                var remoteEP  = (IPEndPoint)sock.RemoteEndPoint;
                                var prefix    = PadPrefix(string.Format("TCP[{0}:{1}]:", remoteEP.Address, remoteEP.Port));
                                var recvState = new TcpRecvState() { Socket = sock, Buffer = new byte[bufSize] };

                                Console.WriteLine("{0} Connect", prefix);
                                tcpConnections.Add(sock);

                                sock.BeginReceive(recvState.Buffer, 0, recvState.Buffer.Length, SocketFlags.None, onTcpReceive, recvState);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("*** {0}: {1}", e.GetType().Name, e.Message);
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

                    Console.WriteLine();
                    Console.WriteLine("*** Closing all TCP connections ***");
                    Console.WriteLine();

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
                    Console.WriteLine();
                    Console.WriteLine("*** Resetting all TCP connections ***");
                    Console.WriteLine();

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
                    var cbRecv   = udpSock.EndReceiveFrom(ar, ref udpRemoteEP);
                    var remoteEP = (IPEndPoint)udpRemoteEP;
                    var prefix   = PadPrefix(string.Format("UDP[{0}:{1}]:", remoteEP.Address, remoteEP.Port));

                    lock (syncLock)
                    {
                        Console.WriteLine("{0} Echoing [{1}] bytes", prefix, cbRecv);
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
                        Console.WriteLine("*** {0}: {1}", e.GetType().Name, e.Message);
                    }
                }
            }

            private void OnTcpReceive(IAsyncResult ar)
            {
                var recvState = (TcpRecvState)ar.AsyncState;
                var sock      = recvState.Socket;
                var endpoint  = (IPEndPoint)sock.RemoteEndPoint;
                var prefix    = PadPrefix(string.Format("TCP[{0}:{1}]:", endpoint.Address, endpoint.Port));

                lock (syncLock)
                {
                    try
                    {
                        var cbRecv = sock.EndReceive(ar);

                        if (cbRecv == 0)
                        {
                            Console.WriteLine("{0} Closed by client", prefix);
                            tcpConnections.Remove(sock);
                        }
                        else
                        {
                            Console.WriteLine("{0} Echoing [{1}] bytes", prefix, cbRecv);

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
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("*** {0}: {1}", e.GetType().Name, e.Message);
                                        tcpConnections.Remove(sock);
                                    }
                                },
                                null);
                        }
                    }
                    catch (SocketClosedException)
                    {
                        tcpConnections.Remove(sock);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("*** {0}: {1}", e.GetType().Name, e.Message);
                        tcpConnections.Remove(sock);
                    }
                }
            }
        }

        private static int Reflector(string portArg)
        {
            try
            {
                NetReflector    reflector;
                int             port;

                if (!int.TryParse(portArg, out port))
                {
                    Program.Error("Invalid network port.");
                    return 1;
                }

                Console.WriteLine();
                Console.WriteLine("Starting network reflector on port [{0}]", port);
                Console.WriteLine("Press [C] to close all connections and [X] to exit the test.");
                Console.WriteLine();

                reflector = new NetReflector(port);

                while (true)
                {
                    var ch = Console.ReadKey(true);

                    switch (ch.KeyChar)
                    {
                        case 'c':
                        case 'C':

                            reflector.CloseConnections();
                            break;

                        case 'x':
                        case 'X':

                            reflector.Stop();
                            return 0;
#if TEST
                        // UDP test code

                        case 'u' :
                        case 'U' :

                            {
                                var sock = new EnhancedSocket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
                                var buf  = new byte[] { 0,1,2,3,4,5,6,7,8,9 };
                                var ep   = (EndPoint) new IPEndPoint(IPAddress.Any,0);

                                sock.Bind();
                                sock.SendTo(buf,0,buf.Length,SocketFlags.None,new IPEndPoint(IPAddress.Loopback,port));

                                for (int i=0;i<buf.Length;i++)
                                    buf[i] = 0;

                                sock.ReceiveFrom(buf,ref ep);
                                sock.Close();
                            }
                            break;

                        // TCP test code

                        case 't' :
                        case 'T' :

                            {
                                var sock = new EnhancedSocket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);

                                sock.NoDelay = true;
                                sock.Connect(new IPEndPoint(IPAddress.Loopback,port));

                                for (int i=0;i<10;i++) {

                                    var buf = new byte[i+1];

                                    for (int j=0;j<i+1;j++)
                                        buf[j] = (byte) j;

                                    sock.Send(buf);

                                    for (int j=0;j<i+1;j++)
                                        buf[j] = 0;

                                    var cbRecv = sock.Receive(buf);
                                }

                                sock.Close();
                            }
                            break;
#endif
                    }
                }
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }
        }
    }
}
