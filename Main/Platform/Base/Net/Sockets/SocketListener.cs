//-----------------------------------------------------------------------------
// FILE:        SocketListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a listening socket running on a dedicated thread.

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Used for raising an event when the listener accepts an
    /// inbound socket connection.
    /// </summary>
    /// <param name="sock">The accepted socket.</param>
    /// <param name="acceptEP">The local endpoint the socket was accepted from.</param>
    public delegate void SocketAcceptDelegate(EnhancedSocket sock, IPEndPoint acceptEP);

    /// <summary>
    /// Implements one or more socket listeners running on dedicated threads.
    /// The methods of this class are thread-safe unless otherwise noted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class handles a lot of the complexities of listening for and
    /// accepting connections on TCP socket.  The class is very easy to use.
    /// Simply instantiate a <see cref="SocketListener" /> instance and add
    /// an handler to the <see cref="SocketAcceptEvent" />.  Then call
    /// <see cref="Start" /> for each endpoint you want to listen on.
    /// </para>
    /// <para>
    /// The class handles the creation of the listening sockets and then
    /// begins accepting incomming connections on a dedicated thread.
    /// <see cref="SocketAcceptEvent" /> will be raised for each accepted
    /// connection, passing the connected <see cref="EnhancedSocket" />
    /// instance as well as the <see cref="IPEndPoint" /> of the remote
    /// side of the connection.
    /// </para>
    /// <para>
    /// Call <see cref="Stop" /> to stop listening on a particular endpoint
    /// and <see cref="StopAll" /> to stop listening on all endpoints.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class SocketListener : IDisposable
    {
        /// <summary>
        /// Raised when a socket connection is accepted by a listener.
        /// </summary>
        public event SocketAcceptDelegate   SocketAcceptEvent;

        private object                      syncLock = new object();
        private EnhancedSocket              sockParam;      // Parameter to the ListenLoop() method.
        private ManualResetEvent            threadStart;    // Indicates when the thread has started
        private WaitCallback                onAccept;       // Accepted socket pool thread handler

        private class Listener
        {

            public Thread Thread;
            public IPEndPoint EndPoint;
            public EnhancedSocket Socket;

            public Listener(Thread thread, IPEndPoint endPoint, EnhancedSocket socket)
            {
                this.Thread   = thread;
                this.EndPoint = endPoint;
                this.Socket   = socket;
            }
        }

        private Hashtable listeners;  // Table of ListenerInstances hashed by endpoint

        /// <summary>
        /// Constructor.
        /// </summary>
        public SocketListener()
        {
            this.listeners = new Hashtable();
            this.onAccept  = new WaitCallback(OnAccept);
        }

        /// <summary>
        /// Release all unmanaged stuff.
        /// </summary>
        ~SocketListener()
        {
            StopAll();
        }

        /// <summary>
        /// Implements the listening thread.
        /// </summary>
        private void ListenLoop()
        {
            EnhancedSocket  sockListen = sockParam;
            EnhancedSocket  sockAccept;

            try
            {
                threadStart.Set();      // Indicate that we have the socket parameter

                // Loop forever or until we get an exception.  This thread is
                // terminated by closing the listening socket which will cause
                // Accept() to throw an exception.

                while (true)
                {
                    try
                    {
                        sockAccept = sockListen.Accept();
                        if (sockAccept == null)
                            return;

                        // Queue the accept so that further processing will happen
                        // on a pool thread, leaving this thread free to accept
                        // additional incoming connections.

                        ThreadPool.UnsafeQueueUserWorkItem(onAccept, new object[] { sockAccept, sockListen.LocalEndPoint });
                    }
                    catch
                    {

                        return;
                    }
                }
            }
            catch (SocketClosedException)
            {
                // Ignore these
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Handles the incoming socket accept processing on a pool thread.
        /// </summary>
        /// <param name="state">The accepted socket.</param>
        private void OnAccept(object state)
        {
            object[] args = (object[])state;

            if (SocketAcceptEvent != null)
                SocketAcceptEvent((EnhancedSocket)args[0], (IPEndPoint)args[1]);
        }

        /// <summary>
        /// Attempts to start a listener on the endpoint specified.  Check for
        /// SocketExceptions.
        /// </summary>
        /// <param name="localEP">The TCP endpoint to listen on.</param>
        /// <param name="maxBackLog">
        /// The maximum number of socket connection attempts to queue.
        /// </param>
        /// <remarks>
        /// This method is not threadsafe.  The intention is that this method will be called
        /// one or more times on the application's startup thread and then not be called
        /// again.
        /// </remarks>
        public void Start(IPEndPoint localEP, int maxBackLog)
        {
            Listener        listener;
            EnhancedSocket  sock;

            if (localEP.Port == 0)
                throw new ArgumentException("Endpoint port cannot be 0.", "localEP");

            if (listeners[localEP] != null)
                throw new InvalidOperationException("Already listening on this port.");

            sock                 = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.DisableHangTest = true;
            sock.Blocking        = true;
            sock.Bind(localEP);
            sock.Listen(maxBackLog);

            listener = new Listener(new Thread(new ThreadStart(ListenLoop)), localEP, sock);
            listeners.Add(localEP, listener);

            // Start the listening thread, passing in the socket.  Note that this
            // is the code that prevents the method from being threadsafe.

            sockParam   = sock;
            threadStart = new ManualResetEvent(false);

            listener.Thread.Start();

            threadStart.WaitOne();
            threadStart.Close();
            threadStart = null;
            sockParam = null;
        }

        /// <summary>
        /// Synchronously stops the listener on the endpoint passed.
        /// </summary>
        /// <param name="endPoint">The network endpoint where listening is to stop.</param>
        public void Stop(IPEndPoint endPoint)
        {
            lock (syncLock)
            {
                var listener = (Listener)listeners[endPoint];

                if (listener != null)
                {
                    listener.Socket.Close();
                    listener.Thread.Join();
                    listeners.Remove(listener);
                }
            }
        }

        /// <summary>
        /// Stops all listeners.
        /// </summary>
        public void StopAll()
        {
            lock (syncLock)
            {
                foreach (Listener listener in listeners.Values)
                {
                    listener.Socket.Close();
                    listener.Thread.Join();
                }

                listeners.Clear();
            }
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            StopAll();
        }
    }
}
