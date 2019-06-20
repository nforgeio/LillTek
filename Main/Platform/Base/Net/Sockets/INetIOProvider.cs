//-----------------------------------------------------------------------------
// FILE:        INetIOProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines a network socket-style I/O interface.

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

#if MOBILE_DEVICE
using AddressFamily = System.Net.Sockets.AddressFamily;
#endif

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Defines socket-like behavior for an abstract object that can transfer data
    /// across a network.
    /// </summary>
    /// <remarks>
    /// This interface defines the methods to be used once the connection has been
    /// established.  For more information about these methods, see the appropriate
    /// Socket and EnhancedSocket documentation.
    /// </remarks>
    interface INetIOProvider
    {
        string OwnerName { get; set; }
        bool DisableHangTest { get; set; }
        AddressFamily AddressFamily { get; }
        int Available { get; }
        bool Connected { get; }
        bool IsOpen { get; }
        EndPoint LocalEndPoint { get; }
        ProtocolType ProtocolType { get; }
        EndPoint RemoteEndPoint { get; }
        IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginReceiveAll(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginReceiveAll(BlockArray blocks, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP, AsyncCallback callback, object state);
        IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginSendAll(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginSendAll(BlockArray blocks, SocketFlags socketFlags, AsyncCallback callback, object state);
        IAsyncResult BeginSendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state);
        IAsyncResult BeginSendTo(BlockArray blocks, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state);
        IAsyncResult BeginSendAllTo(BlockArray blocks, SocketFlags socketFlags, EndPoint remoteEP, AsyncCallback callback, object state);
        int EndReceive(IAsyncResult ar);
        void EndReceiveAll(IAsyncResult ar);
        int EndReceiveFrom(IAsyncResult ar, ref EndPoint fromEP);
        int EndSend(IAsyncResult ar);
        void EndSendAll(IAsyncResult ar);
        int EndSendTo(IAsyncResult ar);
        void Bind(EndPoint localEP);
        void Close();
        int Receive(byte[] buffer);
        int Receive(byte[] buffer, SocketFlags socketFlags);
        int Receive(byte[] buffer, int size, SocketFlags socketFlags);
        int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags);
        int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP);
        int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP);
        int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP);
        int ReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP);
        int Send(byte[] buffer);
        int Send(byte[] buffer, SocketFlags socketFlags);
        int Send(byte[] buffer, int size, SocketFlags socketFlags);
        int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags);
        int SendTo(byte[] buffer, EndPoint remoteEP);
        int SendTo(byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP);
        int SendTo(byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP);
        int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP);
        void AsyncSendClose(BlockArray blocks);
        void AsyncSendClose(byte[] buffer);
        void AsyncSendClose(byte[] buffer, int offset, int size, SocketFlags socketFlags);
        void Shutdown(SocketShutdown how);
    }
}
